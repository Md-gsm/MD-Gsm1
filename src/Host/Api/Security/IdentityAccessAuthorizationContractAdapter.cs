using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.IdentityAccess.Domain;

namespace MDGsm.Api.Security;

/// <summary>
/// Host composition adapter for the public IdentityAccess contract.
/// The principal is taken exclusively from the middleware-validated request context.
/// The Product Owner binding path fails closed until its protected store/lifecycle is implemented.
/// </summary>
public sealed class IdentityAccessAuthorizationContractAdapter(
    IAuthenticatedActorContext actorContext,
    IRolePermissionStore permissions) : IIdentityAccessAuthorizationContract
{
    public Task<CurrentPrincipalDecision> ResolveCurrentPrincipalAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var accountId = actorContext.AccountId;
            if (accountId == Guid.Empty)
                return Task.FromResult(new CurrentPrincipalDecision(
                    PrincipalDecisionStatus.Unauthenticated, null,
                    ModuleContractVersion.Current, "invalid_authenticated_principal"));

            return Task.FromResult(new CurrentPrincipalDecision(
                PrincipalDecisionStatus.Authenticated, accountId,
                ModuleContractVersion.Current, "validated_session"));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new CurrentPrincipalDecision(
                PrincipalDecisionStatus.Unauthenticated, null,
                ModuleContractVersion.Current, "no_validated_session"));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new CurrentPrincipalDecision(
                PrincipalDecisionStatus.Unavailable, null,
                ModuleContractVersion.Current, "principal_resolution_unavailable"));
        }
    }

    public async Task<PermissionDecision> EvaluateCurrentPermissionAsync(
        string permissionKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permissionKey)
            || !PermissionCatalog.IsKnown(permissionKey))
        {
            return new PermissionDecision(
                PermissionDecisionStatus.Denied, permissionKey ?? string.Empty, null,
                ModuleContractVersion.Current, "unknown_permission_key");
        }

        var principal = await ResolveCurrentPrincipalAsync(cancellationToken);
        if (principal.Status != PrincipalDecisionStatus.Authenticated
            || principal.AccountId is not { } accountId)
        {
            var status = principal.Status == PrincipalDecisionStatus.Unavailable
                ? PermissionDecisionStatus.Unavailable
                : PermissionDecisionStatus.Denied;

            return new PermissionDecision(
                status, permissionKey, principal.AccountId,
                ModuleContractVersion.Current,
                principal.Status == PrincipalDecisionStatus.Unavailable
                    ? "principal_resolution_unavailable"
                    : "no_validated_session");
        }

        try
        {
            var allowed = await permissions.HasPermissionAsync(
                AccountId.From(accountId), permissionKey, cancellationToken);

            return new PermissionDecision(
                allowed ? PermissionDecisionStatus.Allowed : PermissionDecisionStatus.Denied,
                permissionKey, accountId, ModuleContractVersion.Current,
                allowed ? "active_permission_grant" : "permission_not_granted");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new PermissionDecision(
                PermissionDecisionStatus.Unavailable, permissionKey, accountId,
                ModuleContractVersion.Current, "permission_store_unavailable");
        }
    }

    public Task<ProductOwnerAuthorityDecision> ResolveCurrentProductOwnerAuthorityAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // No protected Product Owner binding store or accepted enrollment/recovery lifecycle
        // exists yet. Never infer owner authority from role, username or client claims.
        return Task.FromResult(new ProductOwnerAuthorityDecision(
            ProductOwnerAuthorityStatus.Unavailable,
            null,
            null,
            ModuleContractVersion.Current,
            "owner_binding_not_implemented"));
    }
}
