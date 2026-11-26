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

    [Fact]
    public void Global_search_permission_is_separate_and_only_super_admin_has_it_by_default()
    {
        const string permission = "persons.search";
        Assert.True(PermissionCatalog.IsKnown(permission));
        Assert.True(PermissionCatalog.RoleHasPermission(RoleCatalog.SuperAdmin, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Manager, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Receptionist, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Technician, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Warehouse, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Salesperson, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Purchaser, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Cashier, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Accountant, permission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Viewer, permission));
    }
}
