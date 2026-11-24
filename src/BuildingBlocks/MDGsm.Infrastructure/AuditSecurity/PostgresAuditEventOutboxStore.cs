using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using MDGsm.AuditSecurity.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Infrastructure.AuditSecurity;

/// <summary>
/// PostgreSQL source-local Outbox adapter. One instance is configured for one source module schema.
/// Enqueue uses the caller's source transaction; worker claims use this adapter's worker DataSource.
/// </summary>
public sealed class PostgresAuditEventOutboxStore : IAuditEventOutboxWriter, IAuditEventOutboxDeliveryStore
{
    private readonly NpgsqlDataSource _workerDataSource;
    private readonly string _schema;
    private readonly string _workerLoginName;
    private readonly string _workerAclRoleName;

    public PostgresAuditEventOutboxStore(
        NpgsqlDataSource workerDataSource,
        string sourceSchemaName,
        string workerLoginName = AuditEventOutboxWorkerIdentity.PostgreSqlLogin,
        string workerAclRoleName = AuditEventOutboxWorkerIdentity.PostgreSqlAclRole)
    {
        _workerDataSource = workerDataSource ?? throw new ArgumentNullException(nameof(workerDataSource));
        _schema = QuoteSchema(sourceSchemaName);
        _workerLoginName = ValidateRoleName(workerLoginName, nameof(workerLoginName));
        _workerAclRoleName = ValidateRoleName(workerAclRoleName, nameof(workerAclRoleName));
    }

    public async Task EnqueueAsync(
        DbConnection sourceConnection,
        DbTransaction sourceTransaction,
        AuditEventSubmission submission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceConnection);
        ArgumentNullException.ThrowIfNull(sourceTransaction);
        ArgumentNullException.ThrowIfNull(submission);
        if (sourceConnection is not NpgsqlConnection connection
            || sourceTransaction is not NpgsqlTransaction transaction
            || !ReferenceEquals(transaction.Connection, connection)
            || connection.State != ConnectionState.Open)
            throw new ArgumentException("Outbox enqueue requires the caller's active Npgsql connection and transaction.");

        var normalized = AuditEventSubmissionCanonicalizer.NormalizeAndValidate(submission);
        if (normalized.Metadata.Count != 0)
            throw new AuditEventOutboxConflictException(
                "Audit metadata is disabled until a reviewed event-type allow-list is published.");
        var digest = AuditEventSubmissionCanonicalizer.ComputeIntentDigest(normalized);
        var metadataJson = JsonSerializer.Serialize(normalized.Metadata);

        await using (var insert = new NpgsqlCommand($"""
            INSERT INTO {_schema}.audit_outbox_events
            (
                event_id,audit_stream_id,schema_version,canonicalization_version,
                hash_algorithm_code,event_type_code,occurred_at_utc,actor_type_code,actor_id,
                subject_type_code,subject_id,source_module_code,source_event_id,correlation_id,
                causation_id,outcome_code,metadata_json,contract_version,intent_digest
            )
            VALUES
            (
                @event_id,@stream_id,@schema_version,@canonicalization_version,
                @hash_algorithm,@event_type,@occurred_at,@actor_type,@actor_id,
                @subject_type,@subject_id,@source_module,@source_event,@correlation_id,
                @causation_id,@outcome,@metadata_json,@contract_version,@intent_digest
            )
            ON CONFLICT (event_id) DO NOTHING;
            """, connection, transaction))
        {
            AddSubmissionParameters(insert, normalized, metadataJson, digest);
            var inserted = await insert.ExecuteNonQueryAsync(cancellationToken);
            if (inserted == 0)
            {
                await using var existing = new NpgsqlCommand($"""
                    SELECT intent_digest
                    FROM {_schema}.audit_outbox_events
                    WHERE event_id=@event_id;
                    """, connection, transaction);
                existing.Parameters.AddWithValue("event_id", normalized.EventId);
                var stored = await existing.ExecuteScalarAsync(cancellationToken) as byte[]
                    ?? throw new AuditEventOutboxConflictException("The existing Outbox event could not be read.");
                if (!CryptographicOperations.FixedTimeEquals(stored, digest))
                    throw new AuditEventOutboxConflictException("EventId was reused with a different canonical intent.");

                await using var deliveryExists = new NpgsqlCommand($"""
                    SELECT EXISTS (
                        SELECT 1 FROM {_schema}.audit_outbox_delivery WHERE event_id=@event_id
                    );
                    """, connection, transaction);
                deliveryExists.Parameters.AddWithValue("event_id", normalized.EventId);
                if (!Equals(await deliveryExists.ExecuteScalarAsync(cancellationToken), true))
                    throw new AuditEventOutboxConflictException("The existing Outbox event has no delivery state.");
                return;
            }
        }

