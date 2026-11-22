using MDGsm.IdentityAccess.Application;

namespace MDGsm.Infrastructure.IdentityAccess;

/// <summary>
/// Explicit fail-closed authorization store used when no database connection is configured.
/// It returns an error so the public contract maps the decision to Unavailable, never Allow.
/// </summary>
public sealed class UnavailableRequestAuthorizationStore : IRequestAuthorizationStore
{
    public Task<bool> HasPermissionForLiveSessionAsync(
        MDGsm.IdentityAccess.Domain.AccountId accountId,
        Guid sessionId,
        long credentialVersion,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<bool>(
            new InvalidOperationException("IdentityAccess authorization persistence is not configured."));
    }
}
