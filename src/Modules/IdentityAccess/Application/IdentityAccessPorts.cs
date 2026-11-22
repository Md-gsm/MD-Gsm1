using MDGsm.IdentityAccess.Domain;
namespace MDGsm.IdentityAccess.Application;
public interface IDatabaseClock { Task<DateTimeOffset> GetUtcNowAsync(CancellationToken ct); }
public interface IAccountStore { Task<Account?> FindByNormalizedUsernameAsync(string normalizedUsername,CancellationToken ct); Task<Account?> FindByIdAsync(AccountId id,CancellationToken ct); Task SaveAsync(Account account,CancellationToken ct); }
/// <summary>Executes a serializable unit of work and locks the singleton session-policy row before capacity checks.</summary>
public interface IIdentityAccessUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
public interface IPrivacyDigest { byte[] ComputeSha256(string value); }
public interface IPasswordHasher { short CurrentVersion { get; } string DummyHash { get; } string Hash(string password); bool Verify(string hash,string password); }
public interface IAccessTokenFactory { string CreateOpaqueToken(); byte[] ComputeDigest(string token); }
public interface ISessionStore { Task<bool> HasActiveSessionAsync(AccountId id,DateTimeOffset now,CancellationToken ct); Task<int> CountDistinctActiveAccountsAsync(DateTimeOffset now,CancellationToken ct); Task CreateAsync(AccountId id,byte[] tokenDigest,long credentialVersion,Guid clientInstanceId,DateTimeOffset createdAt,DateTimeOffset leaseExpiresAt,CancellationToken ct); Task RevokeAllForAccountAsync(AccountId id,DateTimeOffset now,string reason,CancellationToken ct); Task<bool> TryHeartbeatAsync(byte[] tokenDigest,long credentialVersion,DateTimeOffset now,DateTimeOffset newLeaseExpiry,CancellationToken ct); Task<bool> RevokeByDigestAsync(byte[] tokenDigest,DateTimeOffset now,string reason,CancellationToken ct); }
/// <summary>Identity returned only after the opaque token digest is matched to a live, non-revoked session and current enabled account version.</summary>
public sealed record ValidatedSessionIdentity(AccountId AccountId, long CredentialVersion, bool MustChangePassword = false, Guid SessionId = default);
public interface ISessionIdentityResolver
{
    Task<ValidatedSessionIdentity?> ResolveAsync(byte[] tokenDigest, DateTimeOffset now, CancellationToken ct);
}

/// <summary>
/// Host-supplied request principal accessor. The host implementation may return these
/// values only from claims created by IdentityAccess session middleware after PostgreSQL
/// token-digest resolution and lease renewal.
/// </summary>
public interface IValidatedRequestPrincipalAccessor
{
    Guid AccountId { get; }
    Guid SessionId { get; }
    long CredentialVersion { get; }
    DateTimeOffset AuthenticatedAtUtc { get; }
}

public interface ILoginAttemptStore { Task RecordAsync(string loginDigest,string sourceDigest,string outcome,DateTimeOffset at,CancellationToken ct); Task<bool> IsSourceRateLimitedAsync(string sourceDigest,DateTimeOffset now,CancellationToken ct); }
public interface IRolePermissionStore { Task<bool> HasPermissionAsync(AccountId id,string permissionKey,CancellationToken ct); Task<bool> HasRoleAsync(AccountId id,string roleKey,CancellationToken ct); }
public interface IAuditWriter { Task AppendAsync(string eventType,AccountId? actor,AccountId? subject,string outcome,string correlationId,CancellationToken ct); }
public interface ISystemPolicyStore { Task<SessionPolicy> GetSessionPolicyAsync(CancellationToken ct); }

public interface IRecoveryStore
{
    Task CreateChallengeAsync(AccountId accountId, byte[] challengeDigest, string channel, DateTimeOffset expiresAt, CancellationToken ct);
    Task<bool> ConsumeChallengeAsync(AccountId accountId, byte[] challengeDigest, string channel, DateTimeOffset now, CancellationToken ct);
}


/// <summary>
/// Evaluates one exact permission in the same database statement that validates the
/// current session ID, account enabled state, credential version and lease. This is a
/// request-time authorization decision; mutating consumers still need commit-time
/// revalidation/linearization in their own accepted unit-of-work contract.
/// </summary>
public interface IRequestAuthorizationStore
{
    Task<bool> HasPermissionForLiveSessionAsync(
        AccountId accountId,
        Guid sessionId,
        long credentialVersion,
        string permissionKey,
        CancellationToken cancellationToken);
}
