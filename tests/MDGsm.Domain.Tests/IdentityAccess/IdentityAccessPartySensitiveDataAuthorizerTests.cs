using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Infrastructure.IdentityAccess;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class IdentityAccessPartySensitiveDataAuthorizerTests
{
    private static readonly Guid AccountGuid = Guid.Parse("01936f3e-7b2a-7abc-8def-0123456789ab");

    [Fact]
    public async Task Allows_only_when_live_permission_store_grants_sensitive_read()
    {
        var store = new StubRolePermissionStore(true);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(store);

        Assert.True(await authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
        Assert.Equal(AccountId.From(AccountGuid), store.LastAccountId!.Value);
        Assert.Equal(IdentityAccessPartySensitiveDataAuthorizer.PermissionKey, store.LastPermissionKey);
    }

    [Fact]
    public async Task Denies_when_permission_is_missing()
    {
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(new StubRolePermissionStore(false));
        Assert.False(await authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Denies_invalid_actor_identifiers_without_querying_store(string actorId)
    {
        var store = new StubRolePermissionStore(true);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(store);
        Assert.False(await authorizer.CanReadSensitivePartyDataAsync(actorId, default));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Honors_cancellation_before_permission_lookup()
    {
        var store = new StubRolePermissionStore(true);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), cancellation.Token));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Propagates_store_failures_instead_of_failing_open()
    {
        var store = new StubRolePermissionStore(new InvalidOperationException("permission store unavailable"));
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
    }

    private sealed class StubRolePermissionStore : IRolePermissionStore
    {
        private readonly bool _result;
        private readonly Exception? _failure;
        public StubRolePermissionStore(bool result) => _result = result;
        public StubRolePermissionStore(Exception failure) => _failure = failure;
        public int Calls { get; private set; }
        public AccountId? LastAccountId { get; private set; }
        public string? LastPermissionKey { get; private set; }

        public Task<bool> HasPermissionAsync(AccountId id, string permissionKey, CancellationToken ct)
        {
            Calls++;
            LastAccountId = id;
            LastPermissionKey = permissionKey;
            if (_failure is not null) throw _failure;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_result);
        }

        public Task<bool> HasRoleAsync(AccountId id, string roleKey, CancellationToken ct) =>
            Task.FromResult(false);
    }
}
