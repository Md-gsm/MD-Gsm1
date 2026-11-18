namespace MDGsm.Accounting.Domain;

public enum PostingRuleStatus
{
    Draft, Accepted, Blocked, Inactive
}

public sealed record PostingRuleDefinition(
    string FinancialEventId,
    int Version,
    PostingRuleStatus Status,
    string DebitAccountCode,
    string CreditAccountCode,
    string AmountBasis,
    string RecognitionGate,
    string SourceReference,
    string DecisionReference,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IdempotencyRequired,
    bool AtomicityRequired,
    bool CreatesTaxLine)
{
    public bool IsExecutableOn(DateOnly date) =>
        Status == PostingRuleStatus.Accepted &&
        Version > 0 &&
        IsAccountCode(DebitAccountCode) &&
        IsAccountCode(CreditAccountCode) &&
        !string.IsNullOrWhiteSpace(AmountBasis) &&
        !string.IsNullOrWhiteSpace(RecognitionGate) &&
        !string.IsNullOrWhiteSpace(SourceReference) &&
        !string.IsNullOrWhiteSpace(DecisionReference) &&
        IdempotencyRequired &&
        AtomicityRequired &&
        date >= EffectiveFrom &&
        (!EffectiveTo.HasValue || date <= EffectiveTo.Value);

    private static bool IsAccountCode(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(char.IsAsciiDigit) &&
        !string.Equals(value, "110809", StringComparison.Ordinal);
}

public sealed record TaxPolicySnapshot(
    int TaxMode,
    string? MatrixVersion,
    string? SourceReference,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    bool SpecialistApproved)
{
    public bool AllowsTaxLine(DateOnly date) =>
        TaxMode > 0 &&
        !string.IsNullOrWhiteSpace(MatrixVersion) &&
        !string.IsNullOrWhiteSpace(SourceReference) &&
        EffectiveFrom.HasValue &&
        date >= EffectiveFrom.Value &&
        (!EffectiveTo.HasValue || date <= EffectiveTo.Value) &&
        SpecialistApproved;
}
