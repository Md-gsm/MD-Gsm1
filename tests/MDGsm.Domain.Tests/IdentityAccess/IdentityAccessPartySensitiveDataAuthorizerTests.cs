using MDGsm.IdentityAccess.Contracts;
using MDGsm.Infrastructure.IdentityAccess;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class IdentityAccessPartySensitiveDataAuthorizerTests
{
    private static readonly Guid AccountGuid = Guid.Parse("01936f3e-7b2a-7abc-8def-0123456789ab");
    private static readonly Guid SessionGuid = Guid.Parse("01936f3e-7b2a-7abc-8def-0123456789ac");
    private const long PrincipalVersion = 7;

    [Fact]
    public async Task Allows_only_when_public_contract_returns_allow_for_same_live_principal()
    {
        var contract = new StubAuthorizationContract(AuthenticatedPrincipal(AccountGuid, SessionGuid),
            PermissionDecisionStatus.Allowed, AccountGuid, SessionGuid, PrincipalVersion);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);

        Assert.True(await authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
        Assert.Equal(1, contract.PrincipalCalls);
        Assert.Equal(1, contract.PermissionCalls);
        Assert.Equal(IdentityAccessPartySensitiveDataAuthorizer.PermissionKey, contract.LastPermissionKey);
    }

    [Fact]
    public async Task Denies_when_permission_is_missing()
    {
        var contract = new StubAuthorizationContract(AuthenticatedPrincipal(AccountGuid, SessionGuid),
            PermissionDecisionStatus.Denied, AccountGuid, SessionGuid, PrincipalVersion);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);
        Assert.False(await authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
    }

    [Fact]
    public async Task Denies_when_permission_service_is_unavailable()
    {
        var contract = new StubAuthorizationContract(AuthenticatedPrincipal(AccountGuid, SessionGuid),
            PermissionDecisionStatus.Unavailable, AccountGuid, SessionGuid, PrincipalVersion);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);
        Assert.False(await authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
    }

    [Fact]
    public async Task Denies_when_permission_decision_is_bound_to_another_session_or_version()
    {
        var contract = new StubAuthorizationContract(AuthenticatedPrincipal(AccountGuid, SessionGuid),
            PermissionDecisionStatus.Allowed, AccountGuid, Guid.NewGuid(), PrincipalVersion + 1);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);
        Assert.False(await authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
    }

    [Fact]
    public async Task Denies_when_allowed_decision_names_a_different_permission()
    {
        var contract = new StubAuthorizationContract(AuthenticatedPrincipal(AccountGuid, SessionGuid),
            PermissionDecisionStatus.Allowed, AccountGuid, SessionGuid, PrincipalVersion,
            permissionKeyOverride: "persons.read");
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);
        Assert.False(await authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
    }

    [Fact]
    public async Task Denies_when_actor_does_not_match_validated_principal_without_permission_lookup()
    {
        var contract = new StubAuthorizationContract(AuthenticatedPrincipal(Guid.NewGuid(), SessionGuid),
            PermissionDecisionStatus.Allowed, AccountGuid, SessionGuid, PrincipalVersion);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);
        Assert.False(await authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
        Assert.Equal(0, contract.PermissionCalls);
    }

    [Fact]
    public async Task Denies_when_no_authenticated_principal_exists()
    {
        var principal = new CurrentPrincipalDecision(
            PrincipalDecisionStatus.Unauthenticated, null, null, null, null, null, null,
            ModuleContractVersion.Current, "no_validated_session");
        var contract = new StubAuthorizationContract(principal, PermissionDecisionStatus.Allowed,
            AccountGuid, SessionGuid, PrincipalVersion);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);
        Assert.False(await authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
        Assert.Equal(0, contract.PermissionCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Denies_invalid_actor_identifiers_without_resolving_principal(string actorId)
    {
        var contract = new StubAuthorizationContract(AuthenticatedPrincipal(AccountGuid, SessionGuid),
            PermissionDecisionStatus.Allowed, AccountGuid, SessionGuid, PrincipalVersion);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);
        Assert.False(await authorizer.CanReadSensitivePartyDataAsync(actorId, default));
        Assert.Equal(0, contract.PrincipalCalls);
        Assert.Equal(0, contract.PermissionCalls);
    }

    [Fact]
    public async Task Honors_cancellation_before_contract_lookup()
    {
        var contract = new StubAuthorizationContract(AuthenticatedPrincipal(AccountGuid, SessionGuid),
            PermissionDecisionStatus.Allowed, AccountGuid, SessionGuid, PrincipalVersion);
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), cancellation.Token));
        Assert.Equal(0, contract.PrincipalCalls);
    }

    [Fact]
    public async Task Propagates_contract_failures_instead_of_failing_open()
    {
        var contract = new StubAuthorizationContract(
            new InvalidOperationException("authorization contract unavailable"));
        var authorizer = new IdentityAccessPartySensitiveDataAuthorizer(contract);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            authorizer.CanReadSensitivePartyDataAsync(AccountGuid.ToString("D"), default));
    }

    private static CurrentPrincipalDecision AuthenticatedPrincipal(Guid accountId, Guid sessionId) =>
        new(PrincipalDecisionStatus.Authenticated, accountId, sessionId, PrincipalVersion,
            "ENABLED", "ACTIVE", DateTimeOffset.UtcNow, ModuleContractVersion.Current,
            "validated_server_session");

    private sealed class StubAuthorizationContract : IIdentityAccessAuthorizationContract
    {
        private readonly CurrentPrincipalDecision? _principal;
        private readonly PermissionDecisionStatus _permissionStatus;
        private readonly Guid? _permissionAccountId;
        private readonly Guid? _permissionSessionId;
        private readonly long? _permissionVersion;
        private readonly string? _permissionKeyOverride;
        private readonly Exception? _failure;

        public StubAuthorizationContract(CurrentPrincipalDecision principal,
            PermissionDecisionStatus permissionStatus, Guid? permissionAccountId,
            Guid? permissionSessionId, long? permissionVersion, string? permissionKeyOverride = null)
        {
            _principal = principal;
            _permissionStatus = permissionStatus;
            _permissionAccountId = permissionAccountId;
            _permissionSessionId = permissionSessionId;
            _permissionVersion = permissionVersion;
            _permissionKeyOverride = permissionKeyOverride;
        }

        public StubAuthorizationContract(Exception failure) => _failure = failure;
        public int PrincipalCalls { get; private set; }
        public int PermissionCalls { get; private set; }
        public string? LastPermissionKey { get; private set; }

        public Task<CurrentPrincipalDecision> ResolveCurrentPrincipalAsync(CancellationToken cancellationToken)
        {
            PrincipalCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            if (_failure is not null) throw _failure;
            return Task.FromResult(_principal!);
        }

        public Task<PermissionDecision> EvaluateCurrentPermissionAsync(string permissionKey,
            CancellationToken cancellationToken)
        {
            PermissionCalls++;
            LastPermissionKey = permissionKey;
            cancellationToken.ThrowIfCancellationRequested();
            if (_failure is not null) throw _failure;
            return Task.FromResult(new PermissionDecision(_permissionStatus, _permissionKeyOverride ?? permissionKey,
                _permissionAccountId, _permissionSessionId, _permissionVersion,
                ModuleContractVersion.Current, "test"));
        }

        public Task<PermissionDecision> EvaluateCurrentPermissionInTransactionAsync(string permissionKey,
            System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProductOwnerAuthorityDecision> ResolveCurrentProductOwnerAuthorityAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProductOwnerAuthorityDecision(ProductOwnerAuthorityStatus.Unavailable,
                null, null, null, ModuleContractVersion.Current, "not-in-test-scope"));
    }
}
