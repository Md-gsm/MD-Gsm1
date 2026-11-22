using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL policy PoC for local Organization audit intent and at-least-once
/// delivery semantics. canonical_audit_event is a synthetic remote AuditSecurity endpoint
/// fixture inside the test database; this is not the production AuditSecurity module,
/// WORM storage, key custody, or a production outbox adapter.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OrganizationAuditOutboxPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Local_outbox_is_atomic_remote_ack_replay_is_idempotent_and_payload_conflict_quarantines()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_outbox_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.assignment_fact
                (
                    assignment_id uuid PRIMARY KEY,
                    subject_account_id uuid NOT NULL,
                    warehouse_id uuid NOT NULL,
                    created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );
                CREATE TABLE {q}.local_outbox
                (
                    event_id uuid PRIMARY KEY,
                    event_type text NOT NULL,
                    payload text NOT NULL,
                    payload_hash bytea NOT NULL CHECK (octet_length(payload_hash)=32),
                    state text NOT NULL CHECK (state IN ('PENDING','IN_FLIGHT','ACKNOWLEDGED','QUARANTINED')),
                    attempts integer NOT NULL DEFAULT 0 CHECK (attempts>=0),
                    ack_id uuid NULL,
                    created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );
                CREATE TABLE {q}.canonical_audit_event
                (
                    event_id uuid PRIMARY KEY,
                    payload_hash bytea NOT NULL CHECK (octet_length(payload_hash)=32),
                    payload text NOT NULL,
                    ack_id uuid NOT NULL UNIQUE DEFAULT gen_random_uuid(),
                    accepted_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );
                CREATE TABLE {q}.delivery_quarantine
                (
                    quarantine_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    event_id uuid NOT NULL,
                    original_hash bytea NOT NULL,
                    conflicting_hash bytea NOT NULL,
                    reason text NOT NULL,
                    quarantined_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );

                CREATE FUNCTION {q}.accept_canonical_event(p_event_id uuid,p_payload text,p_hash bytea)
                RETURNS TABLE(result text, durable_ack_id uuid)
                LANGUAGE plpgsql AS $fn$
                DECLARE
                    existing_hash bytea;
                    existing_ack uuid;
                BEGIN
                    IF octet_length(p_hash)<>32 THEN
                        RAISE EXCEPTION 'payload hash must be SHA-256' USING ERRCODE='22023';
                    END IF;

                    SELECT c.payload_hash,c.ack_id
                    INTO existing_hash,existing_ack
                    FROM {q}.canonical_audit_event c
                    WHERE c.event_id=p_event_id
                    FOR UPDATE;

                    IF FOUND THEN
                        IF existing_hash=p_hash THEN
                            RETURN QUERY SELECT 'ACKNOWLEDGED'::text,existing_ack;
                        ELSE
                            INSERT INTO {q}.delivery_quarantine
                                (event_id,original_hash,conflicting_hash,reason)
                            VALUES (p_event_id,existing_hash,p_hash,'EVENT_ID_PAYLOAD_HASH_MISMATCH');
                            RETURN QUERY SELECT 'INTEGRITY_MISMATCH'::text,existing_ack;
                        END IF;
                        RETURN;
                    END IF;

                    INSERT INTO {q}.canonical_audit_event(event_id,payload_hash,payload)
                    VALUES (p_event_id,p_hash,p_payload)
                    RETURNING ack_id INTO existing_ack;

                    RETURN QUERY SELECT 'ACKNOWLEDGED'::text,existing_ack;
                END;
                $fn$;
                """);

            var assignmentId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var payload = $$"""{"action":"organization.assignment.warehouse.created","assignmentId":"{{assignmentId:D}}","subject":"{{subjectId:D}}","warehouse":"{{warehouseId:D}}"}""";
            var originalHash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

            // Inject a local failure after both writes have been staged: neither fact nor intent may survive.
            var rolledBackAssignmentId = Guid.NewGuid();
            var rolledBackEventId = Guid.NewGuid();
            await using (var rollbackConnection = new NpgsqlConnection(cs))
            {
                await rollbackConnection.OpenAsync();
                await using var rollbackTx = await rollbackConnection.BeginTransactionAsync();
                await using (var fact = new NpgsqlCommand(
                    $"INSERT INTO {q}.assignment_fact(assignment_id,subject_account_id,warehouse_id) VALUES (@id,@subject,@warehouse);",
                    rollbackConnection, rollbackTx))
                {
                    fact.Parameters.AddWithValue("id", rolledBackAssignmentId);
                    fact.Parameters.AddWithValue("subject", subjectId);
                    fact.Parameters.AddWithValue("warehouse", warehouseId);
                    await fact.ExecuteNonQueryAsync();
                }
                await using (var intent = new NpgsqlCommand(
                    $"INSERT INTO {q}.local_outbox(event_id,event_type,payload,payload_hash,state) VALUES (@event,'organization.assignment.warehouse.created',@payload,@hash,'PENDING');",
                    rollbackConnection, rollbackTx))
                {
                    intent.Parameters.AddWithValue("event", rolledBackEventId);
                    intent.Parameters.AddWithValue("payload", payload);
                    intent.Parameters.AddWithValue("hash", originalHash);
                    await intent.ExecuteNonQueryAsync();
                }
                await rollbackTx.RollbackAsync();
            }
            Assert.Equal(0L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.assignment_fact WHERE assignment_id=@id;", ("id", rolledBackAssignmentId)));
            Assert.Equal(0L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.local_outbox WHERE event_id=@event;", ("event", rolledBackEventId)));

            // Domain fact and outbox intent are one local transaction.
            await using (var connection = new NpgsqlConnection(cs))
            {
                await connection.OpenAsync();
                await using var tx = await connection.BeginTransactionAsync();
                await using (var fact = new NpgsqlCommand(
                    $"INSERT INTO {q}.assignment_fact(assignment_id,subject_account_id,warehouse_id) VALUES (@id,@subject,@warehouse);",
                    connection, tx))
                {
                    fact.Parameters.AddWithValue("id", assignmentId);
                    fact.Parameters.AddWithValue("subject", subjectId);
                    fact.Parameters.AddWithValue("warehouse", warehouseId);
                    await fact.ExecuteNonQueryAsync();
                }
                await using (var intent = new NpgsqlCommand(
                    $"INSERT INTO {q}.local_outbox(event_id,event_type,payload,payload_hash,state) VALUES (@event,'organization.assignment.warehouse.created',@payload,@hash,'PENDING');",
                    connection, tx))
                {
                    intent.Parameters.AddWithValue("event", eventId);
                    intent.Parameters.AddWithValue("payload", payload);
                    intent.Parameters.AddWithValue("hash", originalHash);
                    await intent.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
            }

            Assert.Equal(1L, await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.assignment_fact WHERE assignment_id=@id;", ("id", assignmentId)));
            Assert.Equal("PENDING", await ScalarStringAsync(cs, $"SELECT state FROM {q}.local_outbox WHERE event_id=@event;", ("event", eventId)));

            // Simulate remote service outage: no remote call/ACK occurs; local PENDING intent remains durable.
            Assert.Equal(0L, await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.canonical_audit_event WHERE event_id=@event;", ("event", eventId)));
            Assert.Equal("PENDING", await ScalarStringAsync(cs, $"SELECT state FROM {q}.local_outbox WHERE event_id=@event;", ("event", eventId)));

            // First remote acceptance commits, but the local worker loses the response before ACK update.
            var firstAck = await AcceptEventAsync(cs, q, eventId, payload, originalHash);
            Assert.Equal("ACKNOWLEDGED", firstAck.Status);
            Assert.NotEqual(Guid.Empty, firstAck.AckId);

            // Retry uses the exact same event ID and bytes. It must return the same durable ACK.
            var replayAck = await AcceptEventAsync(cs, q, eventId, payload, originalHash);
            Assert.Equal("ACKNOWLEDGED", replayAck.Status);
            Assert.Equal(firstAck.AckId, replayAck.AckId);
            Assert.Equal(1L, await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.canonical_audit_event WHERE event_id=@event;", ("event", eventId)));

            await ExecuteAsync(cs,
                $"UPDATE {q}.local_outbox SET state='ACKNOWLEDGED',ack_id=@ack,attempts=attempts+2 WHERE event_id=@event AND state='PENDING';",
                ("ack", firstAck.AckId), ("event", eventId));
            Assert.Equal("ACKNOWLEDGED", await ScalarStringAsync(cs, $"SELECT state FROM {q}.local_outbox WHERE event_id=@event;", ("event", eventId)));

            // Reusing EventId with changed bytes/hash must never overwrite the canonical original.
            var conflictingPayload = payload + " ";
            var conflictingHash = SHA256.HashData(Encoding.UTF8.GetBytes(conflictingPayload));
            var conflict = await AcceptEventAsync(cs, q, eventId, conflictingPayload, conflictingHash);
            Assert.Equal("INTEGRITY_MISMATCH", conflict.Status);
            Assert.Equal(firstAck.AckId, conflict.AckId);
            Assert.Equal(1L, await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.delivery_quarantine WHERE event_id=@event;", ("event", eventId)));
            Assert.Equal(Convert.ToHexString(originalHash),
                Convert.ToHexString(await ScalarBytesAsync(cs, $"SELECT payload_hash FROM {q}.canonical_audit_event WHERE event_id=@event;", ("event", eventId))));
            Assert.Equal(1L, await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.canonical_audit_event WHERE event_id=@event;", ("event", eventId)));
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Competing_outbox_workers_do_not_claim_the_same_pending_row()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_claim_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.outbox
                (
                    event_id uuid PRIMARY KEY,
                    state text NOT NULL CHECK(state IN ('PENDING','IN_FLIGHT','ACKNOWLEDGED')),
                    claimed_by text NULL
                );
                """);
            var eventId = Guid.NewGuid();
            await ExecuteAsync(cs, $"INSERT INTO {q}.outbox(event_id,state) VALUES (@id,'PENDING');", ("id", eventId));

            await using var worker1 = new NpgsqlConnection(cs);
            await using var worker2 = new NpgsqlConnection(cs);
            await worker1.OpenAsync();
            await worker2.OpenAsync();
            await using var tx1 = await worker1.BeginTransactionAsync();
            await using var tx2 = await worker2.BeginTransactionAsync();

            var firstClaim = await TryClaimAsync(worker1, tx1, q, "worker-1");
            Assert.Equal(eventId, firstClaim);

            // SKIP LOCKED makes the second independent worker skip the row locked by worker 1.
            var secondClaim = await TryClaimAsync(worker2, tx2, q, "worker-2");
            Assert.Null(secondClaim);

            await using (var update = new NpgsqlCommand(
                $"UPDATE {q}.outbox SET state='IN_FLIGHT',claimed_by='worker-1' WHERE event_id=@event AND state='PENDING';",
                worker1, tx1))
            {
                update.Parameters.AddWithValue("event", eventId);
                Assert.Equal(1, await update.ExecuteNonQueryAsync());
            }

            await tx1.CommitAsync();
            await tx2.CommitAsync();

            Assert.Equal("worker-1",
                await ScalarStringAsync(cs, $"SELECT claimed_by FROM {q}.outbox WHERE event_id=@event;", ("event", eventId)));
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }

    private static async Task<(string Status, Guid AckId)> AcceptEventAsync(
        string cs, string quotedSchema, Guid eventId, string payload, byte[] hash)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT result,durable_ack_id FROM {quotedSchema}.accept_canonical_event(@event,@payload,@hash);",
            connection);
        command.Parameters.AddWithValue("event", eventId);
        command.Parameters.AddWithValue("payload", payload);
        command.Parameters.AddWithValue("hash", hash);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetGuid(1));
    }

    private static async Task<Guid?> TryClaimAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string quotedSchema, string workerName)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT event_id FROM {quotedSchema}.outbox WHERE state='PENDING' ORDER BY event_id FOR UPDATE SKIP LOCKED LIMIT 1;",
            connection, transaction);
        var result = await command.ExecuteScalarAsync();
        return result is Guid id ? id : null;
    }

    private static async Task<string> OpenMarkedDisposableDatabaseAsync()
    {
        var cs = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(cs));
        var builder = new NpgsqlConnectionStringBuilder(cs);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", builder.Database);
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var marker = new NpgsqlCommand(
            "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname=current_database();",
            connection);
        Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB;", (string?)await marker.ExecuteScalarAsync());
        return cs!;
    }

    private static async Task ExecuteAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarInt64Async(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarStringAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<byte[]> ScalarBytesAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (byte[])(await command.ExecuteScalarAsync())!;
    }

    private static async Task DropSchemaAsync(string cs, string quotedSchema)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
