using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL proof of OD-05 fail-closed retention and Legal Hold eligibility.
/// It does not delete records, select a statutory retention duration, or implement a
/// production Legal Hold service or Organization data model.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OrganizationLegalHoldRetentionPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Disposal_eligibility_denies_until_schedule_hold_and_independent_authority_are_valid()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_hold_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.protected_history
                (
                    record_id uuid PRIMARY KEY,
                    record_class text NOT NULL,
                    retention_until_utc timestamptz NOT NULL,
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.retention_schedule
                (
                    record_class text PRIMARY KEY,
                    schedule_version bigint NOT NULL CHECK (schedule_version > 0),
                    status text NOT NULL CHECK (status IN ('UNSET','DRAFT','APPROVED','SUSPENDED')),
                    retention_period_days integer NULL CHECK (retention_period_days IS NULL OR retention_period_days > 0),
                    approved_by uuid NULL,
                    approved_at_utc timestamptz NULL
                );
                CREATE TABLE {q}.hold_resolver_control
                (
                    singleton boolean PRIMARY KEY DEFAULT true CHECK(singleton),
                    available boolean NOT NULL
                );
                CREATE TABLE {q}.legal_hold
                (
                    hold_id uuid PRIMARY KEY,
                    record_id uuid NOT NULL REFERENCES {q}.protected_history(record_id),
                    state text NOT NULL CHECK (state IN ('PENDING','ACTIVE','RELEASED','UNKNOWN')),
                    changed_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );
                CREATE TABLE {q}.disposal_authorization
                (
                    authorization_id uuid PRIMARY KEY,
                    record_id uuid NOT NULL REFERENCES {q}.protected_history(record_id),
                    schedule_version bigint NOT NULL,
                    actor_account_id uuid NOT NULL,
                    approver_account_id uuid NOT NULL,
                    records_legal_authority_id uuid NOT NULL,
                    independently_approved boolean NOT NULL,
                    reason text NOT NULL,
                    expires_at_utc timestamptz NOT NULL,
                    consumed boolean NOT NULL DEFAULT false
                );

                CREATE FUNCTION {q}.can_dispose
                (
                    p_record_id uuid,
                    p_authorization_id uuid,
                    p_actor uuid
                )
                RETURNS boolean
                LANGUAGE plpgsql AS $fn$
                DECLARE
                    record_row {q}.protected_history%ROWTYPE;
                    schedule_row {q}.retention_schedule%ROWTYPE;
                    authorization_row {q}.disposal_authorization%ROWTYPE;
                    resolver_available boolean;
                BEGIN
                    SELECT * INTO record_row
                    FROM {q}.protected_history
                    WHERE record_id=p_record_id
                    FOR SHARE;
                    IF NOT FOUND THEN
                        RETURN false;
                    END IF;

                    SELECT * INTO schedule_row
                    FROM {q}.retention_schedule
                    WHERE record_class=record_row.record_class
                    FOR SHARE;
                    IF NOT FOUND
                       OR schedule_row.status <> 'APPROVED'
                       OR schedule_row.retention_period_days IS NULL
                       OR schedule_row.approved_by IS NULL
                       OR schedule_row.approved_at_utc IS NULL THEN
                        RETURN false;
                    END IF;

                    IF record_row.retention_until_utc > clock_timestamp() THEN
                        RETURN false;
                    END IF;

                    SELECT available INTO resolver_available
                    FROM {q}.hold_resolver_control
                    WHERE singleton=true
                    FOR SHARE;
                    IF NOT FOUND OR NOT resolver_available THEN
                        RETURN false;
                    END IF;

                    -- Any active, pending or unknown hold blocks disposal. Only RELEASED is safe.
                    IF EXISTS
                    (
                        SELECT 1 FROM {q}.legal_hold
                        WHERE record_id=p_record_id AND state <> 'RELEASED'
                    ) THEN
                        RETURN false;
                    END IF;

                    SELECT * INTO authorization_row
                    FROM {q}.disposal_authorization
                    WHERE authorization_id=p_authorization_id AND record_id=p_record_id
                    FOR UPDATE;
                    IF NOT FOUND
                       OR NOT authorization_row.independently_approved
                       OR authorization_row.consumed
                       OR authorization_row.schedule_version <> schedule_row.schedule_version
                       OR authorization_row.actor_account_id <> p_actor
                       OR authorization_row.actor_account_id = authorization_row.approver_account_id
                       OR authorization_row.records_legal_authority_id IS NULL
                       OR authorization_row.reason IS NULL
                       OR length(btrim(authorization_row.reason))=0
                       OR authorization_row.expires_at_utc <= clock_timestamp() THEN
                        RETURN false;
                    END IF;

                    -- This function only evaluates eligibility; it never deletes or masks a record.
                    RETURN true;
                END;
                $fn$;

                CREATE FUNCTION {q}.place_hold(p_record_id uuid,p_hold_id uuid,p_state text)
                RETURNS void LANGUAGE plpgsql AS $fn$
                BEGIN
                    PERFORM 1 FROM {q}.protected_history
                    WHERE record_id=p_record_id
                    FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'record not found' USING ERRCODE='P0002';
                    END IF;
                    INSERT INTO {q}.legal_hold(hold_id,record_id,state)
                    VALUES (p_hold_id,p_record_id,p_state);
                    UPDATE {q}.protected_history
                    SET row_version=row_version+1
                    WHERE record_id=p_record_id;
                END;
                $fn$;
                """);

            var record = Guid.NewGuid();
            var actor = Guid.NewGuid();
            var approver = Guid.NewGuid();
            var legalAuthority = Guid.NewGuid();
            var scheduleVersion = 4L;
            var authorization = Guid.NewGuid();
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.protected_history(record_id,record_class,retention_until_utc) VALUES (@id,'ORG_ASSIGNMENT_HISTORY',clock_timestamp()+interval '2 days');",
                ("id", record));
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.retention_schedule(record_class,schedule_version,status,retention_period_days,approved_by,approved_at_utc) VALUES ('ORG_ASSIGNMENT_HISTORY',@version,'UNSET',NULL,NULL,NULL);",
                ("version", scheduleVersion));
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.hold_resolver_control(singleton,available) VALUES (true,false);");

            Assert.False(await EligibleAsync(cs, q, record, authorization, actor));

            // Unknown/unavailable hold resolution is deny, even if a caller fabricates approval material.
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.disposal_authorization(authorization_id,record_id,schedule_version,actor_account_id,approver_account_id,records_legal_authority_id,independently_approved,reason,expires_at_utc) VALUES (@id,@record,@version,@actor,@approver,@authority,true,'test reason',clock_timestamp()+interval '5 minutes');",
                ("id", authorization), ("record", record), ("version", scheduleVersion), ("actor", actor),
                ("approver", approver), ("authority", legalAuthority));
            Assert.False(await EligibleAsync(cs, q, record, authorization, actor));

            await ExecuteAsync(cs, $"UPDATE {q}.hold_resolver_control SET available=true WHERE singleton=true;");
            Assert.False(await EligibleAsync(cs, q, record, authorization, actor)); // No approved schedule.

            await ExecuteAsync(cs,
                $"UPDATE {q}.retention_schedule SET status='APPROVED',retention_period_days=365,approved_by=@authority,approved_at_utc=clock_timestamp() WHERE record_class='ORG_ASSIGNMENT_HISTORY';",
                ("authority", legalAuthority));
            Assert.False(await EligibleAsync(cs, q, record, authorization, actor)); // Synthetic future retention deadline.
            await ExecuteAsync(cs,
                $"UPDATE {q}.protected_history SET retention_until_utc=clock_timestamp()-interval '1 second' WHERE record_id=@id;",
                ("id", record));

            var activeHold = Guid.NewGuid();
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.legal_hold(hold_id,record_id,state) VALUES (@hold,@record,'ACTIVE');",
                ("hold", activeHold), ("record", record));
            Assert.False(await EligibleAsync(cs, q, record, authorization, actor));
            await ExecuteAsync(cs, $"UPDATE {q}.legal_hold SET state='RELEASED' WHERE hold_id=@hold;", ("hold", activeHold));

            // Self-approval is denied even with an otherwise valid schedule and authority record.
            await ExecuteAsync(cs,
                $"UPDATE {q}.disposal_authorization SET approver_account_id=actor_account_id WHERE authorization_id=@id;",
                ("id", authorization));
            Assert.False(await EligibleAsync(cs, q, record, authorization, actor));
            await ExecuteAsync(cs,
                $"UPDATE {q}.disposal_authorization SET approver_account_id=@approver WHERE authorization_id=@id;",
                ("approver", approver), ("id", authorization));

            Assert.True(await EligibleAsync(cs, q, record, authorization, actor));

            await ExecuteAsync(cs, $"UPDATE {q}.hold_resolver_control SET available=false WHERE singleton=true;");
            Assert.False(await EligibleAsync(cs, q, record, authorization, actor));
            Assert.Equal(1L, await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.protected_history WHERE record_id=@id;", ("id", record)));
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_hold_placement_serializes_before_disposal_eligibility_decision()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_holdrace_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await CreateEligibilityFixtureAsync(cs, q);
            var record = Guid.NewGuid();
            var actor = Guid.NewGuid();
            var approver = Guid.NewGuid();
            var authority = Guid.NewGuid();
            var auth = Guid.NewGuid();
            await SeedEligibleFixtureAsync(cs, q, record, actor, approver, authority, auth);

            await using var holdConnection = new NpgsqlConnection(cs);
            await holdConnection.OpenAsync();
            await using var holdTx = await holdConnection.BeginTransactionAsync();
            await using (var lockRecord = new NpgsqlCommand(
                $"SELECT record_id FROM {q}.protected_history WHERE record_id=@id FOR UPDATE;",
                holdConnection, holdTx))
            {
                lockRecord.Parameters.AddWithValue("id", record);
                Assert.Equal(record, (Guid)(await lockRecord.ExecuteScalarAsync())!);
            }

            await using var decisionConnection = new NpgsqlConnection(cs);
            await decisionConnection.OpenAsync();
            var backendPid = Convert.ToInt32(await new NpgsqlCommand("SELECT pg_backend_pid();", decisionConnection).ExecuteScalarAsync());
            await using var decisionCommand = new NpgsqlCommand(
                $"SELECT {q}.can_dispose(@record,@authorization,@actor);", decisionConnection);
            decisionCommand.Parameters.AddWithValue("record", record);
            decisionCommand.Parameters.AddWithValue("authorization", auth);
            decisionCommand.Parameters.AddWithValue("actor", actor);
            var decisionTask = decisionCommand.ExecuteScalarAsync();

            var blocked = false;
            await using (var observer = new NpgsqlConnection(cs))
            {
                await observer.OpenAsync();
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    await using var waitCheck = new NpgsqlCommand(
                        "SELECT cardinality(pg_blocking_pids(@pid)) > 0;", observer);
                    waitCheck.Parameters.AddWithValue("pid", backendPid);
                    blocked = (bool)(await waitCheck.ExecuteScalarAsync())!;
                    if (blocked) break;
                    await Task.Delay(50);
                }
            }
            Assert.True(blocked, "The disposal eligibility read must wait behind concurrent Legal Hold placement.");

            await ExecuteAsyncInTransactionAsync(holdConnection, holdTx,
                $"INSERT INTO {q}.legal_hold(hold_id,record_id,state) VALUES (@hold,@record,'ACTIVE');",
                ("hold", Guid.NewGuid()), ("record", record));
            await holdTx.CommitAsync();

            Assert.False((bool)(await decisionTask)!);
            Assert.Equal(1L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.legal_hold WHERE record_id=@record AND state='ACTIVE';",
                ("record", record)));
            Assert.Equal(1L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.protected_history WHERE record_id=@record;",
                ("record", record)));
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }

    private static async Task CreateEligibilityFixtureAsync(string cs, string q)
    {
        await ExecuteAsync(cs, $"""
            CREATE SCHEMA {q};
            CREATE TABLE {q}.protected_history
            (record_id uuid PRIMARY KEY, record_class text NOT NULL, retention_until_utc timestamptz NOT NULL, row_version bigint NOT NULL DEFAULT 1);
            CREATE TABLE {q}.retention_schedule
            (record_class text PRIMARY KEY, schedule_version bigint NOT NULL, status text NOT NULL, retention_period_days integer NULL, approved_by uuid NULL, approved_at_utc timestamptz NULL);
            CREATE TABLE {q}.hold_resolver_control (singleton boolean PRIMARY KEY DEFAULT true CHECK(singleton),available boolean NOT NULL);
            CREATE TABLE {q}.legal_hold (hold_id uuid PRIMARY KEY,record_id uuid NOT NULL REFERENCES {q}.protected_history(record_id),state text NOT NULL,changed_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
            CREATE TABLE {q}.disposal_authorization
            (authorization_id uuid PRIMARY KEY,record_id uuid NOT NULL REFERENCES {q}.protected_history(record_id),schedule_version bigint NOT NULL,actor_account_id uuid NOT NULL,approver_account_id uuid NOT NULL,records_legal_authority_id uuid NOT NULL,independently_approved boolean NOT NULL,reason text NOT NULL,expires_at_utc timestamptz NOT NULL,consumed boolean NOT NULL DEFAULT false);
            CREATE FUNCTION {q}.can_dispose(p_record_id uuid,p_authorization_id uuid,p_actor uuid) RETURNS boolean LANGUAGE plpgsql AS $fn$
            DECLARE r {q}.protected_history%ROWTYPE; s {q}.retention_schedule%ROWTYPE; a {q}.disposal_authorization%ROWTYPE; hold_available boolean;
            BEGIN
                SELECT * INTO r FROM {q}.protected_history WHERE record_id=p_record_id FOR SHARE;
                IF NOT FOUND THEN RETURN false; END IF;
                SELECT * INTO s FROM {q}.retention_schedule WHERE record_class=r.record_class FOR SHARE;
                IF NOT FOUND OR s.status<>'APPROVED' OR s.retention_period_days IS NULL OR s.approved_by IS NULL OR s.approved_at_utc IS NULL THEN RETURN false; END IF;
                IF r.retention_until_utc>clock_timestamp() THEN RETURN false; END IF;
                SELECT available INTO hold_available FROM {q}.hold_resolver_control WHERE singleton=true FOR SHARE;
                IF NOT FOUND OR NOT hold_available THEN RETURN false; END IF;
                IF EXISTS(SELECT 1 FROM {q}.legal_hold WHERE record_id=p_record_id AND state<>'RELEASED') THEN RETURN false; END IF;
                SELECT * INTO a FROM {q}.disposal_authorization WHERE authorization_id=p_authorization_id AND record_id=p_record_id FOR UPDATE;
                IF NOT FOUND OR NOT a.independently_approved OR a.consumed OR a.schedule_version<>s.schedule_version OR a.actor_account_id<>p_actor OR a.actor_account_id=a.approver_account_id OR a.records_legal_authority_id IS NULL OR length(btrim(a.reason))=0 OR a.expires_at_utc<=clock_timestamp() THEN RETURN false; END IF;
                RETURN true;
            END; $fn$;
            """);
    }

    private static async Task SeedEligibleFixtureAsync(string cs,string q,Guid record,Guid actor,Guid approver,Guid authority,Guid auth)
    {
        await ExecuteAsync(cs,$"INSERT INTO {q}.protected_history(record_id,record_class,retention_until_utc) VALUES (@id,'ORG_ASSIGNMENT_HISTORY',clock_timestamp()-interval '1 day');",("id",record));
        await ExecuteAsync(cs,$"INSERT INTO {q}.retention_schedule(record_class,schedule_version,status,retention_period_days,approved_by,approved_at_utc) VALUES ('ORG_ASSIGNMENT_HISTORY',4,'APPROVED',365,@authority,clock_timestamp()-interval '2 days');",("authority",authority));
        await ExecuteAsync(cs,$"INSERT INTO {q}.hold_resolver_control(singleton,available) VALUES (true,true);");
        await ExecuteAsync(cs,$"INSERT INTO {q}.disposal_authorization(authorization_id,record_id,schedule_version,actor_account_id,approver_account_id,records_legal_authority_id,independently_approved,reason,expires_at_utc) VALUES (@auth,@record,4,@actor,@approver,@authority,true,'approved synthetic disposal test',clock_timestamp()+interval '5 minutes');",("auth",auth),("record",record),("actor",actor),("approver",approver),("authority",authority));
    }

    private static async Task<bool> EligibleAsync(string cs,string q,Guid record,Guid auth,Guid actor)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand($"SELECT {q}.can_dispose(@record,@auth,@actor);",connection);
        command.Parameters.AddWithValue("record",record);
        command.Parameters.AddWithValue("auth",auth);
        command.Parameters.AddWithValue("actor",actor);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> OpenMarkedDisposableDatabaseAsync()
    {
        var cs=Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(cs));
        var builder=new NpgsqlConnectionStringBuilder(cs);
        Assert.Contains(builder.Host,new[]{"127.0.0.1","localhost","::1"});
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$",builder.Database);
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var marker=new NpgsqlCommand("SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname=current_database();",connection);
        Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB;",(string?)await marker.ExecuteScalarAsync());
        return cs!;
    }

    private static async Task ExecuteAsync(string cs,string sql,params (string Name,object Value)[] parameters)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand(sql,connection);
        foreach(var (name,value) in parameters) command.Parameters.AddWithValue(name,value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsyncInTransactionAsync(NpgsqlConnection connection,NpgsqlTransaction tx,string sql,params (string Name,object Value)[] parameters)
    {
        await using var command=new NpgsqlCommand(sql,connection,tx);
        foreach(var (name,value) in parameters) command.Parameters.AddWithValue(name,value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarInt64Async(string cs,string sql,params (string Name,object Value)[] parameters)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand(sql,connection);
        foreach(var (name,value) in parameters) command.Parameters.AddWithValue(name,value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task DropSchemaAsync(string cs,string q)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand($"DROP SCHEMA IF EXISTS {q} CASCADE;",connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Q(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
}
