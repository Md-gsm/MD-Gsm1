using MDGsm.Application.Persons;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;

namespace MDGsm.Infrastructure.IdentityAccess;

/// <summary>
/// Resolves the sensitive-data permission against the live IdentityAccess role/permission store.
/// The actor identifier is accepted only from a trusted server-side authentication boundary;
/// it must never be copied from a request body, query string, or other client-controlled field.
/// </summary>
public sealed class IdentityAccessPartySensitiveDataAuthorizer(IRolePermissionStore permissions)
    : IPartySensitiveDataAuthorizer
{
    public const string PermissionKey = "persons.sensitive.read";

    public async Task<bool> CanReadSensitivePartyDataAsync(
        string actorId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(actorId, "D", out var value) || value == Guid.Empty)
            return false;

        return await permissions.HasPermissionAsync(
            AccountId.From(value), PermissionKey, cancellationToken);
    }
}
