using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable catalog proof for the four O-20 support identities.
/// migration_runner may SET ROLE only to its registered NOLOGIN schema owner;
/// backup, reporting and emergency remain deny-all until their positive contracts exist.
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
                    GRANT {Q(owner)} TO {Q(identities[0])} WITH ADMIN FALSE, INHERIT FALSE, SET TRUE;
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
                if (identity == identities[0])
                {
                    await using var migration = new NpgsqlConnection(cs);
                    await migration.OpenAsync();
                    await using (var assumeOwner = new NpgsqlCommand($"SET ROLE {Q(owner)};", migration))
                        await assumeOwner.ExecuteNonQueryAsync();
                    await using (var identityCheck = new NpgsqlCommand("SELECT current_user, session_user;", migration))
                    await using (var reader = await identityCheck.ExecuteReaderAsync())
                    {
                        Assert.True(await reader.ReadAsync());
                        Assert.Equal(owner, reader.GetString(0));
                        Assert.Equal(identity, reader.GetString(1));
                    }
                    await using (var createOwnedObject = new NpgsqlCommand(
                        $"CREATE TABLE {Q(schema)}.migration_created_probe(id integer PRIMARY KEY);", migration))
                        await createOwnedObject.ExecuteNonQueryAsync();
                    await using (var reset = new NpgsqlCommand("RESET ROLE;", migration))
                        await reset.ExecuteNonQueryAsync();
                    await AssertDeniedAsync(cs, $"SELECT * FROM {Q(schema)}.scope_probe;");
                }
                else
                {
                    await AssertDeniedAsync(cs, $"SET ROLE {Q(owner)};");
                }
                await AssertDeniedAsync(cs, $"CREATE ROLE {Q("o20_escalation_" + suffix)};");
                await AssertDeniedAsync(cs, $"SELECT {Q(schema)}.technical_probe();");
            }

            // Capture the default-ACL and PUBLIC baseline for the exact probe scope.
            await using (var catalog = new NpgsqlConnection(adminConnectionString))
            {
                await catalog.OpenAsync();
                await using var catalogCommand = new NpgsqlCommand("""
                    SELECT
                        (SELECT count(*) FROM pg_default_acl d
                         WHERE d.defaclrole IN (
                             SELECT oid FROM pg_roles WHERE rolname = ANY(@roles))),
                        (SELECT count(*) FROM pg_namespace n,
                             LATERAL aclexplode(COALESCE(n.nspacl, acldefault('n', n.nspowner))) acl
                         WHERE n.nspname=@schema AND acl.grantee=0),
                        (SELECT count(*) FROM pg_class r
                         JOIN pg_namespace n ON n.oid=r.relnamespace,
                             LATERAL aclexplode(COALESCE(r.relacl, acldefault('r', r.relowner))) acl
                         WHERE n.nspname=@schema AND r.relkind IN ('r','p','v','m')
                           AND acl.grantee=0),
                        (SELECT count(*) FROM pg_proc p
                         JOIN pg_namespace n ON n.oid=p.pronamespace,
                             LATERAL aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) acl
                         WHERE n.nspname=@schema AND acl.grantee=0);
                    """, catalog);
                catalogCommand.Parameters.AddWithValue("roles", identities.Append(owner).ToArray());
                catalogCommand.Parameters.AddWithValue("schema", schema);
                await using var reader = await catalogCommand.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                for (var i = 0; i < 4; i++)
                    Assert.Equal(0L, reader.GetInt64(i));
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
                        (SELECT count(*) FROM pg_auth_members m
                         WHERE m.member=r.oid AND m.roleid=(SELECT oid FROM pg_roles WHERE rolname=@owner)
                           AND NOT m.admin_option AND NOT m.inherit_option AND m.set_option),
                        (SELECT count(*) FROM pg_shdepend d
                         WHERE d.refclassid='pg_authid'::regclass AND d.refobjid=r.oid
                           AND d.deptype='o')
                    FROM pg_roles r WHERE r.rolname=@role;
                    """, verify);
                command.Parameters.AddWithValue("role", identity);
                command.Parameters.AddWithValue("owner", owner);
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.True(reader.GetBoolean(0));
                for (var i = 1; i <= 5; i++) Assert.False(reader.GetBoolean(i));
                Assert.False(reader.GetBoolean(6));
                Assert.Equal(identity == identities[0] ? 1L : 0L, reader.GetInt64(7));
                Assert.Equal(identity == identities[0] ? 1L : 0L, reader.GetInt64(8));
                Assert.Equal(0L, reader.GetInt64(9));
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
                has_database_privilege(@role, current_database(), 'CREATE')
            """, connection);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", $"{schema}.scope_probe");
        command.Parameters.AddWithValue("function", $"{schema}.technical_probe()");
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        for (var i = 0; i < 9; i++) Assert.False(reader.GetBoolean(i));
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
