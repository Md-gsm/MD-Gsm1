using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using MDGsm.AuditSecurity.Application;
using MDGsm.AuditSecurity.Domain;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Infrastructure.AuditSecurity;

/// <summary>
/// PostgreSQL implementation of the AuditSecurity append/read port.
/// The configured schema must already exist. This adapter never creates schemas, roles or grants.
/// </summary>
public sealed class PostgresAuditEventStore : IAuditEventStore
{
    private const string EventColumns = """
        event_id, audit_stream_id, schema_version, canonicalization_version,
        hash_algorithm_code, event_type_code, occurred_at_utc, actor_type_code, actor_id,
        subject_type_code, subject_id, source_module_code, source_event_id, correlation_id,
        causation_id, outcome_code, metadata_json::text, stream_sequence, recorded_at_utc,
        intent_digest, previous_event_hash, canonical_payload, event_hash
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly string _schema;

    public PostgresAuditEventStore(NpgsqlDataSource dataSource, string schemaName = "audit_security")
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _schema = QuoteSchema(schemaName);
    }

    public async Task<IAuditAppendTransaction> BeginAppendTransactionAsync(
        Guid auditStreamId,
        CancellationToken cancellationToken)
    {
        if (auditStreamId == Guid.Empty)
            throw new ArgumentException("Audit stream ID is required.", nameof(auditStreamId));

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        NpgsqlTransaction? transaction = null;
        try
        {
            transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await using var ensure = new NpgsqlCommand($"""
                INSERT INTO {_schema}.stream_heads (audit_stream_id)
                VALUES (@stream_id)
                ON CONFLICT (audit_stream_id) DO NOTHING;
                """, connection, transaction);
            ensure.Parameters.AddWithValue("stream_id", auditStreamId);
            await ensure.ExecuteNonQueryAsync(cancellationToken);
            return new AppendTransaction(connection, transaction, _schema, auditStreamId);
        }
        catch
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<AuditStreamSnapshot> ReadSnapshotAsync(
        Guid auditStreamId,
        CancellationToken cancellationToken)
    {
        if (auditStreamId == Guid.Empty)
            throw new ArgumentException("Audit stream ID is required.", nameof(auditStreamId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);

        AuditStreamHead head;
        await using (var readHead = new NpgsqlCommand($"""
            SELECT last_sequence, head_hash
            FROM {_schema}.stream_heads
            WHERE audit_stream_id=@stream_id;
            """, connection, transaction))
        {
            readHead.Parameters.AddWithValue("stream_id", auditStreamId);
            await using var reader = await readHead.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                head = new AuditStreamHead(auditStreamId, reader.GetInt64(0), reader.GetFieldValue<byte[]>(1));
            else
                head = new AuditStreamHead(auditStreamId, 0, AuditEventCanonicalizer.GenesisHash);
        }

        var events = new List<AuditEventRecord>();
        await using (var readEvents = new NpgsqlCommand($"""
            SELECT {EventColumns}
            FROM {_schema}.events
            WHERE audit_stream_id=@stream_id
            ORDER BY stream_sequence;
            """, connection, transaction))
        {
            readEvents.Parameters.AddWithValue("stream_id", auditStreamId);
            await using var reader = await readEvents.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                events.Add(ReadRecord(reader));
        }

        await transaction.CommitAsync(cancellationToken);
        return new AuditStreamSnapshot(head, events);
    }

    private static AuditEventRecord ReadRecord(NpgsqlDataReader reader)
    {
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(16))
            ?? throw new InvalidOperationException("Stored audit metadata JSON is invalid.");
        var intent = new AuditEventIntent(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetInt16(2), reader.GetInt16(3),
            reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetString(7), reader.GetGuid(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetGuid(10),
            reader.GetString(11), reader.GetGuid(12), reader.GetGuid(13),
            reader.IsDBNull(14) ? null : reader.GetGuid(14), reader.GetString(15), metadata);
        return AuditEventRecord.Rehydrate(
            intent,
            reader.GetInt64(17),
            reader.GetFieldValue<DateTimeOffset>(18),
            reader.GetFieldValue<byte[]>(19),
            reader.GetFieldValue<byte[]>(20),
            reader.GetFieldValue<byte[]>(21),
            reader.GetFieldValue<byte[]>(22));
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

    private sealed class AppendTransaction(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        Guid streamId) : IAuditAppendTransaction
    {
        private AuditAppendState? _lockedState;
        private bool _eventInserted;
        private bool _headAdvanced;
        private bool _committed;
        private bool _disposed;

        public async Task<AuditAppendState> LockStreamHeadAsync(CancellationToken cancellationToken)
        {
            EnsureActive();
            if (_lockedState is not null)
                throw new InvalidOperationException("The audit stream head has already been locked.");

            long sequence;
            byte[] headHash;
            await using (var command = new NpgsqlCommand($"""
                SELECT last_sequence, head_hash
                FROM {schema}.stream_heads
                WHERE audit_stream_id=@stream_id
                FOR UPDATE;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("stream_id", streamId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException("The audit stream head row disappeared before it could be locked.");
                sequence = reader.GetInt64(0);
                headHash = reader.GetFieldValue<byte[]>(1);
            }

