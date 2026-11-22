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

    [Fact]
    public async Task Current_principal_is_resolved_only_from_validated_actor_context()
    {
        var adapter = Create(actorId: PrincipalId, permissionResult: true);

        var decision = await adapter.ResolveCurrentPrincipalAsync(default);

        Assert.Equal(PrincipalDecisionStatus.Authenticated, decision.Status);
        Assert.Equal(PrincipalId, decision.AccountId);
        Assert.Equal(ModuleContractVersion.Current, decision.ContractVersion);
        Assert.True(decision.IsAuthenticated);
    }

    [Fact]
    public async Task Missing_validated_session_is_not_authenticated()
    {
        var adapter = new IdentityAccessAuthorizationContractAdapter(
            new Actor(() => throw new UnauthorizedAccessException()),
            new PermissionStore(true));

        var principal = await adapter.ResolveCurrentPrincipalAsync(default);
        var permission = await adapter.EvaluateCurrentPermissionAsync("persons.read", default);

        Assert.Equal(PrincipalDecisionStatus.Unauthenticated, principal.Status);
        Assert.False(principal.IsAuthenticated);
        Assert.Equal(PermissionDecisionStatus.Denied, permission.Status);
        Assert.False(permission.IsAllowed);
    }

    [Fact]
    public async Task Unknown_permission_key_is_denied_without_permission_store_call()
    {
        var store = new PermissionStore(true);
        var adapter = new IdentityAccessAuthorizationContractAdapter(new Actor(() => PrincipalId), store);

        var decision = await adapter.EvaluateCurrentPermissionAsync("reporting.branch.read", default);

        Assert.Equal(PermissionDecisionStatus.Denied, decision.Status);
        Assert.Equal("unknown_permission_key", decision.ReasonCode);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Known_permission_is_evaluated_against_current_server_side_grant()
    {
        var store = new PermissionStore(true);
        var adapter = new IdentityAccessAuthorizationContractAdapter(new Actor(() => PrincipalId), store);

        var decision = await adapter.EvaluateCurrentPermissionAsync("reports.read", default);

        Assert.Equal(PermissionDecisionStatus.Allowed, decision.Status);
        Assert.Equal(PrincipalId, decision.PrincipalAccountId);
        Assert.Equal("reports.read", store.LastPermission);
        Assert.Equal(PrincipalId, store.LastAccountId?.Value);
    }

    [Fact]
    public async Task Missing_permission_grant_is_denied()
    {
        var adapter = Create(actorId: PrincipalId, permissionResult: false);

        var decision = await adapter.EvaluateCurrentPermissionAsync("reports.export", default);

        Assert.Equal(PermissionDecisionStatus.Denied, decision.Status);
        Assert.Equal("permission_not_granted", decision.ReasonCode);
    }

    [Fact]
    public async Task Permission_store_failure_returns_unavailable_not_allow()
    {
        var store = new PermissionStore(new InvalidOperationException("simulated store outage"));
        var adapter = new IdentityAccessAuthorizationContractAdapter(new Actor(() => PrincipalId), store);

        var decision = await adapter.EvaluateCurrentPermissionAsync("reports.read", default);

        Assert.Equal(PermissionDecisionStatus.Unavailable, decision.Status);
        Assert.False(decision.IsAllowed);
        Assert.Equal("permission_store_unavailable", decision.ReasonCode);
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
        new(new Actor(() => actorId), new PermissionStore(permissionResult));

    private sealed class Actor(Func<Guid> resolve) : IAuthenticatedActorContext
    {
        public Guid AccountId => resolve();
    }

    private sealed class PermissionStore : IRolePermissionStore
    {
        private readonly object _outcome;

        public PermissionStore(bool allowed) => _outcome = allowed;
        public PermissionStore(Exception exception) => _outcome = exception;

        public int Calls { get; private set; }
        public string? LastPermission { get; private set; }
        public MDGsm.IdentityAccess.Domain.AccountId? LastAccountId { get; private set; }

        public Task<bool> HasPermissionAsync(MDGsm.IdentityAccess.Domain.AccountId id, string permissionKey, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            LastAccountId = id;
            LastPermission = permissionKey;
            if (_outcome is Exception exception) throw exception;
            return Task.FromResult((bool)_outcome);
        }

        public Task<bool> HasRoleAsync(MDGsm.IdentityAccess.Domain.AccountId id, string roleKey, CancellationToken ct) =>
            Task.FromResult(false);
    }
}
