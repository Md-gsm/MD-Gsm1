using MDGsm.IdentityAccess.Domain;
namespace MDGsm.IdentityAccess.Application;
public interface IAccountStore { Task<Account?> FindByNormalizedUsernameAsync(string normalizedUsername,CancellationToken ct); Task<Account?> FindByIdAsync(AccountId id,CancellationToken ct); Task SaveAsync(Account account,CancellationToken ct); }
public interface IPasswordHasher { string Hash(string password); bool Verify(string hash,string password); }
public interface IAccessTokenFactory { string CreateOpaqueToken(); byte[] ComputeDigest(string token); }
public interface ISessionStore { Task<bool> HasActiveSessionAsync(AccountId id,DateTimeOffset now,CancellationToken ct); Task<int> CountDistinctActiveAccountsAsync(DateTimeOffset now,CancellationToken ct); Task CreateAsync(AccountId id,byte[] tokenDigest,long credentialVersion,Guid clientInstanceId,DateTimeOffset createdAt,DateTimeOffset leaseExpiresAt,CancellationToken ct); Task RevokeAllForAccountAsync(AccountId id,DateTimeOffset now,string reason,CancellationToken ct); }
public interface ILoginAttemptStore { Task RecordAsync(string loginDigest,string sourceDigest,string outcome,DateTimeOffset at,CancellationToken ct); Task<bool> IsSourceRateLimitedAsync(string sourceDigest,DateTimeOffset now,CancellationToken ct); }
public interface IRolePermissionStore { Task<bool> HasPermissionAsync(AccountId id,string permissionKey,CancellationToken ct); }
public interface IAuditWriter { Task AppendAsync(string eventType,AccountId? actor,AccountId? subject,string outcome,string correlationId,CancellationToken ct); }
public interface ISystemPolicyStore { Task<SessionPolicy> GetSessionPolicyAsync(CancellationToken ct); }
