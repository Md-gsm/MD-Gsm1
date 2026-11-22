using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.IdentityAccess.Domain;

namespace MDGsm.Api.Security;

/// <summary>
/// Host adapter for the public IdentityAccess authorization contract.
/// Principal facts are taken exclusively from the middleware-validated request context.
/// Permission is evaluated against a live session and current grant in one DB statement.
/// Product Owner authority remains fail-closed until the protected binding lifecycle exists.
/// </summary>
public sealed class IdentityAccessAuthorizationContractAdapter(
    IAuthenticatedActorContext actorContext,
    IRequestAuthorizationStore authorizationStore) : IIdentityAccessAuthorizationContract
{
    public Task<CurrentPrincipalDecision> ResolveCurrentPrincipalAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var accountId = actorContext.AccountId;
            var sessionId = actorContext.SessionId;
            var credentialVersion = actorContext.CredentialVersion;
            var authenticatedAt = actorContext.AuthenticatedAtUtc;

            if (accountId == Guid.Empty || sessionId == Guid.Empty || credentialVersion <= 0)
            {
                return Task.FromResult(new CurrentPrincipalDecision(
                    PrincipalDecisionStatus.Unauthenticated, null, null, null, null, null, null,
                    ModuleContractVersion.Current, "invalid_authenticated_principal"));
            }

            return Task.FromResult(new CurrentPrincipalDecision(
                PrincipalDecisionStatus.Authenticated,
                accountId,
                sessionId,
                credentialVersion,
                "ENABLED",
                "ACTIVE",
                authenticatedAt,
                ModuleContractVersion.Current,
                "validated_server_session"));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new CurrentPrincipalDecision(
                PrincipalDecisionStatus.Unauthenticated, null, null, null, null, null, null,
                ModuleContractVersion.Current, "no_validated_session"));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new CurrentPrincipalDecision(
                PrincipalDecisionStatus.Unavailable, null, null, null, null, null, null,
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
                PermissionDecisionStatus.Denied, permissionKey ?? string.Empty, null, null, null,
                ModuleContractVersion.Current, "unknown_permission_key");
        }

        var principal = await ResolveCurrentPrincipalAsync(cancellationToken);
        if (!principal.IsAuthenticated
            || principal.AccountId is not { } accountId
            || principal.SessionId is not { } sessionId
            || principal.PrincipalVersion is not { } credentialVersion)
        {
            var status = principal.Status == PrincipalDecisionStatus.Unavailable
                ? PermissionDecisionStatus.Unavailable
                : PermissionDecisionStatus.Denied;

            return new PermissionDecision(
                status, permissionKey, principal.AccountId, principal.SessionId, principal.PrincipalVersion,
                ModuleContractVersion.Current,
                principal.Status == PrincipalDecisionStatus.Unavailable
                    ? "principal_resolution_unavailable"
                    : "no_validated_session");
        }

        try
        {
            var allowed = await authorizationStore.HasPermissionForLiveSessionAsync(
                AccountId.From(accountId), sessionId, credentialVersion, permissionKey, cancellationToken);

            return new PermissionDecision(
                allowed ? PermissionDecisionStatus.Allowed : PermissionDecisionStatus.Denied,
                permissionKey, accountId, sessionId, credentialVersion, ModuleContractVersion.Current,
                allowed ? "live_session_and_active_permission_grant" : "session_or_permission_not_active");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new PermissionDecision(
                PermissionDecisionStatus.Unavailable, permissionKey, accountId, sessionId, credentialVersion,
                ModuleContractVersion.Current, "authorization_store_unavailable");
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
            null,
            ModuleContractVersion.Current,
            "owner_binding_not_implemented"));
    }
}
