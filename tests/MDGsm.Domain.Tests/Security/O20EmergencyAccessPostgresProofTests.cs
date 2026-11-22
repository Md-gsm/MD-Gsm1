using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL 18 proof of the accepted O20-F emergency activation policy.
/// This is an isolated policy PoC, not a production IdentityAccess or emergency-access implementation.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20EmergencyAccessPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Emergency_activation_is_two_person_one_use_time_bounded_and_fail_closed()
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
        var schema = "o20f_proof_" + suffix;
        var emergencyRole = "emergency_operator_" + suffix;
        var qSchema = Q(schema);
        var qRole = Q(emergencyRole);

        try
        {
            await using (var connection = new NpgsqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                var setup = $"""
                    CREATE ROLE {qRole} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
                    CREATE SCHEMA {qSchema} AUTHORIZATION CURRENT_USER;
                    CREATE TABLE {qSchema}.authorized_approver
                    (
                        principal text PRIMARY KEY,
                        is_active boolean NOT NULL
                    );
                    INSERT INTO {qSchema}.authorized_approver(principal,is_active)
                    VALUES ('approver-a',true),('approver-b',true),('requester-1',true),({Literal(emergencyRole)},true);
                    CREATE TABLE {qSchema}.emergency_grant
                    (
                        grant_id uuid PRIMARY KEY,
                        operator_principal text NOT NULL,
                        requester_principal text NOT NULL,
                        allowed_operation text NOT NULL,
                        allowed_resource text NOT NULL,
                        requested_seconds integer NOT NULL CHECK (requested_seconds BETWEEN 1 AND 1800),
                        state text NOT NULL DEFAULT 'REQUESTED'
                            CHECK (state IN ('REQUESTED','ACTIVE','REVOKED','EXPIRED','CONSUMED')),
                        approver_a text,
                        approver_b text,
                        expires_at timestamptz,
                        activated_at timestamptz,
                        revoked_at timestamptz,
                        activation_count integer NOT NULL DEFAULT 0 CHECK (activation_count BETWEEN 0 AND 1)
                    );
                    CREATE TABLE {qSchema}.audit_control
                    (
                        singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
                        fail_insert boolean NOT NULL DEFAULT false
                    );
                    INSERT INTO {qSchema}.audit_control(singleton,fail_insert) VALUES (true,false);
                    CREATE TABLE {qSchema}.audit_outbox
                    (
                        event_id uuid PRIMARY KEY,
                        grant_id uuid NOT NULL REFERENCES {qSchema}.emergency_grant(grant_id),
                        event_type text NOT NULL,
                        delivery_state text NOT NULL DEFAULT 'PENDING',
                        canonical_ack_at timestamptz NULL,
                        created_at timestamptz NOT NULL DEFAULT clock_timestamp()
                    );
                    CREATE FUNCTION {qSchema}.reject_audit_when_enabled() RETURNS trigger
                    LANGUAGE plpgsql AS $fn$
                    BEGIN
                        IF (SELECT fail_insert FROM {qSchema}.audit_control WHERE singleton) THEN
                            RAISE EXCEPTION 'simulated local audit-intent failure' USING ERRCODE='P0001';
                        END IF;
                        RETURN NEW;
                    END;
                    $fn$;
                    CREATE TRIGGER reject_audit_intent
                    BEFORE INSERT ON {qSchema}.audit_outbox
                    FOR EACH ROW EXECUTE FUNCTION {qSchema}.reject_audit_when_enabled();

                    CREATE FUNCTION {qSchema}.activate_emergency(p_grant_id uuid, p_approver_a text, p_approver_b text)
                    RETURNS boolean LANGUAGE plpgsql AS $fn$
                    DECLARE g {qSchema}.emergency_grant%ROWTYPE;
                    BEGIN
                        SELECT * INTO g FROM {qSchema}.emergency_grant
                        WHERE grant_id=p_grant_id FOR UPDATE;
                        IF NOT FOUND OR g.state <> 'REQUESTED' OR g.activation_count <> 0 THEN
                            RETURN false;
                        END IF;
                        IF p_approver_a IS NULL OR p_approver_b IS NULL OR p_approver_a=p_approver_b
                           OR p_approver_a=g.requester_principal OR p_approver_b=g.requester_principal
                           OR p_approver_a=g.operator_principal OR p_approver_b=g.operator_principal
                           OR NOT EXISTS (SELECT 1 FROM {qSchema}.authorized_approver a
                                          WHERE a.principal=p_approver_a AND a.is_active)
                           OR NOT EXISTS (SELECT 1 FROM {qSchema}.authorized_approver b
                                          WHERE b.principal=p_approver_b AND b.is_active) THEN
                            RETURN false;
                        END IF;
                        UPDATE {qSchema}.emergency_grant
                        SET state='ACTIVE', approver_a=p_approver_a, approver_b=p_approver_b,
                            activated_at=clock_timestamp(),
                            expires_at=clock_timestamp()+make_interval(secs => g.requested_seconds),
                            activation_count=1
                        WHERE grant_id=p_grant_id;
                        INSERT INTO {qSchema}.audit_outbox(event_id,grant_id,event_type)
                        VALUES (gen_random_uuid(),p_grant_id,'ACTIVATED');
                        RETURN true;
                    END;
                    $fn$;

                    CREATE FUNCTION {qSchema}.emergency_allowed(
                        p_grant_id uuid, p_principal text, p_operation text, p_resource text)
                    RETURNS boolean LANGUAGE sql AS $fn$
                        SELECT EXISTS (
                            SELECT 1 FROM {qSchema}.emergency_grant g
                            WHERE g.grant_id=p_grant_id AND g.state='ACTIVE'
                              AND g.operator_principal=p_principal
                              AND g.allowed_operation=p_operation
                              AND g.allowed_resource=p_resource
                              AND g.expires_at IS NOT NULL
                              AND clock_timestamp() < g.expires_at
                        );
                    $fn$;

                    CREATE FUNCTION {qSchema}.revoke_emergency(p_grant_id uuid)
                    RETURNS boolean LANGUAGE plpgsql AS $fn$
                    BEGIN
                        UPDATE {qSchema}.emergency_grant
                        SET state='REVOKED', revoked_at=clock_timestamp()
                        WHERE grant_id=p_grant_id AND state='ACTIVE';
                        IF NOT FOUND THEN RETURN false; END IF;
                        INSERT INTO {qSchema}.audit_outbox(event_id,grant_id,event_type)
                        VALUES (gen_random_uuid(),p_grant_id,'REVOKED');
                        RETURN true;
                    END;
                    $fn$;

                    REVOKE ALL ON SCHEMA {qSchema} FROM PUBLIC;
                    REVOKE ALL ON ALL TABLES IN SCHEMA {qSchema} FROM PUBLIC;
                    REVOKE ALL ON ALL FUNCTIONS IN SCHEMA {qSchema} FROM PUBLIC;
                    """;
                await using var command = new NpgsqlCommand(setup, connection);
                await command.ExecuteNonQueryAsync();
            }

            // The ephemeral emergency LOGIN is non-elevated, has no membership or object ownership,
            // and receives no schema/table/function/database-create authority.
            await using (var catalog = new NpgsqlConnection(adminConnectionString))
            {
                await catalog.OpenAsync();
                await using var command = new NpgsqlCommand($"""
                    SELECT r.rolcanlogin, r.rolsuper, r.rolcreatedb, r.rolcreaterole,
                           r.rolreplication, r.rolbypassrls, r.rolinherit,
                           (SELECT count(*) FROM pg_auth_members m WHERE m.member=r.oid OR m.roleid=r.oid),
                           (SELECT count(*) FROM pg_shdepend d
                            WHERE d.refclassid='pg_authid'::regclass AND d.refobjid=r.oid AND d.deptype='o'),
                           has_database_privilege(r.rolname,current_database(),'CREATE'),
                           has_schema_privilege(r.rolname,{Literal(schema)},'USAGE'),
                           has_schema_privilege(r.rolname,{Literal(schema)},'CREATE')
                    FROM pg_roles r WHERE r.rolname={Literal(emergencyRole)};
                    """, catalog);
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.True(reader.GetBoolean(0));
                for (var i = 1; i <= 5; i++) Assert.False(reader.GetBoolean(i));
                Assert.False(reader.GetBoolean(6));
                Assert.Equal(0L, reader.GetInt64(7));
                Assert.Equal(0L, reader.GetInt64(8));
                Assert.False(reader.GetBoolean(9));
                Assert.False(reader.GetBoolean(10));
                Assert.False(reader.GetBoolean(11));
            }

            // Duplicate approver, requester-as-approver and emergency-operator-as-approver all deny.
            var invalid = await InsertGrantAsync(adminConnectionString, schema, emergencyRole, "requester-1", "READ", "scope-A", 60);
            Assert.False(await ActivateAsync(adminConnectionString, schema, invalid, "approver-a", "approver-a"));
            Assert.False(await ActivateAsync(adminConnectionString, schema, invalid, "requester-1", "approver-b"));
            Assert.False(await ActivateAsync(adminConnectionString, schema, invalid, emergencyRole, "approver-b"));
            Assert.True(await ActivateAsync(adminConnectionString, schema, invalid, "approver-a", "approver-b"));
            Assert.False(await ActivateAsync(adminConnectionString, schema, invalid, "approver-a", "approver-b"));
            Assert.True(await AllowedAsync(adminConnectionString, schema, invalid, emergencyRole, "READ", "scope-A"));
            Assert.False(await AllowedAsync(adminConnectionString, schema, invalid, emergencyRole, "WRITE", "scope-A"));
            Assert.False(await AllowedAsync(adminConnectionString, schema, invalid, emergencyRole, "READ", "scope-B"));
            Assert.False(await AllowedAsync(adminConnectionString, schema, invalid, "another-principal", "READ", "scope-A"));

            // At now >= expires_at, authorization is denied even though no cleanup ran.
            await ExecuteAsync(adminConnectionString, $"UPDATE {qSchema}.emergency_grant SET expires_at=clock_timestamp() WHERE grant_id=@id;", invalid);
            Assert.False(await AllowedAsync(adminConnectionString, schema, invalid, emergencyRole, "READ", "scope-A"));

            // Revocation is synchronous at the next authorization decision.
            var revoked = await InsertGrantAsync(adminConnectionString, schema, emergencyRole, "requester-1", "READ", "scope-A", 60);
            Assert.True(await ActivateAsync(adminConnectionString, schema, revoked, "approver-a", "approver-b"));
            Assert.True(await AllowedAsync(adminConnectionString, schema, revoked, emergencyRole, "READ", "scope-A"));
            Assert.True(await ScalarBoolAsync(adminConnectionString, $"SELECT {qSchema}.revoke_emergency(@id);", revoked));
            Assert.False(await AllowedAsync(adminConnectionString, schema, revoked, emergencyRole, "READ", "scope-A"));

            // Two concurrent activations serialize on the grant row; exactly one succeeds.
            var concurrent = await InsertGrantAsync(adminConnectionString, schema, emergencyRole, "requester-1", "READ", "scope-C", 60);
            var results = await Task.WhenAll(
                ActivateAsync(adminConnectionString, schema, concurrent, "approver-a", "approver-b"),
                ActivateAsync(adminConnectionString, schema, concurrent, "approver-a", "approver-b"));
            Assert.Single(results.Where(x => x));
            Assert.Single(results.Where(x => !x));

            // Local audit-intent failure rolls back activation atomically.
            var auditFailure = await InsertGrantAsync(adminConnectionString, schema, emergencyRole, "requester-1", "READ", "scope-D", 60);
            await ExecuteAsync(adminConnectionString, $"UPDATE {qSchema}.audit_control SET fail_insert=true WHERE singleton;");
            var auditException = await Assert.ThrowsAsync<PostgresException>(() =>
                ActivateAsync(adminConnectionString, schema, auditFailure, "approver-a", "approver-b"));
            Assert.Equal("P0001", auditException.SqlState);
            var stateAfterFailure = await ScalarStringAsync(adminConnectionString,
                $"SELECT state FROM {qSchema}.emergency_grant WHERE grant_id=@id;", auditFailure);
            Assert.Equal("REQUESTED", stateAfterFailure);
            await ExecuteAsync(adminConnectionString, $"UPDATE {qSchema}.audit_control SET fail_insert=false WHERE singleton;");
            Assert.True(await ActivateAsync(adminConnectionString, schema, auditFailure, "approver-a", "approver-b"));

            // AuditSecurity delivery outage does not erase the committed local outbox intent.
            var pending = await ScalarInt64Async(adminConnectionString,
                $"SELECT count(*) FROM {qSchema}.audit_outbox WHERE grant_id=@id AND delivery_state='PENDING' AND canonical_ack_at IS NULL;", auditFailure);
            Assert.Equal(1L, pending);

            // The database CHECK constraint enforces the accepted 30-minute ceiling.
            var tooLong = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertGrantAsync(adminConnectionString, schema, emergencyRole, "requester-1", "READ", "scope-E", 1801));
            Assert.Equal("23514", tooLong.SqlState);
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(adminConnectionString);
            await cleanup.OpenAsync();
            await using var command = new NpgsqlCommand($"""
                DROP SCHEMA IF EXISTS {qSchema} CASCADE;
                DROP ROLE IF EXISTS {qRole};
                """, cleanup);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Guid> InsertGrantAsync(
        string connectionString, string schema, string operatorPrincipal, string requester,
        string operation, string resource, int requestedSeconds)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"""
            INSERT INTO {Q(schema)}.emergency_grant
                (grant_id,operator_principal,requester_principal,allowed_operation,allowed_resource,requested_seconds)
            VALUES (gen_random_uuid(),@operator,@requester,@operation,@resource,@seconds)
            RETURNING grant_id;
            """, connection);
        command.Parameters.AddWithValue("operator", operatorPrincipal);
        command.Parameters.AddWithValue("requester", requester);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("resource", resource);
        command.Parameters.AddWithValue("seconds", requestedSeconds);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static Task<bool> ActivateAsync(
        string connectionString, string schema, Guid id, string approverA, string approverB) =>
        ScalarBoolAsync(connectionString, $"SELECT {Q(schema)}.activate_emergency(@id,@a,@b);", id,
            ("a", approverA), ("b", approverB));

    private static Task<bool> AllowedAsync(
        string connectionString, string schema, Guid id, string principal, string operation, string resource) =>
        ScalarBoolAsync(connectionString,
            $"SELECT {Q(schema)}.emergency_allowed(@id,@principal,@operation,@resource);", id,
            ("principal", principal), ("operation", operation), ("resource", resource));

    private static async Task<bool> ScalarBoolAsync(
        string connectionString, string sql, Guid id, params (string Name, string Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        foreach (var item in parameters) command.Parameters.AddWithValue(item.Name, item.Value);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> ScalarStringAsync(string connectionString, string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> ScalarInt64Async(string connectionString, string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(string connectionString, string sql, Guid? id = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        if (id.HasValue) command.Parameters.AddWithValue("id", id.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
    private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
}
