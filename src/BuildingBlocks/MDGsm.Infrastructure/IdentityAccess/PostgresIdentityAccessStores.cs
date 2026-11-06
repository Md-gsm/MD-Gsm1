using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace MDGsm.Infrastructure.IdentityAccess;
public sealed class PostgresIdentityAccessStores(MdGsmDbContext db, TimeProvider clock) :
 IAccountStore, ISessionStore, ILoginAttemptStore, IRolePermissionStore, IAuditWriter, ISystemPolicyStore, IRecoveryStore
{
 public async Task<Account?> FindByNormalizedUsernameAsync(string normalizedUsername,CancellationToken ct)
 {
  await db.Database.ExecuteSqlInterpolatedAsync($"SELECT account_id FROM identity.accounts WHERE login_name_normalized = {normalizedUsername} FOR UPDATE",ct);
  var row=await db.IdentityAccounts.SingleOrDefaultAsync(x=>x.LoginNameNormalized==normalizedUsername,ct);
  return row is null?null:Map(row);
 }
 public async Task<Account?> FindByIdAsync(AccountId id,CancellationToken ct)
 {
  await db.Database.ExecuteSqlInterpolatedAsync($"SELECT account_id FROM identity.accounts WHERE account_id = {id.Value} FOR UPDATE",ct);
  var row=await db.IdentityAccounts.SingleOrDefaultAsync(x=>x.AccountId==id.Value,ct);
  return row is null?null:Map(row);
 }
 public async Task SaveAsync(Account account,CancellationToken ct)
 {
  ArgumentNullException.ThrowIfNull(account);
  var row=await db.IdentityAccounts.SingleOrDefaultAsync(x=>x.AccountId==account.Id.Value,ct);
  if(row is null){row=new(){AccountId=account.Id.Value,CreatedAtUtc=clock.GetUtcNow()};db.IdentityAccounts.Add(row);}
  row.LoginName=account.Username.DisplayValue;row.LoginNameNormalized=account.Username.NormalizedValue;
  row.PasswordHash=account.PasswordHash;row.PasswordHashVersion=account.PasswordHashVersion;
  row.MustChangePassword=account.MustChangePassword;row.IsEnabled=account.IsEnabled;row.CredentialVersion=account.CredentialVersion;
  row.FailedAttemptCount=account.FailedAttemptCount;row.FailureWindowStartedAt=account.FailureWindowStartedAt;row.LockedUntil=account.LockedUntil;
  row.UpdatedAtUtc=clock.GetUtcNow();await db.SaveChangesAsync(ct);
 }
 public Task<bool> HasActiveSessionAsync(AccountId id,DateTimeOffset now,CancellationToken ct)=>
  db.IdentitySessions.AnyAsync(x=>x.AccountId==id.Value&&x.RevokedAtUtc==null&&x.LeaseExpiresAtUtc>now,ct);
 public Task<int> CountDistinctActiveAccountsAsync(DateTimeOffset now,CancellationToken ct)=>
  db.IdentitySessions.Where(x=>x.RevokedAtUtc==null&&x.LeaseExpiresAtUtc>now).Select(x=>x.AccountId).Distinct().CountAsync(ct);
 public async Task CreateAsync(AccountId id,byte[] tokenDigest,long credentialVersion,Guid clientInstanceId,DateTimeOffset createdAt,DateTimeOffset leaseExpiresAt,CancellationToken ct)
 {
  RequireDigest(tokenDigest,nameof(tokenDigest));
  db.IdentitySessions.Add(new(){SessionId=Guid.CreateVersion7(),AccountId=id.Value,TokenDigest=tokenDigest.ToArray(),CreatedAtUtc=createdAt,LastSeenAtUtc=createdAt,LeaseExpiresAtUtc=leaseExpiresAt,CredentialVersionAtIssue=credentialVersion,ClientInstanceId=clientInstanceId});
  await db.SaveChangesAsync(ct);
 }
 public async Task RevokeAllForAccountAsync(AccountId id,DateTimeOffset now,string reason,CancellationToken ct)
 { await db.IdentitySessions.Where(x=>x.AccountId==id.Value&&x.RevokedAtUtc==null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAtUtc,now).SetProperty(x=>x.RevokeReason,reason),ct); }
 public async Task<bool> TryHeartbeatAsync(byte[] tokenDigest,long credentialVersion,DateTimeOffset now,DateTimeOffset newLeaseExpiry,CancellationToken ct)
 {
  if(tokenDigest is not {Length:32})return false;
  var count=await db.IdentitySessions.Where(x=>x.TokenDigest==tokenDigest&&x.RevokedAtUtc==null&&x.LeaseExpiresAtUtc>now&&x.CredentialVersionAtIssue==credentialVersion&&db.IdentityAccounts.Any(a=>a.AccountId==x.AccountId&&a.IsEnabled&&a.CredentialVersion==credentialVersion))
   .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LastSeenAtUtc,now).SetProperty(x=>x.LeaseExpiresAtUtc,newLeaseExpiry),ct);
  return count==1;
 }
 public async Task<bool> RevokeByDigestAsync(byte[] tokenDigest,DateTimeOffset now,string reason,CancellationToken ct)
 {
  if(tokenDigest is not {Length:32})return false;
  return await db.IdentitySessions.Where(x=>x.TokenDigest==tokenDigest&&x.RevokedAtUtc==null)
   .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAtUtc,now).SetProperty(x=>x.RevokeReason,reason),ct)==1;
 }
 public async Task RecordAsync(string loginDigest,string sourceDigest,string outcome,DateTimeOffset at,CancellationToken ct)
 {
  var login=Convert.FromHexString(loginDigest);var source=Convert.FromHexString(sourceDigest);RequireDigest(login,nameof(loginDigest));RequireDigest(source,nameof(sourceDigest));
  var persistedOutcome=outcome switch{"success"=>"success","capacity_rejected" or "source_rate_limited" or "throttled"=>"throttled","locked"=>"locked",_=>"failure"};
  db.IdentityLoginAttempts.Add(new(){AttemptId=Guid.CreateVersion7(),LoginNameDigest=login,SourceKeyDigest=source,Outcome=persistedOutcome,OccurredAtUtc=at});
  await db.SaveChangesAsync(ct);
 }
 public async Task<bool> IsSourceRateLimitedAsync(string sourceDigest,DateTimeOffset now,CancellationToken ct)
 {
  var digest=Convert.FromHexString(sourceDigest);RequireDigest(digest,nameof(sourceDigest));
  return await db.IdentityLoginAttempts.CountAsync(x=>x.SourceKeyDigest==digest&&x.Outcome!="success"&&x.OccurredAtUtc>now.AddMinutes(-5),ct)>=20;
 }
 public Task<bool> HasPermissionAsync(AccountId id,string permissionKey,CancellationToken ct)=>
  (from account in db.IdentityAccounts
   join ar in db.IdentityAccountRoles on account.AccountId equals ar.AccountId
   join rp in db.IdentityRolePermissions on ar.RoleId equals rp.RoleId
   join p in db.IdentityPermissions on rp.PermissionId equals p.PermissionId
   where account.AccountId==id.Value && account.IsEnabled && p.PermissionKey==permissionKey && p.IsActive
   select p.PermissionId).AnyAsync(ct);
 public Task<bool> HasRoleAsync(AccountId id,string roleKey,CancellationToken ct)=>
  (from ar in db.IdentityAccountRoles join role in db.IdentityRoles on ar.RoleId equals role.RoleId where ar.AccountId==id.Value&&role.RoleKey==roleKey select role.RoleId).AnyAsync(ct);
 public async Task AppendAsync(string eventType,AccountId? actor,AccountId? subject,string outcome,string correlationId,CancellationToken ct)
 {
  db.IdentityAuditEvents.Add(new(){EventId=Guid.CreateVersion7(),OccurredAtUtc=clock.GetUtcNow(),ActorAccountId=actor?.Value,SubjectAccountId=subject?.Value,EventType=eventType,Outcome=outcome,CorrelationId=correlationId});
  await db.SaveChangesAsync(ct);
 }
 public async Task<SessionPolicy> GetSessionPolicyAsync(CancellationToken ct)
 {
  var row=await db.IdentitySystemSettings.AsNoTracking().SingleAsync(x=>x.SettingsId==1,ct);
  return new SessionPolicy(row.MaxActiveUsers,TimeSpan.FromSeconds(row.HeartbeatIntervalSeconds),TimeSpan.FromSeconds(row.SessionLeaseSeconds)).Validate();
 }
 public async Task CreateChallengeAsync(AccountId accountId,byte[] challengeDigest,string channel,DateTimeOffset expiresAt,CancellationToken ct)
 {
  RequireDigest(challengeDigest,nameof(challengeDigest));if(channel is not ("email" or "mobile"))throw new ArgumentOutOfRangeException(nameof(channel));
  var now=clock.GetUtcNow();
  await db.IdentityRecoveryChallenges.Where(x=>x.AccountId==accountId.Value&&x.Channel==channel&&x.ConsumedAtUtc==null&&x.RevokedAtUtc==null)
   .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAtUtc,now),ct);
  db.IdentityRecoveryChallenges.Add(new(){ChallengeId=Guid.CreateVersion7(),AccountId=accountId.Value,ChallengeDigest=challengeDigest.ToArray(),Channel=channel,CreatedAtUtc=now,ExpiresAtUtc=expiresAt});
  await db.SaveChangesAsync(ct);
 }
 public async Task<bool> ConsumeChallengeAsync(AccountId accountId,byte[] challengeDigest,string channel,DateTimeOffset now,CancellationToken ct)
 {
  if(challengeDigest is not {Length:32}||channel is not ("email" or "mobile"))return false;
  return await db.IdentityRecoveryChallenges.Where(x=>x.AccountId==accountId.Value&&x.ChallengeDigest==challengeDigest&&x.Channel==channel&&x.ConsumedAtUtc==null&&x.RevokedAtUtc==null&&x.ExpiresAtUtc>now)
   .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ConsumedAtUtc,now),ct)==1;
 }
 public async Task<bool> ConsumeRecoveryCodeAsync(AccountId accountId,byte[] codeDigest,DateTimeOffset now,CancellationToken ct)
 {
  if(codeDigest is not {Length:32})return false;
  return await db.IdentityRecoveryCodes.Where(x=>x.AccountId==accountId.Value&&x.CodeDigest==codeDigest&&x.UsedAtUtc==null&&x.RevokedAtUtc==null)
   .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.UsedAtUtc,now),ct)==1;
 }
 private static Account Map(IdentityAccountRow row)=>Account.Rehydrate(AccountId.From(row.AccountId),Username.Parse(row.LoginName),row.PasswordHash,row.PasswordHashVersion,row.IsEnabled,row.MustChangePassword,row.CredentialVersion,row.FailedAttemptCount,row.FailureWindowStartedAt,row.LockedUntil);
 private static void RequireDigest(byte[]? digest,string name){if(digest is not {Length:32})throw new ArgumentException("A SHA-256 digest of exactly 32 bytes is required.",name);}
}