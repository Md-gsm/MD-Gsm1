using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.Infrastructure.IdentityAccess;
using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL integration proof for the actual IdentityAccess authorization
/// adapter's transaction-scoped permission guard. It uses the real identity.* tables and
/// keeps the locks in the same transaction as a synthetic protected-write row. It is not
/// yet integrated into a production Organization module or approved global lock order.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class IdentityAccessTransactionScopedAuthorizationGuardPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Actual_identityaccess_guard_holds_account_session_membership_role_grant_and_permission_locks_until_commit()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var accountId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var clientInstanceId = Guid.NewGuid();
        var testPermissionId = Guid.NewGuid();
        var tokenDigest = SHA256.HashData(Guid.NewGuid().ToByteArray());
        var suffix = Guid.NewGuid().ToString("N");
        var loginName = "o20txguard" + suffix;
        var loginNormalized = loginName.ToUpperInvariant();
        var roleKey = "o20-tx-guard-" + suffix;
        var permissionKey = "reports.read";
        var permissionCreatedByTest = false;

        try
        {
            await ExecuteAsync(cs, """
                INSERT INTO identity.accounts
                    (account_id,login_name,login_name_normalized,password_hash,password_hash_version,
                     must_change_password,is_enabled,is_bootstrap_account,credential_version,failed_attempt_count,
                     created_at_utc,updated_at_utc)
                VALUES (@account,@login,@normalized,'test-only-hash',1,false,true,false,9,0,clock_timestamp(),clock_timestamp());
                """,
                ("account", accountId), ("login", loginName), ("normalized", loginNormalized));

            await ExecuteAsync(cs, """
                INSERT INTO identity.roles
                    (role_id,role_key,display_name,is_system_role,is_assignable,created_at_utc,updated_at_utc)
                VALUES (@role,@role_key,'O20 transaction guard disposable role',false,true,clock_timestamp(),clock_timestamp());
                """,
                ("role", roleId), ("role_key", roleKey));

            // The permission name is already in the owner-approved IdentityAccess catalog.
            // Reuse a seeded row when present; otherwise add and later remove a disposable row.
            var insertedPermission = await ScalarGuidNullableAsync(cs, """
                INSERT INTO identity.permissions(permission_id,permission_key,display_name,risk_level,is_active)
                VALUES (@permission,@permission_key,'O20 transaction guard permission','standard',true)
                ON CONFLICT (permission_key) DO NOTHING
                RETURNING permission_id;
                """,
                ("permission", testPermissionId), ("permission_key", permissionKey));
            permissionCreatedByTest = insertedPermission.HasValue;
            var permissionId = insertedPermission
                ?? await ScalarGuidAsync(cs,
                    "SELECT permission_id FROM identity.permissions WHERE permission_key=@permission_key;",
                    ("permission_key", permissionKey));

            await ExecuteAsync(cs, """
                INSERT INTO identity.account_roles(account_id,role_id,assigned_at_utc,assignment_reason)
                VALUES (@account,@role,clock_timestamp(),'disposable transaction guard proof');
                """,
                ("account", accountId), ("role", roleId));
            await ExecuteAsync(cs, """
                INSERT INTO identity.role_permissions(role_id,permission_id)
                VALUES (@role,@permission);
                """,
                ("role", roleId), ("permission", permissionId));
            await ExecuteAsync(cs, """
                INSERT INTO identity.sessions
                    (session_id,account_id,token_digest,created_at_utc,last_seen_at_utc,lease_expires_at_utc,
                     credential_version_at_issue,client_instance_id)
                VALUES
                    (@session,@account,@digest,clock_timestamp(),clock_timestamp(),
                     clock_timestamp()+interval '5 minutes',9,@client);
                """,
                ("session", sessionId), ("account", accountId), ("digest", tokenDigest), ("client", clientInstanceId));

            var options = new DbContextOptionsBuilder<MdGsmDbContext>().UseNpgsql(cs).Options;
            await using var db = new MdGsmDbContext(options);
            var stores = new PostgresIdentityAccessStores(db);
            var adapter = new IdentityAccessAuthorizationContractAdapter(
                new FixedPrincipal(accountId, sessionId, 9),
                stores);

            await using var writer = new NpgsqlConnection(cs);
            await writer.OpenAsync();
            var branchId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var assignmentId = Guid.NewGuid();
            var historyEventId = Guid.NewGuid();
            var outboxEventId = Guid.NewGuid();
            var outboxHash = SHA256.HashData(Guid.NewGuid().ToByteArray());

            await using (var tempTables = new NpgsqlCommand("""
                CREATE TEMP TABLE protected_write_probe
                (
                    write_id uuid PRIMARY KEY,
                    account_id uuid NOT NULL,
                    created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );
                -- Synthetic Organization-owned rows only. These tables exist only for this
                -- disposable test connection; they are not a production Organization schema.
                CREATE TEMP TABLE organization_branch_probe
                (
                    branch_id uuid PRIMARY KEY,
                    row_version bigint NOT NULL
                );
                CREATE TEMP TABLE organization_warehouse_probe
                (
                    warehouse_id uuid PRIMARY KEY,
                    branch_id uuid NOT NULL,
                    row_version bigint NOT NULL
                );
                CREATE TEMP TABLE organization_assignment_probe
                (
                    assignment_id uuid PRIMARY KEY,
                    account_id uuid NOT NULL,
                    warehouse_id uuid NOT NULL,
                    row_version bigint NOT NULL
                );
                CREATE TEMP TABLE organization_domain_history_probe
                (
                    event_id uuid PRIMARY KEY,
                    aggregate_id uuid NOT NULL,
                    aggregate_version bigint NOT NULL,
                    event_type text NOT NULL
                );
                CREATE TEMP TABLE organization_audit_outbox_probe
                (
                    event_id uuid PRIMARY KEY,
                    payload_hash bytea NOT NULL CHECK (octet_length(payload_hash)=32),
                    state text NOT NULL CHECK (state IN ('PENDING','ACKNOWLEDGED'))
                );
                """, writer))
            {
                await tempTables.ExecuteNonQueryAsync();
            }

            await using (var seedOrganization = new NpgsqlCommand("""
                INSERT INTO organization_branch_probe(branch_id,row_version) VALUES (@branch,1);
                INSERT INTO organization_warehouse_probe(warehouse_id,branch_id,row_version) VALUES (@warehouse,@branch,1);
                INSERT INTO organization_assignment_probe(assignment_id,account_id,warehouse_id,row_version)
                    VALUES (@assignment,@account,@warehouse,1);
                """, writer))
            {
                seedOrganization.Parameters.AddWithValue("branch", branchId);
                seedOrganization.Parameters.AddWithValue("warehouse", warehouseId);
                seedOrganization.Parameters.AddWithValue("assignment", assignmentId);
                seedOrganization.Parameters.AddWithValue("account", accountId);
                await seedOrganization.ExecuteNonQueryAsync();
            }

            await using var writerTransaction = await writer.BeginTransactionAsync();

            var unknownKey = await adapter.EvaluateCurrentPermissionInTransactionAsync(
                "identity.owner.override.unapproved", writer, writerTransaction, CancellationToken.None);
            Assert.Equal(PermissionDecisionStatus.Denied, unknownKey.Status);
            Assert.Equal("unknown_permission_key", unknownKey.ReasonCode);

            var knownButUnassigned = await adapter.EvaluateCurrentPermissionInTransactionAsync(
                "reports.export", writer, writerTransaction, CancellationToken.None);
            Assert.Equal(PermissionDecisionStatus.Denied, knownButUnassigned.Status);
            Assert.Equal("session_or_permission_not_active", knownButUnassigned.ReasonCode);

            await using (var otherConnection = new NpgsqlConnection(cs))
            {
                await otherConnection.OpenAsync();
                await using var otherTransaction = await otherConnection.BeginTransactionAsync();
                var mismatch = await adapter.EvaluateCurrentPermissionInTransactionAsync(
                    permissionKey, writer, otherTransaction, CancellationToken.None);
                Assert.Equal(PermissionDecisionStatus.Unavailable, mismatch.Status);
                Assert.Equal("transaction_connection_mismatch", mismatch.ReasonCode);
                await otherTransaction.RollbackAsync();
            }

            var decision = await adapter.EvaluateCurrentPermissionInTransactionAsync(
                permissionKey, writer, writerTransaction, CancellationToken.None);
            Assert.Equal(PermissionDecisionStatus.Allowed, decision.Status);
            Assert.Equal(accountId, decision.PrincipalAccountId);
            Assert.Equal(sessionId, decision.PrincipalSessionId);
            Assert.Equal(9L, decision.PrincipalVersion);
            Assert.Equal("live_permission_locked_until_transaction_end", decision.ReasonCode);

            // The real IdentityAccess guard and the synthetic Organization write facts
            // participate in this exact same PostgreSQL connection/transaction.
            await using (var protectedWrite = new NpgsqlCommand(
                "INSERT INTO protected_write_probe(write_id,account_id) VALUES (@write,@account);",
                writer, writerTransaction))
            {
                protectedWrite.Parameters.AddWithValue("write", Guid.NewGuid());
                protectedWrite.Parameters.AddWithValue("account", accountId);
                Assert.Equal(1, await protectedWrite.ExecuteNonQueryAsync());
            }

            await using (var domainMutation = new NpgsqlCommand("""
                UPDATE organization_branch_probe SET row_version=row_version+1 WHERE branch_id=@branch;
                UPDATE organization_warehouse_probe SET row_version=row_version+1 WHERE warehouse_id=@warehouse AND branch_id=@branch;
                UPDATE organization_assignment_probe SET row_version=row_version+1
                    WHERE assignment_id=@assignment AND account_id=@account AND warehouse_id=@warehouse;
                INSERT INTO organization_domain_history_probe(event_id,aggregate_id,aggregate_version,event_type)
                    VALUES (@history_event,@assignment,2,'ASSIGNMENT_UPDATED');
                INSERT INTO organization_audit_outbox_probe(event_id,payload_hash,state)
                    VALUES (@outbox_event,@outbox_hash,'PENDING');
                """, writer, writerTransaction))
            {
                domainMutation.Parameters.AddWithValue("branch", branchId);
                domainMutation.Parameters.AddWithValue("warehouse", warehouseId);
                domainMutation.Parameters.AddWithValue("assignment", assignmentId);
                domainMutation.Parameters.AddWithValue("account", accountId);
                domainMutation.Parameters.AddWithValue("history_event", historyEventId);
                domainMutation.Parameters.AddWithValue("outbox_event", outboxEventId);
                domainMutation.Parameters.AddWithValue("outbox_hash", outboxHash);
                Assert.Equal(5, await domainMutation.ExecuteNonQueryAsync());
            }

            var pidsReady = Enumerable.Range(0, 5)
                .Select(_ => new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously))
                .ToArray();

            var competingWriters = new[]
            {
                StartCompetingWriterAsync(cs, pidsReady[0], """
                    UPDATE identity.sessions SET revoked_at_utc=clock_timestamp(),revoke_reason='transaction guard proof'
                    WHERE session_id=@session;
                    """, ("session", sessionId)),
                StartCompetingWriterAsync(cs, pidsReady[1], """
                    UPDATE identity.accounts
                    SET is_enabled=false,credential_version=credential_version+1,updated_at_utc=clock_timestamp()
                    WHERE account_id=@account;
                    """, ("account", accountId)),
                StartCompetingWriterAsync(cs, pidsReady[2], """
                    DELETE FROM identity.account_roles WHERE account_id=@account AND role_id=@role;
                    """, ("account", accountId), ("role", roleId)),
                StartCompetingWriterAsync(cs, pidsReady[3], """
                    DELETE FROM identity.role_permissions WHERE role_id=@role AND permission_id=@permission;
                    """, ("role", roleId), ("permission", permissionId)),
                StartCompetingWriterAsync(cs, pidsReady[4], """
                    UPDATE identity.permissions SET is_active=false WHERE permission_id=@permission;
                    """, ("permission", permissionId))
            };

            var competingPids = await Task.WhenAll(pidsReady.Select(x => x.Task));
            try
            {
                // Confirm every real IdentityAccess mutation is waiting on the row locks
                // acquired by the adapter, not merely that a time delay elapsed.
                await WaitForAllBlockedAsync(cs, competingPids);
                await writerTransaction.CommitAsync();
                await Task.WhenAll(competingWriters);

                await using (var verifyWrite = new NpgsqlCommand(
                    "SELECT count(*) FROM protected_write_probe WHERE account_id=@account;", writer))
                {
                    verifyWrite.Parameters.AddWithValue("account", accountId);
                    Assert.Equal(1L, Convert.ToInt64(await verifyWrite.ExecuteScalarAsync()));
                }

                await using (var verifyOrganization = new NpgsqlCommand("""
                    SELECT
                        (SELECT row_version FROM organization_branch_probe WHERE branch_id=@branch),
                        (SELECT row_version FROM organization_warehouse_probe WHERE warehouse_id=@warehouse),
                        (SELECT row_version FROM organization_assignment_probe WHERE assignment_id=@assignment),
                        (SELECT count(*) FROM organization_domain_history_probe WHERE event_id=@history_event),
                        (SELECT count(*) FROM organization_audit_outbox_probe
                         WHERE event_id=@outbox_event AND payload_hash=@outbox_hash AND state='PENDING');
                    """, writer))
                {
                    verifyOrganization.Parameters.AddWithValue("branch", branchId);
                    verifyOrganization.Parameters.AddWithValue("warehouse", warehouseId);
                    verifyOrganization.Parameters.AddWithValue("assignment", assignmentId);
                    verifyOrganization.Parameters.AddWithValue("history_event", historyEventId);
                    verifyOrganization.Parameters.AddWithValue("outbox_event", outboxEventId);
                    verifyOrganization.Parameters.AddWithValue("outbox_hash", outboxHash);
                    await using var reader = await verifyOrganization.ExecuteReaderAsync();
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal(2L, reader.GetInt64(0));
                    Assert.Equal(2L, reader.GetInt64(1));
                    Assert.Equal(2L, reader.GetInt64(2));
                    Assert.Equal(1L, reader.GetInt64(3));
                    Assert.Equal(1L, reader.GetInt64(4));
                }

                Assert.False(await stores.HasPermissionForLiveSessionAsync(
                    MDGsm.IdentityAccess.Domain.AccountId.From(accountId),
                    sessionId,
                    9,
                    permissionKey,
                    CancellationToken.None));
            }
            finally
            {
                try { await writerTransaction.RollbackAsync(); }
                catch (InvalidOperationException) { /* Already committed or rolled back. */ }
                catch (PostgresException) { /* Connection was aborted during the proof. */ }
                try { await Task.WhenAll(competingWriters).WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception) { /* The outer test reports the original failure. */ }
            }
        }
        finally
        {
            await ExecuteAsync(cs, "DELETE FROM identity.sessions WHERE session_id=@id;", ("id", sessionId));
            await ExecuteAsync(cs, "DELETE FROM identity.role_permissions WHERE role_id=@id;", ("id", roleId));
            await ExecuteAsync(cs, "DELETE FROM identity.account_roles WHERE account_id=@id;", ("id", accountId));
            await ExecuteAsync(cs, "DELETE FROM identity.roles WHERE role_id=@id;", ("id", roleId));
            await ExecuteAsync(cs, "DELETE FROM identity.accounts WHERE account_id=@id;", ("id", accountId));
            if (permissionCreatedByTest)
            {
                await ExecuteAsync(cs, """
                    DELETE FROM identity.permissions p
                    WHERE p.permission_id=@id
                      AND NOT EXISTS (SELECT 1 FROM identity.role_permissions rp WHERE rp.permission_id=p.permission_id);
                    """, ("id", testPermissionId));
            }
        }
    }

    private static async Task<long> StartCompetingWriterAsync(
        string cs,
        TaskCompletionSource<long> backendPid,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        long backendId;
        await using (var pid = new NpgsqlCommand("SELECT pg_backend_pid()::bigint;", connection, transaction))
        {
            backendId = Convert.ToInt64(await pid.ExecuteScalarAsync());
            backendPid.TrySetResult(backendId);
        }

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return backendId;
    }

    private static async Task WaitForAllBlockedAsync(string cs, long[] backendPids)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        for (var attempt = 0; attempt < 150; attempt++)
        {
            await using var command = new NpgsqlCommand("""
                SELECT count(*)
                FROM unnest(@pids::bigint[]) AS p(pid)
                WHERE cardinality(pg_blocking_pids(pid::integer)) > 0;
                """, connection);
            command.Parameters.AddWithValue("pids", backendPids);
            var blocked = Convert.ToInt64(await command.ExecuteScalarAsync());
            if (blocked == backendPids.Length) return;
            await Task.Delay(20);
        }
        throw new Xunit.Sdk.XunitException("Not all IdentityAccess mutations were blocked by the transaction-scoped permission guard.");
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

    private static async Task<Guid?> ScalarGuidNullableAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var scalarValue = await command.ExecuteScalarAsync();
        return scalarValue is null or DBNull ? null : (Guid)scalarValue;
    }

    private static async Task<string> ScalarStringAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private sealed class FixedPrincipal(Guid accountId, Guid sessionId, long credentialVersion)
        : IValidatedRequestPrincipalAccessor
    {
        public Guid AccountId => accountId;
        public Guid SessionId => sessionId;
        public long CredentialVersion => credentialVersion;
        public DateTimeOffset AuthenticatedAtUtc => DateTimeOffset.UtcNow;
    }
}
