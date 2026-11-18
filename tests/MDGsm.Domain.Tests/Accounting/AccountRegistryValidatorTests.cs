using MDGsm.Accounting.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.Accounting;

public sealed class AccountRegistryValidatorTests
{
    [Fact]
    public void Valid_snapshot_accepts_only_explicit_source_declared_parent_relationships()
    {
        var parentId = Guid.CreateVersion7();
        var parent = CanonicalAccount("1103", parentId, null, 1);
        var child = CanonicalAccount("110302", Guid.CreateVersion7(), parentId, 2);

        var issues = AccountRegistryValidator.Validate([parent, child]);

        Assert.Empty(issues);
    }

    [Fact]
    public void Duplicate_account_ids_and_codes_are_reported()
    {
        var duplicateId = Guid.CreateVersion7();
        var accounts = new[]
        {
            CanonicalAccount("1101", duplicateId, null, 1),
            CanonicalAccount("1102", duplicateId, null, 1),
            CanonicalAccount("1103", Guid.CreateVersion7(), null, 1),
            CanonicalAccount("1103", Guid.CreateVersion7(), null, 1)
        };

        var issues = AccountRegistryValidator.Validate(accounts);

        Assert.Contains(issues, issue => issue.Code == AccountRegistryIssueCode.DuplicateAccountId);
        Assert.Contains(issues, issue => issue.Code == AccountRegistryIssueCode.DuplicateAccountCode);
    }

    [Fact]
    public void Prohibited_110809_is_reported_even_with_complete_attributes()
    {
        var account = CanonicalAccount("110809", Guid.CreateVersion7(), null, 1);

        var issues = AccountRegistryValidator.Validate([account]);

        Assert.Contains(issues, issue => issue.Code == AccountRegistryIssueCode.ProhibitedAccountCode);
    }

    [Fact]
    public void Missing_parent_account_is_reported()
    {
        var account = CanonicalAccount("110302", Guid.CreateVersion7(), Guid.CreateVersion7(), 2);

        var issues = AccountRegistryValidator.Validate([account]);

        Assert.Contains(issues, issue => issue.Code == AccountRegistryIssueCode.MissingParentAccount);
    }

    [Fact]
    public void Parent_cycles_are_reported()
    {
        var firstId = Guid.CreateVersion7();
        var secondId = Guid.CreateVersion7();
        var first = CanonicalAccount("1101", firstId, secondId, 2);
        var second = CanonicalAccount("1102", secondId, firstId, 2);

        var issues = AccountRegistryValidator.Validate([first, second]);

        Assert.Contains(issues, issue => issue.Code == AccountRegistryIssueCode.ParentCycle);
    }

    [Fact]
    public void Child_level_must_be_greater_than_its_explicit_parent_level()
    {
        var parentId = Guid.CreateVersion7();
        var parent = CanonicalAccount("1103", parentId, null, 2);
        var child = CanonicalAccount("110302", Guid.CreateVersion7(), parentId, 2);

        var issues = AccountRegistryValidator.Validate([parent, child]);

        Assert.Contains(issues, issue =>
            issue.Code == AccountRegistryIssueCode.ParentLevelOrderMismatch &&
            issue.AccountCode == "110302");
    }

    [Fact]
    public void Parent_relationship_is_not_inferred_from_account_code_prefix()
    {
        var parent = CanonicalAccount("1103", Guid.CreateVersion7(), null, 1);
        var child = CanonicalAccount("110302", Guid.CreateVersion7(), null, 2);

        var issues = AccountRegistryValidator.Validate([parent, child]);

        Assert.Contains(issues, issue =>
            issue.Code == AccountRegistryIssueCode.ExplicitParentRelationMismatch &&
            issue.AccountCode == "110302");
    }

    [Fact]
    public void Explicit_parent_relation_must_match_the_source_declared_parent_id()
    {
        var parent = CanonicalAccount("1103", Guid.CreateVersion7(), null, 1);
        var unrelatedParent = CanonicalAccount("1102", Guid.CreateVersion7(), null, 1);
        var child = CanonicalAccount("110302", Guid.CreateVersion7(), unrelatedParent.AccountId, 2);

        var issues = AccountRegistryValidator.Validate([parent, unrelatedParent, child]);

        Assert.Contains(issues, issue =>
            issue.Code == AccountRegistryIssueCode.ExplicitParentRelationMismatch &&
            issue.AccountCode == "110302");
    }

    private static AccountDefinition CanonicalAccount(
        string code,
        Guid id,
        Guid? parentAccountId,
        int level)
    {
        var account = new AccountDefinition(
            id,
            code,
            "حساب آزمون",
            AccountNature.Asset,
            AccountNormalBalance.Debit,
            true,
            true,
            AccountStatus.Active,
            new DateOnly(2026, 1, 1),
            null,
            new HashSet<string>(StringComparer.Ordinal) { "FE-01" },
            new HashSet<string>(StringComparer.Ordinal),
            false);

        var attributes = new CanonicalAccountAttributes(
            "Test account",
            parentAccountId,
            level,
            "ASSET",
            "NONE",
            0,
            "NONE",
            "TEST_RECOGNITION",
            "NOT_REQUIRED",
            "NOT_REQUIRED",
            "TEST_MAPPING",
            "test-source",
            "test-decision",
            null,
            1,
            "test-audit",
            null);

        var evidence = CanonicalAccountAttributes.RequiredFields.Select(field =>
        {
            var value = attributes.GetExpectedValue(account, field);
            return value is null
                ? new FieldEvidence(field, EvidenceDisposition.NotApplicable, null, "test-source", "Explicitly not applicable in this controlled test fixture.")
                : new FieldEvidence(field, EvidenceDisposition.Verified, value, "test-source", null);
        }).ToArray();

        return account with { CanonicalAttributes = attributes with { Evidence = evidence } };
    }
}
