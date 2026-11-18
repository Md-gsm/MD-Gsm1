using MDGsm.Accounting.Domain;

namespace MDGsm.Accounting.Application;

public interface IPostingRuleCatalog
{
    ValueTask<PostingRuleDefinition?> FindAsync(string financialEventId, DateOnly effectiveDate, CancellationToken cancellationToken);
}

public interface IAccountCatalog
{
    ValueTask<AccountDefinition?> FindByCodeAsync(string accountCode, CancellationToken cancellationToken);
}

public interface IFiscalPeriodCatalog
{
    ValueTask<FiscalPeriod?> FindForDateAsync(DateOnly effectiveDate, CancellationToken cancellationToken);
}

public enum PostingBlockReason
{
    InvalidFinancialEvent,
    PostingRuleNotFound,
    PostingRuleNotAcceptedOrIncomplete,
    FiscalPeriodNotFound,
    FiscalPeriodClosedOrDateOutside,
    DebitAccountNotFound,
    CreditAccountNotFound,
    DebitAccountIneligible,
    CreditAccountIneligible,
    TaxModeDisabled,
    TaxMatrixNotVerified
}

public sealed record PostingPreflightRequest(
    string FinancialEventId,
    DateOnly EffectiveDate,
    bool RecognitionEvidencePresent,
    TaxPolicySnapshot? TaxPolicy);

public sealed record PostingPreflightResult(
    bool IsEligible,
    IReadOnlyList<PostingBlockReason> BlockReasons,
    int? PostingRuleVersion)
{
    public static PostingPreflightResult Blocked(IEnumerable<PostingBlockReason> reasons, int? ruleVersion = null) =>
        new(false, Array.AsReadOnly(reasons.Distinct().ToArray()), ruleVersion);

    public static PostingPreflightResult Eligible(int ruleVersion) =>
        new(true, Array.Empty<PostingBlockReason>(), ruleVersion);
}

/// <summary>
/// Read-only fail-closed evaluation. This service never creates a Journal or mutates a
/// subledger; posting requires a separately implemented transactional writer.
/// </summary>
public sealed class PostingPreflightService(
    IPostingRuleCatalog rules,
    IAccountCatalog accounts,
    IFiscalPeriodCatalog periods)
{
    public async ValueTask<PostingPreflightResult> EvaluateAsync(
        PostingPreflightRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.FinancialEventId))
            return PostingPreflightResult.Blocked([PostingBlockReason.InvalidFinancialEvent]);

        var rule = await rules.FindAsync(request.FinancialEventId, request.EffectiveDate, cancellationToken);
        if (rule is null)
            return PostingPreflightResult.Blocked([PostingBlockReason.PostingRuleNotFound]);

        if (!string.Equals(rule.FinancialEventId, request.FinancialEventId, StringComparison.Ordinal) ||
            !rule.IsExecutableOn(request.EffectiveDate))
            return PostingPreflightResult.Blocked([PostingBlockReason.PostingRuleNotAcceptedOrIncomplete], rule.Version);

        var period = await periods.FindForDateAsync(request.EffectiveDate, cancellationToken);
        if (period is null)
            return PostingPreflightResult.Blocked([PostingBlockReason.FiscalPeriodNotFound], rule.Version);
        if (!period.AllowsPosting(request.EffectiveDate))
            return PostingPreflightResult.Blocked([PostingBlockReason.FiscalPeriodClosedOrDateOutside], rule.Version);

        var debitAccount = await accounts.FindByCodeAsync(rule.DebitAccountCode, cancellationToken);
        if (debitAccount is null)
            return PostingPreflightResult.Blocked([PostingBlockReason.DebitAccountNotFound], rule.Version);
        if (!debitAccount.CanAutomatedPost(request.FinancialEventId, request.EffectiveDate, request.RecognitionEvidencePresent))
            return PostingPreflightResult.Blocked([PostingBlockReason.DebitAccountIneligible], rule.Version);

        var creditAccount = await accounts.FindByCodeAsync(rule.CreditAccountCode, cancellationToken);
        if (creditAccount is null)
            return PostingPreflightResult.Blocked([PostingBlockReason.CreditAccountNotFound], rule.Version);
        if (!creditAccount.CanAutomatedPost(request.FinancialEventId, request.EffectiveDate, request.RecognitionEvidencePresent))
            return PostingPreflightResult.Blocked([PostingBlockReason.CreditAccountIneligible], rule.Version);

        if (rule.CreatesTaxLine)
        {
            if (request.TaxPolicy?.TaxMode == 0)
                return PostingPreflightResult.Blocked([PostingBlockReason.TaxModeDisabled], rule.Version);
            if (request.TaxPolicy is null || !request.TaxPolicy.AllowsTaxLine(request.EffectiveDate))
                return PostingPreflightResult.Blocked([PostingBlockReason.TaxMatrixNotVerified], rule.Version);
        }

        return PostingPreflightResult.Eligible(rule.Version);
    }
}
