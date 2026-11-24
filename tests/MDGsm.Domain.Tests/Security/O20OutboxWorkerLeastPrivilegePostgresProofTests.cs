using System;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL proof for a proposed dedicated O21 outbox-worker identity.
/// The fixture deliberately separates immutable event payload from mutable delivery
/// metadata. It does not create a production worker, LOGIN, credential, grant or provider.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OutboxWorkerLeastPrivilegePostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Worker_can_read_immutable_event_and_update_only_delivery_metadata()
    {
        var cs = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(cs));
        var builder = new NpgsqlConnectionStringBuilder(cs);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", builder.Database);

        await using (var guard = new NpgsqlConnection(cs))
        {
            await guard.OpenAsync();
            await using var marker = new NpgsqlCommand(
                "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname=current_database();",
                guard);
            Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB;", (string?)await marker.ExecuteScalarAsync());
        }

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var schema = "o21_worker_" + suffix;
        var owner = "o21_schema_owner_" + suffix;
        var workerRole = "mdgsm_outbox_worker_" + suffix;
        var workerLogin = "outbox_worker_" + suffix;
        var runtimeRole = "mdgsm_runtime_" + suffix;
        var migrationRunner = "migration_runner_" + suffix;
        var backupOperator = "backup_operator_" + suffix;
        var reportingObserver = "reporting_ro_" + suffix;
        var emergencyOperator = "emergency_operator_" + suffix;
        var eventId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        try
        {
            await using (var admin = new NpgsqlConnection(cs))
            {
                await admin.OpenAsync();
                await using var setup = new NpgsqlCommand($"""
                    CREATE ROLE {Q(owner)}
                        NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(workerRole)}
                        NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(workerLogin)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(runtimeRole)}
                        NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(migrationRunner)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(backupOperator)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(reportingObserver)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(emergencyOperator)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;

                    GRANT {Q(workerRole)} TO {Q(workerLogin)}
                        WITH ADMIN FALSE, INHERIT FALSE, SET TRUE;

                    CREATE SCHEMA {Q(schema)} AUTHORIZATION {Q(owner)};
                    CREATE TABLE {Q(schema)}.audit_outbox_event
                    (
                        event_id uuid PRIMARY KEY,
                        contract_version integer NOT NULL,
                        payload_hash bytea NOT NULL,
                        canonical_payload jsonb NOT NULL,
                        created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                    );
                    CREATE TABLE {Q(schema)}.audit_outbox_delivery
                    (
                        event_id uuid PRIMARY KEY REFERENCES {Q(schema)}.audit_outbox_event(event_id),
                        state text NOT NULL CHECK (state IN ('PENDING','IN_FLIGHT','ACKNOWLEDGED','QUARANTINED')),
                        attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
                        lease_expires_at_utc timestamptz NULL,
                        claimed_by text NULL,
                        ack_reference text NULL,
                        quarantine_reason text NULL,
                        updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                    );
                    CREATE TABLE {Q(schema)}.branch
                    (
                        branch_id uuid PRIMARY KEY,
                        branch_code text NOT NULL
                    );
                    CREATE TABLE {Q(schema)}.warehouse
                    (
                        warehouse_id uuid PRIMARY KEY,
                        branch_id uuid NOT NULL REFERENCES {Q(schema)}.branch(branch_id)
                    );
                    CREATE TABLE {Q(schema)}.identity_credentials
                    (
                        account_id uuid PRIMARY KEY,
                        credential_hash text NOT NULL,
                        recovery_secret_hash text NULL
                    );
                    ALTER TABLE {Q(schema)}.audit_outbox_event OWNER TO {Q(owner)};
                    ALTER TABLE {Q(schema)}.audit_outbox_delivery OWNER TO {Q(owner)};
                    ALTER TABLE {Q(schema)}.branch OWNER TO {Q(owner)};
                    ALTER TABLE {Q(schema)}.warehouse OWNER TO {Q(owner)};
                    ALTER TABLE {Q(schema)}.identity_credentials OWNER TO {Q(owner)};
                    REVOKE ALL ON SCHEMA {Q(schema)} FROM PUBLIC;
                    REVOKE ALL ON ALL TABLES IN SCHEMA {Q(schema)} FROM PUBLIC;

                    GRANT USAGE ON SCHEMA {Q(schema)} TO {Q(workerRole)};
                    GRANT SELECT ON {Q(schema)}.audit_outbox_event TO {Q(workerRole)};
                    GRANT SELECT ON {Q(schema)}.audit_outbox_delivery TO {Q(workerRole)};
                    GRANT UPDATE
                        (state,attempts,lease_expires_at_utc,claimed_by,ack_reference,quarantine_reason,updated_at_utc)
                    ON {Q(schema)}.audit_outbox_delivery TO {Q(workerRole)};

                    INSERT INTO {Q(schema)}.audit_outbox_event
                        (event_id,contract_version,payload_hash,canonical_payload)
                    VALUES (@event,@version,@hash,@payload::jsonb);
                    INSERT INTO {Q(schema)}.audit_outbox_delivery(event_id,state,attempts)
                    VALUES (@event,'PENDING',0);
                    INSERT INTO {Q(schema)}.branch(branch_id,branch_code) VALUES (@branch,'B-PRIVATE');
                    INSERT INTO {Q(schema)}.warehouse(warehouse_id,branch_id) VALUES (@warehouse,@branch);
                    INSERT INTO {Q(schema)}.identity_credentials(account_id,credential_hash,recovery_secret_hash)
                    VALUES (@account,'synthetic-hash','synthetic-recovery-hash');
                    """, admin);
                setup.Parameters.AddWithValue("event", eventId);
                setup.Parameters.AddWithValue("version", 1);
                setup.Parameters.AddWithValue("hash", System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray()));
                setup.Parameters.AddWithValue("payload", """{"event":"organization.assignment.changed","reason":"test"}""");
                setup.Parameters.AddWithValue("branch", branchId);
                setup.Parameters.AddWithValue("warehouse", warehouseId);
                setup.Parameters.AddWithValue("account", accountId);
                await setup.ExecuteNonQueryAsync();
            }

            var workerConnectionString = WithUser(cs, workerLogin);
            await AssertDeniedAsync(workerConnectionString, $"SELECT * FROM {Q(schema)}.audit_outbox_event;");
            await AssertDeniedAsync(workerConnectionString, $"SELECT * FROM {Q(schema)}.audit_outbox_delivery;");
            await AssertDeniedAsync(workerConnectionString, $"SET ROLE {Q(owner)};");
            await AssertDeniedAsync(workerConnectionString, $"SET ROLE {Q(runtimeRole)};");
            await AssertDeniedAsync(workerConnectionString, $"SET ROLE {Q(migrationRunner)};");
            await AssertDeniedAsync(workerConnectionString, $"SET ROLE {Q(backupOperator)};");
            await AssertDeniedAsync(workerConnectionString, $"SET ROLE {Q(reportingObserver)};");
            await AssertDeniedAsync(workerConnectionString, $"SET ROLE {Q(emergencyOperator)};");
            await AssertDeniedAsync(workerConnectionString, $"CREATE ROLE {Q("worker_escalation_" + suffix)};");

            await using (var worker = new NpgsqlConnection(workerConnectionString))
            {
                await worker.OpenAsync();
                await using (var set = new NpgsqlCommand($"SET ROLE {Q(workerRole)};", worker))
                    await set.ExecuteNonQueryAsync();

                await using (var identity = new NpgsqlCommand("SELECT current_user,session_user;", worker))
                await using (var reader = await identity.ExecuteReaderAsync())
                {
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal(workerRole, reader.GetString(0));
                    Assert.Equal(workerLogin, reader.GetString(1));
                }

                await using (var read = new NpgsqlCommand(
                    $"SELECT contract_version,canonical_payload::text FROM {Q(schema)}.audit_outbox_event WHERE event_id=@id;", worker))
                {
                    read.Parameters.AddWithValue("id", eventId);
                    await using var reader = await read.ExecuteReaderAsync();
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Contains("organization.assignment.changed", reader.GetString(1));
                }

                await using (var update = new NpgsqlCommand($"""
                    UPDATE {Q(schema)}.audit_outbox_delivery
                    SET state='IN_FLIGHT',attempts=attempts+1,
                        lease_expires_at_utc=clock_timestamp()+interval '30 seconds',
                        claimed_by='test-worker',updated_at_utc=clock_timestamp()
                    WHERE event_id=@id;
                    """, worker))
                {
                    update.Parameters.AddWithValue("id", eventId);
                    Assert.Equal(1, await update.ExecuteNonQueryAsync());
                }

                Assert.Equal("IN_FLIGHT", await ScalarStringAsync(worker,
                    $"SELECT state FROM {Q(schema)}.audit_outbox_delivery WHERE event_id=@id;", ("id", eventId)));
                Assert.Equal(1L, await ScalarInt64Async(worker,
                    $"SELECT attempts FROM {Q(schema)}.audit_outbox_delivery WHERE event_id=@id;", ("id", eventId)));

                await AssertDeniedOnOpenConnectionAsync(worker,
                    $"UPDATE {Q(schema)}.audit_outbox_event SET canonical_payload='{{}}'::jsonb WHERE event_id='{eventId:D}';");
                await AssertDeniedOnOpenConnectionAsync(worker,
                    $"UPDATE {Q(schema)}.audit_outbox_delivery SET event_id=gen_random_uuid() WHERE event_id='{eventId:D}';");
                await AssertDeniedOnOpenConnectionAsync(worker,
                    $"DELETE FROM {Q(schema)}.audit_outbox_event WHERE event_id='{eventId:D}';");
                await AssertDeniedOnOpenConnectionAsync(worker,
                    $"DELETE FROM {Q(schema)}.audit_outbox_delivery WHERE event_id='{eventId:D}';");
                await AssertDeniedOnOpenConnectionAsync(worker,
                    $"SELECT * FROM {Q(schema)}.branch;");
                await AssertDeniedOnOpenConnectionAsync(worker,
                    $"SELECT * FROM {Q(schema)}.warehouse;");
                await AssertDeniedOnOpenConnectionAsync(worker,
                    $"SELECT * FROM {Q(schema)}.identity_credentials;");
                await AssertDeniedOnOpenConnectionAsync(worker,
                    $"UPDATE {Q(schema)}.branch SET branch_code='B-LEAK' WHERE branch_id='{branchId:D}';");
                await AssertDeniedOnOpenConnectionAsync(worker,
                    $"CREATE TABLE {Q(schema)}.worker_ddl_probe(id integer);");

                await using var reset = new NpgsqlCommand("RESET ROLE;", worker);
                await reset.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(cs);
            await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand($"""
                DROP SCHEMA IF EXISTS {Q(schema)} CASCADE;
                DROP ROLE IF EXISTS {Q(workerLogin)}, {Q(workerRole)}, {Q(runtimeRole)},
                    {Q(migrationRunner)}, {Q(backupOperator)}, {Q(reportingObserver)},
                    {Q(emergencyOperator)}, {Q(owner)};
                """, cleanup);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static string WithUser(string connectionString, string username) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Username = username,
            Password = null,
            IncludeErrorDetail = false
        }.ConnectionString;

    private static async Task AssertDeniedAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await AssertDeniedOnOpenConnectionAsync(connection, sql);
    }

    private static async Task AssertDeniedOnOpenConnectionAsync(NpgsqlConnection connection, string sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("42501", exception.SqlState);
    }

    private static async Task<long> ScalarInt64Async(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarStringAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
