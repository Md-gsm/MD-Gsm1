using MDGsm.IdentityAccess.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class ApprovedDashboardReportPermissionCatalogTests
{
    private static readonly IReadOnlySet<string> ReportExportRoles = new HashSet<string>(StringComparer.Ordinal)
    {
        RoleCatalog.SuperAdmin,
        RoleCatalog.Manager,
        RoleCatalog.Warehouse,
        RoleCatalog.Purchaser,
        RoleCatalog.Accountant
    };

    [Fact]
    public void Approved_dashboard_and_report_permissions_are_catalogued()
    {
        Assert.All(
            new[]
            {
                "dashboard.read",
                "dashboard.configure.self",
                "dashboard.assign",
                "reports.read",
                "reports.export",
                "reports.print"
            },
            permission => Assert.True(PermissionCatalog.IsKnown(permission), $"Missing approved permission: {permission}"));
    }

    [Fact]
    public void Dashboard_and_report_role_grants_match_the_owner_approved_matrix()
    {
        foreach (var role in RoleCatalog.All)
        {
            Assert.True(PermissionCatalog.RoleHasPermission(role, "dashboard.read"), $"{role} must read its dashboard.");
            Assert.True(PermissionCatalog.RoleHasPermission(role, "dashboard.configure.self"), $"{role} may configure its own dashboard.");
            Assert.True(PermissionCatalog.RoleHasPermission(role, "reports.read"), $"{role} has the approved reports.read grant.");
            Assert.True(PermissionCatalog.RoleHasPermission(role, "reports.print"), $"{role} has the approved reports.print grant.");

            Assert.Equal(
                role == RoleCatalog.SuperAdmin,
                PermissionCatalog.RoleHasPermission(role, "dashboard.assign"));

            Assert.Equal(
                ReportExportRoles.Contains(role),
                PermissionCatalog.RoleHasPermission(role, "reports.export"));
        }
    }

    [Fact]
    public void Unapproved_organization_specific_reporting_keys_remain_unknown_and_denied()
    {
        const string unapprovedPermission = "reporting.branch.read";

        Assert.False(PermissionCatalog.IsKnown(unapprovedPermission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.SuperAdmin, unapprovedPermission));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Manager, unapprovedPermission));
    }
}
