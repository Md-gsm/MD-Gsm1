using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL proof of the accepted Organization assignment actor/approver policy.
/// It uses seeded synthetic principals and a seeded trusted owner binding; it is not the
/// production IdentityAccess owner-binding enrollment flow or an Organization runtime module.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OrganizationAssignmentApprovalPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Non_owner_requires_independent_approval_but_bound_product_owner_executes_directly()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_approval_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.principal
                (
                    account_id uuid PRIMARY KEY,
                    role_name text NOT NULL,
                    enabled boolean NOT NULL
                );
                CREATE TABLE {q}.owner_binding
                (
                    binding_id uuid PRIMARY KEY,
                    owner_account_id uuid NOT NULL,
                    binding_version bigint NOT NULL CHECK (binding_version > 0),
                    state text NOT NULL CHECK (state IN ('ACTIVE','REVOKED'))
                );
                CREATE UNIQUE INDEX one_active_owner_binding_per_account
                    ON {q}.owner_binding(owner_account_id) WHERE state='ACTIVE';
                CREATE TABLE {q}.warehouse
                (
                    warehouse_id uuid PRIMARY KEY,
                    state text NOT NULL CHECK (state IN ('ACTIVE','INACTIVE','ARCHIVED'))
                );
                CREATE TABLE {q}.assignment_request
                (
                    request_id uuid PRIMARY KEY,
                    actor_account_id uuid NOT NULL,
                    subject_account_id uuid NOT NULL,
                    warehouse_id uuid NOT NULL REFERENCES {q}.warehouse(warehouse_id),
                    reason text NOT NULL CHECK (length(btrim(reason)) > 0),
                    status text NOT NULL CHECK (status IN ('PENDING','APPROVED','REJECTED')),
                    approver_account_id uuid NULL,
                    execution_mode text NOT NULL CHECK (execution_mode='STANDARD_MAKER_CHECKER'),
                    requested_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );
                CREATE TABLE {q}.account_warehouse_assignment
                (
                    assignment_id uuid PRIMARY KEY,
                    subject_account_id uuid NOT NULL,
                    warehouse_id uuid NOT NULL REFERENCES {q}.warehouse(warehouse_id),
                    state text NOT NULL CHECK (state IN ('ACTIVE','REVOKED')),
                    execution_mode text NOT NULL CHECK (execution_mode IN ('STANDARD_MAKER_CHECKER','PRODUCT_OWNER_DIRECT')),
                    actor_account_id uuid NOT NULL,
                    approver_account_id uuid NULL,
                    owner_binding_version bigint NULL,
                    reason text NOT NULL CHECK (length(btrim(reason)) > 0),
                    created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                    UNIQUE(subject_account_id, warehouse_id)
                );
                CREATE TABLE {q}.audit_intent
                (
                    audit_intent_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    action text NOT NULL,
                    actor_account_id uuid NOT NULL,
                    subject_account_id uuid NOT NULL,
                    execution_mode text NOT NULL,
                    approver_account_id uuid NULL,
                    binding_version bigint NULL,
                    recorded_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );

                CREATE FUNCTION {q}.request_assignment
                (
                    p_actor uuid,
                    p_subject uuid,
                    p_warehouse uuid,
                    p_reason text,
                    p_untrusted_owner_hint boolean
                )
                RETURNS uuid LANGUAGE plpgsql AS $fn$
                DECLARE
                    owner_version bigint;
                    request_id uuid;
                    assignment_id uuid;
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM {q}.principal WHERE account_id=p_actor AND enabled) THEN
                        RAISE EXCEPTION 'actor is not an active principal' USING ERRCODE='42501';
                    END IF;
                    IF p_reason IS NULL OR length(btrim(p_reason))=0 THEN
                        RAISE EXCEPTION 'reason is required' USING ERRCODE='23514';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM {q}.warehouse WHERE warehouse_id=p_warehouse AND state='ACTIVE') THEN
                        RAISE EXCEPTION 'target Warehouse must be active' USING ERRCODE='23514';
                    END IF;

                    -- p_untrusted_owner_hint is intentionally never consulted.
                    -- Only the protected server-side owner_binding record can grant the exception.
                    SELECT binding_version INTO owner_version
                    FROM {q}.owner_binding
                    WHERE owner_account_id=p_actor AND state='ACTIVE'
                    FOR SHARE;

                    IF FOUND THEN
                        assignment_id := gen_random_uuid();
                        INSERT INTO {q}.account_warehouse_assignment
                            (assignment_id,subject_account_id,warehouse_id,state,execution_mode,
                             actor_account_id,approver_account_id,owner_binding_version,reason)
                        VALUES
                            (assignment_id,p_subject,p_warehouse,'ACTIVE','PRODUCT_OWNER_DIRECT',
                             p_actor,NULL,owner_version,p_reason);
                        INSERT INTO {q}.audit_intent
                            (action,actor_account_id,subject_account_id,execution_mode,approver_account_id,binding_version)
                        VALUES
                            ('ASSIGNMENT_DIRECT',p_actor,p_subject,'PRODUCT_OWNER_DIRECT',NULL,owner_version);
                        RETURN assignment_id;
                    END IF;

                    IF p_actor=p_subject THEN
                        RAISE EXCEPTION 'non-owner self-assignment is prohibited' USING ERRCODE='23514';
                    END IF;

                    request_id := gen_random_uuid();
                    INSERT INTO {q}.assignment_request
                        (request_id,actor_account_id,subject_account_id,warehouse_id,reason,status,execution_mode)
                    VALUES
                        (request_id,p_actor,p_subject,p_warehouse,p_reason,'PENDING','STANDARD_MAKER_CHECKER');
                    INSERT INTO {q}.audit_intent
                        (action,actor_account_id,subject_account_id,execution_mode,approver_account_id,binding_version)
                    VALUES
                        ('ASSIGNMENT_REQUESTED',p_actor,p_subject,'STANDARD_MAKER_CHECKER',NULL,NULL);
                    RETURN request_id;
                END;
                $fn$;

                CREATE FUNCTION {q}.approve_assignment(p_approver uuid,p_request uuid)
                RETURNS uuid LANGUAGE plpgsql AS $fn$
                DECLARE
                    request_row record;
                    assignment_id uuid;
                BEGIN
                    SELECT * INTO request_row
                    FROM {q}.assignment_request
                    WHERE request_id=p_request
                    FOR UPDATE;
                    IF NOT FOUND OR request_row.status<>'PENDING' THEN
                        RAISE EXCEPTION 'assignment request is not pending' USING ERRCODE='23514';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM {q}.principal WHERE account_id=p_approver AND enabled) THEN
                        RAISE EXCEPTION 'approver is not an active principal' USING ERRCODE='42501';
                    END IF;
                    IF p_approver=request_row.actor_account_id OR p_approver=request_row.subject_account_id THEN
                        RAISE EXCEPTION 'approver must be distinct from actor and subject' USING ERRCODE='23514';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM {q}.warehouse WHERE warehouse_id=request_row.warehouse_id AND state='ACTIVE') THEN
                        RAISE EXCEPTION 'target Warehouse is no longer active' USING ERRCODE='23514';
                    END IF;

                    assignment_id := gen_random_uuid();
                    INSERT INTO {q}.account_warehouse_assignment
                        (assignment_id,subject_account_id,warehouse_id,state,execution_mode,
                         actor_account_id,approver_account_id,owner_binding_version,reason)
                    VALUES
                        (assignment_id,request_row.subject_account_id,request_row.warehouse_id,'ACTIVE',
                         'STANDARD_MAKER_CHECKER',request_row.actor_account_id,p_approver,NULL,request_row.reason);
                    UPDATE {q}.assignment_request
                    SET status='APPROVED',approver_account_id=p_approver
                    WHERE request_id=p_request;
                    INSERT INTO {q}.audit_intent
                        (action,actor_account_id,subject_account_id,execution_mode,approver_account_id,binding_version)
                    VALUES
                        ('ASSIGNMENT_APPROVED',request_row.actor_account_id,request_row.subject_account_id,
                         'STANDARD_MAKER_CHECKER',p_approver,NULL);
                    RETURN assignment_id;
                END;
                $fn$;
                """);

            var actor = Guid.NewGuid();
            var subject = Guid.NewGuid();
            var approver = Guid.NewGuid();
            var superAdmin = Guid.NewGuid();
            var owner = Guid.NewGuid();
            var ownerTarget = Guid.NewGuid();
            var warehouse1 = Guid.NewGuid();
            var warehouse2 = Guid.NewGuid();
            var warehouse3 = Guid.NewGuid();
            var warehouse4 = Guid.NewGuid();

            await ExecuteAsync(cs, $"""
                INSERT INTO {q}.principal(account_id,role_name,enabled) VALUES
                    (@actor,'manager',true),
                    (@subject,'viewer',true),
                    (@approver,'manager',true),
                    (@superAdmin,'super_admin',true),
                    (@owner,'viewer',true),
                    (@ownerTarget,'viewer',true);
                INSERT INTO {q}.warehouse(warehouse_id,state) VALUES
                    (@w1,'ACTIVE'),(@w2,'ACTIVE'),(@w3,'ACTIVE'),(@w4,'ACTIVE');
                INSERT INTO {q}.owner_binding(binding_id,owner_account_id,binding_version,state)
                    VALUES (@binding,@owner,7,'ACTIVE');
                """,
                ("actor", actor), ("subject", subject), ("approver", approver),
                ("superAdmin", superAdmin), ("owner", owner), ("ownerTarget", ownerTarget),
                ("w1", warehouse1), ("w2", warehouse2), ("w3", warehouse3), ("w4", warehouse4),
                ("binding", Guid.NewGuid()));

            // A caller-supplied owner hint cannot bypass the server-side owner binding.
            var forgedSelfAssignment = await Assert.ThrowsAsync<PostgresException>(() =>
                ScalarGuidAsync(cs,
                    $"SELECT {q}.request_assignment(@actor,@actor,@warehouse,@reason,true);",
                    ("actor", actor), ("warehouse", warehouse1), ("reason", "forged owner hint")));
            Assert.Equal("23514", forgedSelfAssignment.SqlState);

            // A normal actor's operation remains pending even when the caller claims to be the owner.
            var request = await ScalarGuidAsync(cs,
                $"SELECT {q}.request_assignment(@actor,@subject,@warehouse,@reason,true);",
                ("actor", actor), ("subject", subject), ("warehouse", warehouse1), ("reason", "requested warehouse access"));
            Assert.Equal("PENDING",
                await ScalarStringAsync(cs, $"SELECT status FROM {q}.assignment_request WHERE request_id=@request;", ("request", request)));
            Assert.Equal(0L,
                await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.account_warehouse_assignment WHERE warehouse_id=@warehouse;", ("warehouse", warehouse1)));

            var actorApproves = await Assert.ThrowsAsync<PostgresException>(() =>
                ScalarGuidAsync(cs, $"SELECT {q}.approve_assignment(@approver,@request);",
                    ("approver", actor), ("request", request)));
            Assert.Equal("23514", actorApproves.SqlState);
            var subjectApproves = await Assert.ThrowsAsync<PostgresException>(() =>
                ScalarGuidAsync(cs, $"SELECT {q}.approve_assignment(@approver,@request);",
                    ("approver", subject), ("request", request)));
            Assert.Equal("23514", subjectApproves.SqlState);
            Assert.Equal("PENDING",
                await ScalarStringAsync(cs, $"SELECT status FROM {q}.assignment_request WHERE request_id=@request;", ("request", request)));

            var normalAssignment = await ScalarGuidAsync(cs,
                $"SELECT {q}.approve_assignment(@approver,@request);",
                ("approver", approver), ("request", request));
            Assert.Equal(normalAssignment,
                await ScalarGuidAsync(cs, $"SELECT assignment_id FROM {q}.account_warehouse_assignment WHERE assignment_id=@assignment;",
                    ("assignment", normalAssignment)));
            Assert.Equal(approver,
                await ScalarGuidAsync(cs, $"SELECT approver_account_id FROM {q}.account_warehouse_assignment WHERE assignment_id=@assignment;",
                    ("assignment", normalAssignment)));
            Assert.Equal("APPROVED",
                await ScalarStringAsync(cs, $"SELECT status FROM {q}.assignment_request WHERE request_id=@request;", ("request", request)));

            // A super_admin role and a forged owner hint do not create Product Owner authority.
            var superAdminSelfAssignment = await Assert.ThrowsAsync<PostgresException>(() =>
                ScalarGuidAsync(cs,
                    $"SELECT {q}.request_assignment(@actor,@actor,@warehouse,@reason,true);",
                    ("actor", superAdmin), ("warehouse", warehouse4), ("reason", "role is not owner binding")));
            Assert.Equal("23514", superAdminSelfAssignment.SqlState);

            // The exact protected owner binding allows direct self/other assignment without a second approver.
            var ownerSelfAssignment = await ScalarGuidAsync(cs,
                $"SELECT {q}.request_assignment(@actor,@actor,@warehouse,@reason,false);",
                ("actor", owner), ("warehouse", warehouse2), ("reason", "owner direct self assignment"));
            var ownerOtherAssignment = await ScalarGuidAsync(cs,
                $"SELECT {q}.request_assignment(@actor,@subject,@warehouse,@reason,false);",
                ("actor", owner), ("subject", ownerTarget), ("warehouse", warehouse3), ("reason", "owner direct other assignment"));

            Assert.Equal("PRODUCT_OWNER_DIRECT",
                await ScalarStringAsync(cs, $"SELECT execution_mode FROM {q}.account_warehouse_assignment WHERE assignment_id=@assignment;",
                    ("assignment", ownerSelfAssignment)));
            Assert.Equal(7L,
                await ScalarInt64Async(cs, $"SELECT owner_binding_version FROM {q}.account_warehouse_assignment WHERE assignment_id=@assignment;",
                    ("assignment", ownerSelfAssignment)));
            Assert.Equal(Guid.Empty,
                await ScalarGuidNullableAsEmptyAsync(cs, $"SELECT approver_account_id FROM {q}.account_warehouse_assignment WHERE assignment_id=@assignment;",
                    ("assignment", ownerOtherAssignment)));
            Assert.Equal(2L,
                await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.audit_intent WHERE execution_mode='PRODUCT_OWNER_DIRECT' AND actor_account_id=@owner;",
                    ("owner", owner)));
            Assert.Equal(1L,
                await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.audit_intent WHERE action='ASSIGNMENT_APPROVED' AND approver_account_id=@approver;",
                    ("approver", approver)));
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
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

    private static async Task<Guid> ScalarGuidAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> ScalarGuidNullableAsEmptyAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var scalarValue = await command.ExecuteScalarAsync();
        return scalarValue is null or DBNull ? Guid.Empty : (Guid)scalarValue;
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

    private static async Task DropSchemaAsync(string cs, string quotedSchema)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
