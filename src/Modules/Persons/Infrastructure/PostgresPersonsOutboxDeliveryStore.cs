using MDGsm.Persons.Application;
using Npgsql;

namespace MDGsm.Persons.Infrastructure;

/// <summary>Durable PostgreSQL delivery state with SKIP LOCKED claims and owner/expiry fencing.</summary>
public sealed class PostgresPersonsOutboxDeliveryStore(NpgsqlDataSource dataSource):IPersonsOutboxDeliveryStore
{
    private const int MaximumBatchSize=100;
    private const int MaximumLeaseOwnerLength=128;

    public async Task<IReadOnlyList<LeasedPersonsOutboxEvent>> LeaseDueAsync(
        string leaseOwner,int batchSize,DateTimeOffset nowUtc,DateTimeOffset leaseExpiresAtUtc,CancellationToken cancellationToken)
    {
        var owner=NormalizeLeaseOwner(leaseOwner);
        if(batchSize is <1 or >MaximumBatchSize)throw new ArgumentOutOfRangeException(nameof(batchSize));
        var now=nowUtc.ToUniversalTime();var expiry=leaseExpiresAtUtc.ToUniversalTime();
        if(expiry<=now)throw new ArgumentException("Lease expiry must be later than the lease time.",nameof(leaseExpiresAtUtc));

        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction=await connection.BeginTransactionAsync(cancellationToken);
        await using var command=new NpgsqlCommand("""
            WITH candidates AS (
                SELECT d.event_id,(d.delivery_status='leased') AS lease_expired
                FROM persons_outbox_deliveries d
                WHERE (d.delivery_status='pending' AND d.next_attempt_at_utc<=@now)
                   OR (d.delivery_status='leased' AND d.lease_expires_at_utc<=@now)
                ORDER BY d.next_attempt_at_utc,d.event_id
                LIMIT @batch_size
                FOR UPDATE OF d SKIP LOCKED
            ),
            claimed AS (
                UPDATE persons_outbox_deliveries d
                SET delivery_status='leased',attempt_count=d.attempt_count+1,
                    lease_owner=@owner,lease_expires_at_utc=@expiry,last_attempt_at_utc=@now,
                    last_error_code=CASE WHEN c.lease_expired THEN @lease_expired ELSE d.last_error_code END,
                    row_version=d.row_version+1
                FROM candidates c WHERE d.event_id=c.event_id
                RETURNING d.event_id,d.attempt_count,d.row_version
            )
            SELECT e.event_id,e.event_type,e.contract_version,e.aggregate_id,e.actor_reference,
                   e.occurred_at_utc,e.correlation_id,e.causation_id,e.idempotency_key,c.attempt_count,c.row_version
            FROM claimed c JOIN persons_outbox_events e ON e.event_id=c.event_id
            ORDER BY e.occurred_at_utc,e.event_id;
            """,connection,transaction);
        command.Parameters.AddWithValue("now",now);
        command.Parameters.AddWithValue("batch_size",batchSize);
        command.Parameters.AddWithValue("owner",owner);
        command.Parameters.AddWithValue("expiry",expiry);
        command.Parameters.AddWithValue("lease_expired",PersonsOutboxDeliveryFailureCodes.LeaseExpired);

        var result=new List<LeasedPersonsOutboxEvent>(batchSize);
        await using(var reader=await command.ExecuteReaderAsync(cancellationToken))
        {
            while(await reader.ReadAsync(cancellationToken))
            {
                var message=new PersonsOutboxEvent(
                    reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetGuid(3),reader.GetString(4),
                    reader.GetFieldValue<DateTimeOffset>(5).ToUniversalTime(),
                    reader.IsDBNull(6)?null:reader.GetGuid(6),reader.IsDBNull(7)?null:reader.GetGuid(7),reader.GetGuid(8));
                result.Add(new LeasedPersonsOutboxEvent(message,reader.GetInt32(9),reader.GetInt64(10)));
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<bool> MarkDeliveredAsync(Guid eventId,long leaseVersion,string leaseOwner,DateTimeOffset deliveredAtUtc,CancellationToken cancellationToken)
    {
        ValidateEventId(eventId);if(leaseVersion<1)throw new ArgumentOutOfRangeException(nameof(leaseVersion));var owner=NormalizeLeaseOwner(leaseOwner);var delivered=deliveredAtUtc.ToUniversalTime();
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command=new NpgsqlCommand("""
            UPDATE persons_outbox_deliveries
            SET delivery_status='delivered',delivered_at_utc=@delivered,lease_owner=NULL,lease_expires_at_utc=NULL,
                last_error_code=NULL,row_version=row_version+1
            WHERE event_id=@event AND delivery_status='leased' AND lease_owner=@owner
              AND row_version=@lease_version AND lease_expires_at_utc>@delivered;
            """,connection);
        command.Parameters.AddWithValue("event",eventId);command.Parameters.AddWithValue("owner",owner);command.Parameters.AddWithValue("lease_version",leaseVersion);
        command.Parameters.AddWithValue("delivered",delivered);
        return await command.ExecuteNonQueryAsync(cancellationToken)==1;
    }

    public async Task<bool> ScheduleRetryAsync(
        Guid eventId,long leaseVersion,string leaseOwner,DateTimeOffset nowUtc,DateTimeOffset nextAttemptAtUtc,string failureCode,CancellationToken cancellationToken)
    {
        ValidateEventId(eventId);if(leaseVersion<1)throw new ArgumentOutOfRangeException(nameof(leaseVersion));var owner=NormalizeLeaseOwner(leaseOwner);
        var now=nowUtc.ToUniversalTime();var next=nextAttemptAtUtc.ToUniversalTime();
        if(next<now)throw new ArgumentException("Next attempt cannot precede current time.",nameof(nextAttemptAtUtc));
        if(!string.Equals(failureCode,PersonsOutboxDeliveryFailureCodes.PublishFailed,StringComparison.Ordinal))
            throw new ArgumentException("Only a registered, non-sensitive failure code may be stored.",nameof(failureCode));
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command=new NpgsqlCommand("""
            UPDATE persons_outbox_deliveries
            SET delivery_status='pending',next_attempt_at_utc=@next,lease_owner=NULL,lease_expires_at_utc=NULL,
                last_error_code=@failure,row_version=row_version+1
            WHERE event_id=@event AND delivery_status='leased' AND lease_owner=@owner
              AND row_version=@lease_version AND lease_expires_at_utc>@now;
            """,connection);
        command.Parameters.AddWithValue("event",eventId);command.Parameters.AddWithValue("owner",owner);command.Parameters.AddWithValue("lease_version",leaseVersion);
        command.Parameters.AddWithValue("now",now);command.Parameters.AddWithValue("next",next);
        command.Parameters.AddWithValue("failure",failureCode);
        return await command.ExecuteNonQueryAsync(cancellationToken)==1;
    }

    private static string NormalizeLeaseOwner(string leaseOwner)
    {
        if(string.IsNullOrWhiteSpace(leaseOwner))throw new ArgumentException("A non-empty lease owner is required.",nameof(leaseOwner));
        var owner=leaseOwner.Trim();if(owner.Length>MaximumLeaseOwnerLength)throw new ArgumentOutOfRangeException(nameof(leaseOwner));
        return owner;
    }
    private static void ValidateEventId(Guid eventId)
    {
        if(eventId==Guid.Empty)throw new ArgumentException("A non-empty EventId is required.",nameof(eventId));
    }
}
