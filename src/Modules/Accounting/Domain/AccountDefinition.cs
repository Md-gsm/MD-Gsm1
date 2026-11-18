using System.Globalization;

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

public enum EvidenceDisposition
{
    Verified, NotApplicable, Unverified
}

public sealed record FieldEvidence(
    string Field,
    EvidenceDisposition Disposition,
    string? Value,
    string SourceReference,
    string? Rationale);

public static class FieldEvidenceValidator
{
    public static bool IsComplete(
        IReadOnlyCollection<FieldEvidence>? evidence,
        IReadOnlyCollection<string> requiredFields,
        IReadOnlyCollection<string>? mustBeVerifiedFields = null)
    {
        if (evidence is null || evidence.Count != requiredFields.Count ||
            evidence.Any(item => item is null || string.IsNullOrWhiteSpace(item.Field)))
            return false;

        var groups = evidence.GroupBy(item => item.Field, StringComparer.Ordinal).ToArray();
        if (groups.Any(group => group.Count() != 1))
            return false;

        var indexed = groups.ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        if (indexed.Count != requiredFields.Count || requiredFields.Any(field => !indexed.ContainsKey(field)))
            return false;

        var requiredVerified = mustBeVerifiedFields ?? Array.Empty<string>();
        if (requiredVerified.Any(field => !requiredFields.Contains(field, StringComparer.Ordinal)))
            return false;

        return requiredFields.All(field =>
        {
            var item = indexed[field];
            if (string.IsNullOrWhiteSpace(item.SourceReference))
                return false;

            if (requiredVerified.Contains(field, StringComparer.Ordinal) &&
                item.Disposition != EvidenceDisposition.Verified)
                return false;

            return item.Disposition switch
            {
                EvidenceDisposition.Verified =>
                    !string.IsNullOrWhiteSpace(item.Value) && string.IsNullOrWhiteSpace(item.Rationale),
                EvidenceDisposition.NotApplicable =>
                    item.Value is null && !string.IsNullOrWhiteSpace(item.Rationale),
                _ => false
            };
        });
    }

    public static bool Matches(
        IReadOnlyCollection<FieldEvidence>? evidence,
        string field,
        string? expectedValue)
    {
        if (evidence is null)
            return false;

        var matches = evidence.Where(item => string.Equals(item.Field, field, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            return false;

        var item = matches[0];
        return expectedValue is null
            ? item.Disposition == EvidenceDisposition.NotApplicable && item.Value is null && !string.IsNullOrWhiteSpace(item.Rationale)
            : item.Disposition == EvidenceDisposition.Verified && string.Equals(item.Value, expectedValue, StringComparison.Ordinal);
    }
}

public sealed record CanonicalAccountAttributes(
    string? TitleEn,
    Guid? ParentAccountId,
    int? Level,
    string? AccountClass,
    string? SubledgerType,
    int? TaxMode,
    string? TaxTreatment,
    string? RecognitionGate,
    string? ReconciliationPolicy,
    string? AgingPolicy,
    string? StatementMapping,
    string? SourceReference,
    string? DecisionReference,
    string? ChangeRequestReference,
    int? DefinitionVersion,
    string? AuditReference,
    IReadOnlyCollection<FieldEvidence>? Evidence)
{
    public static IReadOnlyList<string> RequiredFields { get; } =
    [
        "AccountId", "AccountCode", "TitleFa", "TitleEn", "ParentAccountId", "Level",
        "AccountClass", "Nature", "NormalBalance", "Postable", "DirectPostingAllowed",
        "Status", "EffectiveFrom", "EffectiveTo", "SubledgerType", "FinancialEventEligibility",
        "TaxMode", "TaxTreatment", "RecognitionGate", "ReconciliationPolicy", "AgingPolicy",
        "StatementMapping", "SourceReference", "DecisionReference", "ChangeRequestReference",
        "DefinitionVersion", "AuditReference"
    ];

    public static IReadOnlyList<string> RequiredVerifiedFields { get; } =
    [
        "AccountId", "AccountCode", "TitleFa", "TitleEn", "Level", "AccountClass",
        "Nature", "NormalBalance", "Postable", "DirectPostingAllowed", "Status",
        "EffectiveFrom", "FinancialEventEligibility", "TaxMode", "RecognitionGate",
        "SourceReference", "DecisionReference", "DefinitionVersion", "AuditReference"
    ];

    public bool IsCompleteFor(AccountDefinition account)
    {
        if (!FieldEvidenceValidator.IsComplete(Evidence, RequiredFields, RequiredVerifiedFields))
            return false;

        return RequiredFields.All(field =>
            FieldEvidenceValidator.Matches(Evidence, field, ExpectedValue(account, field)));
    }

    public string? GetExpectedValue(AccountDefinition account, string field) => ExpectedValue(account, field);

    private string? ExpectedValue(AccountDefinition account, string field) => field switch
    {
        "AccountId" => account.AccountId?.ToString("D"),
        "AccountCode" => account.AccountCode,
        "TitleFa" => account.TitleFa,
        "TitleEn" => TitleEn,
        "ParentAccountId" => ParentAccountId?.ToString("D"),
        "Level" => Level?.ToString(CultureInfo.InvariantCulture),
        "AccountClass" => AccountClass,
        "Nature" => account.Nature?.ToString(),
        "NormalBalance" => account.NormalBalance?.ToString(),
        "Postable" => account.Postable?.ToString().ToLowerInvariant(),
        "DirectPostingAllowed" => account.DirectPostingAllowed?.ToString().ToLowerInvariant(),
        "Status" => account.Status.ToString(),
        "EffectiveFrom" => account.EffectiveFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "EffectiveTo" => account.EffectiveTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "SubledgerType" => SubledgerType,
        "FinancialEventEligibility" => account.AllowedFinancialEvents is { Count: > 0 }
            ? string.Join("\u001f", account.AllowedFinancialEvents.OrderBy(value => value, StringComparer.Ordinal))
            : null,
        "TaxMode" => TaxMode?.ToString(CultureInfo.InvariantCulture),
        "TaxTreatment" => TaxTreatment,
        "RecognitionGate" => RecognitionGate,
        "ReconciliationPolicy" => ReconciliationPolicy,
        "AgingPolicy" => AgingPolicy,
        "StatementMapping" => StatementMapping,
        "SourceReference" => SourceReference,
        "DecisionReference" => DecisionReference,
        "ChangeRequestReference" => ChangeRequestReference,
        "DefinitionVersion" => DefinitionVersion?.ToString(CultureInfo.InvariantCulture),
        "AuditReference" => AuditReference,
        _ => null
    };
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
    bool RecognitionEvidenceRequired,
    CanonicalAccountAttributes? CanonicalAttributes = null)
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
            ForbiddenFinancialEvents.Contains(financialEventId) ||
            CanonicalAttributes is null ||
            !CanonicalAttributes.IsCompleteFor(this))
            return false;

        return !RecognitionEvidenceRequired || recognitionEvidencePresent;
    }

    public bool CanManualPost(string financialEventId, DateOnly effectiveDate, bool recognitionEvidencePresent) =>
        DirectPostingAllowed is true &&
        CanAutomatedPost(financialEventId, effectiveDate, recognitionEvidencePresent);
}
