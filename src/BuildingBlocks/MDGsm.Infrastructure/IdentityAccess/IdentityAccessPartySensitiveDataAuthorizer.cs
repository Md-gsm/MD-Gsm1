using MDGsm.Application.Persons;
using MDGsm.IdentityAccess.Contracts;

namespace MDGsm.Infrastructure.IdentityAccess;

/// <summary>
/// Resolves sensitive-data permission through the public IdentityAccess contract.
/// The actor identifier is accepted only as a consistency check against the validated
/// server-side principal; it is never used as the authorization identity.
/// </summary>
public sealed class IdentityAccessPartySensitiveDataAuthorizer(
    IIdentityAccessAuthorizationContract authorization)
    : IPartySensitiveDataAuthorizer
{
    public const string PermissionKey = "persons.sensitive.read";

    public async Task<bool> CanReadSensitivePartyDataAsync(
        string actorId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(actorId, "D", out var requestedAccountId)
            || requestedAccountId == Guid.Empty)
            return false;

        var principal = await authorization.ResolveCurrentPrincipalAsync(cancellationToken);
        if (!principal.IsAuthenticated
            || principal.AccountId != requestedAccountId
            || principal.ContractVersion != ModuleContractVersion.Current)
            return false;

        var decision = await authorization.EvaluateCurrentPermissionAsync(
            PermissionKey, cancellationToken);

        // Bind the decision to the exact principal snapshot used for this request.
        // An Allowed result for another account/session/version is never accepted.
        return decision.IsAllowed
            && decision.ContractVersion == ModuleContractVersion.Current
            && decision.PermissionKey == PermissionKey
            && decision.PrincipalAccountId == requestedAccountId
            && decision.PrincipalSessionId == principal.SessionId
            && decision.PrincipalVersion == principal.PrincipalVersion;
    }
}
