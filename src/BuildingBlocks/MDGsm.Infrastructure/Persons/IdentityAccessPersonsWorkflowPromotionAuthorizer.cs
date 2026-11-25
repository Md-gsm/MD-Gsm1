using MDGsm.Application.Persons;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;

namespace MDGsm.Infrastructure.Persons;

/// <summary>Delegates promotion permissions to the canonical IdentityAccess permission store.</summary>
public sealed class IdentityAccessPersonsWorkflowPromotionAuthorizer(
    IRolePermissionStore permissions) : IPersonsWorkflowPromotionAuthorizer
{
    public Task<bool> HasPermissionAsync(
        Guid actorId, string permissionKey, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(permissionKey))
            return Task.FromResult(false);
        return permissions.HasPermissionAsync(AccountId.From(actorId), permissionKey, cancellationToken);
    }
}
