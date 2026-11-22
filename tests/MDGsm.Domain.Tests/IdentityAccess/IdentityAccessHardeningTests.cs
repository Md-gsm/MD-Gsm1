using System.Security.Cryptography;
using System.Text;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Infrastructure.IdentityAccess;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class IdentityAccessHardeningTests
{
    [Theory]
    [InlineData("unknownuser", false, false)]
    [InlineData("disableduser", true, false)]
    [InlineData("lockeduser", true, true)]
    [InlineData("activeuser", true, false)]
    public async Task Login_always_verifies_one_hash_for_known_and_unknown_states(string name, bool exists, bool locked)
    {
        var now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(now);
        var hasher = new SpyHasher();
        var accounts = new MemoryAccounts();
        if (exists)
        {
            var account = new Account(AccountId.New(), Username.Parse(name), "stored-hash", 1);
            if (name == "disableduser") account.SetEnabled(false);
            if (locked) account = Account.Rehydrate(account.Id, account.Username, account.PasswordHash, 1,
                true, false, 1, 5, now.AddMinutes(-1), now.AddMinutes(5));
            accounts.Add(account);
        }
        var handler = new LoginHandler(new DirectUnitOfWork(), accounts, hasher, new TokenFactory(),
            new PrivacyDigest(), new Sessions(), new Attempts(), new Policies(), new Audit(), clock);
        var result = await handler.HandleAsync(new LoginCommand(name, "not-the-password", Guid.CreateVersion7(),
            new string('A', 64), "timing-test"), CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal(1, hasher.Calls);
        Assert.Equal(exists && name == "activeuser" ? "stored-hash" : hasher.DummyHash, hasher.LastHash);
    }

    [Fact]
    public void Privacy_digest_is_keyed_and_rejects_missing_or_short_secret()
    {
        var firstKey = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();
        var secondKey = Enumerable.Range(2, 32).Select(x => (byte)x).ToArray();
        var first = new HmacPrivacyDigest(Config(firstKey));
        var repeat = new HmacPrivacyDigest(Config(firstKey));
        var other = new HmacPrivacyDigest(Config(secondKey));
        Assert.Equal(first.ComputeSha256("same-input"), repeat.ComputeSha256("same-input"));
        Assert.NotEqual(first.ComputeSha256("same-input"), other.ComputeSha256("same-input"));
        Assert.Equal(32, first.ComputeSha256("same-input").Length);
        Assert.Throws<InvalidOperationException>(() => new HmacPrivacyDigest(new ConfigurationBuilder().Build()));
        Assert.Throws<InvalidOperationException>(() => new HmacPrivacyDigest(Config(new byte[16])));
    }

    private static IConfiguration Config(byte[] key) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { [HmacPrivacyDigest.ConfigurationKey] = Convert.ToBase64String(key) }).Build();

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class SpyHasher : IPasswordHasher
    {
        public short CurrentVersion => 1;
        public string DummyHash => "dummy-hash";
        public int Calls { get; private set; }
        public string? LastHash { get; private set; }
        public string Hash(string password) => "hash";
        public bool Verify(string hash, string password) { Calls++; LastHash = hash; return false; }
    }
    private sealed class MemoryAccounts : IAccountStore
    {
        private readonly Dictionary<Guid, Account> _ids = new();
        private readonly Dictionary<string, Account> _names = new(StringComparer.Ordinal);
        public void Add(Account account) { _ids[account.Id.Value] = account; _names[account.Username.NormalizedValue] = account; }
        public Task<Account?> FindByNormalizedUsernameAsync(string name, CancellationToken ct) => Task.FromResult(_names.GetValueOrDefault(name));
        public Task<Account?> FindByIdAsync(AccountId id, CancellationToken ct) => Task.FromResult(_ids.GetValueOrDefault(id.Value));
        public Task SaveAsync(Account account, CancellationToken ct) { Add(account); return Task.CompletedTask; }
    }
    private sealed class DirectUnitOfWork : IIdentityAccessUnitOfWork
    { public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> op, CancellationToken ct) => op(ct); }
    private sealed class TokenFactory : IAccessTokenFactory
    {
        public string CreateOpaqueToken() => "opaque-token";
        public byte[] ComputeDigest(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
    private sealed class PrivacyDigest : IPrivacyDigest
    { public byte[] ComputeSha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value)); }
    private sealed class Sessions : ISessionStore
    {
        public Task<bool> HasActiveSessionAsync(AccountId id, DateTimeOffset now, CancellationToken ct) => Task.FromResult(false);
        public Task<int> CountDistinctActiveAccountsAsync(DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
        public Task CreateAsync(AccountId id, byte[] digest, long version, Guid client, DateTimeOffset created, DateTimeOffset expiry, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAllForAccountAsync(AccountId id, DateTimeOffset now, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> TryHeartbeatAsync(byte[] digest, long version, DateTimeOffset now, DateTimeOffset expiry, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> RevokeByDigestAsync(byte[] digest, DateTimeOffset now, string reason, CancellationToken ct) => Task.FromResult(false);
    }
    private sealed class Attempts : ILoginAttemptStore
    {
        public Task RecordAsync(string login, string source, string outcome, DateTimeOffset at, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> IsSourceRateLimitedAsync(string source, DateTimeOffset now, CancellationToken ct) => Task.FromResult(false);
    }
    private sealed class Policies : ISystemPolicyStore
    { public Task<SessionPolicy> GetSessionPolicyAsync(CancellationToken ct) => Task.FromResult(SessionPolicy.Initial); }
    private sealed class Audit : IAuditWriter
    { public Task AppendAsync(string type, AccountId? actor, AccountId? subject, string outcome, string correlation, CancellationToken ct) => Task.CompletedTask; }
}