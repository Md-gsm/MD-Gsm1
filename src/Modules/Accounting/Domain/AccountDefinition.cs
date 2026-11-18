namespace MDGsm.Accounting.Domain;

public enum AccountNature
{
    Asset, Liability, Equity, Revenue, ContraRevenue, CostOfGoodsSold, Expense, Control, TechnicalClearing
}

public enum AccountNormalBalance
{
    Debit, Credit, Either
}

public enum AccountStatus
{
    Draft, Active, Inactive, Blocked, Retired
}

public sealed record AccountDefinition(
    Guid? AccountId,
    string AccountCode,
    string TitleFa,
    AccountNature? Nature,
    AccountNormalBalance? NormalBalance,
    bool? Postable,
    bool? DirectPostingAllowed,
    AccountStatus Status,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    IReadOnlySet<string> AllowedFinancialEvents,
    IReadOnlySet<string> ForbiddenFinancialEvents,
    bool RecognitionEvidenceRequired)
{
    public bool IsProhibited => string.Equals(AccountCode, "110809", StringComparison.Ordinal);

    public bool CanAutomatedPost(string financialEventId, DateOnly effectiveDate, bool recognitionEvidencePresent)
    {
        if (IsProhibited ||
            AccountId is null ||
            AccountId == Guid.Empty ||
            string.IsNullOrWhiteSpace(AccountCode) ||
            AccountCode.Any(character => !char.IsAsciiDigit(character)) ||
            string.IsNullOrWhiteSpace(TitleFa) ||
            Nature is null ||
            NormalBalance is null ||
            Postable is not true ||
            Status != AccountStatus.Active ||
            EffectiveFrom is null ||
            effectiveDate < EffectiveFrom.Value ||
            (EffectiveTo.HasValue && effectiveDate > EffectiveTo.Value) ||
            string.IsNullOrWhiteSpace(financialEventId) ||
            AllowedFinancialEvents is null ||
            !AllowedFinancialEvents.Contains(financialEventId) ||
            ForbiddenFinancialEvents is null ||
            ForbiddenFinancialEvents.Contains(financialEventId))
            return false;

        return !RecognitionEvidenceRequired || recognitionEvidencePresent;
    }

    public bool CanManualPost(string financialEventId, DateOnly effectiveDate, bool recognitionEvidencePresent) =>
        DirectPostingAllowed is true &&
        CanAutomatedPost(financialEventId, effectiveDate, recognitionEvidencePresent);
}
