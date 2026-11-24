using System;
using System.Threading.Tasks;
using MDGsm.Infrastructure.IdentityAccess;
using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Integration proof for the IdentityAccess request authorization query.
/// It verifies that an exact permission grant is allowed only while the same
/// session, account and credential version remain live in PostgreSQL.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class IdentityAccessLiveSessionPermissionPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Permission_requires_live_session_enabled_account_matching_credential_version_and_active_grant()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var accountId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var clientInstanceId = Guid.NewGuid();
        var tokenDigest = System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray());
        var suffix = Guid.NewGuid().ToString("N");
        var loginName = "o20session" + suffix;
        var loginNormalized = loginName.ToUpperInvariant();
        var roleKey = "o20-session-" + suffix;
        var permissionKey = "o20.permission." + suffix;

        try
        {
            await ExecuteAsync(cs, """
                INSERT INTO identity.accounts
                    (account_id,login_name,login_name_normalized,password_hash,password_hash_version,
                     must_change_password,is_enabled,is_bootstrap_account,credential_version,failed_attempt_count,
                     created_at_utc,updated_at_utc)
                VALUES (@account,@login,@normalized,'test-only-hash',1,false,true,false,2,0,clock_timestamp(),clock_timestamp());
                """,
                ("account", accountId), ("login", loginName), ("normalized", loginNormalized));

            await ExecuteAsync(cs, """
                INSERT INTO identity.roles
                    (role_id,role_key,display_name,is_system_role,is_assignable,created_at_utc,updated_at_utc)
                VALUES (@role,@role_key,'O20 session authorization proof',false,true,clock_timestamp(),clock_timestamp());
                """,
                ("role", roleId), ("role_key", roleKey));

            await ExecuteAsync(cs, """
                INSERT INTO identity.permissions(permission_id,permission_key,display_name,risk_level,is_active)
                VALUES (@permission,@permission_key,'O20 session proof permission','standard',true);
                """,
                ("permission", permissionId), ("permission_key", permissionKey));

            await ExecuteAsync(cs, """
                INSERT INTO identity.account_roles(account_id,role_id,assigned_at_utc,assignment_reason)
                VALUES (@account,@role,clock_timestamp(),'disposable integration test');
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
                     clock_timestamp()+interval '5 minutes',2,@client);
                """,
                ("session", sessionId), ("account", accountId), ("digest", tokenDigest), ("client", clientInstanceId));

            var options = new DbContextOptionsBuilder<MdGsmDbContext>()
                .UseNpgsql(cs)
                .Options;
            await using var db = new MdGsmDbContext(options);
            var store = new PostgresIdentityAccessStores(db);
            var account = MDGsm.IdentityAccess.Domain.AccountId.From(accountId);

            Assert.True(await store.HasPermissionForLiveSessionAsync(
                account, sessionId, 2, permissionKey, default));

            await ExecuteAsync(cs,
                "UPDATE identity.sessions SET revoked_at_utc=clock_timestamp(),revoke_reason='proof' WHERE session_id=@session;",
                ("session", sessionId));
            Assert.False(await store.HasPermissionForLiveSessionAsync(
                account, sessionId, 2, permissionKey, default));

            await ExecuteAsync(cs,
                "UPDATE identity.sessions SET revoked_at_utc=NULL,revoke_reason=NULL,lease_expires_at_utc=clock_timestamp()-interval '1 second' WHERE session_id=@session;",
                ("session", sessionId));
            Assert.False(await store.HasPermissionForLiveSessionAsync(
                account, sessionId, 2, permissionKey, default));

            await ExecuteAsync(cs,
                "UPDATE identity.sessions SET lease_expires_at_utc=clock_timestamp()+interval '5 minutes' WHERE session_id=@session;",
                ("session", sessionId));
            await ExecuteAsync(cs,
                "UPDATE identity.accounts SET credential_version=3 WHERE account_id=@account;",
                ("account", accountId));
            Assert.False(await store.HasPermissionForLiveSessionAsync(
                account, sessionId, 2, permissionKey, default));

            await ExecuteAsync(cs,
                "UPDATE identity.accounts SET credential_version=2,is_enabled=false WHERE account_id=@account;",
                ("account", accountId));
            Assert.False(await store.HasPermissionForLiveSessionAsync(
                account, sessionId, 2, permissionKey, default));

            await ExecuteAsync(cs,
                "UPDATE identity.accounts SET is_enabled=true WHERE account_id=@account;",
                ("account", accountId));
            await ExecuteAsync(cs,
                "UPDATE identity.permissions SET is_active=false WHERE permission_id=@permission;",
                ("permission", permissionId));
            Assert.False(await store.HasPermissionForLiveSessionAsync(
                account, sessionId, 2, permissionKey, default));
        }
        finally
        {
            await ExecuteAsync(cs, "DELETE FROM identity.sessions WHERE session_id=@id;", ("id", sessionId));
            await ExecuteAsync(cs, "DELETE FROM identity.account_roles WHERE account_id=@id;", ("id", accountId));
            await ExecuteAsync(cs, "DELETE FROM identity.role_permissions WHERE role_id=@id;", ("id", roleId));
            await ExecuteAsync(cs, "DELETE FROM identity.permissions WHERE permission_id=@id;", ("id", permissionId));
            await ExecuteAsync(cs, "DELETE FROM identity.roles WHERE role_id=@id;", ("id", roleId));
            await ExecuteAsync(cs, "DELETE FROM identity.accounts WHERE account_id=@id;", ("id", accountId));
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
}
