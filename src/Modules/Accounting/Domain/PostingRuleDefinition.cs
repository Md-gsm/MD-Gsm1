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
    bool CreatesTaxLine,
    IReadOnlyCollection<FieldEvidence>? AcceptanceEvidence = null)
{
    public static IReadOnlyList<string> RequiredAcceptanceFields { get; } =
    [
        "FinancialEventId", "SourceEvent", "Trigger", "Preconditions", "RecognitionTime",
        "SourceDocument", "DebitAccountCode", "CreditAccountCode", "DebitAccountConditions",
        "CreditAccountConditions", "AmountBasis", "CostBasis", "TaxMode", "SubledgerUpdates",
        "Dimensions", "InventoryEffect", "PartyEffect", "PostingIdentity", "IdempotencyKey",
        "ReversalRule", "PeriodRule", "AtomicityBoundary", "RetryBehavior", "Authorization",
        "AuditEvents", "TestCaseIds", "EvidenceRequirements", "SourceReferences", "DecisionReferences"
    ];

    public bool HasCompleteAcceptanceEvidence =>
        FieldEvidenceValidator.IsComplete(AcceptanceEvidence, RequiredAcceptanceFields) &&
        RequiredAcceptanceFields
            .Select(field => (Field: field, Expected: ExpectedEvidenceValue(field)))
            .Where(item => item.Expected is not null)
            .All(item => FieldEvidenceValidator.Matches(AcceptanceEvidence, item.Field, item.Expected));

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
        HasCompleteAcceptanceEvidence &&
        date >= EffectiveFrom &&
        (!EffectiveTo.HasValue || date <= EffectiveTo.Value);

    private string? ExpectedEvidenceValue(string field) => field switch
    {
        "FinancialEventId" => FinancialEventId,
        "DebitAccountCode" => DebitAccountCode,
        "CreditAccountCode" => CreditAccountCode,
        "AmountBasis" => AmountBasis,
        "RecognitionTime" => RecognitionGate,
        "IdempotencyKey" => IdempotencyRequired ? "REQUIRED" : "NOT_REQUIRED",
        "AtomicityBoundary" => AtomicityRequired ? "REQUIRED" : "NOT_REQUIRED",
        "SourceReferences" => SourceReference,
        "DecisionReferences" => DecisionReference,
        _ => null
    };

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
