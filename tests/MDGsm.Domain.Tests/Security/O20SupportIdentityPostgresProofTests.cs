using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Fail-closed catalog proof for the four O-20 support identities.
/// This proves the current safe baseline (distinct LOGINs, no inherited/owner powers,
/// and no operational grants); it deliberately does not claim migration, backup,
/// RowScope, or emergency positive-operation acceptance.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20SupportIdentityPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Four_support_logins_have_no_implicit_or_inherited_database_authority()
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
        var schema = "o20_support_" + suffix;
        var owner = "o20_owner_" + suffix;
        var identities = new[]
        {
            "migration_runner_" + suffix,
            "backup_operator_" + suffix,
            "reporting_ro_" + suffix,
            "emergency_operator_" + suffix
        };
        var identityConnections = identities.ToDictionary(
            name => name,
            name => WithUser(adminConnectionString, name),
            StringComparer.Ordinal);

        try
        {
            await using (var connection = new NpgsqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                var roleSql = $"""
                    CREATE ROLE {Q(owner)} NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    {string.Join(Environment.NewLine, identities.Select(name =>
                        $"CREATE ROLE {Q(name)} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;"))}
                    CREATE SCHEMA {Q(schema)} AUTHORIZATION {Q(owner)};
                    CREATE TABLE {Q(schema)}.scope_probe(id integer PRIMARY KEY, payload text NOT NULL);
                    CREATE FUNCTION {Q(schema)}.technical_probe() RETURNS integer
                        LANGUAGE sql IMMUTABLE AS 'SELECT 1';
                    ALTER TABLE {Q(schema)}.scope_probe OWNER TO {Q(owner)};
                    ALTER FUNCTION {Q(schema)}.technical_probe() OWNER TO {Q(owner)};
                    REVOKE ALL ON SCHEMA {Q(schema)} FROM PUBLIC;
                    REVOKE ALL ON ALL TABLES IN SCHEMA {Q(schema)} FROM PUBLIC;
                    REVOKE ALL ON ALL FUNCTIONS IN SCHEMA {Q(schema)} FROM PUBLIC;
                    """;
                await using var command = new NpgsqlCommand(roleSql, connection);
                await command.ExecuteNonQueryAsync();
            }

            foreach (var identity in identities)
            {
                var cs = identityConnections[identity];
                await AssertNoAuthorityAsync(adminConnectionString, identity, schema);
                await AssertDeniedAsync(cs, $"SELECT * FROM {Q(schema)}.scope_probe;");
                await AssertDeniedAsync(cs, $"INSERT INTO {Q(schema)}.scope_probe(id,payload) VALUES(1,'x');");
                await AssertDeniedAsync(cs, $"UPDATE {Q(schema)}.scope_probe SET payload='x' WHERE id=1;");
                await AssertDeniedAsync(cs, $"DELETE FROM {Q(schema)}.scope_probe;");
                await AssertDeniedAsync(cs, $"TRUNCATE TABLE {Q(schema)}.scope_probe;");
                await AssertDeniedAsync(cs, $"CREATE TABLE {Q(schema)}.forbidden_ddl(id integer);");
                await AssertDeniedAsync(cs, $"SELECT {Q(schema)}.technical_probe();");
            }

            // The identities must not own any object and must not be members of one another.
            await using var verify = new NpgsqlConnection(adminConnectionString);
            await verify.OpenAsync();
            foreach (var identity in identities)
            {
                await using var command = new NpgsqlCommand("""
                    SELECT
                        r.rolcanlogin, r.rolsuper, r.rolcreatedb, r.rolcreaterole,
                        r.rolreplication, r.rolbypassrls, r.rolinherit,
                        (SELECT count(*) FROM pg_auth_members m
                         WHERE m.member=r.oid OR m.roleid=r.oid),
                        (SELECT count(*) FROM pg_shdepend d
                         WHERE d.refclassid='pg_authid'::regclass AND d.refobjid=r.oid
                           AND d.deptype='o')
                    FROM pg_roles r WHERE r.rolname=@role;
                    """, verify);
                command.Parameters.AddWithValue("role", identity);
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.True(reader.GetBoolean(0));
                for (var i = 1; i <= 5; i++) Assert.False(reader.GetBoolean(i));
                Assert.False(reader.GetBoolean(6));
                Assert.Equal(0L, reader.GetInt64(7));
                Assert.Equal(0L, reader.GetInt64(8));
            }
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(adminConnectionString);
            await cleanup.OpenAsync();
            await using var command = new NpgsqlCommand($"""
                DROP SCHEMA IF EXISTS {Q(schema)} CASCADE;
                DROP ROLE IF EXISTS {string.Join(", ", identities.Select(Q))}, {Q(owner)};
                """, cleanup);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertNoAuthorityAsync(string adminConnectionString, string role, string schema)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT
                has_schema_privilege(@role, @schema, 'USAGE'),
                has_schema_privilege(@role, @schema, 'CREATE'),
                has_table_privilege(@role, @table, 'SELECT'),
                has_table_privilege(@role, @table, 'INSERT'),
                has_table_privilege(@role, @table, 'UPDATE'),
                has_table_privilege(@role, @table, 'DELETE'),
                has_table_privilege(@role, @table, 'TRUNCATE'),
                has_function_privilege(@role, @function, 'EXECUTE'),
                has_database_privilege(@role, current_database(), 'CREATE'),
                has_database_privilege(@role, current_database(), 'TEMP')
            """, connection);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", $"{schema}.scope_probe");
        command.Parameters.AddWithValue("function", $"{schema}.technical_probe()");
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        for (var i = 0; i < 10; i++) Assert.False(reader.GetBoolean(i));
    }

    private static async Task AssertDeniedAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
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
