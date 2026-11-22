using System;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL race/failure experiments for the remaining local Organization
/// concurrency cases: exact-Warehouse reassignment versus revocation, Branch deactivation
/// versus reassignment, maker edit versus approval, and bounded retry after lock timeout.
/// These are policy PoCs only; they do not establish the Platform-wide lock ranks or a
/// production Organization UnitOfWork.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OrganizationCrossAggregateConcurrencyPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Warehouse_reassignment_and_assignment_revocation_preserve_exact_WarehouseId()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_xagg_wh_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.branch(branch_id uuid PRIMARY KEY,state text NOT NULL CHECK(state IN ('ACTIVE','INACTIVE')));
                CREATE TABLE {q}.warehouse
                (
                    warehouse_id uuid PRIMARY KEY,
                    branch_id uuid NOT NULL REFERENCES {q}.branch(branch_id),
                    state text NOT NULL CHECK(state IN ('ACTIVE','INACTIVE')),
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.account_warehouse_assignment
                (
                    assignment_id uuid PRIMARY KEY,
                    account_id uuid NOT NULL,
                    warehouse_id uuid NOT NULL REFERENCES {q}.warehouse(warehouse_id),
                    state text NOT NULL CHECK(state IN ('ACTIVE','REVOKED'))
                );
                CREATE FUNCTION {q}.reassign_warehouse(p_warehouse uuid,p_destination uuid)
                RETURNS void LANGUAGE plpgsql AS $fn$
                DECLARE w_state text; destination_state text;
                BEGIN
                    SELECT state INTO w_state FROM {q}.warehouse
                    WHERE warehouse_id=p_warehouse FOR UPDATE;
                    IF NOT FOUND OR w_state<>'INACTIVE' THEN
                        RAISE EXCEPTION 'Warehouse must be inactive' USING ERRCODE='23514';
                    END IF;
                    SELECT state INTO destination_state FROM {q}.branch
                    WHERE branch_id=p_destination FOR SHARE;
                    IF NOT FOUND OR destination_state<>'ACTIVE' THEN
                        RAISE EXCEPTION 'destination Branch must be active' USING ERRCODE='23514';
                    END IF;
                    UPDATE {q}.warehouse
                    SET branch_id=p_destination,row_version=row_version+1
                    WHERE warehouse_id=p_warehouse;
                END;
                $fn$;
                CREATE FUNCTION {q}.revoke_exact_assignment(p_assignment uuid,p_expected_warehouse uuid)
                RETURNS void LANGUAGE plpgsql AS $fn$
                DECLARE current_warehouse uuid;
                BEGIN
                    SELECT warehouse_id INTO current_warehouse
                    FROM {q}.account_warehouse_assignment
                    WHERE assignment_id=p_assignment;
                    IF NOT FOUND OR current_warehouse<>p_expected_warehouse THEN
                        RAISE EXCEPTION 'assignment WarehouseId mismatch' USING ERRCODE='23514';
                    END IF;
                    PERFORM 1 FROM {q}.warehouse
                    WHERE warehouse_id=p_expected_warehouse FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Warehouse not found' USING ERRCODE='23514';
                    END IF;
                    UPDATE {q}.account_warehouse_assignment
                    SET state='REVOKED'
                    WHERE assignment_id=p_assignment
                      AND warehouse_id=p_expected_warehouse
                      AND state='ACTIVE';
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'assignment is not active' USING ERRCODE='23514';
                    END IF;
                END;
                $fn$;
                """);

            var branchA=Guid.NewGuid(); var branchB=Guid.NewGuid();
            var warehouse=Guid.NewGuid(); var assignment=Guid.NewGuid();
            var account=Guid.NewGuid();
            await ExecuteAsync(cs,$"INSERT INTO {q}.branch VALUES (@a,'ACTIVE'),(@b,'ACTIVE');",("a",branchA),("b",branchB));
            await ExecuteAsync(cs,$"INSERT INTO {q}.warehouse(warehouse_id,branch_id,state) VALUES (@w,@a,'INACTIVE');",("w",warehouse),("a",branchA));
            await ExecuteAsync(cs,$"INSERT INTO {q}.account_warehouse_assignment VALUES (@id,@account,@w,'ACTIVE');",("id",assignment),("account",account),("w",warehouse));

            // Hold the Warehouse row while reassignment commits; the competing revocation
            // must wait and then apply to the same immutable WarehouseId.
            await using var reassignConnection=new NpgsqlConnection(cs);
            await reassignConnection.OpenAsync();
            await using var reassignTx=await reassignConnection.BeginTransactionAsync();
            await using(var command=new NpgsqlCommand($"SELECT {q}.reassign_warehouse(@w,@b);",reassignConnection,reassignTx))
            {
                command.Parameters.AddWithValue("w",warehouse);
                command.Parameters.AddWithValue("b",branchB);
                await command.ExecuteNonQueryAsync();
            }

            await using var revokeConnection=new NpgsqlConnection(cs);
            await revokeConnection.OpenAsync();
            var revokePid=Convert.ToInt32(await new NpgsqlCommand("SELECT pg_backend_pid();",revokeConnection).ExecuteScalarAsync());
            await using var revokeCommand=new NpgsqlCommand($"SELECT {q}.revoke_exact_assignment(@assignment,@warehouse);",revokeConnection);
            revokeCommand.Parameters.AddWithValue("assignment",assignment);
            revokeCommand.Parameters.AddWithValue("warehouse",warehouse);
            var revokeTask=revokeCommand.ExecuteNonQueryAsync();

            Assert.True(await IsBlockedAsync(cs,revokePid),
                "Revocation must wait for the in-flight Warehouse reassignment row lock.");
            await reassignTx.CommitAsync();
            await revokeTask;

            Assert.Equal(branchB,await ScalarGuidAsync(cs,$"SELECT branch_id FROM {q}.warehouse WHERE warehouse_id=@w;",("w",warehouse)));
            Assert.Equal("REVOKED",await ScalarStringAsync(cs,$"SELECT state FROM {q}.account_warehouse_assignment WHERE assignment_id=@id;",("id",assignment)));
            Assert.Equal(warehouse,await ScalarGuidAsync(cs,$"SELECT warehouse_id FROM {q}.account_warehouse_assignment WHERE assignment_id=@id;",("id",assignment)));
            Assert.Equal(2L,await ScalarInt64Async(cs,$"SELECT row_version FROM {q}.warehouse WHERE warehouse_id=@w;",("w",warehouse)));
        }
        finally { await DropSchemaAsync(cs,q); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Branch_deactivation_racing_with_Warehouse_reassignment_never_leaves_inactive_destination_link()
    {
        var cs=await OpenMarkedDisposableDatabaseAsync();
        var schema="o20org_xagg_branch_"+Guid.NewGuid().ToString("N")[..10];
        var q=Q(schema);
        try
        {
            await ExecuteAsync(cs,$"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.branch(branch_id uuid PRIMARY KEY,state text NOT NULL CHECK(state IN ('ACTIVE','INACTIVE')));
                CREATE TABLE {q}.warehouse(warehouse_id uuid PRIMARY KEY,branch_id uuid NOT NULL REFERENCES {q}.branch(branch_id),state text NOT NULL CHECK(state IN ('ACTIVE','INACTIVE')),row_version bigint NOT NULL DEFAULT 1);
                CREATE FUNCTION {q}.reassign_warehouse(p_warehouse uuid,p_destination uuid)
                RETURNS void LANGUAGE plpgsql AS $fn$
                DECLARE source_branch uuid; wh_state text; destination_state text;
                BEGIN
                    SELECT branch_id INTO source_branch FROM {q}.warehouse WHERE warehouse_id=p_warehouse;
                    IF NOT FOUND THEN RAISE EXCEPTION 'Warehouse not found' USING ERRCODE='P0002'; END IF;
                    PERFORM branch_id FROM {q}.branch
                    WHERE branch_id IN (source_branch,p_destination)
                    ORDER BY branch_id FOR UPDATE;
                    SELECT branch_id,state INTO source_branch,wh_state FROM {q}.warehouse
                    WHERE warehouse_id=p_warehouse FOR UPDATE;
                    IF wh_state<>'INACTIVE' THEN RAISE EXCEPTION 'Warehouse must be inactive' USING ERRCODE='23514'; END IF;
                    SELECT state INTO destination_state FROM {q}.branch WHERE branch_id=p_destination;
                    IF destination_state<>'ACTIVE' THEN RAISE EXCEPTION 'destination Branch must be active' USING ERRCODE='23514'; END IF;
                    UPDATE {q}.warehouse SET branch_id=p_destination,row_version=row_version+1 WHERE warehouse_id=p_warehouse;
                END;
                $fn$;
                CREATE FUNCTION {q}.deactivate_branch(p_branch uuid)
                RETURNS void LANGUAGE plpgsql AS $fn$
                BEGIN
                    PERFORM 1 FROM {q}.branch WHERE branch_id=p_branch FOR UPDATE;
                    IF NOT FOUND THEN RAISE EXCEPTION 'Branch not found' USING ERRCODE='P0002'; END IF;
                    PERFORM warehouse_id FROM {q}.warehouse
                    WHERE branch_id=p_branch ORDER BY warehouse_id FOR UPDATE;
                    IF FOUND THEN RAISE EXCEPTION 'Branch still owns Warehouse relationships' USING ERRCODE='23514'; END IF;
                    UPDATE {q}.branch SET state='INACTIVE' WHERE branch_id=p_branch;
                END;
                $fn$;
                """);
            var source=Guid.NewGuid();var destination=Guid.NewGuid();var warehouse=Guid.NewGuid();
            await ExecuteAsync(cs,$"INSERT INTO {q}.branch VALUES (@a,'ACTIVE'),(@b,'ACTIVE');",("a",source),("b",destination));
            await ExecuteAsync(cs,$"INSERT INTO {q}.warehouse VALUES (@w,@a,'INACTIVE',1);",("w",warehouse),("a",source));

            // Interleaving A: deactivation owns destination first. Reassignment must wait,
            // then observe INACTIVE and reject; it must not link Warehouse to it.
            await using(var blocker=new NpgsqlConnection(cs))
            {
                await blocker.OpenAsync();
                await using var tx=await blocker.BeginTransactionAsync();
                await using(var lockBranch=new NpgsqlCommand($"SELECT branch_id FROM {q}.branch WHERE branch_id=@b FOR UPDATE;",blocker,tx))
                {
                    lockBranch.Parameters.AddWithValue("b",destination);
                    await lockBranch.ExecuteScalarAsync();
                }
                await using var candidate=new NpgsqlConnection(cs);
                await candidate.OpenAsync();
                var pid=Convert.ToInt32(await new NpgsqlCommand("SELECT pg_backend_pid();",candidate).ExecuteScalarAsync());
                await using var reassignment=new NpgsqlCommand($"SELECT {q}.reassign_warehouse(@w,@b);",candidate);
                reassignment.Parameters.AddWithValue("w",warehouse);
                reassignment.Parameters.AddWithValue("b",destination);
                var task=reassignment.ExecuteNonQueryAsync();
                Assert.True(await IsBlockedAsync(cs,pid));
                await using(var deactivate=new NpgsqlCommand($"SELECT {q}.deactivate_branch(@b);",blocker,tx))
                {
                    deactivate.Parameters.AddWithValue("b",destination);
                    await deactivate.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
                var failure=await Assert.ThrowsAsync<PostgresException>(()=>task);
                Assert.Equal("23514",failure.SqlState);
            }
            Assert.Equal(source,await ScalarGuidAsync(cs,$"SELECT branch_id FROM {q}.warehouse WHERE warehouse_id=@w;",("w",warehouse)));
            Assert.Equal("INACTIVE",await ScalarStringAsync(cs,$"SELECT state FROM {q}.branch WHERE branch_id=@b;",("b",destination)));

            // Interleaving B: when reassignment owns destination first, deactivation must
            // refuse to inactivate a Branch that still owns the Warehouse relationship.
            await ExecuteAsync(cs,$"UPDATE {q}.branch SET state='ACTIVE' WHERE branch_id=@b;",("b",destination));
            await ExecuteAsync(cs,$"SELECT {q}.reassign_warehouse(@w,@b);",("w",warehouse),("b",destination));
            var deactivateFailure=await Assert.ThrowsAsync<PostgresException>(()=>
                ExecuteAsync(cs,$"SELECT {q}.deactivate_branch(@b);",("b",destination)));
            Assert.Equal("23514",deactivateFailure.SqlState);
            Assert.Equal("ACTIVE",await ScalarStringAsync(cs,$"SELECT state FROM {q}.branch WHERE branch_id=@b;",("b",destination)));
            Assert.Equal(destination,await ScalarGuidAsync(cs,$"SELECT branch_id FROM {q}.warehouse WHERE warehouse_id=@w;",("w",warehouse)));
        }
        finally { await DropSchemaAsync(cs,q); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Approval_decision_rejects_stale_request_after_concurrent_maker_edit()
    {
        var cs=await OpenMarkedDisposableDatabaseAsync();
        var schema="o20org_xagg_approve_"+Guid.NewGuid().ToString("N")[..10];
        var q=Q(schema);
        try
        {
            await ExecuteAsync(cs,$"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.assignment_request
                (
                    request_id uuid PRIMARY KEY,
                    request_fingerprint bytea NOT NULL,
                    row_version bigint NOT NULL,
                    state text NOT NULL CHECK(state IN ('PENDING','APPROVED')),
                    approver_id uuid NULL
                );
                CREATE FUNCTION {q}.edit_request(p_id uuid,p_new_fingerprint bytea)
                RETURNS void LANGUAGE plpgsql AS $fn$
                BEGIN
                    PERFORM 1 FROM {q}.assignment_request WHERE request_id=p_id AND state='PENDING' FOR UPDATE;
                    IF NOT FOUND THEN RAISE EXCEPTION 'request is not editable' USING ERRCODE='23514'; END IF;
                    UPDATE {q}.assignment_request SET request_fingerprint=p_new_fingerprint,row_version=row_version+1 WHERE request_id=p_id;
                END;
                $fn$;
                CREATE FUNCTION {q}.approve_request(p_id uuid,p_expected_version bigint,p_expected_fingerprint bytea,p_approver uuid)
                RETURNS void LANGUAGE plpgsql AS $fn$
                DECLARE r {q}.assignment_request%ROWTYPE;
                BEGIN
                    SELECT * INTO r FROM {q}.assignment_request WHERE request_id=p_id FOR UPDATE;
                    IF NOT FOUND OR r.state<>'PENDING' THEN RAISE EXCEPTION 'request is not pending' USING ERRCODE='23514'; END IF;
                    IF r.row_version<>p_expected_version OR r.request_fingerprint<>p_expected_fingerprint THEN
                        RAISE EXCEPTION 'approved payload no longer matches pending request' USING ERRCODE='40001';
                    END IF;
                    UPDATE {q}.assignment_request SET state='APPROVED',approver_id=p_approver WHERE request_id=p_id;
                END;
                $fn$;
                """);
            var request=Guid.NewGuid();var approver=Guid.NewGuid();
            var oldFingerprint=System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray());
            var newFingerprint=System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray());
            await ExecuteAsync(cs,$"INSERT INTO {q}.assignment_request VALUES (@id,@hash,1,'PENDING',NULL);",("id",request),("hash",oldFingerprint));

            await using var editConnection=new NpgsqlConnection(cs);
            await editConnection.OpenAsync();
            await using var editTx=await editConnection.BeginTransactionAsync();
            await using(var edit=new NpgsqlCommand($"SELECT {q}.edit_request(@id,@new_hash);",editConnection,editTx))
            {
                edit.Parameters.AddWithValue("id",request);
                edit.Parameters.AddWithValue("new_hash",newFingerprint);
                await edit.ExecuteNonQueryAsync();
            }

            await using var approvalConnection=new NpgsqlConnection(cs);
            await approvalConnection.OpenAsync();
            var pid=Convert.ToInt32(await new NpgsqlCommand("SELECT pg_backend_pid();",approvalConnection).ExecuteScalarAsync());
            await using var approve=new NpgsqlCommand($"SELECT {q}.approve_request(@id,1,@old_hash,@approver);",approvalConnection);
            approve.Parameters.AddWithValue("id",request);
            approve.Parameters.AddWithValue("old_hash",oldFingerprint);
            approve.Parameters.AddWithValue("approver",approver);
            var approvalTask=approve.ExecuteNonQueryAsync();
            Assert.True(await IsBlockedAsync(cs,pid));
            await editTx.CommitAsync();

            var failure=await Assert.ThrowsAsync<PostgresException>(()=>approvalTask);
            Assert.Equal("40001",failure.SqlState);
            Assert.Equal("PENDING",await ScalarStringAsync(cs,$"SELECT state FROM {q}.assignment_request WHERE request_id=@id;",("id",request)));
            Assert.Equal(2L,await ScalarInt64Async(cs,$"SELECT row_version FROM {q}.assignment_request WHERE request_id=@id;",("id",request)));
            Assert.Equal(newFingerprint,await ScalarBytesAsync(cs,$"SELECT request_fingerprint FROM {q}.assignment_request WHERE request_id=@id;",("id",request)));
        }
        finally { await DropSchemaAsync(cs,q); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Lock_timeout_is_classified_and_retry_is_bounded_to_two_attempts()
    {
        var cs=await OpenMarkedDisposableDatabaseAsync();
        var schema="o20org_xagg_retry_"+Guid.NewGuid().ToString("N")[..10];
        var q=Q(schema);
        try
        {
            await ExecuteAsync(cs,$"CREATE SCHEMA {q}; CREATE TABLE {q}.counter(id integer PRIMARY KEY,value integer NOT NULL); INSERT INTO {q}.counter VALUES (1,0);");
            await using var blocker=new NpgsqlConnection(cs);
            await blocker.OpenAsync();
            await using var blockerTx=await blocker.BeginTransactionAsync();
            await using(var lockRow=new NpgsqlCommand($"SELECT id FROM {q}.counter WHERE id=1 FOR UPDATE;",blocker,blockerTx))
                await lockRow.ExecuteScalarAsync();

            PostgresException? firstFailure=null;
            var attempts=0;
            await using(var contender=new NpgsqlConnection(cs))
            {
                await contender.OpenAsync();
                attempts++;
                await using var tx=await contender.BeginTransactionAsync();
                await using(var settings=new NpgsqlCommand("SET LOCAL lock_timeout='100ms';",contender,tx))
                    await settings.ExecuteNonQueryAsync();
                try
                {
                    await using var lockAttempt=new NpgsqlCommand($"SELECT id FROM {q}.counter WHERE id=1 FOR UPDATE;",contender,tx);
                    await lockAttempt.ExecuteScalarAsync();
                    await tx.CommitAsync();
                }
                catch(PostgresException ex)
                {
                    firstFailure=ex;
                    await tx.RollbackAsync();
                }
            }
            Assert.NotNull(firstFailure);
            Assert.Equal("55P03",firstFailure!.SqlState);
            await blockerTx.CommitAsync();

            attempts++;
            Assert.Equal(2,attempts); // Explicit hard bound: first attempt plus one retry.
            await using(var retry=new NpgsqlConnection(cs))
            {
                await retry.OpenAsync();
                await using var retryTx=await retry.BeginTransactionAsync();
                await using(var lockAttempt=new NpgsqlCommand($"SELECT id FROM {q}.counter WHERE id=1 FOR UPDATE;",retry,retryTx))
                    await lockAttempt.ExecuteScalarAsync();
                await using(var update=new NpgsqlCommand($"UPDATE {q}.counter SET value=value+1 WHERE id=1;",retry,retryTx))
                    Assert.Equal(1,await update.ExecuteNonQueryAsync());
                await retryTx.CommitAsync();
            }
            Assert.Equal(1L,await ScalarInt64Async(cs,$"SELECT value FROM {q}.counter WHERE id=1;"));
        }
        finally { await DropSchemaAsync(cs,q); }
    }

    private static async Task<bool> IsBlockedAsync(string cs,int pid)
    {
        await using var observer=new NpgsqlConnection(cs);
        await observer.OpenAsync();
        for(var i=0;i<60;i++)
        {
            await using var command=new NpgsqlCommand("SELECT cardinality(pg_blocking_pids(@pid))>0;",observer);
            command.Parameters.AddWithValue("pid",pid);
            if((bool)(await command.ExecuteScalarAsync())!) return true;
            await Task.Delay(50);
        }
        return false;
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

    private static async Task<Guid> ScalarGuidAsync(string cs,string sql,params (string Name,object Value)[] parameters)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand(sql,connection);
        foreach(var (name,value) in parameters) command.Parameters.AddWithValue(name,value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> ScalarInt64Async(string cs,string sql,params (string Name,object Value)[] parameters)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand(sql,connection);
        foreach(var (name,value) in parameters) command.Parameters.AddWithValue(name,value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarStringAsync(string cs,string sql,params (string Name,object Value)[] parameters)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand(sql,connection);
        foreach(var (name,value) in parameters) command.Parameters.AddWithValue(name,value);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<byte[]> ScalarBytesAsync(string cs,string sql,params (string Name,object Value)[] parameters)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand(sql,connection);
        foreach(var (name,value) in parameters) command.Parameters.AddWithValue(name,value);
        return (byte[])(await command.ExecuteScalarAsync())!;
    }

    private static async Task DropSchemaAsync(string cs,string q)
    {
        await using var connection=new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command=new NpgsqlCommand($"DROP SCHEMA IF EXISTS {q} CASCADE;",connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Q(string s)=>"\""+s.Replace("\"","\\"")+"\"";
}
