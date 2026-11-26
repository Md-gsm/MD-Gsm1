using MDGsm.Persons.Application;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Persons.Infrastructure;

/// <summary>
/// PostgreSQL lease/retry persistence for the Persons source Outbox.
/// This adapter is not registered into the API host: a future authorized runtime identity and publisher
/// must be supplied before delivery can be activated.
/// </summary>
public sealed class PostgresPersonsOutboxDeliveryStore(NpgsqlDataSource dataSource) : IPersonsOutboxDeliveryStore
{
    public async Task<IReadOnlyList<LeasedPersonsOutboxEvent>> LeaseDueAsync(
        string leaseOwner,
        int batchSize,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? eventIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        if (leaseOwner.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(leaseOwner));
        if (batchSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        var now = nowUtc.ToUniversalTime();
        var leaseExpiry = leaseExpiresAtUtc.ToUniversalTime();
        if (leaseExpiry <= now)
            throw new ArgumentException("Lease expiry must be later than the lease acquisition time.", nameof(leaseExpiresAtUtc));
        if (eventIds is { Count: 0 })
            return Array.Empty<LeasedPersonsOutboxEvent>();
        var filteredEventIds = eventIds?.Distinct().ToArray();
        if (filteredEventIds is { Length: > 100 })
            throw new ArgumentOutOfRangeException(nameof(eventIds), "At most 100 event IDs may be selected in one lease operation.");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            WITH due AS (
                SELECT event_id, delivery_status
                FROM persons_outbox_deliveries
                WHERE (
                    (delivery_status = 'pending' AND next_attempt_at_utc <= @now)
                    OR (delivery_status = 'leased' AND lease_expires_at_utc <= @now)
                )
                  AND (NOT @filter_events OR event_id = ANY(@event_ids))
                ORDER BY
                    CASE WHEN delivery_status = 'leased'
                         THEN lease_expires_at_utc ELSE next_attempt_at_utc END,
                    event_id
                LIMIT @batch_size
                FOR UPDATE SKIP LOCKED
            ),
            claimed AS (
                UPDATE persons_outbox_deliveries AS delivery
                SET delivery_status = 'leased',
                    attempt_count = delivery.attempt_count + 1,
                    lease_owner = @owner,
                    lease_expires_at_utc = @lease_expiry,
                    last_attempt_at_utc = @now,
                    last_error_code = CASE
                        WHEN due.delivery_status = 'leased' THEN 'lease_expired'
                        ELSE delivery.last_error_code
                    END,
                    row_version = delivery.row_version + 1
                FROM due
                WHERE delivery.event_id = due.event_id
                RETURNING delivery.event_id, delivery.attempt_count, delivery.row_version
            )
            SELECT event.event_id, event.event_type, event.contract_version,
                   event.aggregate_id, event.actor_reference, event.occurred_at_utc,
                   event.correlation_id, event.causation_id, event.idempotency_key,
                   claimed.attempt_count, claimed.row_version
            FROM claimed
            INNER JOIN persons_outbox_events AS event USING (event_id)
            ORDER BY event.occurred_at_utc, event.event_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("batch_size", batchSize);
        command.Parameters.AddWithValue("owner", leaseOwner);
        command.Parameters.AddWithValue("lease_expiry", leaseExpiry);
        command.Parameters.AddWithValue("filter_events", filteredEventIds is not null);
        command.Parameters.Add(new NpgsqlParameter("event_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = filteredEventIds ?? Array.Empty<Guid>()
        });

        var leased = new List<LeasedPersonsOutboxEvent>(batchSize);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var message = new PersonsOutboxEvent(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetGuid(3),
                    reader.GetString(4),
                    reader.GetFieldValue<DateTimeOffset>(5),
                    reader.IsDBNull(6) ? null : reader.GetGuid(6),
                    reader.IsDBNull(7) ? null : reader.GetGuid(7),
                    reader.GetGuid(8));
                leased.Add(new LeasedPersonsOutboxEvent(message, reader.GetInt32(9), reader.GetInt64(10)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return leased;
    }

    public async Task<bool> MarkDeliveredAsync(
        Guid eventId,
        long leaseVersion,
        string leaseOwner,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        var deliveredAt = deliveredAtUtc.ToUniversalTime();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE persons_outbox_deliveries
            SET delivery_status = 'delivered',
                lease_owner = NULL,
                lease_expires_at_utc = NULL,
                delivered_at_utc = @delivered,
                last_error_code = NULL,
                row_version = row_version + 1
            WHERE event_id = @event
              AND delivery_status = 'leased'
              AND lease_owner = @owner
              AND row_version = @lease_version
              AND lease_expires_at_utc > @delivered;
            """, connection);
        command.Parameters.AddWithValue("event", eventId);
        command.Parameters.AddWithValue("owner", leaseOwner);
        command.Parameters.AddWithValue("lease_version", leaseVersion);
        command.Parameters.AddWithValue("delivered", deliveredAt);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> ScheduleRetryAsync(
        Guid eventId,
        long leaseVersion,
        string leaseOwner,
        DateTimeOffset nowUtc,
        DateTimeOffset nextAttemptAtUtc,
        string failureCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        if (!string.Equals(failureCode, PersonsOutboxDeliveryFailureCodes.PublishFailed, StringComparison.Ordinal))
            throw new ArgumentException("Only a closed Outbox delivery failure code is accepted.", nameof(failureCode));
        var now = nowUtc.ToUniversalTime();
        var nextAttempt = nextAttemptAtUtc.ToUniversalTime();
        if (nextAttempt <= now)
            throw new ArgumentException("The next attempt must be scheduled after the failed attempt.", nameof(nextAttemptAtUtc));

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE persons_outbox_deliveries
            SET delivery_status = 'pending',
                next_attempt_at_utc = @next_attempt,
                lease_owner = NULL,
                lease_expires_at_utc = NULL,
                delivered_at_utc = NULL,
                last_error_code = @failure_code,
                row_version = row_version + 1
            WHERE event_id = @event
              AND delivery_status = 'leased'
              AND lease_owner = @owner
              AND row_version = @lease_version
              AND lease_expires_at_utc > @now;
            """, connection);
        command.Parameters.AddWithValue("event", eventId);
        command.Parameters.AddWithValue("owner", leaseOwner);
        command.Parameters.AddWithValue("lease_version", leaseVersion);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("next_attempt", nextAttempt);
        command.Parameters.AddWithValue("failure_code", failureCode);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