        await using var addDelivery = new NpgsqlCommand($"""
            INSERT INTO {_schema}.audit_outbox_delivery(event_id)
            VALUES (@event_id);
            """, connection, transaction);
        addDelivery.Parameters.AddWithValue("event_id", normalized.EventId);
        await addDelivery.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEventOutboxLease>> ClaimBatchAsync(
        string workerIdentity,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(workerIdentity, AuditEventOutboxWorkerIdentity.LogicalIdentity, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the designated Audit delivery worker may claim this Outbox.");
        if (batchSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(15))
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        await using var connection = await _workerDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var candidates = new List<Guid>();
        await using (var select = new NpgsqlCommand($"""
            SELECT event_id
            FROM {_schema}.audit_outbox_delivery
            WHERE (state='PENDING' AND next_attempt_at_utc<=clock_timestamp())
               OR (state='IN_FLIGHT' AND lease_expires_at_utc<=clock_timestamp())
            ORDER BY next_attempt_at_utc,event_id
            LIMIT @batch_size
            FOR UPDATE SKIP LOCKED;
            """, connection, transaction))
        {
            select.Parameters.AddWithValue("batch_size", batchSize);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                candidates.Add(reader.GetGuid(0));
        }

        foreach (var eventId in candidates)
        {
            await using var update = new NpgsqlCommand($"""
                UPDATE {_schema}.audit_outbox_delivery
                SET state='IN_FLIGHT',
                    attempts=attempts+1,
                    lease_expires_at_utc=clock_timestamp()+@lease_duration,
                    lease_token=@lease_token,
                    claimed_by=@worker_identity,
                    updated_at_utc=clock_timestamp()
                WHERE event_id=@event_id;
                """, connection, transaction);
            update.Parameters.AddWithValue("lease_duration", leaseDuration);
            update.Parameters.AddWithValue("lease_token", Guid.NewGuid());
            update.Parameters.AddWithValue("worker_identity", workerIdentity);
            update.Parameters.AddWithValue("event_id", eventId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Locked Outbox delivery row disappeared during claim.");
        }

        var leases = new List<AuditEventOutboxLease>(candidates.Count);
        if (candidates.Count > 0)
        {
            await using var read = new NpgsqlCommand($"""
                SELECT e.event_id,e.audit_stream_id,e.schema_version,e.canonicalization_version,
                       e.hash_algorithm_code,e.event_type_code,e.occurred_at_utc,e.actor_type_code,e.actor_id,
                       e.subject_type_code,e.subject_id,e.source_module_code,e.source_event_id,e.correlation_id,
                       e.causation_id,e.outcome_code,e.metadata_json::text,e.contract_version,
                       e.intent_digest,e.created_at_utc,d.lease_token,d.attempts,d.lease_expires_at_utc
                FROM {_schema}.audit_outbox_events e
                JOIN {_schema}.audit_outbox_delivery d ON d.event_id=e.event_id
                WHERE e.event_id=ANY(@event_ids)
                ORDER BY e.created_at_utc,e.event_id;
                """, connection, transaction);
            read.Parameters.AddWithValue("event_ids", candidates.ToArray());
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(16))
                    ?? throw new InvalidOperationException("Stored Outbox metadata JSON is invalid.");
                var submission = AuditEventSubmissionCanonicalizer.NormalizeAndValidate(new AuditEventSubmission(
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetInt16(2), reader.GetInt16(3),
                    reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6),
                    reader.GetString(7), reader.GetGuid(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetGuid(10),
                    reader.GetString(11), reader.GetGuid(12), reader.GetGuid(13),
                    reader.IsDBNull(14) ? null : reader.GetGuid(14), reader.GetString(15),
                    metadata, reader.GetString(17)));
                var digest = Convert.ToHexString(reader.GetFieldValue<byte[]>(18));
                leases.Add(new AuditEventOutboxLease(
                    reader.GetGuid(0), reader.GetGuid(20), submission, digest,
                    reader.GetInt32(21), reader.GetFieldValue<DateTimeOffset>(19),
                    reader.GetFieldValue<DateTimeOffset>(22)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        await ResetWorkerRoleAsync(connection, cancellationToken);
        return leases;
    }

    public async Task MarkAcknowledgedAsync(
        AuditEventOutboxLease lease,
        AuditAppendReceipt receipt,
        CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.EventId != lease.EventId
            || receipt.AuditStreamId != lease.Submission.AuditStreamId
            || receipt.Sequence < 1
            || !string.Equals(receipt.ContractVersion, ModuleContractVersion.Current, StringComparison.Ordinal)
            || !FixedHexEquals(lease.IntentDigestHex, receipt.IntentDigestHex)
            || !IsSha256Hex(receipt.EventHashHex))
            throw new AuditEventDeliveryRejectedException("durable_acknowledgement_mismatch");

        var ackReference = string.Join("|", "v1", receipt.EventId.ToString("N"),
            receipt.AuditStreamId.ToString("N"), receipt.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            receipt.IntentDigestHex.ToUpperInvariant(), receipt.EventHashHex.ToUpperInvariant());

        await using var connection = await OpenWorkerConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"""
            UPDATE {_schema}.audit_outbox_delivery d
            SET state='ACKNOWLEDGED',ack_reference=@ack_reference,
                lease_expires_at_utc=NULL,lease_token=NULL,claimed_by=NULL,
                last_failure_code=NULL,updated_at_utc=clock_timestamp()
            WHERE d.event_id=@event_id AND d.state='IN_FLIGHT' AND d.lease_token=@lease_token
              AND d.lease_expires_at_utc > clock_timestamp()
              AND EXISTS (
                  SELECT 1 FROM {_schema}.audit_outbox_events e
                  WHERE e.event_id=d.event_id AND e.intent_digest=@intent_digest
              );
            """, connection);
        command.Parameters.AddWithValue("ack_reference", ackReference);
        command.Parameters.AddWithValue("event_id", lease.EventId);
        command.Parameters.AddWithValue("lease_token", lease.LeaseToken);
        command.Parameters.AddWithValue("intent_digest", Convert.FromHexString(lease.IntentDigestHex));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new AuditEventOutboxLeaseLostException("Outbox lease was lost or expired before durable acknowledgement could be recorded.");
        await ResetWorkerRoleAsync(connection, cancellationToken);
    }

    public async Task ScheduleRetryAsync(
        AuditEventOutboxLease lease,
        string failureCode,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        var reason = AuditEventOutboxReasonCode.Validate(failureCode);
        if (delay < TimeSpan.Zero || delay > TimeSpan.FromHours(24))
            throw new ArgumentOutOfRangeException(nameof(delay));

        await using var connection = await OpenWorkerConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"""
            UPDATE {_schema}.audit_outbox_delivery
            SET state='PENDING',next_attempt_at_utc=clock_timestamp()+@delay,
                lease_expires_at_utc=NULL,lease_token=NULL,claimed_by=NULL,
                last_failure_code=@failure_code,updated_at_utc=clock_timestamp()
            WHERE event_id=@event_id AND state='IN_FLIGHT' AND lease_token=@lease_token
              AND lease_expires_at_utc > clock_timestamp();
            """, connection);
        command.Parameters.AddWithValue("delay", delay);
        command.Parameters.AddWithValue("failure_code", reason);
        command.Parameters.AddWithValue("event_id", lease.EventId);
        command.Parameters.AddWithValue("lease_token", lease.LeaseToken);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new AuditEventOutboxLeaseLostException("Outbox lease was lost or expired before retry scheduling.");
        await ResetWorkerRoleAsync(connection, cancellationToken);
    }

    public async Task QuarantineAsync(
        AuditEventOutboxLease lease,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        var reason = AuditEventOutboxReasonCode.Validate(reasonCode);
        await using var connection = await OpenWorkerConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"""
            UPDATE {_schema}.audit_outbox_delivery
            SET state='QUARANTINED',quarantine_reason=@reason_code,last_failure_code=@reason_code,
                lease_expires_at_utc=NULL,lease_token=NULL,claimed_by=NULL,
                quarantined_at_utc=clock_timestamp(),updated_at_utc=clock_timestamp()
            WHERE event_id=@event_id AND state='IN_FLIGHT' AND lease_token=@lease_token
              AND lease_expires_at_utc > clock_timestamp();
            """, connection);
        command.Parameters.AddWithValue("reason_code", reason);
        command.Parameters.AddWithValue("event_id", lease.EventId);
        command.Parameters.AddWithValue("lease_token", lease.LeaseToken);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new AuditEventOutboxLeaseLostException("Outbox lease was lost or expired before quarantine.");
        await ResetWorkerRoleAsync(connection, cancellationToken);
    }


