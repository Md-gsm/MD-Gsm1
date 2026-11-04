using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class IdentityAccessPersistenceModelTests
{
 [Fact]
 public void Maps_all_identity_access_persistence_tables_to_identity_schema_without_connecting()
 {
  var options=new DbContextOptionsBuilder<MdGsmDbContext>()
   .UseNpgsql("Host=127.0.0.1;Database=mdgsm_model_only;Username=model_only")
   .Options;
  using var db=new MdGsmDbContext(options);
  var entities=db.Model.GetEntityTypes().ToDictionary(x=>x.ClrType.Name);
  var expected=new[]{"IdentityAccountRow","IdentityRoleRow","IdentityPermissionRow","IdentityAccountRoleRow",
   "IdentityRolePermissionRow","IdentitySessionRow","IdentityLoginAttemptRow","IdentityRecoveryCodeRow",
   "IdentityRecoveryChallengeRow","IdentityAuditEventRow","IdentitySystemSettingsRow"};
  foreach(var name in expected)
  {
   Assert.Contains(name,entities.Keys);
   Assert.Equal("identity",entities[name].GetSchema());
   Assert.False(string.IsNullOrWhiteSpace(entities[name].GetTableName()));
  }
  Assert.Equal("accounts",entities["IdentityAccountRow"].GetTableName());
  Assert.Equal("sessions",entities["IdentitySessionRow"].GetTableName());
  Assert.Equal("system_settings",entities["IdentitySystemSettingsRow"].GetTableName());
 }

 [Fact]
 public void Session_token_digest_is_unique_and_recovery_digest_is_unique()
 {
  var options=new DbContextOptionsBuilder<MdGsmDbContext>()
   .UseNpgsql("Host=127.0.0.1;Database=mdgsm_model_only;Username=model_only").Options;
  using var db=new MdGsmDbContext(options);
  var session=db.Model.FindEntityType(typeof(IdentitySessionRow))!;
  var recovery=db.Model.FindEntityType(typeof(IdentityRecoveryCodeRow))!;
  Assert.Contains(session.GetIndexes(),x=>x.IsUnique&&x.Properties.Any(p=>p.Name==nameof(IdentitySessionRow.TokenDigest)));
  Assert.Contains(recovery.GetIndexes(),x=>x.IsUnique&&x.Properties.Any(p=>p.Name==nameof(IdentityRecoveryCodeRow.CodeDigest)));
 }

 [Fact]
 public void Account_rehydration_preserves_security_state()
 {
  var id=MDGsm.IdentityAccess.Domain.AccountId.New();
  var until=DateTimeOffset.UtcNow.AddMinutes(4);
  var account=MDGsm.IdentityAccess.Domain.Account.Rehydrate(id,
   MDGsm.IdentityAccess.Domain.Username.Parse("User123"),"hash",1,false,true,7,4,until.AddMinutes(-2),until);
  Assert.Equal(7,account.CredentialVersion);
  Assert.Equal(4,account.FailedAttemptCount);
  Assert.Equal(until,account.LockedUntil);
  Assert.False(account.IsEnabled);
  Assert.True(account.MustChangePassword);
 }
}