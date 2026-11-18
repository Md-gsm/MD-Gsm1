namespace MDGsm.Accounting.Domain;

public enum PostingRuleStatus
{
    Draft, Accepted, Blocked, Inactive
}

/// <summary>
/// Runtime projection of the complete 7B posting-rule contract. Field-level
/// AcceptanceEvidence must prove every required 7B field before this rule can
/// be considered executable. This type does not itself create a journal.
/// </summary>
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
    public string? EventName { get; init; }
    public string? DesignStatus { get; init; }
    public string? TaxModeDefinition { get; init; }
    public string? IdempotencyKeyDefinition { get; init; }
    public string? AtomicityBoundaryDefinition { get; init; }
    public IReadOnlyCollection<string> OpenIssueIds { get; init; } = Array.Empty<string>();

    public static IReadOnlyList<string> RequiredAcceptanceFields { get; } =
    [
        "FEId", "EventName", "SourceEvent", "Trigger", "Preconditions", "RecognitionTime",
        "SourceDocument", "DebitAccountCode", "DebitCondition", "CreditAccountCode",
        "CreditCondition", "AmountBasis", "CostBasis", "TaxMode", "SubledgerUpdates",
        "Dimensions", "InventoryEffect", "PartyEffect", "PostingIdentity", "IdempotencyKey",
        "ReversalRule", "PeriodRule", "AtomicityBoundary", "RetryBehavior", "Authorization",
        "AuditEvents", "TestCaseIDs", "EvidenceRequirements", "SourceRefs", "DesignStatus",
        "OpenIssueIDs", "DecisionReferences"
    ];

    // Every contract field must be explicitly verified. Non-applicable behavior
    // is represented by a verified "NOT_APPLICABLE" value, never an empty field.
    public static IReadOnlyList<string> RequiredVerifiedAcceptanceFields { get; } =
        RequiredAcceptanceFields;

    public bool HasCompleteAcceptanceEvidence =>
        FieldEvidenceValidator.IsComplete(
            AcceptanceEvidence,
            RequiredAcceptanceFields,
            new HashSet<string>(StringComparer.Ordinal)) &&
        RequiredAcceptanceFields
            .Select(fieldName => (Field: fieldName, Expected: ExpectedEvidenceValue(fieldName)))
            .Where(item => item.Expected is not null)
            .All(item => FieldEvidenceValidator.Matches(AcceptanceEvidence, item.Field, item.Expected));

    public bool IsExecutableOn(DateOnly date) =>
        Status == PostingRuleStatus.Accepted &&
        Version > 0 &&
        IsAccountCode(DebitAccountCode) &&
        IsAccountCode(CreditAccountCode) &&
        !string.IsNullOrWhiteSpace(FinancialEventId) &&
        !string.IsNullOrWhiteSpace(EventName) &&
        string.Equals(DesignStatus, "ACCEPTED", StringComparison.Ordinal) &&
        OpenIssueIds is not null &&
        OpenIssueIds.Count == 0 &&
        !string.IsNullOrWhiteSpace(AmountBasis) &&
        !string.IsNullOrWhiteSpace(RecognitionGate) &&
        !string.IsNullOrWhiteSpace(SourceReference) &&
        !string.IsNullOrWhiteSpace(DecisionReference) &&
        !string.IsNullOrWhiteSpace(TaxModeDefinition) &&
        !string.IsNullOrWhiteSpace(IdempotencyKeyDefinition) &&
        !string.IsNullOrWhiteSpace(AtomicityBoundaryDefinition) &&
        IdempotencyRequired &&
        AtomicityRequired &&
        HasCompleteAcceptanceEvidence &&
        date >= EffectiveFrom &&
        (!EffectiveTo.HasValue || date <= EffectiveTo.Value);

    public string? GetExpectedEvidenceValueForTesting(string fieldName) =>
        ExpectedEvidenceValue(fieldName);

    private string? ExpectedEvidenceValue(string fieldName) => fieldName switch
    {
        "FEId" => FinancialEventId,
        "EventName" => EventName,
        "DebitAccountCode" => DebitAccountCode,
        "CreditAccountCode" => CreditAccountCode,
        "AmountBasis" => AmountBasis,
        "RecognitionTime" => RecognitionGate,
        "TaxMode" => TaxModeDefinition,
        "IdempotencyKey" => IdempotencyKeyDefinition,
        "AtomicityBoundary" => AtomicityBoundaryDefinition,
        "SourceRefs" => SourceReference,
        "DecisionReferences" => DecisionReference,
        "DesignStatus" => DesignStatus,
        "OpenIssueIDs" => OpenIssueIds is { Count: 0 } ? "NONE" : string.Join("\u001f", OpenIssueIds),
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
