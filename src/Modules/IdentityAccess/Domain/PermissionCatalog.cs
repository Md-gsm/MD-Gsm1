namespace MDGsm.IdentityAccess.Domain;

/// <summary>
/// Owner-approved deny-by-default permission matrix for the initial role set (2026-11-06).
/// Approval defines policy only: it does not seed runtime grants or authorize G1.
/// No wildcard permissions are used. Technician row scope must be enforced by the API
/// using authoritative assignment data; this catalog does not implement row scoping.
/// </summary>
public static class PermissionCatalog
{
    private static readonly string[] AllPermissions =
    [
        "persons.read", "persons.create", "persons.update", "persons.delete",
        "persons.sensitive.read",
        "repairs.read", "repairs.create", "repairs.update", "repairs.assign",
        "repairs.complete", "repairs.deliver", "repairs.worklog.write", "repairs.parts.consume",
        "inventory.read", "inventory.receipt.create", "inventory.issue.create", "inventory.count.execute",
        "sales.read", "sales.create", "sales.price.override",
        "purchasing.read", "purchasing.create",
        "cash.read", "cash.receipt.create", "cash.payment.create",
        "finance.read", "finance.voucher.create", "finance.voucher.delete", "finance.period.close",
        "identity.accounts.read", "identity.accounts.create", "identity.accounts.update",
        "identity.credentials.reset", "identity.roles.assign", "identity.permissions.manage",
        "identity.sessions.revoke", "identity.settings.manage", "identity.recovery.execute",
        "system.settings.read", "system.settings.manage"
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Matrix =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [RoleCatalog.SuperAdmin] = Set(AllPermissions),
            [RoleCatalog.Manager] = Set(
                "persons.read", "persons.create", "persons.update",
                "repairs.read", "repairs.create", "repairs.update", "repairs.assign", "repairs.complete",
                "repairs.deliver", "repairs.worklog.write", "repairs.parts.consume",
                "inventory.read", "inventory.receipt.create", "inventory.issue.create", "inventory.count.execute",
                "sales.read", "sales.create", "purchasing.read", "purchasing.create",
                "cash.read", "cash.receipt.create", "cash.payment.create",
                "finance.read", "finance.voucher.create", "identity.accounts.read", "system.settings.read"),
            [RoleCatalog.Receptionist] = Set(
                "persons.read", "persons.create", "persons.update",
                "repairs.read", "repairs.create", "repairs.update", "repairs.deliver"),
            [RoleCatalog.Technician] = Set(
                "persons.read", "repairs.read", "repairs.complete", "repairs.worklog.write",
                "repairs.parts.consume", "inventory.read"),
            [RoleCatalog.Warehouse] = Set(
                "persons.read", "inventory.read", "inventory.receipt.create",
                "inventory.issue.create", "inventory.count.execute", "purchasing.read"),
            [RoleCatalog.Salesperson] = Set(
                "persons.read", "persons.create", "inventory.read", "sales.read", "sales.create"),
            [RoleCatalog.Purchaser] = Set(
                "persons.read", "persons.create", "inventory.read", "inventory.receipt.create",
                "purchasing.read", "purchasing.create"),
            [RoleCatalog.Cashier] = Set(
                "persons.read", "sales.read", "cash.read", "cash.receipt.create", "cash.payment.create"),
            [RoleCatalog.Accountant] = Set(
                "persons.read", "persons.create", "persons.update", "repairs.read", "inventory.read",
                "sales.read", "purchasing.read", "cash.read", "finance.read",
                "finance.voucher.create", "finance.period.close"),
            [RoleCatalog.Viewer] = Set(
                "persons.read", "repairs.read", "inventory.read", "sales.read",
                "purchasing.read", "cash.read", "finance.read")
        };

    public static IReadOnlySet<string> Catalog { get; } = new HashSet<string>(AllPermissions, StringComparer.Ordinal);

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
