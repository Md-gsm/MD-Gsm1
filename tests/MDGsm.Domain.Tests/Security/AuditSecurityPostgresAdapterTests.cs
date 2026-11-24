using System.Security.Cryptography;
using System.Text;
using MDGsm.AuditSecurity.Application;
using MDGsm.AuditSecurity.Contracts;
using MDGsm.AuditSecurity.Domain;
using MDGsm.Infrastructure.AuditSecurity;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL 18 integration proof for the real Npgsql AuditSecurity append adapter
/// and source-local Outbox adapter. It creates only unique test schemas and never applies a
/// production migration or provisions a production role/credential/grant.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class AuditSecurityPostgresAdapterTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Postgres_append_and_source_outbox_are_atomic_idempotent_and_verifiable()
    {
        var connectionString = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", builder.Database);

        await using (var guard = new NpgsqlConnection(connectionString))
        {
            await guard.OpenAsync();
            await using var marker = new NpgsqlCommand(
                "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname=current_database();",
                guard);
            Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB;", (string?)await marker.ExecuteScalarAsync());
        }

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var auditSchema = "audit_adapter_" + suffix;
        var sourceSchema = "source_outbox_" + suffix;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        try
        {
            await CreateAuditSchemaAsync(connectionString, auditSchema);
            await CreateSourceOutboxSchemaAsync(connectionString, sourceSchema);

            var auditStore = new PostgresAuditEventStore(dataSource, auditSchema);
            var appender = new AuditEventAppender(auditStore, new EmptyOnlyAuditEventMetadataPolicy());
            var streamId = Guid.NewGuid();
            var firstIntent = CreateIntent(streamId, Guid.NewGuid(), "audit.adapter.first");

            var first = await appender.AppendAsync(firstIntent, CancellationToken.None);
            var duplicate = await appender.AppendAsync(firstIntent, CancellationToken.None);
            Assert.False(first.Duplicate);
            Assert.True(duplicate.Duplicate);
            Assert.Equal(first.Sequence, duplicate.Sequence);
            Assert.Equal(first.IntentDigestHex, duplicate.IntentDigestHex);

            var writerA = appender.AppendAsync(
                CreateIntent(streamId, Guid.NewGuid(), "audit.adapter.concurrent.a"), CancellationToken.None);
            var writerB = appender.AppendAsync(
                CreateIntent(streamId, Guid.NewGuid(), "audit.adapter.concurrent.b"), CancellationToken.None);
            var concurrent = await Task.WhenAll(writerA, writerB);
            Assert.Equal(2, concurrent.Select(receipt => receipt.Sequence).Distinct().Count());

            var verifier = new AuditChainVerificationService(auditStore);
            Assert.True((await verifier.VerifyAsync(streamId, CancellationToken.None)).IsValid);

            var outboxStore = new PostgresAuditEventOutboxStore(dataSource, sourceSchema);
            var sourceEvent = CreateSubmission(Guid.NewGuid(), streamId, "IdentityAccess");
            await using (var rollbackConnection = new NpgsqlConnection(connectionString))
            {
                await rollbackConnection.OpenAsync();
                await using var rollback = await rollbackConnection.BeginTransactionAsync();
                await InsertSourceFactAsync(rollbackConnection, rollback, sourceSchema, sourceEvent.EventId);
                await outboxStore.EnqueueAsync(rollbackConnection, rollback, sourceEvent, CancellationToken.None);
                await rollback.RollbackAsync();
            }
            Assert.Equal(0L, await ScalarAsync(connectionString,
                $"SELECT count(*) FROM {Q(sourceSchema)}.audit_outbox_events WHERE event_id=@id;", sourceEvent.EventId));

            await using (var sourceConnection = new NpgsqlConnection(connectionString))
            {
                await sourceConnection.OpenAsync();
                await using var transaction = await sourceConnection.BeginTransactionAsync();
                await InsertSourceFactAsync(sourceConnection, transaction, sourceSchema, sourceEvent.EventId);
                await outboxStore.EnqueueAsync(sourceConnection, transaction, sourceEvent, CancellationToken.None);
                await transaction.CommitAsync();
            }
            Assert.Equal(1L, await ScalarAsync(connectionString,
                $"SELECT count(*) FROM {Q(sourceSchema)}.audit_outbox_events WHERE event_id=@id;", sourceEvent.EventId));

            await using (var duplicateConnection = new NpgsqlConnection(connectionString))
            {
                await duplicateConnection.OpenAsync();
                await using var transaction = await duplicateConnection.BeginTransactionAsync();
                await outboxStore.EnqueueAsync(duplicateConnection, transaction, sourceEvent, CancellationToken.None);
                await transaction.CommitAsync();
            }

            var changedSubmission = sourceEvent with { OutcomeCode = "denied" };
            await using (var conflictConnection = new NpgsqlConnection(connectionString))
            {
                await conflictConnection.OpenAsync();
                await using var transaction = await conflictConnection.BeginTransactionAsync();
                await Assert.ThrowsAsync<AuditEventOutboxConflictException>(
                    () => outboxStore.EnqueueAsync(conflictConnection, transaction, changedSubmission, CancellationToken.None));
                await transaction.RollbackAsync();
            }

            var leases = await outboxStore.ClaimBatchAsync(
                AuditEventOutboxWorkerIdentity.LogicalIdentity, 10, TimeSpan.FromMinutes(1), CancellationToken.None);
            var lease = Assert.Single(leases);
            Assert.Equal(sourceEvent.EventId, lease.EventId);

            var ingestion = new AuditEventIngestionContractAdapter(appender);
            var receipt = await ingestion.AppendAsync(lease.Submission, CancellationToken.None);
            await outboxStore.MarkAcknowledgedAsync(lease, receipt, CancellationToken.None);
            Assert.Equal("ACKNOWLEDGED", await ScalarTextAsync(connectionString,
                $"SELECT state FROM {Q(sourceSchema)}.audit_outbox_delivery WHERE event_id=@id;", sourceEvent.EventId));

            var noFurtherClaims = await outboxStore.ClaimBatchAsync(
                AuditEventOutboxWorkerIdentity.LogicalIdentity, 10, TimeSpan.FromMinutes(1), CancellationToken.None);
            Assert.Empty(noFurtherClaims);

            Assert.True((await verifier.VerifyAsync(streamId, CancellationToken.None)).IsValid);
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS {Q(sourceSchema)} CASCADE; DROP SCHEMA IF EXISTS {Q(auditSchema)} CASCADE;", cleanup);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task CreateAuditSchemaAsync(string cs, string schema)
    {
        await ExecuteAsync(cs, $"CREATE SCHEMA {Q(schema)};");
        await ExecuteAsync(cs, $"""
            CREATE TABLE {Q(schema)}.stream_heads
            (
                audit_stream_id uuid PRIMARY KEY,
                last_sequence bigint NOT NULL DEFAULT 0 CHECK(last_sequence>=0),
                head_hash bytea NOT NULL CHECK(octet_length(head_hash)=32),
                updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
            );
            """);
        await ExecuteAsync(cs, $"""
            CREATE TABLE {Q(schema)}.events
            (
                event_id uuid PRIMARY KEY,
                audit_stream_id uuid NOT NULL,
                schema_version smallint NOT NULL,
                canonicalization_version smallint NOT NULL,
                hash_algorithm_code text NOT NULL,
                event_type_code text NOT NULL,
                occurred_at_utc timestamptz NOT NULL,
                actor_type_code text NOT NULL,
                actor_id uuid NOT NULL,
                subject_type_code text NULL,
                subject_id uuid NULL,
                source_module_code text NOT NULL,
                source_event_id uuid NOT NULL,
                correlation_id uuid NOT NULL,
                causation_id uuid NULL,
                outcome_code text NOT NULL,
                metadata_json jsonb NOT NULL,
                stream_sequence bigint NOT NULL CHECK(stream_sequence>0),
                recorded_at_utc timestamptz NOT NULL,
                intent_digest bytea NOT NULL CHECK(octet_length(intent_digest)=32),
                previous_event_hash bytea NOT NULL CHECK(octet_length(previous_event_hash)=32),
                canonical_payload bytea NOT NULL,
                event_hash bytea NOT NULL CHECK(octet_length(event_hash)=32),
                CONSTRAINT uq_events_stream_sequence UNIQUE(audit_stream_id,stream_sequence),
                CONSTRAINT fk_events_stream FOREIGN KEY(audit_stream_id) REFERENCES {Q(schema)}.stream_heads(audit_stream_id)
            );
            """);
    }

    private static async Task CreateSourceOutboxSchemaAsync(string cs, string schema)
    {
        await ExecuteAsync(cs, $"CREATE SCHEMA {Q(schema)};");
        await ExecuteAsync(cs, $"""
            CREATE TABLE {Q(schema)}.source_fact(event_id uuid PRIMARY KEY);
            """);
        await ExecuteAsync(cs, $"""
            CREATE TABLE {Q(schema)}.audit_outbox_events
            (
                event_id uuid PRIMARY KEY,
                audit_stream_id uuid NOT NULL,
                schema_version smallint NOT NULL,
                canonicalization_version smallint NOT NULL,
                hash_algorithm_code text NOT NULL,
                event_type_code text NOT NULL,
                occurred_at_utc timestamptz NOT NULL,
                actor_type_code text NOT NULL,
                actor_id uuid NOT NULL,
                subject_type_code text NULL,
                subject_id uuid NULL,
                source_module_code text NOT NULL,
                source_event_id uuid NOT NULL,
                correlation_id uuid NOT NULL,
                causation_id uuid NULL,
                outcome_code text NOT NULL,
                metadata_json jsonb NOT NULL,
                contract_version text NOT NULL,
                intent_digest bytea NOT NULL CHECK(octet_length(intent_digest)=32),
                created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
            );
            """);
        await ExecuteAsync(cs, $"""
            CREATE TABLE {Q(schema)}.audit_outbox_delivery
            (
                event_id uuid PRIMARY KEY REFERENCES {Q(schema)}.audit_outbox_events(event_id),
                state text NOT NULL DEFAULT 'PENDING' CHECK(state IN ('PENDING','IN_FLIGHT','ACKNOWLEDGED','QUARANTINED')),
                attempts integer NOT NULL DEFAULT 0 CHECK(attempts>=0),
                next_attempt_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                lease_expires_at_utc timestamptz NULL,
                lease_token uuid NULL,
                claimed_by text NULL,
                ack_reference text NULL,
                last_failure_code text NULL,
                quarantine_reason text NULL,
                quarantined_at_utc timestamptz NULL,
                updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
            );
            """);
    }

    private static async Task InsertSourceFactAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string schema, Guid eventId)
    {
        await using var command = new NpgsqlCommand(
            $"INSERT INTO {Q(schema)}.source_fact(event_id) VALUES (@id);", connection, transaction);
        command.Parameters.AddWithValue("id", eventId);
        await command.ExecuteNonQueryAsync();
    }

    private static AuditEventIntent CreateIntent(Guid streamId, Guid eventId, string eventType) => new(
        eventId, streamId, 1, 1, "SHA-256", eventType,
        DateTimeOffset.UtcNow, "service", Guid.NewGuid(), null, null,
        "AuditSecurityTest", Guid.NewGuid(), Guid.NewGuid(), null, "succeeded",
        new Dictionary<string, string>());

    private static AuditEventSubmission CreateSubmission(Guid eventId, Guid streamId, string sourceModule) => new(
        eventId, streamId, 1, 1, "SHA-256", "identity.login.succeeded",
        DateTimeOffset.UtcNow, "account", Guid.NewGuid(), null, null,
        sourceModule, Guid.NewGuid(), Guid.NewGuid(), null, "succeeded",
        new Dictionary<string, string>(), ModuleContractVersion.Current);

    private static async Task ExecuteAsync(string cs, string sql)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(string cs, string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarTextAsync(string cs, string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Expected scalar row was not found."));
    }

    private static string Q(string identifier) => "\"" + identifier + "\"";
}
