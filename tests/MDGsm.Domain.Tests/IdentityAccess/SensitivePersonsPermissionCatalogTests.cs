using MDGsm.IdentityAccess.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class SensitivePersonsPermissionCatalogTests
{
    [Fact]
    public void Sensitive_read_is_catalogued_and_granted_only_to_super_admin_by_default()
    {
        const string permission = "persons.sensitive.read";
        Assert.True(PermissionCatalog.IsKnown(permission));
        Assert.True(PermissionCatalog.RoleHasPermission(RoleCatalog.SuperAdmin, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Manager, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Receptionist, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Technician, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Viewer, permission));
    }
}
