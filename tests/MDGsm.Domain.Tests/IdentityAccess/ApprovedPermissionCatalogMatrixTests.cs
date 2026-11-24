using System;
using System.Collections.Generic;
using System.Linq;
using MDGsm.IdentityAccess.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class ApprovedPermissionCatalogMatrixTests
{
    private static readonly string[] ApprovedCatalog =
    [
        "persons.read", "persons.create", "persons.update", "persons.archive", "persons.merge", "persons.export",
        "persons.sensitive.read",
        "repairs.read", "repairs.create", "repairs.update", "repairs.assign", "repairs.complete",
        "repairs.worklog.write", "repairs.parts.consume", "repairs.deliver", "repairs.cancel", "repairs.export",
        "inventory.read", "inventory.item.create", "inventory.item.update", "inventory.receipt.create",
        "inventory.issue.create", "inventory.transfer.create", "inventory.count.execute",
        "inventory.adjust.approve", "inventory.export",
        "sales.read", "sales.create", "sales.price.override", "sales.discount.approve", "sales.refund.approve",
        "purchasing.read", "purchasing.create", "purchasing.approve",
        "cash.read", "cash.receipt.create", "cash.payment.create", "cash.reconcile",
        "finance.read", "finance.voucher.create", "finance.voucher.post", "finance.voucher.reverse",
        "finance.period.close", "finance.period.reopen", "finance.export",
        "dashboard.read", "dashboard.configure.self", "dashboard.assign",
        "reports.read", "reports.export", "reports.print",
        "identity.accounts.read", "identity.accounts.create", "identity.accounts.update",
        "identity.credentials.reset", "identity.roles.assign", "identity.permissions.manage",
        "identity.sessions.revoke", "identity.sessions.settings.manage", "identity.recovery.execute",
        "system.settings.read", "system.settings.manage", "audit.security.read"
    ];

    [Fact]
    public void Catalog_matches_exactly_the_owner_approved_permission_names()
    {
        Assert.Equal(ApprovedCatalog.OrderBy(x => x, StringComparer.Ordinal),
            PermissionCatalog.Catalog.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void All_ten_role_grant_sets_match_the_owner_approved_matrix()
    {
        AssertRole(RoleCatalog.SuperAdmin, ApprovedCatalog);

        AssertRole(RoleCatalog.Manager,
            "persons.read", "persons.create", "persons.update", "persons.export",
            "repairs.read", "repairs.create", "repairs.update", "repairs.assign", "repairs.complete",
            "repairs.worklog.write", "repairs.parts.consume", "repairs.deliver", "repairs.cancel", "repairs.export",
            "inventory.read", "inventory.item.create", "inventory.item.update", "inventory.receipt.create",
            "inventory.issue.create", "inventory.transfer.create", "inventory.count.execute", "inventory.adjust.approve",
            "inventory.export",
            "sales.read", "sales.create", "sales.discount.approve", "sales.refund.approve",
            "purchasing.read", "purchasing.create", "purchasing.approve",
            "cash.read", "cash.receipt.create", "cash.payment.create", "cash.reconcile",
            "finance.read", "finance.voucher.post", "finance.voucher.reverse", "finance.export",
            "dashboard.read", "dashboard.configure.self",
            "reports.read", "reports.export", "reports.print",
            "identity.accounts.read", "system.settings.read");

        AssertRole(RoleCatalog.Receptionist,
            "persons.read", "persons.create", "persons.update",
            "repairs.read", "repairs.create", "repairs.update", "repairs.deliver",
            "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print");

        AssertRole(RoleCatalog.Technician,
            "persons.read", "repairs.read", "repairs.complete", "repairs.worklog.write",
            "repairs.parts.consume", "inventory.read",
            "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print");

        AssertRole(RoleCatalog.Warehouse,
            "persons.read",
            "inventory.read", "inventory.item.create", "inventory.item.update", "inventory.receipt.create",
            "inventory.issue.create", "inventory.transfer.create", "inventory.count.execute", "inventory.export",
            "purchasing.read",
            "dashboard.read", "dashboard.configure.self",
            "reports.read", "reports.export", "reports.print");

        AssertRole(RoleCatalog.Salesperson,
            "persons.read", "persons.create", "inventory.read", "sales.read", "sales.create",
            "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print");

        AssertRole(RoleCatalog.Purchaser,
            "persons.read", "persons.create",
            "inventory.read", "inventory.item.create", "inventory.item.update", "inventory.receipt.create", "inventory.export",
            "purchasing.read", "purchasing.create",
            "dashboard.read", "dashboard.configure.self",
            "reports.read", "reports.export", "reports.print");

        AssertRole(RoleCatalog.Cashier,
            "persons.read", "sales.read", "cash.read", "cash.receipt.create", "cash.payment.create",
            "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print");

        AssertRole(RoleCatalog.Accountant,
            "persons.read", "persons.create", "persons.update", "persons.export",
            "repairs.read", "repairs.export", "inventory.read", "inventory.export",
            "sales.read", "purchasing.read", "cash.read", "cash.reconcile",
            "finance.read", "finance.voucher.create", "finance.voucher.post", "finance.voucher.reverse",
            "finance.period.close", "finance.export",
            "dashboard.read", "dashboard.configure.self",
            "reports.read", "reports.export", "reports.print");

        AssertRole(RoleCatalog.Viewer,
            "persons.read", "repairs.read", "inventory.read", "sales.read",
            "purchasing.read", "cash.read", "finance.read",
            "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print");
    }

    [Fact]
    public void Sensitive_person_data_permission_remains_super_admin_only()
    {
        Assert.True(PermissionCatalog.IsKnown("persons.sensitive.read"));
        Assert.True(PermissionCatalog.RoleHasPermission(RoleCatalog.SuperAdmin, "persons.sensitive.read"));

        foreach (var role in RoleCatalog.All.Where(x => x != RoleCatalog.SuperAdmin))
        {
            Assert.False(PermissionCatalog.RoleHasPermission(role, "persons.sensitive.read"));
        }
    }

    [Fact]
    public void Unapproved_permission_names_and_unknown_roles_fail_closed()
    {
        foreach (var key in new[]
        {
            "persons.delete",
            "finance.voucher.delete",
            "reporting.branch.read",
            "reporting.warehouse.read",
            "organization.assignments.create"
        })
        {
            Assert.False(PermissionCatalog.IsKnown(key), $"Unexpected permission key: {key}");
            Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.SuperAdmin, key));
        }

        Assert.Empty(PermissionCatalog.PermissionsForRole("unknown-role"));
        Assert.False(PermissionCatalog.RoleHasPermission("unknown-role", "persons.read"));
    }

    [Fact]
    public void Public_catalog_views_are_read_only_and_cannot_be_mutated_by_consumers()
    {
        var permissionCatalog = Assert.IsAssignableFrom<ICollection<string>>(PermissionCatalog.Catalog);
        var managerPermissions = Assert.IsAssignableFrom<ICollection<string>>(
            PermissionCatalog.PermissionsForRole(RoleCatalog.Manager));
        var unknownRolePermissions = Assert.IsAssignableFrom<ICollection<string>>(
            PermissionCatalog.PermissionsForRole("unknown-role"));
        var roles = Assert.IsAssignableFrom<ICollection<string>>(RoleCatalog.All);

        Assert.True(permissionCatalog.IsReadOnly);
        Assert.True(managerPermissions.IsReadOnly);
        Assert.True(unknownRolePermissions.IsReadOnly);
        Assert.True(roles.IsReadOnly);

        Assert.Throws<NotSupportedException>(() => permissionCatalog.Add("organization.assignments.create"));
        Assert.Throws<NotSupportedException>(() => managerPermissions.Add("identity.permissions.manage"));
        Assert.Throws<NotSupportedException>(() => unknownRolePermissions.Add("persons.read"));
        Assert.Throws<NotSupportedException>(() => roles.Add("unapproved-role"));

        Assert.False(PermissionCatalog.IsKnown("organization.assignments.create"));
        Assert.False(PermissionCatalog.RoleHasPermission(RoleCatalog.Manager, "identity.permissions.manage"));
        Assert.False(PermissionCatalog.RoleHasPermission("unapproved-role", "persons.read"));
    }

    private static void AssertRole(string role, params string[] expected)
    {
        Assert.Equal(
            expected.OrderBy(x => x, StringComparer.Ordinal),
            PermissionCatalog.PermissionsForRole(role).OrderBy(x => x, StringComparer.Ordinal));
    }
}
