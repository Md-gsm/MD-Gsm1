using System.Numerics;
using MDGsm.Accounting.Application;
using MDGsm.Accounting.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.Accounting;

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
    public void Direct_posting_permission_is_separate_from_automated_posting_eligibility()
    {
        var account = EligibleAccount("1101", Guid.CreateVersion7(), "FE-03") with { DirectPostingAllowed = false };
        Assert.True(account.CanAutomatedPost("FE-03", EffectiveDate, true));
        Assert.False(account.CanManualPost("FE-03", EffectiveDate, true));
    }

    [Fact]
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
        var finalized = journal.Finalize(openPeriod, "actor-1", UtcNow);

        Assert.Equal(JournalStatus.Draft, journal.Status);
        Assert.Equal(JournalStatus.Finalized, finalized.Status);
        Assert.Equal(new IrrAmount(100), finalized.TotalDebits);
        Assert.Equal(new IrrAmount(100), finalized.TotalCredits);

        var closed = openPeriod with { Status = FiscalPeriodStatus.Closed };
        Assert.Throws<InvalidOperationException>(() => journal.Finalize(closed, "actor-1", UtcNow));
    }

    [Fact]
    public void Journal_rejects_unbalanced_lines_and_zero_or_two_sided_lines()
    {
        var debit = JournalLine.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "1101", new(100), IrrAmount.Zero);
        var credit = JournalLine.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "4101", IrrAmount.Zero, new(99));

        Assert.Throws<InvalidOperationException>(() => Journal.CreateDraft(
            Guid.CreateVersion7(), Guid.CreateVersion7(), EffectiveDate, Identity("SALE"),
            [debit, credit], "SALE-1", "actor-1", UtcNow));
        Assert.Throws<ArgumentException>(() => JournalLine.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "1101", IrrAmount.Zero, IrrAmount.Zero));
        Assert.Throws<ArgumentException>(() => JournalLine.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "1101", new(1), new(1)));
    }

    [Fact]
    public void Reversal_is_a_new_journal_and_does_not_mutate_the_finalized_original()
    {
        var originalDraft = CreateBalancedDraft();
        var original = originalDraft.Finalize(OpenPeriod(originalDraft.FiscalPeriodId), "actor-1", UtcNow);
        var reversal = original.CreateReversal(
            Guid.CreateVersion7(), Guid.CreateVersion7(), EffectiveDate, Identity("REVERSAL"), "actor-2", UtcNow);

        Assert.Equal(JournalStatus.Finalized, original.Status);
        Assert.Equal(JournalStatus.Draft, reversal.Status);
        Assert.Equal(original.JournalId, reversal.ReversalOfJournalId);
        Assert.Equal(original.Lines[0].Debit, reversal.Lines[0].Credit);
        Assert.Equal(original.Lines[0].Credit, reversal.Lines[0].Debit);
        Assert.NotEqual(original.JournalId, reversal.JournalId);
    }

    [Fact]
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

    [Theory]
    [InlineData("FE-01")]
    [InlineData("FE-02")]
    [InlineData("FE-03")]
    [InlineData("FE-04")]
    [InlineData("FE-05")]
    [InlineData("FE-06")]
    [InlineData("FE-07")]
    [InlineData("FE-08")]
    [InlineData("FE-09")]
    [InlineData("FE-10")]
    [InlineData("FE-10b")]
    [InlineData("FE-11")]
    [InlineData("FE-12")]
    [InlineData("FE-13")]
    [InlineData("FE-14")]
    [InlineData("FE-15")]
    [InlineData("FE-16")]
    [InlineData("FE-17")]
    [InlineData("FE-18")]
    [InlineData("FE-19")]
    [InlineData("FE-20")]
    [InlineData("FE-21")]
    [InlineData("FE-22")]
    [InlineData("FE-23")]
    [InlineData("FE-24")]
    [InlineData("FE-25")]
    [InlineData("FE-26")]
    [InlineData("FE-27")]
    [InlineData("FE-28")]
    [InlineData("FE-29")]
    [InlineData("FE-30")]
    [InlineData("FE-31")]
    [InlineData("FE-32")]
    [InlineData("FE-33")]
    [InlineData("FE-34")]
    [InlineData("FE-35")]
    [InlineData("FE-36")]
    [InlineData("FE-37")]
    [InlineData("FE-38")]
    public async Task Every_financial_event_without_an_accepted_rule_is_blocked(string financialEventId)
    {
        var service = new PostingPreflightService(
            new RuleCatalog(null),
            new AccountCatalog([]),
            new PeriodCatalog(OpenPeriod()));

        var result = await service.EvaluateAsync(
            new(financialEventId, EffectiveDate, true, null));

        Assert.False(result.IsEligible);
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
    public async Task Posting_preflight_blocks_tax_lines_when_tax_mode_is_zero()
    {
        var rule = AcceptedRule("FE-06") with { CreatesTaxLine = true };
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
        AccountNormalBalance normalBalance = AccountNormalBalance.Debit)
    {
        var account = new AccountDefinition(id, code, "حساب آزمون", nature, normalBalance, true, true,
            AccountStatus.Active, new DateOnly(2026, 1, 1), null,
            new HashSet<string>(StringComparer.Ordinal) { eventId },
            new HashSet<string>(StringComparer.Ordinal), false);

        var attributes = new CanonicalAccountAttributes(
            "Test account", null, 1, nature.ToString(), null, 0, "NONE",
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

    private static PostingRuleDefinition AcceptedRule(string eventId)
    {
        var rule = new PostingRuleDefinition(eventId, 1, PostingRuleStatus.Accepted, "1101", "4101",
            "SOURCE_AMOUNT", "SOURCE_DOCUMENT_FINALIZED", "test-source", "test-decision",
            new DateOnly(2026, 1, 1), null, true, true, false);
        var evidence = PostingRuleDefinition.RequiredAcceptanceFields.Select(field =>
        {
            var value = field switch
            {
                "FinancialEventId" => rule.FinancialEventId,
                "DebitAccountCode" => rule.DebitAccountCode,
                "CreditAccountCode" => rule.CreditAccountCode,
                "AmountBasis" => rule.AmountBasis,
                "RecognitionTime" => rule.RecognitionGate,
                "IdempotencyKey" => "REQUIRED",
                "AtomicityBoundary" => "REQUIRED",
                "SourceReferences" => rule.SourceReference,
                "DecisionReferences" => rule.DecisionReference,
                _ => $"TEST:{field}"
            };
            return new FieldEvidence(field, EvidenceDisposition.Verified, value, "test-source", null);
        }).ToArray();
        return rule with { AcceptanceEvidence = evidence };
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
        return Journal.CreateDraft(Guid.CreateVersion7(), periodId, EffectiveDate, Identity("SALE"),
            [debit, credit], "SALE-1", "actor-1", UtcNow);
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
