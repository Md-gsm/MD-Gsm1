using System.Data.Common;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.IdentityAccess.Domain;

namespace MDGsm.Infrastructure.IdentityAccess;

/// <summary>
/// IdentityAccess-owned implementation of the public authorization contract.
/// It consumes a host-supplied accessor whose values come only from session middleware,
/// and uses IdentityAccess-owned persistence for live permission/session evaluation.
/// </summary>
public sealed class IdentityAccessAuthorizationContractAdapter(
    IValidatedRequestPrincipalAccessor principalAccessor,
    IRequestAuthorizationStore authorizationStore) : IIdentityAccessAuthorizationContract
{
    public Task<CurrentPrincipalDecision> ResolveCurrentPrincipalAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var accountId = principalAccessor.AccountId;
            var sessionId = principalAccessor.SessionId;
            var credentialVersion = principalAccessor.CredentialVersion;
            var authenticatedAt = principalAccessor.AuthenticatedAtUtc;

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
        if (string.IsNullOrWhiteSpace(permissionKey) || !PermissionCatalog.IsKnown(permissionKey))
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

    public async Task<PermissionDecision> EvaluateCurrentPermissionInTransactionAsync(
        string permissionKey,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(permissionKey) || !PermissionCatalog.IsKnown(permissionKey))
        {
            return new PermissionDecision(
                PermissionDecisionStatus.Denied, permissionKey ?? string.Empty, null, null, null,
                ModuleContractVersion.Current, "unknown_permission_key");
        }

        if (connection.State != System.Data.ConnectionState.Open
            || !ReferenceEquals(transaction.Connection, connection))
        {
            return new PermissionDecision(
                PermissionDecisionStatus.Unavailable, permissionKey, null, null, null,
                ModuleContractVersion.Current, "transaction_connection_mismatch");
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
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT a.account_id, s.session_id, ar.role_id, rp.permission_id, p.permission_key
                FROM identity.accounts AS a
                JOIN identity.sessions AS s ON s.account_id = a.account_id
                JOIN identity.account_roles AS ar ON ar.account_id = a.account_id
                JOIN identity.roles AS r ON r.role_id = ar.role_id
                JOIN identity.role_permissions AS rp ON rp.role_id = r.role_id
                JOIN identity.permissions AS p ON p.permission_id = rp.permission_id
                WHERE a.account_id = @account_id
                  AND s.session_id = @session_id
                  AND a.is_enabled
                  AND a.credential_version = @credential_version
                  AND s.credential_version_at_issue = @credential_version
                  AND s.revoked_at_utc IS NULL
                  AND s.lease_expires_at_utc > clock_timestamp()
                  AND p.permission_key = @permission_key
                  AND p.is_active
                ORDER BY ar.role_id, rp.permission_id
                FOR SHARE OF a, s, ar, r, rp, p;
                """;

            AddParameter(command, "@account_id", accountId);
            AddParameter(command, "@session_id", sessionId);
            AddParameter(command, "@credential_version", credentialVersion);
            AddParameter(command, "@permission_key", permissionKey);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var allowed = await reader.ReadAsync(cancellationToken);
            if (allowed
                && (reader.GetGuid(0) != accountId
                    || reader.GetGuid(1) != sessionId
                    || !string.Equals(reader.GetString(4), permissionKey, StringComparison.Ordinal)))
            {
                return new PermissionDecision(
                    PermissionDecisionStatus.Unavailable, permissionKey, accountId, sessionId,
                    credentialVersion, ModuleContractVersion.Current, "authorization_row_identity_mismatch");
            }

            return new PermissionDecision(
                allowed ? PermissionDecisionStatus.Allowed : PermissionDecisionStatus.Denied,
                permissionKey, accountId, sessionId, credentialVersion, ModuleContractVersion.Current,
                allowed
                    ? "live_permission_locked_until_transaction_end"
                    : "session_or_permission_not_active");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new PermissionDecision(
                PermissionDecisionStatus.Unavailable, permissionKey, accountId, sessionId,
                credentialVersion, ModuleContractVersion.Current, "transaction_authorization_unavailable");
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public Task<ProductOwnerAuthorityDecision> ResolveCurrentProductOwnerAuthorityAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Do not infer owner from role or client data. Unavailable until protected binding exists.
        return Task.FromResult(new ProductOwnerAuthorityDecision(
            ProductOwnerAuthorityStatus.Unavailable, null, null, null,
            ModuleContractVersion.Current, "owner_binding_not_implemented"));
    }
}
