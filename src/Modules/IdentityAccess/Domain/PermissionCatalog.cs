namespace MDGsm.IdentityAccess.Domain;

/// <summary>
/// Product-owner-approved deny-by-default permission matrix for the initial role set.
/// Role grants are exact permission-key grants only; resource scopes are evaluated separately.
/// This catalog does not implement Organization assignment checks or RowScope and does not
/// seed database grants or authorize G1.
/// </summary>
public static class PermissionCatalog
{
    private static readonly string[] AllPermissions =
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

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Matrix =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [RoleCatalog.SuperAdmin] = Set(AllPermissions),

            [RoleCatalog.Manager] = Set(
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
                "identity.accounts.read", "system.settings.read"),

            [RoleCatalog.Receptionist] = Set(
                "persons.read", "persons.create", "persons.update",
                "repairs.read", "repairs.create", "repairs.update", "repairs.deliver",
                "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print"),

            [RoleCatalog.Technician] = Set(
                "persons.read", "repairs.read", "repairs.complete", "repairs.worklog.write",
                "repairs.parts.consume", "inventory.read",
                "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print"),

            [RoleCatalog.Warehouse] = Set(
                "persons.read",
                "inventory.read", "inventory.item.create", "inventory.item.update", "inventory.receipt.create",
                "inventory.issue.create", "inventory.transfer.create", "inventory.count.execute", "inventory.export",
                "purchasing.read",
                "dashboard.read", "dashboard.configure.self",
                "reports.read", "reports.export", "reports.print"),

            [RoleCatalog.Salesperson] = Set(
                "persons.read", "persons.create", "inventory.read", "sales.read", "sales.create",
                "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print"),

            [RoleCatalog.Purchaser] = Set(
                "persons.read", "persons.create",
                "inventory.read", "inventory.item.create", "inventory.item.update",
                "inventory.receipt.create", "inventory.export",
                "purchasing.read", "purchasing.create",
                "dashboard.read", "dashboard.configure.self",
                "reports.read", "reports.export", "reports.print"),

            [RoleCatalog.Cashier] = Set(
                "persons.read", "sales.read",
                "cash.read", "cash.receipt.create", "cash.payment.create",
                "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print"),

            [RoleCatalog.Accountant] = Set(
                "persons.read", "persons.create", "persons.update", "persons.export",
                "repairs.read", "repairs.export", "inventory.read", "inventory.export",
                "sales.read", "purchasing.read", "cash.read", "cash.reconcile",
                "finance.read", "finance.voucher.create", "finance.voucher.post", "finance.voucher.reverse",
                "finance.period.close", "finance.export",
                "dashboard.read", "dashboard.configure.self",
                "reports.read", "reports.export", "reports.print"),

            [RoleCatalog.Viewer] = Set(
                "persons.read", "repairs.read", "inventory.read", "sales.read",
                "purchasing.read", "cash.read", "finance.read",
                "dashboard.read", "dashboard.configure.self", "reports.read", "reports.print")
        };

    public static IReadOnlySet<string> Catalog { get; } =
        new HashSet<string>(AllPermissions, StringComparer.Ordinal);

    public static bool IsKnown(string permission) => Catalog.Contains(permission);

    public static bool RoleHasPermission(string role, string permission) =>
        Matrix.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    public static IReadOnlySet<string> PermissionsForRole(string role) =>
        Matrix.TryGetValue(role, out var permissions)
            ? permissions
            : new HashSet<string>(StringComparer.Ordinal);

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
