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
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The marked disposable PostgreSQL integration connection string is required.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("The disposable PostgreSQL database name is required.");
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", databaseName);

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
        var workerLogin = "audit_delivery_worker_" + suffix;
        var workerAclRole = "mdgsm_audit_delivery_worker_" + suffix;
        var workerPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        try
        {
            await CreateAuditSchemaAsync(connectionString, auditSchema);
            await CreateSourceOutboxSchemaAsync(connectionString, sourceSchema);
            await CreateWorkerRoleAsync(connectionString, sourceSchema, workerLogin, workerAclRole, workerPassword);
            var workerBuilder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Username = workerLogin,
                Password = workerPassword
            };
            await using var workerDataSource = NpgsqlDataSource.Create(workerBuilder.ConnectionString);
            await using (var noRole = new NpgsqlConnection(workerBuilder.ConnectionString))
            {
                await noRole.OpenAsync();
                await using var forbidden = new NpgsqlCommand(
                    $"SELECT state FROM {Q(sourceSchema)}.audit_outbox_delivery LIMIT 1;", noRole);
                await Assert.ThrowsAsync<PostgresException>(() => forbidden.ExecuteScalarAsync());
            }
            await using (var roleBoundary = new NpgsqlConnection(workerBuilder.ConnectionString))
            {
                await roleBoundary.OpenAsync();
                await using (var setRole = new NpgsqlCommand($"SET ROLE {Q(workerAclRole)};", roleBoundary))
                    await setRole.ExecuteNonQueryAsync();
                await using var forbiddenLedger = new NpgsqlCommand(
                    $"SELECT event_id FROM {Q(auditSchema)}.events LIMIT 1;", roleBoundary);
                await Assert.ThrowsAsync<PostgresException>(() => forbiddenLedger.ExecuteScalarAsync());
                await using var resetRole = new NpgsqlCommand("RESET ROLE;", roleBoundary);
                await resetRole.ExecuteNonQueryAsync();
            }

            var auditStore = new PostgresAuditEventStore(dataSource, auditSchema);
            var appender = new AuditEventAppender(auditStore, new EmptyOnlyAuditEventMetadataPolicy());
            var streamId = Guid.NewGuid();
            var firstIntent = CreateIntent(streamId, Guid.NewGuid(), "audit.adapter.first");

            // A partially staged event + head update must both roll back when the transaction is disposed.
            var rollbackStream = Guid.NewGuid();
            await using (var staged = await auditStore.BeginAppendTransactionAsync(rollbackStream, CancellationToken.None))
            {
                var state = await staged.LockStreamHeadAsync(CancellationToken.None);
                var stagedIntent = CreateIntent(rollbackStream, Guid.NewGuid(), "audit.adapter.rollback");
                var stagedRecord = AuditEventCanonicalizer.CreateRecord(
                    stagedIntent, 1, state.DatabaseRecordedAtUtc, state.Head.HeadHash.Span);
                await staged.InsertEventAsync(stagedRecord, CancellationToken.None);
                await staged.AdvanceStreamHeadAsync(
                    rollbackStream, 1, stagedRecord.EventHash, CancellationToken.None);
                // Deliberately omit CommitAsync.
            }
            var rolledBack = await auditStore.ReadSnapshotAsync(rollbackStream, CancellationToken.None);
            Assert.Empty(rolledBack.Events);
            Assert.Equal(0L, rolledBack.Head.LastSequence);

            var first = await appender.AppendAsync(firstIntent, CancellationToken.None);
            var duplicate = await appender.AppendAsync(firstIntent, CancellationToken.None);
            Assert.False(first.Duplicate);
            Assert.True(duplicate.Duplicate);
            Assert.Equal(first.Sequence, duplicate.Sequence);
            Assert.Equal(first.IntentDigestHex, duplicate.IntentDigestHex);
            Assert.Equal(AuditEventSubmissionCanonicalizer.ComputeIntentDigestHex(
                ToSubmission(firstIntent)), first.IntentDigestHex);

            var changedIntent = firstIntent with { OutcomeCode = "denied" };
            await Assert.ThrowsAsync<AuditEventConflictException>(
                () => appender.AppendAsync(changedIntent, CancellationToken.None));

            var writerA = appender.AppendAsync(
                CreateIntent(streamId, Guid.NewGuid(), "audit.adapter.concurrent.a"), CancellationToken.None);
            var writerB = appender.AppendAsync(
                CreateIntent(streamId, Guid.NewGuid(), "audit.adapter.concurrent.b"), CancellationToken.None);
            var concurrent = await Task.WhenAll(writerA, writerB);
            Assert.Equal(2, concurrent.Select(receipt => receipt.Sequence).Distinct().Count());

            var verifier = new AuditChainVerificationService(auditStore);
            Assert.True((await verifier.VerifyAsync(streamId, CancellationToken.None)).IsValid);

            var outboxStore = new PostgresAuditEventOutboxStore(
                workerDataSource, sourceSchema, workerLogin, workerAclRole);
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

            // After SET ROLE, the worker may SELECT immutable envelopes but cannot update them or EventId.
            await using (var immutableGuard = new NpgsqlConnection(workerBuilder.ConnectionString))
            {
                await immutableGuard.OpenAsync();
                await using (var setRole = new NpgsqlCommand($"SET ROLE {Q(workerAclRole)};", immutableGuard))
                    await setRole.ExecuteNonQueryAsync();
                await using var mutateEnvelope = new NpgsqlCommand(
                    $"UPDATE {Q(sourceSchema)}.audit_outbox_events SET outcome_code='tampered' WHERE event_id=@id;",
                    immutableGuard);
                mutateEnvelope.Parameters.AddWithValue("id", sourceEvent.EventId);
                await Assert.ThrowsAsync<PostgresException>(() => mutateEnvelope.ExecuteNonQueryAsync());
                await using var mutateDeliveryIdentity = new NpgsqlCommand(
                    $"UPDATE {Q(sourceSchema)}.audit_outbox_delivery SET event_id=@other WHERE event_id=@id;",
                    immutableGuard);
                mutateDeliveryIdentity.Parameters.AddWithValue("other", Guid.NewGuid());
                mutateDeliveryIdentity.Parameters.AddWithValue("id", sourceEvent.EventId);
                await Assert.ThrowsAsync<PostgresException>(() => mutateDeliveryIdentity.ExecuteNonQueryAsync());
                await using var resetRole = new NpgsqlCommand("RESET ROLE;", immutableGuard);
                await resetRole.ExecuteNonQueryAsync();
            }

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

            var expiringEvent = CreateSubmission(Guid.NewGuid(), streamId, "IdentityAccess");
            await using (var sourceConnection = new NpgsqlConnection(connectionString))
            {
                await sourceConnection.OpenAsync();
                await using var transaction = await sourceConnection.BeginTransactionAsync();
                await InsertSourceFactAsync(sourceConnection, transaction, sourceSchema, expiringEvent.EventId);
                await outboxStore.EnqueueAsync(sourceConnection, transaction, expiringEvent, CancellationToken.None);
                await transaction.CommitAsync();
            }

            var expiringLease = Assert.Single(await outboxStore.ClaimBatchAsync(
                AuditEventOutboxWorkerIdentity.LogicalIdentity, 10, TimeSpan.FromMilliseconds(100), CancellationToken.None));
            var expiringReceipt = await ingestion.AppendAsync(expiringLease.Submission, CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            await Assert.ThrowsAsync<AuditEventOutboxLeaseLostException>(
                () => outboxStore.MarkAcknowledgedAsync(expiringLease, expiringReceipt, CancellationToken.None));

            var reclaimedLease = Assert.Single(await outboxStore.ClaimBatchAsync(
                AuditEventOutboxWorkerIdentity.LogicalIdentity, 10, TimeSpan.FromMinutes(1), CancellationToken.None));
            await Assert.ThrowsAsync<AuditEventOutboxLeaseLostException>(
                () => outboxStore.MarkAcknowledgedAsync(expiringLease, expiringReceipt, CancellationToken.None));
            await outboxStore.MarkAcknowledgedAsync(reclaimedLease, expiringReceipt, CancellationToken.None);

            Assert.True((await verifier.VerifyAsync(streamId, CancellationToken.None)).IsValid);
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS {Q(sourceSchema)} CASCADE; DROP SCHEMA IF EXISTS {Q(auditSchema)} CASCADE; " +
                $"REVOKE CONNECT ON DATABASE {Q(databaseName)} FROM {Q(workerLogin)}; " +
                $"REVOKE {Q(workerAclRole)} FROM {Q(workerLogin)}; " +
                $"DROP ROLE IF EXISTS {Q(workerLogin)}; DROP ROLE IF EXISTS {Q(workerAclRole)};", cleanup);
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

    private static async Task CreateWorkerRoleAsync(
        string cs, string schema, string login, string aclRole, string password)
    {
        await ExecuteAsync(cs, $"""
            CREATE ROLE {Q(aclRole)} NOLOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
            """);
        await ExecuteAsync(cs, $"""
            CREATE ROLE {Q(login)} LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS
            PASSWORD '{password}';
            """);
        await ExecuteAsync(cs, $"GRANT {Q(aclRole)} TO {Q(login)} WITH ADMIN FALSE, INHERIT FALSE, SET TRUE;");
        var databaseName = new NpgsqlConnectionStringBuilder(cs).Database;
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("The disposable PostgreSQL database name is required.");
        await ExecuteAsync(cs, $"GRANT CONNECT ON DATABASE {Q(databaseName)} TO {Q(login)};");
        await ExecuteAsync(cs, $"GRANT USAGE ON SCHEMA {Q(schema)} TO {Q(aclRole)};");
        await ExecuteAsync(cs, $"GRANT SELECT ON {Q(schema)}.audit_outbox_events TO {Q(aclRole)};");
        await ExecuteAsync(cs, $"GRANT SELECT ON {Q(schema)}.audit_outbox_delivery TO {Q(aclRole)};");
        await ExecuteAsync(cs, $"""
            GRANT UPDATE
            (
                state, attempts, next_attempt_at_utc, lease_expires_at_utc, lease_token,
                claimed_by, ack_reference, last_failure_code, quarantine_reason,
                quarantined_at_utc, updated_at_utc
            )
            ON {Q(schema)}.audit_outbox_delivery TO {Q(aclRole)};
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

    private static AuditEventSubmission ToSubmission(AuditEventIntent intent) => new(
        intent.EventId, intent.AuditStreamId, intent.SchemaVersion, intent.CanonicalizationVersion,
        intent.HashAlgorithmCode, intent.EventTypeCode, intent.OccurredAtUtc, intent.ActorTypeCode,
        intent.ActorId, intent.SubjectTypeCode, intent.SubjectId, intent.SourceModuleCode,
        intent.SourceEventId, intent.CorrelationId, intent.CausationId, intent.OutcomeCode,
        intent.Metadata, ModuleContractVersion.Current);

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
