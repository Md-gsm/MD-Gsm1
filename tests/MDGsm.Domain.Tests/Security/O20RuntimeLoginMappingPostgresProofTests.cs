using System;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL proof for the candidate LOGIN shim to the O20 runtime ACL role.
/// runtime_app is a LOGIN principal; mdgsm_runtime is a NOLOGIN, NOINHERIT ACL role.
/// Membership is SET-only, so application startup must explicitly assume mdgsm_runtime.
/// This proves only the role mapping shape in a marked test database, not a production
/// connection-string, secret, deployment or grant configuration.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20RuntimeLoginMappingPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Runtime_login_can_set_only_runtime_acl_role_and_cannot_assume_owner_or_support_roles()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(adminConnectionString));
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", builder.Database);

        await using (var guard = new NpgsqlConnection(adminConnectionString))
        {
            await guard.OpenAsync();
            await using var marker = new NpgsqlCommand(
                "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname=current_database();",
                guard);
            Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB;", (string?)await marker.ExecuteScalarAsync());
        }

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var schema = "o20_runtime_map_" + suffix;
        var owner = "o20_schema_owner_" + suffix;
        var runtimeRole = "mdgsm_runtime_" + suffix;
        var runtimeLogin = "runtime_app_" + suffix;
        var migrationRunner = "migration_runner_" + suffix;
        var backupOperator = "backup_operator_" + suffix;
        var reportingObserver = "reporting_ro_" + suffix;
        var emergencyOperator = "emergency_operator_" + suffix;

        try
        {
            await using (var admin = new NpgsqlConnection(adminConnectionString))
            {
                await admin.OpenAsync();
                await using var setup = new NpgsqlCommand($"""
                    CREATE ROLE {Q(owner)}
                        NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(runtimeRole)}
                        NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(runtimeLogin)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(migrationRunner)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(backupOperator)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(reportingObserver)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE ROLE {Q(emergencyOperator)}
                        LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;

                    GRANT {Q(runtimeRole)} TO {Q(runtimeLogin)}
                        WITH ADMIN FALSE, INHERIT FALSE, SET TRUE;

                    CREATE SCHEMA {Q(schema)} AUTHORIZATION {Q(owner)};
                    CREATE TABLE {Q(schema)}.runtime_probe
                    (
                        id integer PRIMARY KEY,
                        payload text NOT NULL
                    );
                    ALTER TABLE {Q(schema)}.runtime_probe OWNER TO {Q(owner)};
                    REVOKE ALL ON SCHEMA {Q(schema)} FROM PUBLIC;
                    REVOKE ALL ON ALL TABLES IN SCHEMA {Q(schema)} FROM PUBLIC;
                    GRANT USAGE ON SCHEMA {Q(schema)} TO {Q(runtimeRole)};
                    GRANT SELECT, INSERT ON {Q(schema)}.runtime_probe TO {Q(runtimeRole)};
                    """, admin);
                await setup.ExecuteNonQueryAsync();
            }

            var runtimeConnectionString = WithUser(adminConnectionString, runtimeLogin);
            await AssertRoleAttributesAsync(adminConnectionString, runtimeLogin, canLogin: true, inherits: false);
            await AssertRoleAttributesAsync(adminConnectionString, runtimeRole, canLogin: false, inherits: false);

            // A fresh connection starts as the LOGIN principal. No privileges are inherited.
            await AssertDeniedAsync(runtimeConnectionString, $"SELECT * FROM {Q(schema)}.runtime_probe;");
            await AssertDeniedAsync(runtimeConnectionString, $"INSERT INTO {Q(schema)}.runtime_probe(id,payload) VALUES(1,'before-set-role');");
            await AssertDeniedAsync(runtimeConnectionString, $"SET ROLE {Q(owner)};");
            await AssertDeniedAsync(runtimeConnectionString, $"SET ROLE {Q(migrationRunner)};");
            await AssertDeniedAsync(runtimeConnectionString, $"SET ROLE {Q(backupOperator)};");
            await AssertDeniedAsync(runtimeConnectionString, $"SET ROLE {Q(reportingObserver)};");
            await AssertDeniedAsync(runtimeConnectionString, $"SET ROLE {Q(emergencyOperator)};");
            await AssertDeniedAsync(runtimeConnectionString, $"CREATE ROLE {Q("o20_runtime_escalation_" + suffix)};");

            // The approved shim requires an explicit SET ROLE and preserves session_user.
            await using (var runtime = new NpgsqlConnection(runtimeConnectionString))
            {
                await runtime.OpenAsync();
                await using (var set = new NpgsqlCommand($"SET ROLE {Q(runtimeRole)};", runtime))
                    await set.ExecuteNonQueryAsync();

                await using (var identity = new NpgsqlCommand("SELECT current_user, session_user;", runtime))
                await using (var reader = await identity.ExecuteReaderAsync())
                {
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal(runtimeRole, reader.GetString(0));
                    Assert.Equal(runtimeLogin, reader.GetString(1));
                }

                await using (var insert = new NpgsqlCommand(
                    $"INSERT INTO {Q(schema)}.runtime_probe(id,payload) VALUES(1,'after-set-role');", runtime))
                    Assert.Equal(1, await insert.ExecuteNonQueryAsync());
                await using (var read = new NpgsqlCommand(
                    $"SELECT payload FROM {Q(schema)}.runtime_probe WHERE id=1;", runtime))
                    Assert.Equal("after-set-role", (string?)await read.ExecuteScalarAsync());

                await AssertDeniedOnOpenConnectionAsync(runtime, $"UPDATE {Q(schema)}.runtime_probe SET payload='x' WHERE id=1;");
                await AssertDeniedOnOpenConnectionAsync(runtime, $"DELETE FROM {Q(schema)}.runtime_probe;");
                await AssertDeniedOnOpenConnectionAsync(runtime, $"TRUNCATE TABLE {Q(schema)}.runtime_probe;");
                await AssertDeniedOnOpenConnectionAsync(runtime, $"CREATE TABLE {Q(schema)}.runtime_ddl_probe(id integer);");

                await using (var reset = new NpgsqlCommand("RESET ROLE;", runtime))
                    await reset.ExecuteNonQueryAsync();
                await using (var afterReset = new NpgsqlCommand("SELECT current_user, session_user;", runtime))
                await using (var reader = await afterReset.ExecuteReaderAsync())
                {
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal(runtimeLogin, reader.GetString(0));
                    Assert.Equal(runtimeLogin, reader.GetString(1));
                }
            }

            // Verify the only runtime-login membership is exactly SET-only membership in mdgsm_runtime.
            await using (var admin = new NpgsqlConnection(adminConnectionString))
            {
                await admin.OpenAsync();
                await using var membership = new NpgsqlCommand("""
                    SELECT
                        count(*),
                        count(*) FILTER (
                            WHERE roleid=(SELECT oid FROM pg_roles WHERE rolname=@runtime_role)
                              AND NOT admin_option AND NOT inherit_option AND set_option)
                    FROM pg_auth_members
                    WHERE member=(SELECT oid FROM pg_roles WHERE rolname=@runtime_login);
                    """, admin);
                membership.Parameters.AddWithValue("runtime_role", runtimeRole);
                membership.Parameters.AddWithValue("runtime_login", runtimeLogin);
                await using var reader = await membership.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(1L, reader.GetInt64(0));
                Assert.Equal(1L, reader.GetInt64(1));
            }
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(adminConnectionString);
            await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand($"""
                DROP SCHEMA IF EXISTS {Q(schema)} CASCADE;
                DROP ROLE IF EXISTS {Q(runtimeLogin)}, {Q(runtimeRole)}, {Q(migrationRunner)},
                    {Q(backupOperator)}, {Q(reportingObserver)}, {Q(emergencyOperator)}, {Q(owner)};
                """, cleanup);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertRoleAttributesAsync(
        string adminConnectionString, string role, bool canLogin, bool inherits)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT rolcanlogin, rolinherit, rolsuper, rolcreatedb, rolcreaterole,
                   rolreplication, rolbypassrls
            FROM pg_roles WHERE rolname=@role;
            """, connection);
        command.Parameters.AddWithValue("role", role);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(canLogin, reader.GetBoolean(0));
        Assert.Equal(inherits, reader.GetBoolean(1));
        for (var i = 2; i < 7; i++) Assert.False(reader.GetBoolean(i));
    }

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

    private static string WithUser(string adminConnectionString, string username) =>
        new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = username,
            Password = null,
            IncludeErrorDetail = false
        }.ConnectionString;

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
