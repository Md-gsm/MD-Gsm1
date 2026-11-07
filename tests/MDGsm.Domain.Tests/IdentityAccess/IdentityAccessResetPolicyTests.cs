using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class IdentityAccessResetPolicyTests
{
    [Fact]
    public async Task Reset_rejects_global_administrator_and_records_reason()
    {
        var target = new Account(AccountId.New(), Username.Parse("globaladmin"), "old-hash", 1);
        var accounts = new Accounts(target);
        var roles = new Roles(true, true);
        var audit = new Audit();
        var sessions = new Sessions();
        var handler = new AdminResetPasswordHandler(new UnitOfWork(), accounts, new Hasher(), roles,
            sessions, audit, TimeProvider.System);
        var result = await handler.HandleAsync(AccountId.New(), target.Id, "temporary-value", "test", CancellationToken.None);
        Assert.False(result);
        Assert.Equal("identity.credentials.reset", roles.PermissionAsked);
        Assert.Equal(RoleCatalog.SuperAdmin, roles.RoleAsked);
        Assert.Contains(audit.Outcomes, x => x == "denied_super_admin_recovery_required");
        Assert.Equal(0, sessions.Revocations);
        Assert.Equal("old-hash", (await accounts.FindByIdAsync(target.Id, CancellationToken.None))!.PasswordHash);
    }

    [Fact]
    public async Task Reset_works_for_non_global_account_only_with_catalog_permission()
    {
        var target = new Account(AccountId.New(), Username.Parse("ordinaryuser"), "old-hash", 1);
        var accounts = new Accounts(target);
        var roles = new Roles(true, false);
        var sessions = new Sessions();
        var handler = new AdminResetPasswordHandler(new UnitOfWork(), accounts, new Hasher(), roles,
            sessions, new Audit(), TimeProvider.System);
        var result = await handler.HandleAsync(AccountId.New(), target.Id, "temporary-value", "test", CancellationToken.None);
        Assert.True(result);
        Assert.Equal(1, sessions.Revocations);
        var updated = await accounts.FindByIdAsync(target.Id, CancellationToken.None);
        Assert.True(updated!.MustChangePassword);
        Assert.Equal(2, updated.CredentialVersion);
    }

    [Fact]
    public async Task Reset_denies_actor_without_catalog_permission()
    {
        var target = new Account(AccountId.New(), Username.Parse("ordinaryuser"), "old-hash", 1);
        var accounts = new Accounts(target);
        var roles = new Roles(false, false);
        var audit = new Audit();
        var handler = new AdminResetPasswordHandler(new UnitOfWork(), accounts, new Hasher(), roles,
            new Sessions(), audit, TimeProvider.System);
        Assert.False(await handler.HandleAsync(AccountId.New(), target.Id, "temporary-value", "test", CancellationToken.None));
        Assert.Contains(audit.Outcomes, x => x == "denied_missing_permission");
    }

    private sealed class UnitOfWork : IIdentityAccessUnitOfWork
    { public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct); }
    private sealed class Accounts(Account account) : IAccountStore
    {
        private Account _account = account;
        public Task<Account?> FindByNormalizedUsernameAsync(string name, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<Account?> FindByIdAsync(AccountId id, CancellationToken ct) => Task.FromResult<Account?>(_account.Id == id ? _account : null);
        public Task SaveAsync(Account value, CancellationToken ct) { _account = value; return Task.CompletedTask; }
    }
    private sealed class Hasher : IPasswordHasher
    {
        public short CurrentVersion => 1;
        public string DummyHash => "dummy";
        public string Hash(string password) => "hashed-new";
        public bool Verify(string hash, string password) => true;
    }
    private sealed class Roles(bool allowed, bool isGlobal) : IRolePermissionStore
    {
        public string? PermissionAsked { get; private set; }
        public string? RoleAsked { get; private set; }
        public Task<bool> HasPermissionAsync(AccountId id, string key, CancellationToken ct)
        { PermissionAsked = key; return Task.FromResult(allowed); }
        public Task<bool> HasRoleAsync(AccountId id, string key, CancellationToken ct)
        { RoleAsked = key; return Task.FromResult(isGlobal); }
    }
    private sealed class Sessions : ISessionStore
    {
        public int Revocations { get; private set; }
        public Task<bool> HasActiveSessionAsync(AccountId id, DateTimeOffset now, CancellationToken ct) => Task.FromResult(false);
        public Task<int> CountDistinctActiveAccountsAsync(DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
        public Task CreateAsync(AccountId id, byte[] digest, long version, Guid client, DateTimeOffset created, DateTimeOffset expiry, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAllForAccountAsync(AccountId id, DateTimeOffset now, string reason, CancellationToken ct) { Revocations++; return Task.CompletedTask; }
        public Task<bool> TryHeartbeatAsync(byte[] digest, long version, DateTimeOffset now, DateTimeOffset expiry, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> RevokeByDigestAsync(byte[] digest, DateTimeOffset now, string reason, CancellationToken ct) => Task.FromResult(false);
    }
    private sealed class Audit : IAuditWriter
    {
        public List<string> Outcomes { get; } = [];
        public Task AppendAsync(string type, AccountId? actor, AccountId? subject, string outcome, string correlation, CancellationToken ct)
        { Outcomes.Add(outcome); return Task.CompletedTask; }
    }
}