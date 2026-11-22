using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL proof of a candidate commit-time authorization linearization
/// mechanism. A protected mutation transaction takes FOR SHARE locks over the exact
/// account/session/role-grant/permission rows and holds them through commit. Concurrent
/// revocation/update transactions must serialize after it. This is not yet wired into a
/// production Organization UnitOfWork and does not define the Platform-wide lock order.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class IdentityAccessAuthorizationLinearizationPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Revocation_and_permission_removal_serialize_behind_protected_write_lock_set()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "ia_authz_linear_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.account
                (
                    account_id uuid PRIMARY KEY,
                    enabled boolean NOT NULL,
                    credential_version bigint NOT NULL
                );
                CREATE TABLE {q}.session
                (
                    session_id uuid PRIMARY KEY,
                    account_id uuid NOT NULL REFERENCES {q}.account(account_id),
                    revoked_at_utc timestamptz NULL,
                    lease_expires_at_utc timestamptz NOT NULL,
                    credential_version_at_issue bigint NOT NULL
                );
                CREATE TABLE {q}.role
                (
                    role_id uuid PRIMARY KEY,
                    active boolean NOT NULL
                );
                CREATE TABLE {q}.permission
                (
                    permission_id uuid PRIMARY KEY,
                    permission_key text NOT NULL UNIQUE,
                    active boolean NOT NULL
                );
                CREATE TABLE {q}.account_role
                (
                    account_id uuid NOT NULL REFERENCES {q}.account(account_id),
                    role_id uuid NOT NULL REFERENCES {q}.role(role_id),
                    PRIMARY KEY(account_id,role_id)
                );
                CREATE TABLE {q}.role_permission
                (
                    role_id uuid NOT NULL REFERENCES {q}.role(role_id),
                    permission_id uuid NOT NULL REFERENCES {q}.permission(permission_id),
                    PRIMARY KEY(role_id,permission_id)
                );
                CREATE TABLE {q}.protected_write
                (
                    write_id uuid PRIMARY KEY,
                    account_id uuid NOT NULL,
                    session_id uuid NOT NULL,
                    permission_key text NOT NULL,
                    committed_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );
                """);

            await ExecuteAsync(cs, $"""
                INSERT INTO {q}.account(account_id,enabled,credential_version)
                VALUES (@account,true,9);
                INSERT INTO {q}.session(session_id,account_id,lease_expires_at_utc,credential_version_at_issue)
                VALUES (@session,@account,clock_timestamp()+interval '5 minutes',9);
                INSERT INTO {q}.role(role_id,active) VALUES (@role,true);
                INSERT INTO {q}.permission(permission_id,permission_key,active)
                VALUES (@permission,'o20.linearization.probe',true);
                INSERT INTO {q}.account_role(account_id,role_id) VALUES (@account,@role);
                INSERT INTO {q}.role_permission(role_id,permission_id) VALUES (@role,@permission);
                """,
                ("account", accountId), ("session", sessionId), ("role", roleId), ("permission", permissionId));

            await using var writer = new NpgsqlConnection(cs);
            await writer.OpenAsync();
            await using var writerTx = await writer.BeginTransactionAsync();

            // Lock order for this disposable experiment only:
            // account -> session -> account_role -> role_permission -> permission.
            await using (var guard = new NpgsqlCommand($"""
                SELECT a.account_id,s.session_id,ar.role_id,rp.permission_id,p.permission_key
                FROM {q}.account a
                JOIN {q}.session s ON s.account_id=a.account_id
                JOIN {q}.account_role ar ON ar.account_id=a.account_id
                JOIN {q}.role r ON r.role_id=ar.role_id
                JOIN {q}.role_permission rp ON rp.role_id=r.role_id
                JOIN {q}.permission p ON p.permission_id=rp.permission_id
                WHERE a.account_id=@account
                  AND s.session_id=@session
                  AND a.enabled
                  AND a.credential_version=9
                  AND s.credential_version_at_issue=9
                  AND s.revoked_at_utc IS NULL
                  AND s.lease_expires_at_utc>clock_timestamp()
                  AND r.active
                  AND p.permission_key='o20.linearization.probe'
                  AND p.active
                FOR SHARE OF a,s,ar,rp,p;
                """, writer, writerTx))
            {
                guard.Parameters.AddWithValue("account", accountId);
                guard.Parameters.AddWithValue("session", sessionId);
                await using var reader = await guard.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(accountId, reader.GetGuid(0));
                Assert.Equal(sessionId, reader.GetGuid(1));
                Assert.Equal(roleId, reader.GetGuid(2));
                Assert.Equal(permissionId, reader.GetGuid(3));
                Assert.Equal("o20.linearization.probe", reader.GetString(4));
            }

            // The protected mutation executes in the same transaction while the guard rows
            // remain locked. Two competing authorization revocations are started concurrently.
            await ExecuteAsync(writer, writerTx,
                $"INSERT INTO {q}.protected_write(write_id,account_id,session_id,permission_key) VALUES (@id,@account,@session,'o20.linearization.probe');",
                ("id", assignmentId), ("account", accountId));

            var sessionUpdateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var grantRemovalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var revokeSession = Task.Run(async () =>
            {
                await using var connection = new NpgsqlConnection(cs);
                await connection.OpenAsync();
                await using var tx = await connection.BeginTransactionAsync();
                sessionUpdateStarted.TrySetResult();
                await ExecuteAsync(connection, tx,
                    $"UPDATE {q}.session SET revoked_at_utc=clock_timestamp() WHERE session_id=@session;",
                    ("session", sessionId));
                await tx.CommitAsync();
            });

            var removeGrant = Task.Run(async () =>
            {
                await using var connection = new NpgsqlConnection(cs);
                await connection.OpenAsync();
                await using var tx = await connection.BeginTransactionAsync();
                grantRemovalStarted.TrySetResult();
                await ExecuteAsync(connection, tx,
                    $"DELETE FROM {q}.role_permission WHERE role_id=@role AND permission_id=@permission;",
                    ("role", roleId), ("permission", permissionId));
                await tx.CommitAsync();
            });

            await Task.WhenAll(sessionUpdateStarted.Task, grantRemovalStarted.Task);
            await Task.Delay(200);
            Assert.False(revokeSession.IsCompleted, "Session revocation must wait for the protected transaction.");
            Assert.False(removeGrant.IsCompleted, "Permission-grant removal must wait for the protected transaction.");

            await writerTx.CommitAsync();
            await Task.WhenAll(revokeSession, removeGrant);

            Assert.Equal(1L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.protected_write WHERE write_id=@id;", ("id", assignmentId)));
            Assert.Equal(1L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.session WHERE session_id=@id AND revoked_at_utc IS NOT NULL;",
                ("id", sessionId)));
            Assert.Equal(0L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.role_permission WHERE role_id=@role AND permission_id=@permission;",
                ("role", roleId), ("permission", permissionId)));

            // A new decision after both revocations must deny. There is no stale Allow cache.
            Assert.Equal(0L, await ScalarInt64Async(cs, $"""
                SELECT count(*)
                FROM {q}.account a
                JOIN {q}.session s ON s.account_id=a.account_id
                JOIN {q}.account_role ar ON ar.account_id=a.account_id
                JOIN {q}.role r ON r.role_id=ar.role_id
                JOIN {q}.role_permission rp ON rp.role_id=r.role_id
                JOIN {q}.permission p ON p.permission_id=rp.permission_id
                WHERE a.account_id=@account AND s.session_id=@session
                  AND a.enabled AND a.credential_version=9
                  AND s.credential_version_at_issue=9
                  AND s.revoked_at_utc IS NULL
                  AND s.lease_expires_at_utc>clock_timestamp()
                  AND r.active AND p.permission_key='o20.linearization.probe' AND p.active;
                """, ("account", accountId), ("session", sessionId)));
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

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
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

    private static async Task DropSchemaAsync(string cs, string quotedSchema)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
