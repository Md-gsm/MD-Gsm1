namespace MDGsm.Accounting.Domain;

public enum AccountRegistryIssueCode
{
    MissingAccountId,
    DuplicateAccountId,
    InvalidAccountCode,
    DuplicateAccountCode,
    ProhibitedAccountCode,
    IncompleteCanonicalAttributes,
    MissingParentAccount,
    ParentCycle,
    ParentLevelOrderMismatch,
    ExplicitParentRelationMismatch
}

public sealed record AccountRegistryIssue(
    AccountRegistryIssueCode Code,
    string AccountCode,
    string Description);

/// <summary>
/// Validates one proposed canonical COA snapshot. This is not a substitute for
/// database-wide uniqueness constraints or historical code-reuse checks.
/// Parent links are accepted only from explicit ParentAccountId evidence; the
/// validator never infers hierarchy from AccountCode prefixes.
/// </summary>
public static class AccountRegistryValidator
{
    private static readonly (string ChildCode, string ParentCode)[] ExplicitParentRelations =
    [
        ("110302", "1103"),
        ("610801", "6108"),
        ("620201", "6202")
    ];

    public static IReadOnlyList<AccountRegistryIssue> Validate(IEnumerable<AccountDefinition> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        var items = accounts.ToArray();
        var issues = new List<AccountRegistryIssue>();

        foreach (var account in items)
        {
            if (account.AccountId is null || account.AccountId == Guid.Empty)
                issues.Add(new(AccountRegistryIssueCode.MissingAccountId, account.AccountCode, "Canonical AccountId is missing or empty."));

            if (string.IsNullOrWhiteSpace(account.AccountCode) ||
                account.AccountCode.Any(character => !char.IsAsciiDigit(character)))
                issues.Add(new(AccountRegistryIssueCode.InvalidAccountCode, account.AccountCode, "AccountCode must contain ASCII digits only."));

            if (account.IsProhibited)
                issues.Add(new(AccountRegistryIssueCode.ProhibitedAccountCode, account.AccountCode, "110809 is prohibited and cannot be registered or used."));

            if (account.CanonicalAttributes is null || !account.CanonicalAttributes.IsCompleteFor(account))
                issues.Add(new(AccountRegistryIssueCode.IncompleteCanonicalAttributes, account.AccountCode, "Canonical field-level evidence is missing, incomplete or inconsistent."));
        }

        foreach (var group in items
                     .Where(item => item.AccountId.HasValue && item.AccountId != Guid.Empty)
                     .GroupBy(item => item.AccountId!.Value)
                     .Where(group => group.Count() > 1))
        {
            foreach (var account in group)
                issues.Add(new(AccountRegistryIssueCode.DuplicateAccountId, account.AccountCode, "AccountId must be unique within the canonical registry snapshot."));
        }

        foreach (var group in items
                     .Where(item => !string.IsNullOrWhiteSpace(item.AccountCode))
                     .GroupBy(item => item.AccountCode, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            foreach (var account in group)
                issues.Add(new(AccountRegistryIssueCode.DuplicateAccountCode, account.AccountCode, "AccountCode must be unique within the canonical registry snapshot."));
        }

        var uniqueById = items
            .Where(item => item.AccountId.HasValue && item.AccountId != Guid.Empty)
            .GroupBy(item => item.AccountId!.Value)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());

        foreach (var account in items)
        {
            var parentId = account.CanonicalAttributes?.ParentAccountId;
            if (!parentId.HasValue)
                continue;

            if (!uniqueById.TryGetValue(parentId.Value, out var parent))
            {
                issues.Add(new(AccountRegistryIssueCode.MissingParentAccount, account.AccountCode, "ParentAccountId must resolve to exactly one canonical account."));
                continue;
            }

            var parentLevel = parent.CanonicalAttributes?.Level;
            var childLevel = account.CanonicalAttributes?.Level;
            if (parentLevel.HasValue && childLevel.HasValue && parentLevel.Value >= childLevel.Value)
                issues.Add(new(AccountRegistryIssueCode.ParentLevelOrderMismatch, account.AccountCode, "A child account must have a strictly greater Level than its explicitly referenced parent."));
        }

        var parentById = uniqueById.Values
            .Where(account => account.CanonicalAttributes?.ParentAccountId is not null)
            .ToDictionary(account => account.AccountId!.Value, account => account.CanonicalAttributes!.ParentAccountId!.Value);

        foreach (var startId in parentById.Keys)
        {
            var visited = new HashSet<Guid>();
            var current = startId;
            while (parentById.TryGetValue(current, out var parentId))
            {
                if (!visited.Add(current))
                {
                    var accountCode = uniqueById.TryGetValue(current, out var cycleAccount)
                        ? cycleAccount.AccountCode
                        : current.ToString("D");
                    issues.Add(new(AccountRegistryIssueCode.ParentCycle, accountCode, "ParentAccountId relationships must be acyclic."));
                    break;
                }

                current = parentId;
            }
        }

        var uniqueByCode = items
            .Where(item => !string.IsNullOrWhiteSpace(item.AccountCode))
            .GroupBy(item => item.AccountCode, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        foreach (var (childCode, parentCode) in ExplicitParentRelations)
        {
            if (!uniqueByCode.TryGetValue(childCode, out var child))
                continue;

            if (!uniqueByCode.TryGetValue(parentCode, out var parent) ||
                parent.AccountId is null ||
                child.CanonicalAttributes?.ParentAccountId != parent.AccountId)
            {
                issues.Add(new(
                    AccountRegistryIssueCode.ExplicitParentRelationMismatch,
                    childCode,
                    $"The explicit source-declared parent relation {parentCode} -> {childCode} is not represented by ParentAccountId."));
            }
        }

        return issues
            .Distinct()
            .OrderBy(issue => issue.Code)
            .ThenBy(issue => issue.AccountCode, StringComparer.Ordinal)
            .ToArray();
    }
}
