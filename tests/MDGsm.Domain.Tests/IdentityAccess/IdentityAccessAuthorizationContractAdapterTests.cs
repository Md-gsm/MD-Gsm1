using System;
using System.Threading;
using System.Threading.Tasks;
using MDGsm.Api.Security;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Contracts;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class IdentityAccessAuthorizationContractAdapterTests
{
    private static readonly Guid PrincipalId = Guid.Parse("60000000-0000-7000-8000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("60000000-0000-7000-8000-000000000002");
    private static readonly DateTimeOffset AuthenticatedAt =
        new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Current_principal_includes_only_server_validated_session_metadata()
    {
        var adapter = Create(actorId: PrincipalId, permissionResult: true);

        var decision = await adapter.ResolveCurrentPrincipalAsync(default);

        Assert.Equal(PrincipalDecisionStatus.Authenticated, decision.Status);
        Assert.Equal(PrincipalId, decision.AccountId);
        Assert.Equal(SessionId, decision.SessionId);
        Assert.Equal(7L, decision.PrincipalVersion);
        Assert.Equal("ENABLED", decision.AccountState);
        Assert.Equal("ACTIVE", decision.SessionState);
        Assert.Equal(AuthenticatedAt, decision.AuthenticatedAtUtc);
        Assert.Equal(ModuleContractVersion.Current, decision.ContractVersion);
        Assert.True(decision.IsAuthenticated);
    }

    [Fact]
    public async Task Missing_validated_session_is_not_authenticated()
    {
        var adapter = new IdentityAccessAuthorizationContractAdapter(
            new Actor(() => throw new UnauthorizedAccessException()),
            new AuthorizationStore(true));

        var principal = await adapter.ResolveCurrentPrincipalAsync(default);
        var permission = await adapter.EvaluateCurrentPermissionAsync("persons.read", default);

        Assert.Equal(PrincipalDecisionStatus.Unauthenticated, principal.Status);
        Assert.False(principal.IsAuthenticated);
        Assert.Equal(PermissionDecisionStatus.Denied, permission.Status);
        Assert.False(permission.IsAllowed);
    }

    [Fact]
    public async Task Unknown_permission_key_is_denied_without_authorization_store_call()
    {
        var store = new AuthorizationStore(true);
        var adapter = new IdentityAccessAuthorizationContractAdapter(new Actor(() => PrincipalId), store);

        var decision = await adapter.EvaluateCurrentPermissionAsync("reporting.branch.read", default);

        Assert.Equal(PermissionDecisionStatus.Denied, decision.Status);
        Assert.Equal("unknown_permission_key", decision.ReasonCode);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Permission_decision_is_bound_to_account_session_and_credential_version()
    {
        var store = new AuthorizationStore(true);
        var adapter = new IdentityAccessAuthorizationContractAdapter(new Actor(() => PrincipalId), store);

        var decision = await adapter.EvaluateCurrentPermissionAsync("reports.read", default);

        Assert.Equal(PermissionDecisionStatus.Allowed, decision.Status);
        Assert.Equal(PrincipalId, decision.PrincipalAccountId);
        Assert.Equal(SessionId, decision.PrincipalSessionId);
        Assert.Equal(7L, decision.PrincipalVersion);
        Assert.Equal("reports.read", store.LastPermission);
        Assert.Equal(PrincipalId, store.LastAccountId?.Value);
        Assert.Equal(SessionId, store.LastSessionId);
        Assert.Equal(7L, store.LastCredentialVersion);
    }

    [Fact]
    public async Task Inactive_or_revoked_session_is_denied_even_when_permission_fixture_is_false()
    {
        var store = new AuthorizationStore(false);
        var adapter = new IdentityAccessAuthorizationContractAdapter(new Actor(() => PrincipalId), store);

        var decision = await adapter.EvaluateCurrentPermissionAsync("reports.export", default);

        Assert.Equal(PermissionDecisionStatus.Denied, decision.Status);
        Assert.Equal("session_or_permission_not_active", decision.ReasonCode);
        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public async Task Authorization_store_failure_returns_unavailable_not_allow()
    {
        var store = new AuthorizationStore(new InvalidOperationException("simulated store outage"));
        var adapter = new IdentityAccessAuthorizationContractAdapter(new Actor(() => PrincipalId), store);

        var decision = await adapter.EvaluateCurrentPermissionAsync("reports.read", default);

        Assert.Equal(PermissionDecisionStatus.Unavailable, decision.Status);
        Assert.False(decision.IsAllowed);
        Assert.Equal("authorization_store_unavailable", decision.ReasonCode);
    }

    [Fact]
    public async Task Product_owner_authority_fails_closed_until_protected_binding_is_implemented()
    {
        var adapter = Create(actorId: PrincipalId, permissionResult: true);

        var decision = await adapter.ResolveCurrentProductOwnerAuthorityAsync(default);

        Assert.Equal(ProductOwnerAuthorityStatus.Unavailable, decision.Status);
        Assert.False(decision.IsBoundOwner);
        Assert.Null(decision.BindingVersion);
        Assert.Equal("owner_binding_not_implemented", decision.ReasonCode);
    }

    private static IdentityAccessAuthorizationContractAdapter Create(Guid actorId, bool permissionResult) =>
        new(new Actor(() => actorId), new AuthorizationStore(permissionResult));

    private sealed class Actor(Func<Guid> resolve) : IAuthenticatedActorContext
    {
        public Guid AccountId => resolve();
        public Guid SessionId => resolve() == Guid.Empty ? Guid.Empty : IdentityAccessAuthorizationContractAdapterTests.SessionId;
        public long CredentialVersion => resolve() == Guid.Empty ? 0 : 7;
        public DateTimeOffset AuthenticatedAtUtc => resolve() == Guid.Empty ? default : AuthenticatedAt;
    }

    private sealed class AuthorizationStore : IRequestAuthorizationStore
    {
        private readonly object _outcome;

        public AuthorizationStore(bool allowed) => _outcome = allowed;
        public AuthorizationStore(Exception exception) => _outcome = exception;

        public int Calls { get; private set; }
        public string? LastPermission { get; private set; }
        public MDGsm.IdentityAccess.Domain.AccountId? LastAccountId { get; private set; }
        public Guid? LastSessionId { get; private set; }
        public long? LastCredentialVersion { get; private set; }

        public Task<bool> HasPermissionForLiveSessionAsync(
            MDGsm.IdentityAccess.Domain.AccountId accountId,
            Guid sessionId,
            long credentialVersion,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastAccountId = accountId;
            LastSessionId = sessionId;
            LastCredentialVersion = credentialVersion;
            LastPermission = permissionKey;
            if (_outcome is Exception exception) throw exception;
            return Task.FromResult((bool)_outcome);
        }
    }
}
