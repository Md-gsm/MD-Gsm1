using MDGsm.Accounting.Application;
using MDGsm.Accounting.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.Accounting;

[Trait("TestType", "UnitFailClosed")]
public sealed class AccountCanonicalGateTests
{
    private static readonly DateOnly TestDate = new(2026, 11, 18);

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-001")]
    public void Account_without_canonical_attribute_evidence_remains_unverified()
    {
        var account = CreateAccount("1101", Guid.CreateVersion7()) with { CanonicalAttributes = null };

        Assert.False(account.CanAutomatedPost("FE-03", TestDate, true));
        Assert.Contains(AccountRegistryValidator.Validate([account]), issue =>
            issue.Code == AccountRegistryIssueCode.IncompleteCanonicalAttributes);
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-002")]
    public void Duplicate_or_empty_account_ids_are_rejected_in_registry_snapshot()
    {
        var duplicateId = Guid.CreateVersion7();
        var issues = AccountRegistryValidator.Validate([
            CreateAccount("1101", duplicateId),
            CreateAccount("1102", duplicateId),
            CreateAccount("1103", Guid.Empty)
        ]);

        Assert.Contains(issues, issue => issue.Code == AccountRegistryIssueCode.DuplicateAccountId);
        Assert.Contains(issues, issue => issue.Code == AccountRegistryIssueCode.MissingAccountId);
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-003")]
    public void Parent_cycles_and_unapproved_parent_inference_are_rejected()
    {
        var firstId = Guid.CreateVersion7();
        var secondId = Guid.CreateVersion7();
        var cycle = AccountRegistryValidator.Validate([
            CreateAccount("7001", firstId, secondId),
            CreateAccount("7002", secondId, firstId)
        ]);
        Assert.Contains(cycle, issue => issue.Code == AccountRegistryIssueCode.ParentCycle);

        var parentId = Guid.CreateVersion7();
        var parent = CreateAccount("1103", parentId);
        var childWithExplicitParent = CreateAccount("110302", Guid.CreateVersion7(), parentId);
        Assert.DoesNotContain(AccountRegistryValidator.Validate([parent, childWithExplicitParent]),
            issue => issue.Code == AccountRegistryIssueCode.ExplicitParentRelationMismatch);

        var childWithoutParentEvidence = CreateAccount("110302", Guid.CreateVersion7());
        Assert.Contains(AccountRegistryValidator.Validate([parent, childWithoutParentEvidence]),
            issue => issue.Code == AccountRegistryIssueCode.ExplicitParentRelationMismatch);
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-004")]
    public void Unknown_nature_or_normal_balance_blocks_account_eligibility()
    {
        var account = CreateAccount("1101", Guid.CreateVersion7()) with
        {
            Nature = null,
            NormalBalance = null
        };

        Assert.False(account.CanAutomatedPost("FE-03", TestDate, true));
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-005")]
    public void Inactive_nonpostable_and_prohibited_accounts_are_blocked()
    {
        var inactive = CreateAccount("1101", Guid.CreateVersion7()) with { Status = AccountStatus.Inactive };
        var nonPostable = CreateAccount("1102", Guid.CreateVersion7()) with { Postable = false };
        var prohibited = CreateAccount("110809", Guid.CreateVersion7());

        Assert.False(inactive.CanAutomatedPost("FE-03", TestDate, true));
        Assert.False(nonPostable.CanAutomatedPost("FE-03", TestDate, true));
        Assert.False(prohibited.CanAutomatedPost("FE-03", TestDate, true));
        Assert.Contains(AccountRegistryValidator.Validate([prohibited]),
            issue => issue.Code == AccountRegistryIssueCode.ProhibitedAccountCode);
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-006")]
    public void Missing_subledger_type_is_not_treated_as_an_implicit_none()
    {
        var account = CreateAccount("110802", Guid.CreateVersion7(), subledgerType: null);

        Assert.False(account.CanAutomatedPost("FE-07", TestDate, true));
        Assert.Contains(AccountRegistryValidator.Validate([account]),
            issue => issue.Code == AccountRegistryIssueCode.IncompleteCanonicalAttributes);
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-007")]
    public void Missing_or_invalid_effective_start_date_blocks_account()
    {
        var missing = CreateAccount("1101", Guid.CreateVersion7(), effectiveFrom: null);
        var expired = CreateAccount("1102", Guid.CreateVersion7(), effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2026, 1, 31));

        Assert.False(missing.CanAutomatedPost("FE-03", TestDate, true));
        Assert.False(expired.CanAutomatedPost("FE-03", TestDate, true));
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-008")]
    public void Account_2114_remains_blocked_until_canonical_attributes_and_FE20_are_complete()
    {
        var serviceAccount = CreateAccount("2114", Guid.CreateVersion7()) with { CanonicalAttributes = null };

        Assert.False(serviceAccount.CanAutomatedPost("FE-20", TestDate, true));
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-009")]
    public void Duplicate_account_codes_are_rejected_even_when_account_ids_differ()
    {
        var issues = AccountRegistryValidator.Validate([
            CreateAccount("4107", Guid.CreateVersion7()),
            CreateAccount("4107", Guid.CreateVersion7()),
            CreateAccount("5106", Guid.CreateVersion7())
        ]);

        Assert.Contains(issues, issue => issue.Code == AccountRegistryIssueCode.DuplicateAccountCode);
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-010")]
    public void Evidence_conflicting_with_owner_decision_reference_blocks_account()
    {
        var account = CreateAccount("2101", Guid.CreateVersion7());
        var conflicting = account with
        {
            CanonicalAttributes = account.CanonicalAttributes! with { DecisionReference = "UNAPPROVED-DECISION" }
        };

        Assert.False(conflicting.CanAutomatedPost("FE-06", TestDate, true));
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-011")]
    public void Unresolved_equity_accounts_3101_through_3105_remain_blocked()
    {
        foreach (var code in new[] { "3101", "3102", "3103", "3104", "3105" })
        {
            var account = CreateAccount(code, Guid.CreateVersion7()) with { NormalBalance = null };
            Assert.False(account.CanAutomatedPost("FE-34", TestDate, true));
        }
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-012")]
    public void Posting_rule_without_full_source_and_test_crosswalk_is_not_executable()
    {
        var rule = new PostingRuleDefinition(
            "FE-03", 1, PostingRuleStatus.Accepted, "1101", "4101",
            "SOURCE_AMOUNT", "SOURCE_DOCUMENT_FINALIZED", "test-source", "test-decision",
            new DateOnly(2026, 1, 1), null, true, true, false);

        Assert.False(rule.IsExecutableOn(TestDate));
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-013")]
    public void Discount_accounts_without_explicit_FE_eligibility_are_blocked()
    {
        foreach (var code in new[] { "4201", "4202", "4203" })
        {
            var account = CreateAccount(code, Guid.CreateVersion7(), allowedEvents: []);
            Assert.False(account.CanAutomatedPost("FE-13", TestDate, true));
        }
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-014")]
    public void Trade_in_inventory_account_requires_ownership_and_recognition_evidence()
    {
        var account = CreateAccount("110803", Guid.CreateVersion7(), recognitionEvidenceRequired: true);

        Assert.False(account.CanAutomatedPost("FE-21", TestDate, false));
        Assert.True(account.CanAutomatedPost("FE-21", TestDate, true));
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-015")]
    public void Subledger_account_requires_explicit_subledger_contract_value()
    {
        var account = CreateAccount("110802", Guid.CreateVersion7(), subledgerType: null);

        Assert.False(account.CanAutomatedPost("FE-07", TestDate, true));
    }

    [Fact]
    [Trait("TestId", "MDG-COA-GATE-016")]
    public async Task PSP_or_cash_transfer_without_accepted_FE18_rule_is_blocked()
    {
        var service = new PostingPreflightService(
            new RuleCatalog(null),
            new AccountCatalog([]),
            new PeriodCatalog(new(Guid.CreateVersion7(), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), FiscalPeriodStatus.Open)));

        var result = await service.EvaluateAsync(new("FE-18", TestDate, true, null));

        Assert.False(result.IsEligible);
        Assert.Contains(PostingBlockReason.PostingRuleNotFound, result.BlockReasons);
    }

    private static AccountDefinition CreateAccount(
        string code,
        Guid id,
        Guid? parentId = null,
        AccountNature? nature = AccountNature.Asset,
        AccountNormalBalance? normalBalance = AccountNormalBalance.Debit,
        bool? postable = true,
        bool? directPostingAllowed = true,
        AccountStatus status = AccountStatus.Active,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null,
        string? subledgerType = "NONE",
        IReadOnlySet<string>? allowedEvents = null,
        bool recognitionEvidenceRequired = false)
    {
        var allowed = allowedEvents ?? new HashSet<string>(StringComparer.Ordinal) { "FE-03" };
        var account = new AccountDefinition(
            id, code, "حساب آزمون", nature, normalBalance, postable, directPostingAllowed, status,
            effectiveFrom ?? new DateOnly(2026, 1, 1), effectiveTo,
            allowed,
            new HashSet<string>(StringComparer.Ordinal) { "FE-36" },
            recognitionEvidenceRequired);

        var attributes = new CanonicalAccountAttributes(
            "Test account", parentId, 1, "ASSET", subledgerType, 0, "NONE",
            "SOURCE_DOCUMENT_FINALIZED", "NOT_REQUIRED", "NOT_REQUIRED", "TEST_MAPPING",
            "test-source", "test-decision", "test-cr", 1, "test-audit", null);

        var evidence = CanonicalAccountAttributes.RequiredFields.Select(field =>
        {
            var value = attributes.GetExpectedValue(account, field);
            return value is null
                ? new FieldEvidence(field, EvidenceDisposition.NotApplicable, null, "test-source", "Explicitly not applicable in this controlled test fixture.")
                : new FieldEvidence(field, EvidenceDisposition.Verified, value, "test-source", null);
        }).ToArray();

        return account with { CanonicalAttributes = attributes with { Evidence = evidence } };
    }

    private sealed class RuleCatalog(PostingRuleDefinition? rule) : IPostingRuleCatalog
    {
        public ValueTask<PostingRuleDefinition?> FindAsync(string financialEventId, DateOnly effectiveDate, CancellationToken cancellationToken) =>
            ValueTask.FromResult(rule);
    }

    private sealed class AccountCatalog(IReadOnlyList<AccountDefinition> items) : IAccountCatalog
    {
        public ValueTask<AccountDefinition?> FindByCodeAsync(string accountCode, CancellationToken cancellationToken) =>
            ValueTask.FromResult(items.SingleOrDefault(item => item.AccountCode == accountCode));
    }

    private sealed class PeriodCatalog(FiscalPeriod period) : IFiscalPeriodCatalog
    {
        public ValueTask<FiscalPeriod?> FindForDateAsync(DateOnly effectiveDate, CancellationToken cancellationToken) =>
            ValueTask.FromResult<FiscalPeriod?>(period);
    }
}