    private async Task<NpgsqlConnection> OpenWorkerConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = await _workerDataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            var initial = await ReadDatabaseIdentityAsync(connection, cancellationToken);
            if (!string.Equals(initial.SessionUser, _workerLoginName, StringComparison.Ordinal)
                || !string.Equals(initial.CurrentUser, _workerLoginName, StringComparison.Ordinal))
                throw new UnauthorizedAccessException(
                    "Worker connection pool returned an unexpected PostgreSQL session identity or a contaminated role state.");

            await using (var setRole = new NpgsqlCommand(
                $"SET ROLE {QuoteRole(_workerAclRoleName)};", connection))
            {
                await setRole.ExecuteNonQueryAsync(cancellationToken);
            }

            var active = await ReadDatabaseIdentityAsync(connection, cancellationToken);
            if (!string.Equals(active.SessionUser, _workerLoginName, StringComparison.Ordinal)
                || !string.Equals(active.CurrentUser, _workerAclRoleName, StringComparison.Ordinal))
                throw new UnauthorizedAccessException(
                    "Worker PostgreSQL session_user/current_user did not match the approved SET ROLE boundary.");

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<(string SessionUser, string CurrentUser)> ReadDatabaseIdentityAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT session_user, current_user;", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new UnauthorizedAccessException("PostgreSQL did not return the active worker identity.");
        return (reader.GetString(0), reader.GetString(1));
    }

    private static async Task ResetWorkerRoleAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("RESET ROLE;", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ValidateRoleName(string roleName, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        if (roleName.Length > 63 || roleName[0] is >= '0' and <= '9'
            || roleName.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')))
            throw new ArgumentException("Worker role names must be canonical lowercase PostgreSQL identifiers.", parameterName);
        return roleName;
    }

    private static string QuoteRole(string roleName) => "\"" + roleName + "\"";

    private static void AddSubmissionParameters(
        NpgsqlCommand command,
        AuditEventSubmission item,
        string metadataJson,
        byte[] digest)
    {
        command.Parameters.AddWithValue("event_id", item.EventId);
        command.Parameters.AddWithValue("stream_id", item.AuditStreamId);
        command.Parameters.AddWithValue("schema_version", item.SchemaVersion);
        command.Parameters.AddWithValue("canonicalization_version", item.CanonicalizationVersion);
        command.Parameters.AddWithValue("hash_algorithm", item.HashAlgorithmCode);
        command.Parameters.AddWithValue("event_type", item.EventTypeCode);
        command.Parameters.AddWithValue("occurred_at", item.OccurredAtUtc);
        command.Parameters.AddWithValue("actor_type", item.ActorTypeCode);
        command.Parameters.AddWithValue("actor_id", item.ActorId);
        command.Parameters.AddWithValue("subject_type", (object?)item.SubjectTypeCode ?? DBNull.Value);
        command.Parameters.AddWithValue("subject_id", (object?)item.SubjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("source_module", item.SourceModuleCode);
        command.Parameters.AddWithValue("source_event", item.SourceEventId);
        command.Parameters.AddWithValue("correlation_id", item.CorrelationId);
        command.Parameters.AddWithValue("causation_id", (object?)item.CausationId ?? DBNull.Value);
        command.Parameters.AddWithValue("outcome", item.OutcomeCode);
        command.Parameters.Add(new NpgsqlParameter("metadata_json", NpgsqlDbType.Jsonb) { Value = metadataJson });
        command.Parameters.AddWithValue("contract_version", item.ContractVersion);
        command.Parameters.AddWithValue("intent_digest", digest);
    }

    private static void ValidateLease(AuditEventOutboxLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (lease.EventId == Guid.Empty || lease.LeaseToken == Guid.Empty
            || lease.EventId != lease.Submission.EventId
            || !FixedHexEquals(lease.IntentDigestHex,
                AuditEventSubmissionCanonicalizer.ComputeIntentDigestHex(lease.Submission)))
            throw new ArgumentException("Outbox lease is invalid or its canonical digest does not match.", nameof(lease));
    }

    private static bool FixedHexEquals(string left, string right)
    {
        try
        {
            var a = Convert.FromHexString(left);
            var b = Convert.FromHexString(right);
            return a.Length == 32 && b.Length == 32 && CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch (FormatException) { return false; }
    }

    private static bool IsSha256Hex(string value)
    {
        try { return Convert.FromHexString(value).Length == 32; }
        catch (FormatException) { return false; }
    }

    private static string QuoteSchema(string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        if (schemaName.Length > 63 || schemaName.Any(character =>
            !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
            || schemaName[0] is >= '0' and <= '9')
            throw new ArgumentException("Schema name must be a canonical lowercase PostgreSQL identifier.", nameof(schemaName));
        return "\"" + schemaName + "\"";
    }
}
