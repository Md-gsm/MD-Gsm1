using System.Numerics;
using MDGsm.Accounting.Application;
using MDGsm.Accounting.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.Accounting;

[Trait("TestType", "UnitSubset")]
public sealed class AccountingDomainTests
{
    private static readonly DateOnly EffectiveDate = new(2026, 11, 18);
    private static readonly DateTimeOffset UtcNow = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("1250", "100", 13)]
    [InlineData("-1250", "100", -13)]
    [InlineData("1249", "100", 12)]
    [InlineData("-1249", "100", -12)]
    public void Irr_rounding_uses_away_from_zero(string numerator, string denominator, long expected)
    {
        var actual = IrrAmount.RoundAwayFromZero(BigInteger.Parse(numerator), BigInteger.Parse(denominator));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Irr_rounding_handles_int64_minimum_and_rejects_overflow()
    {
        Assert.Equal(long.MinValue, IrrAmount.RoundAwayFromZero(new BigInteger(long.MinValue), BigInteger.One));
        Assert.Throws<OverflowException>(() =>
            IrrAmount.RoundAwayFromZero(BigInteger.Parse("9223372036854775808"), BigInteger.One));
    }

    [Fact]
    public void Largest_remainder_allocation_is_conservative_and_order_independent()
    {
        var first = IrrAmount.AllocateLargestRemainder(new(100), [new("C", 1), new("B", 1), new("A", 1)]);
        var second = IrrAmount.AllocateLargestRemainder(new(100), [new("A", 1), new("B", 1), new("C", 1)]);

        Assert.Equal(first, second);
        Assert.Equal(new[] { "A", "B", "C" }, first.Select(item => item.LineId));
        Assert.Equal(new long[] { 34, 33, 33 }, first.Select(item => item.Amount.Value));
        Assert.Equal(100, first.Sum(item => item.Amount.Value));

        var negative = IrrAmount.AllocateLargestRemainder(new(-100), [new("A", 1), new("B", 1), new("C", 1)]);
        Assert.Equal(new long[] { -34, -33, -33 }, negative.Select(item => item.Amount.Value));
        Assert.Equal(-100, negative.Sum(item => item.Amount.Value));
    }

    [Fact]
    public void Largest_remainder_rejects_duplicate_ids_negative_weights_and_zero_total_weight()
    {
        Assert.Throws<ArgumentException>(() => IrrAmount.AllocateLargestRemainder(new(1), [new("A", 1), new("A", 2)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => IrrAmount.AllocateLargestRemainder(new(1), [new("A", -1)]));
        Assert.Throws<ArgumentException>(() => IrrAmount.AllocateLargestRemainder(new(1), [new("A", 0)]));
    }

    [Fact]
    [Trait("TestId", "MDG-FIN-COA-001")]
    public void Unverified_or_prohibited_accounts_fail_closed()
    {
        var unverified = new AccountDefinition(
            null, "1101", "صندوق", null, null, null, null, AccountStatus.Active, null, null,
            new HashSet<string>(StringComparer.Ordinal) { "FE-03" },
            new HashSet<string>(StringComparer.Ordinal), true);
        var prohibited = EligibleAccount("110809", Guid.CreateVersion7(), "FE-03");

        Assert.False(unverified.CanAutomatedPost("FE-03", EffectiveDate, true));
        Assert.False(prohibited.CanAutomatedPost("FE-03", EffectiveDate, true));
    }

    [Fact]
    [Trait("TestId", "MDG-FIN-COA-001")]
    public void Direct_posting_permission_is_separate_from_automated_posting_eligibility()
    {
        var account = EligibleAccount("1101", Guid.CreateVersion7(), "FE-03", directPostingAllowed: false);
        Assert.True(account.CanAutomatedPost("FE-03", EffectiveDate, true));
        Assert.False(account.CanManualPost("FE-03", EffectiveDate, true));
    }

    [Fact]
    [Trait("TestId", "MDG-FIN-COA-001")]
    public void Recognition_evidence_and_event_allowlist_are_enforced()
    {
        var account = EligibleAccount("1101", Guid.CreateVersion7(), "FE-03") with { RecognitionEvidenceRequired = true };
        Assert.False(account.CanAutomatedPost("FE-03", EffectiveDate, false));
        Assert.False(account.CanAutomatedPost("FE-04", EffectiveDate, true));
        Assert.True(account.CanAutomatedPost("FE-03", EffectiveDate, true));
    }

    [Fact]
    public void Journal_requires_balanced_positive_lines_and_an_open_period_to_finalize()
    {
        var journal = CreateBalancedDraft();
        var openPeriod = OpenPeriod(journal.FiscalPeriodId);
        var finalized = journal.Finalize(openPeriod, "actor-1", UtcNow, "audit-finalize");

        Assert.Equal(JournalStatus.Draft, journal.Status);
        Assert.Equal(JournalStatus.Finalized, finalized.Status);
        Assert.Equal(new IrrAmount(100), finalized.TotalDebits);
        Assert.Equal(new IrrAmount(100), finalized.TotalCredits);
        Assert.Equal("audit-create", finalized.CreatedAuditEventId);
        Assert.Equal("audit-finalize", finalized.FinalizedAuditEventId);

        var closed = openPeriod with { Status = FiscalPeriodStatus.Closed };
        Assert.Throws<InvalidOperationException>(() => journal.Finalize(closed, "actor-1", UtcNow, "audit-finalize"));
    }

    [Fact]
    public void Journal_rejects_unbalanced_lines_and_zero_or_two_sided_lines()
    {
        var debit = JournalLine.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "1101", new(100), IrrAmount.Zero);
        var credit = JournalLine.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "4101", IrrAmount.Zero, new(99));

        Assert.Throws<InvalidOperationException>(() => Journal.CreateDraft(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), EffectiveDate, Identity("SALE"),
            [debit, credit], "SALE-1", "actor-1", UtcNow, "audit-create"));
        Assert.Throws<ArgumentException>(() => JournalLine.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "1101", IrrAmount.Zero, IrrAmount.Zero));
        Assert.Throws<ArgumentException>(() => JournalLine.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "1101", new(1), new(1)));
    }

    [Fact]
    [Trait("TestId", "MDG-FIN-REV-001")]
    public void Reversal_is_a_new_journal_and_does_not_mutate_the_finalized_original()
    {
        var originalDraft = CreateBalancedDraft();
        var original = originalDraft.Finalize(OpenPeriod(originalDraft.FiscalPeriodId), "actor-1", UtcNow, "audit-finalize");
        var reversal = original.CreateReversal(
            Guid.CreateVersion7(), Guid.CreateVersion7(), original.CoaSnapshotId, EffectiveDate, Identity("REVERSAL"), "actor-2", UtcNow, "audit-reversal-create");

        Assert.Equal(JournalStatus.Finalized, original.Status);
        Assert.Equal(JournalStatus.Draft, reversal.Status);
        Assert.Equal(original.JournalId, reversal.ReversalOfJournalId);
        Assert.Equal(original.Lines[0].Debit, reversal.Lines[0].Credit);
        Assert.Equal(original.Lines[0].Credit, reversal.Lines[0].Debit);
        Assert.NotEqual(original.JournalId, reversal.JournalId);
        Assert.Equal("audit-reversal-create", reversal.CreatedAuditEventId);
    }

    [Fact]
    [Trait("TestId", "MDG-FIN-PERIOD-001")]
    public void Fiscal_period_close_requires_all_reconciliations_and_reopen_requires_independent_approval()
    {
        var period = OpenPeriod();
        var incomplete = new PeriodCloseEvidence(true, true, true, false, true, true, true, true);
        Assert.Throws<InvalidOperationException>(() => period.Close(incomplete, "actor-1", UtcNow, "audit-close"));

        var complete = new PeriodCloseEvidence(true, true, true, true, true, true, true, true);
        var closed = period.Close(complete, "actor-1", UtcNow, "audit-close");
        Assert.Equal(FiscalPeriodStatus.Closed, closed.Status);
        Assert.Throws<InvalidOperationException>(() =>
            closed.Reopen(new("actor-1", "actor-1", "reason", "audit-reopen"), UtcNow));

        var reopened = closed.Reopen(new("actor-1", "actor-2", "مغایرت مستند", "audit-reopen"), UtcNow);
        Assert.Equal(FiscalPeriodStatus.Open, reopened.Status);
        Assert.Equal("audit-reopen", reopened.LastAuditEventId);
    }

    [Fact]
    [Trait("TestId", "MDG-FIN-IDEM-001")]
    [Trait("TestId", "MDG-FIN-IDEM-002")]
    public void Posting_identity_is_stable_and_distinguishes_source_version_and_allocation()
    {
        var first = Identity("SALE");
        var replay = Identity("SALE");
        var newVersion = PostingIdentity.Create("Sales", "sale-1", "delivered", "SALE", "2");
        var allocation = PostingIdentity.Create("Sales", "sale-1", "delivered", "SALE", "1", allocationId: "alloc-2");

        Assert.Equal(first.IdempotencyKey, replay.IdempotencyKey);
        Assert.NotEqual(first.IdempotencyKey, newVersion.IdempotencyKey);
        Assert.NotEqual(first.IdempotencyKey, allocation.IdempotencyKey);
    }

    [Fact]
    public async Task Posting_preflight_fails_closed_when_no_accepted_rule_exists()
    {
        var service = new PostingPreflightService(new RuleCatalog(null), new AccountCatalog([]), new PeriodCatalog(OpenPeriod()));
        var result = await service.EvaluateAsync(new("FE-03", EffectiveDate, true, null));

        Assert.False(result.IsEligible);
        Assert.Contains(PostingBlockReason.PostingRuleNotFound, result.BlockReasons);
    }

    public static IEnumerable<object[]> FinancialEventCases { get; } =
    [
        new object[] { "MDG-FIN-GATE-001", "FE-01" },
        new object[] { "MDG-FIN-GATE-002", "FE-02" },
        new object[] { "MDG-FIN-GATE-003", "FE-03" },
        new object[] { "MDG-FIN-GATE-004", "FE-04" },
        new object[] { "MDG-FIN-GATE-005", "FE-05" },
        new object[] { "MDG-FIN-GATE-006", "FE-06" },
        new object[] { "MDG-FIN-GATE-007", "FE-07" },
        new object[] { "MDG-FIN-GATE-008", "FE-08" },
        new object[] { "MDG-FIN-GATE-009", "FE-09" },
        new object[] { "MDG-FIN-GATE-010", "FE-10" },
        new object[] { "MDG-FIN-GATE-011", "FE-10b" },
        new object[] { "MDG-FIN-GATE-012", "FE-11" },
        new object[] { "MDG-FIN-GATE-013", "FE-12" },
        new object[] { "MDG-FIN-GATE-014", "FE-13" },
        new object[] { "MDG-FIN-GATE-015", "FE-14" },
        new object[] { "MDG-FIN-GATE-016", "FE-15" },
        new object[] { "MDG-FIN-GATE-017", "FE-16" },
        new object[] { "MDG-FIN-GATE-018", "FE-17" },
        new object[] { "MDG-FIN-GATE-019", "FE-18" },
        new object[] { "MDG-FIN-GATE-020", "FE-19" },
        new object[] { "MDG-FIN-GATE-021", "FE-20" },
        new object[] { "MDG-FIN-GATE-022", "FE-21" },
        new object[] { "MDG-FIN-GATE-023", "FE-22" },
        new object[] { "MDG-FIN-GATE-024", "FE-23" },
        new object[] { "MDG-FIN-GATE-025", "FE-24" },
        new object[] { "MDG-FIN-GATE-026", "FE-25" },
        new object[] { "MDG-FIN-GATE-027", "FE-26" },
        new object[] { "MDG-FIN-GATE-028", "FE-27" },
        new object[] { "MDG-FIN-GATE-029", "FE-28" },
        new object[] { "MDG-FIN-GATE-030", "FE-29" },
        new object[] { "MDG-FIN-GATE-031", "FE-30" },
        new object[] { "MDG-FIN-GATE-032", "FE-31" },
        new object[] { "MDG-FIN-GATE-033", "FE-32" },
        new object[] { "MDG-FIN-GATE-034", "FE-33" },
        new object[] { "MDG-FIN-GATE-035", "FE-34" },
        new object[] { "MDG-FIN-GATE-036", "FE-35" },
        new object[] { "MDG-FIN-GATE-037", "FE-36" },
        new object[] { "MDG-FIN-GATE-038", "FE-37" },
        new object[] { "MDG-FIN-GATE-039", "FE-38" }
    ];

    [Theory]
    [MemberData(nameof(FinancialEventCases))]
    public async Task Every_financial_event_without_an_accepted_rule_is_blocked(string testId, string financialEventId)
    {
        var service = new PostingPreflightService(
            new RuleCatalog(null),
            new AccountCatalog([]),
            new PeriodCatalog(OpenPeriod()));

        var result = await service.EvaluateAsync(
            new(financialEventId, EffectiveDate, true, null));

        Assert.False(result.IsEligible, $"{testId} ({financialEventId}) must fail closed without an accepted rule.");
        Assert.Contains(PostingBlockReason.PostingRuleNotFound, result.BlockReasons);
    }

    [Fact]
    public async Task Posting_preflight_requires_complete_rule_accounts_and_open_period()
    {
        var rule = AcceptedRule("FE-03");
        var accounts = new AccountCatalog([
            EligibleAccount("1101", Guid.CreateVersion7(), "FE-03"),
            EligibleAccount("4101", Guid.CreateVersion7(), "FE-03", AccountNature.Revenue, AccountNormalBalance.Credit)
        ]);
        var service = new PostingPreflightService(new RuleCatalog(rule), accounts, new PeriodCatalog(OpenPeriod()));
        var result = await service.EvaluateAsync(new("FE-03", EffectiveDate, true, null));

        Assert.True(result.IsEligible);
        Assert.Empty(result.BlockReasons);
    }

    [Fact]
    public async Task Posting_preflight_honors_cancellation_before_catalog_reads()
    {
        var service = new PostingPreflightService(
            new RuleCatalog(null),
            new AccountCatalog([]),
            new PeriodCatalog(OpenPeriod()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.EvaluateAsync(new("FE-03", EffectiveDate, true, null), cancellation.Token).AsTask());
    }

    [Fact]
    [Trait("TestId", "MDG-FIN-TAX-001")]
    public async Task Posting_preflight_blocks_tax_lines_when_tax_mode_is_zero()
    {
        var rule = AcceptedRule("FE-06", "2101") with { CreatesTaxLine = true };
        var service = new PostingPreflightService(
            new RuleCatalog(rule),
            new AccountCatalog([
                EligibleAccount("1101", Guid.CreateVersion7(), "FE-06"),
                EligibleAccount("2101", Guid.CreateVersion7(), "FE-06", AccountNature.Liability, AccountNormalBalance.Credit)
            ]),
            new PeriodCatalog(OpenPeriod()));

        var result = await service.EvaluateAsync(
            new("FE-06", EffectiveDate, true, new TaxPolicySnapshot(0, null, null, null, null, false)));

        Assert.False(result.IsEligible);
        Assert.Contains(PostingBlockReason.TaxModeDisabled, result.BlockReasons);
    }

    [Fact]
    public async Task Posting_preflight_blocks_accounts_without_canonical_attributes()
    {
        var rule = AcceptedRule("FE-03");
        var unresolved = EligibleAccount("1101", Guid.CreateVersion7(), "FE-03") with { NormalBalance = null };
        var service = new PostingPreflightService(
            new RuleCatalog(rule),
            new AccountCatalog([
                unresolved,
                EligibleAccount("4101", Guid.CreateVersion7(), "FE-03", AccountNature.Revenue, AccountNormalBalance.Credit)
            ]),
            new PeriodCatalog(OpenPeriod()));

        var result = await service.EvaluateAsync(new("FE-03", EffectiveDate, true, null));
        Assert.False(result.IsEligible);
        Assert.Contains(PostingBlockReason.DebitAccountIneligible, result.BlockReasons);
    }

    private static AccountDefinition EligibleAccount(
        string code, Guid id, string eventId,
        AccountNature nature = AccountNature.Asset,
        AccountNormalBalance normalBalance = AccountNormalBalance.Debit,
        bool directPostingAllowed = true)
    {
        var account = new AccountDefinition(id, code, "حساب آزمون", nature, normalBalance, true, directPostingAllowed,
            AccountStatus.Active, new DateOnly(2026, 1, 1), null,
            new HashSet<string>(StringComparer.Ordinal) { eventId },
            new HashSet<string>(StringComparer.Ordinal), false);

        var attributes = new CanonicalAccountAttributes(
            "Test account", null, 1, nature.ToString(), "NONE", 0, "NONE",
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

    private static PostingRuleDefinition AcceptedRule(string eventId, string creditAccountCode = "4101")
    {
        var rule = new PostingRuleDefinition(eventId, 1, PostingRuleStatus.Accepted, "1101", creditAccountCode,
            "SOURCE_AMOUNT", "SOURCE_DOCUMENT_FINALIZED", "test-source", "test-decision",
            new DateOnly(2026, 1, 1), null, true, true, false)
        {
            EventName = "Test financial event", SourceEvent = "TEST_SOURCE_EVENT",
            Trigger = "SOURCE_DOCUMENT_FINALIZED", Preconditions = "SOURCE_DOCUMENT_VALIDATED",
SourceDocument = "TEST_SOURCE_DOCUMENT",
            DebitCondition = "DEBIT_ACCOUNT_ELIGIBLE", CreditCondition = "CREDIT_ACCOUNT_ELIGIBLE",
            CostBasis = "NOT_APPLICABLE", DesignStatus = "ACCEPTED",
            TaxModeDefinition = "TaxMode=0 blocks Tax Lines; positive TaxMode requires approved Tax Matrix",
            SubledgerUpdates = "NONE", Dimensions = "SOURCE_DIMENSIONS",
            InventoryEffect = "NOT_APPLICABLE", PartyEffect = "NOT_APPLICABLE",
            PostingIdentityDefinition = "SourceModule|SourceDocumentId|SourceEventId|PostingPurpose|SourceVersion|AllocationId",
            IdempotencyKeyDefinition = "SHA256_CANONICAL_POSTING_IDENTITY",
            ReversalRule = "REVERSAL_DOCUMENT", PeriodRule = "OPEN_FISCAL_PERIOD",
            AtomicityBoundaryDefinition = "Journal, GL, Subledger, source state and audit writes in one database transaction",
            RetryBehavior = "IDEMPOTENT_RETRY", Authorization = "SERVER_SIDE_AUTHORIZATION",
            AuditEvents = "POSTING_CREATED|POSTING_FINALIZED", TestCaseIds = "TEST_ACCOUNTING_DOMAIN",
            EvidenceRequirements = "SOURCE_AND_DECISION_EVIDENCE", OpenIssueIds = Array.Empty<string>()
        };
        var evidence = PostingRuleDefinition.RequiredAcceptanceFields.Select(field =>
        {
            var value = rule.GetExpectedEvidenceValueForTesting(field) ?? $"TEST:{field}";
            return new FieldEvidence(field, EvidenceDisposition.Verified, value, "test-source", null);
        }).ToArray();
        return rule with { AcceptanceEvidence = evidence };
    }

    [Fact]
    public void Every_7B_contract_field_must_be_present_and_match_its_runtime_value()
    {
        var accepted = AcceptedRule("FE-03");

        foreach (var field in PostingRuleDefinition.RequiredAcceptanceFields)
        {
            var missing = accepted with
            {
                AcceptanceEvidence = accepted.AcceptanceEvidence!
                    .Where(item => item.Field != field).ToArray()
            };
            var altered = accepted with
            {
                AcceptanceEvidence = accepted.AcceptanceEvidence!
                    .Select(item => item.Field == field
                        ? item with { Value = item.Value + "_ALTERED" }
                        : item).ToArray()
            };

            Assert.False(missing.IsExecutableOn(EffectiveDate), $"Missing 7B field: {field}");
            Assert.False(altered.IsExecutableOn(EffectiveDate), $"Altered 7B field: {field}");
        }
    }

    [Fact]
    public void Posting_rule_evidence_must_match_each_runtime_contract_field()
    {
        var accepted = AcceptedRule("FE-03");
        var changedTrigger = accepted with { Trigger = "A_DIFFERENT_TRIGGER" };
        var changedRecognitionTime = accepted with { RecognitionTime = "A_DIFFERENT_RECOGNITION_TIME" };
        var changedEvidence = accepted with
        {
            AcceptanceEvidence = accepted.AcceptanceEvidence!
                .Select(item => item.Field == "DebitCondition"
                    ? item with { Value = "A_DIFFERENT_DEBIT_CONDITION" }
                    : item).ToArray()
        };
        var missingRuntimeField = accepted with { RetryBehavior = null };

        Assert.True(accepted.IsExecutableOn(EffectiveDate));
        Assert.False(changedTrigger.IsExecutableOn(EffectiveDate));
        Assert.False(changedRecognitionTime.IsExecutableOn(EffectiveDate));
        Assert.False(changedEvidence.IsExecutableOn(EffectiveDate));
        Assert.False(missingRuntimeField.IsExecutableOn(EffectiveDate));
    }

    [Fact]
    public void Posting_rule_without_complete_field_level_evidence_is_not_executable()
    {
        var accepted = AcceptedRule("FE-03");
        var incomplete = accepted with
        {
            AcceptanceEvidence = accepted.AcceptanceEvidence!
                .Where(item => item.Field != "InventoryEffect").ToArray()
        };

        Assert.True(accepted.IsExecutableOn(EffectiveDate));
        Assert.False(incomplete.IsExecutableOn(EffectiveDate));
    }

    [Fact]
    public void Posting_rule_requires_verified_not_applicable_values_for_every_contract_field()
    {
        var accepted = AcceptedRule("FE-03");
        var explicitNotApplicable = accepted with
        {
            AcceptanceEvidence = accepted.AcceptanceEvidence!
                .Select(item => item.Field == "InventoryEffect"
                    ? item with { Value = "NOT_APPLICABLE" }
                    : item).ToArray()
        };
        var unverifiedDisposition = accepted with
        {
            AcceptanceEvidence = accepted.AcceptanceEvidence!
                .Select(item => item.Field == "InventoryEffect"
                    ? item with
                    {
                        Disposition = EvidenceDisposition.NotApplicable,
                        Value = null,
                        Rationale = "Not applicable"
                    }
                    : item).ToArray()
        };

        Assert.True(explicitNotApplicable.IsExecutableOn(EffectiveDate));
        Assert.False(unverifiedDisposition.IsExecutableOn(EffectiveDate));
    }

    [Fact]
    public void Posting_rule_with_open_issues_or_nonaccepted_design_status_is_not_executable()
    {
        var accepted = AcceptedRule("FE-03");
        var withOpenIssue = accepted with { OpenIssueIds = ["7B-OPEN-001"] };
        var draftDesign = accepted with { DesignStatus = "DRAFT" };

        Assert.True(accepted.IsExecutableOn(EffectiveDate));
        Assert.False(withOpenIssue.IsExecutableOn(EffectiveDate));
        Assert.False(draftDesign.IsExecutableOn(EffectiveDate));
    }

    [Fact]
    public void Account_without_complete_canonical_field_evidence_is_not_eligible()
    {
        var account = EligibleAccount("1101", Guid.CreateVersion7(), "FE-03");
        var incomplete = account with
        {
            CanonicalAttributes = account.CanonicalAttributes! with
            {
                Evidence = account.CanonicalAttributes.Evidence!
                    .Where(item => item.Field != "StatementMapping").ToArray()
            }
        };

        Assert.True(account.CanAutomatedPost("FE-03", EffectiveDate, true));
        Assert.False(incomplete.CanAutomatedPost("FE-03", EffectiveDate, true));
    }

    private static FiscalPeriod OpenPeriod(Guid? id = null) =>
        new(id ?? Guid.CreateVersion7(), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), FiscalPeriodStatus.Open);

    private static PostingIdentity Identity(string purpose) =>
        PostingIdentity.Create("Sales", "sale-1", "delivered", purpose, "1");

    private static Journal CreateBalancedDraft()
    {
        var periodId = Guid.CreateVersion7();
        var debit = JournalLine.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "1101", new(100), IrrAmount.Zero);
        var credit = JournalLine.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "4101", IrrAmount.Zero, new(100));
        return Journal.CreateDraft(Guid.CreateVersion7(), periodId, Guid.CreateVersion7(), EffectiveDate, Identity("SALE"),
            [debit, credit], "SALE-1", "actor-1", UtcNow, "audit-create");
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

    private sealed class PeriodCatalog(FiscalPeriod? period) : IFiscalPeriodCatalog
    {
        public ValueTask<FiscalPeriod?> FindForDateAsync(DateOnly effectiveDate, CancellationToken cancellationToken) =>
            ValueTask.FromResult(period);
    }
}
