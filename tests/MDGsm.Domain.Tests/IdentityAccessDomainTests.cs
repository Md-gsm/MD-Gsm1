using MDGsm.IdentityAccess.Domain;
using Xunit;
namespace MDGsm.Domain.Tests;
public sealed class IdentityAccessDomainTests
{
 [Fact] public void AccountId_New_UsesUuidV7(){var id=AccountId.New();var s=id.Value.ToString("N");Assert.Equal('7',s[12]);Assert.Contains(s[16],new[]{'8','9','a','b'});}
 [Theory][InlineData("user123")][InlineData("کاربر۱۲۳")][InlineData("123456")] public void Username_AcceptsLettersAndDigits(string value)=>Assert.Equal(value.Normalize(),Username.Parse(value).DisplayValue);
 [Theory][InlineData("ab")][InlineData("abc ")][InlineData("a-b-c")][InlineData("a@bc")] public void Username_RejectsInvalid(string value)=>Assert.Throws<FormatException>(()=>Username.Parse(value));
 [Fact] public void Username_RejectsMixedScripts()=>Assert.Throws<FormatException>(()=>Username.Parse("userکاربر"));
 [Fact] public void Account_LocksAfterFiveFailures(){var now=DateTimeOffset.UtcNow;var a=new Account(AccountId.New(),Username.Parse("user123"),"hash",1);for(int i=0;i<5;i++)a.RecordFailedLogin(now.AddMinutes(i));Assert.True(a.IsLocked(now.AddMinutes(5)));Assert.False(a.IsLocked(now.AddMinutes(21)));}
 [Fact] public void PasswordChange_IncrementsCredentialVersion(){var a=new Account(AccountId.New(),Username.Parse("user123"),"old",1);a.ChangePassword("new",1);Assert.Equal(2,a.CredentialVersion);}
 [Fact] public void SessionPolicy_RequiresLeaseLongerThanHeartbeat()=>Assert.Throws<ArgumentException>(()=>new SessionPolicy(5,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30)).Validate());
 [Fact] public void RoleCatalog_HasTenRoles()=>Assert.Equal(10,RoleCatalog.All.Count);
 [Fact] public void PermissionMatrix_DoesNotGrantManagerSensitiveOperations()
 {
  Assert.True(PermissionCatalog.RoleHasPermission(RoleCatalog.Manager,"persons.update"));
  Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Manager,"persons.delete"));
  Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Manager,"identity.credentials.reset"));
  Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Manager,"system.settings.manage"));
 }
 [Fact] public void PermissionMatrix_DoesNotUseWildcard()
 {
  Assert.DoesNotContain("*",PermissionCatalog.Catalog);
  Assert.All(PermissionCatalog.Catalog, permission=>Assert.DoesNotContain("*",permission));
 }
 [Fact] public void TechnicianPermissionsAreExplicitAndLimited()
 {
  Assert.True(PermissionCatalog.RoleHasPermission(RoleCatalog.Technician,"repairs.worklog.write"));
  Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Technician,"sales.read"));
  Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Technician,"finance.read"));
 }
}