            await using var clock = new NpgsqlCommand("SELECT clock_timestamp();", connection, transaction);
            var databaseTime = await clock.ExecuteScalarAsync(cancellationToken) as DateTimeOffset?
                ?? throw new InvalidOperationException("PostgreSQL did not return a database timestamp.");
            _lockedState = new AuditAppendState(
                new AuditStreamHead(streamId, sequence, headHash),
                AuditEventCanonicalizer.NormalizeTimestamp(databaseTime));
            return _lockedState;
        }

        public async Task<AuditEventRecord?> FindByEventIdAsync(
            Guid eventId,
            CancellationToken cancellationToken)
        {
            EnsureLocked();
            if (eventId == Guid.Empty)
                throw new ArgumentException("EventId is required.", nameof(eventId));

            await using var command = new NpgsqlCommand($"""
                SELECT {EventColumns}
                FROM {schema}.events
                WHERE event_id=@event_id;
                """, connection, transaction);
            command.Parameters.AddWithValue("event_id", eventId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
        }

        public async Task InsertEventAsync(AuditEventRecord record, CancellationToken cancellationToken)
        {
            EnsureLocked();
            ArgumentNullException.ThrowIfNull(record);
            if (_eventInserted || record.Intent.AuditStreamId != streamId
                || record.Sequence != checked(_lockedState!.Head.LastSequence + 1)
                || record.RecordedAtUtc != _lockedState.DatabaseRecordedAtUtc
                || !AuditEventCanonicalizer.VerifyRecord(record))
                throw new InvalidOperationException("Audit event does not match the locked PostgreSQL append state.");

            await using var command = new NpgsqlCommand($"""
                INSERT INTO {schema}.events
                (
                    event_id,audit_stream_id,schema_version,canonicalization_version,
                    hash_algorithm_code,event_type_code,occurred_at_utc,actor_type_code,actor_id,
                    subject_type_code,subject_id,source_module_code,source_event_id,correlation_id,
                    causation_id,outcome_code,metadata_json,stream_sequence,recorded_at_utc,
                    intent_digest,previous_event_hash,canonical_payload,event_hash
                )
                VALUES
                (
                    @event_id,@stream_id,@schema_version,@canonicalization_version,
                    @hash_algorithm,@event_type,@occurred_at,@actor_type,@actor_id,
                    @subject_type,@subject_id,@source_module,@source_event,@correlation_id,
                    @causation_id,@outcome,@metadata_json,@sequence,@recorded_at,
                    @intent_digest,@previous_hash,@canonical_payload,@event_hash
                );
                """, connection, transaction);
            var intent = record.Intent;
            command.Parameters.AddWithValue("event_id", intent.EventId);
            command.Parameters.AddWithValue("stream_id", intent.AuditStreamId);
            command.Parameters.AddWithValue("schema_version", intent.SchemaVersion);
            command.Parameters.AddWithValue("canonicalization_version", intent.CanonicalizationVersion);
            command.Parameters.AddWithValue("hash_algorithm", intent.HashAlgorithmCode);
            command.Parameters.AddWithValue("event_type", intent.EventTypeCode);
            command.Parameters.AddWithValue("occurred_at", intent.OccurredAtUtc);
            command.Parameters.AddWithValue("actor_type", intent.ActorTypeCode);
            command.Parameters.AddWithValue("actor_id", intent.ActorId);
            command.Parameters.AddWithValue("subject_type", (object?)intent.SubjectTypeCode ?? DBNull.Value);
            command.Parameters.AddWithValue("subject_id", (object?)intent.SubjectId ?? DBNull.Value);
            command.Parameters.AddWithValue("source_module", intent.SourceModuleCode);
            command.Parameters.AddWithValue("source_event", intent.SourceEventId);
            command.Parameters.AddWithValue("correlation_id", intent.CorrelationId);
            command.Parameters.AddWithValue("causation_id", (object?)intent.CausationId ?? DBNull.Value);
            command.Parameters.AddWithValue("outcome", intent.OutcomeCode);
            command.Parameters.Add(new NpgsqlParameter("metadata_json", NpgsqlDbType.Jsonb)
            {
                Value = JsonSerializer.Serialize(intent.Metadata)
            });
            command.Parameters.AddWithValue("sequence", record.Sequence);
            command.Parameters.AddWithValue("recorded_at", record.RecordedAtUtc);
            command.Parameters.AddWithValue("intent_digest", record.IntentDigest.ToArray());
            command.Parameters.AddWithValue("previous_hash", record.PreviousEventHash.ToArray());
            command.Parameters.AddWithValue("canonical_payload", record.CanonicalPayload.ToArray());
            command.Parameters.AddWithValue("event_hash", record.EventHash.ToArray());
            await command.ExecuteNonQueryAsync(cancellationToken);
            _eventInserted = true;
        }

        public async Task AdvanceStreamHeadAsync(
            Guid auditStreamId,
            long newSequence,
            ReadOnlyMemory<byte> newHeadHash,
            CancellationToken cancellationToken)
        {
            EnsureLocked();
            if (!_eventInserted || _headAdvanced || auditStreamId != streamId
                || newSequence != checked(_lockedState!.Head.LastSequence + 1)
                || newHeadHash.Length != 32)
                throw new InvalidOperationException("Audit stream head advancement does not match the inserted event.");

            await using var command = new NpgsqlCommand($"""
                UPDATE {schema}.stream_heads
                SET last_sequence=@new_sequence, head_hash=@new_head_hash, updated_at_utc=clock_timestamp()
                WHERE audit_stream_id=@stream_id AND last_sequence=@old_sequence;
                """, connection, transaction);
            command.Parameters.AddWithValue("new_sequence", newSequence);
            command.Parameters.AddWithValue("new_head_hash", newHeadHash.ToArray());
            command.Parameters.AddWithValue("stream_id", streamId);
            command.Parameters.AddWithValue("old_sequence", _lockedState.Head.LastSequence);
            var changed = await command.ExecuteNonQueryAsync(cancellationToken);
            if (changed != 1)
                throw new InvalidOperationException("Audit stream head compare-and-swap failed.");
            _headAdvanced = true;
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            EnsureLocked();
            if (!_eventInserted || !_headAdvanced)
                throw new InvalidOperationException("An append transaction cannot commit without both the event and its stream head.");
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }

        private void EnsureActive()
        {
            if (_disposed || _committed)
                throw new ObjectDisposedException(nameof(AppendTransaction));
        }

        private void EnsureLocked()
        {
            EnsureActive();
            if (_lockedState is null)
                throw new InvalidOperationException("The stream head must be locked before this operation.");
        }
    }
}
