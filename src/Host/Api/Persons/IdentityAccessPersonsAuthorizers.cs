using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Persons.Application;

namespace MDGsm.Api.Persons;

/// <summary>Host integration adapter that resolves sensitive-data permission through the canonical IdentityAccess store.</summary>
public sealed class IdentityAccessPartySensitiveDataAuthorizer(IRolePermissionStore permissions)
    : IPartySensitiveDataAuthorizer
{
    public const string PermissionKey = "persons.sensitive.read";

    public async Task<bool> CanReadSensitivePartyDataAsync(string actorId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(actorId, "D", out var value) || value == Guid.Empty)
            return false;
        return await permissions.HasPermissionAsync(AccountId.From(value), PermissionKey, cancellationToken);
    }
}

/// <summary>Host integration adapter that delegates Promotion permissions to IdentityAccess.</summary>
public sealed class IdentityAccessPersonsWorkflowPromotionAuthorizer(IRolePermissionStore permissions)
    : IPersonsWorkflowPromotionAuthorizer
{
    public Task<bool> HasPermissionAsync(Guid actorId, string permissionKey, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(permissionKey))
            return Task.FromResult(false);
        return permissions.HasPermissionAsync(AccountId.From(actorId), permissionKey, cancellationToken);
    }
}
