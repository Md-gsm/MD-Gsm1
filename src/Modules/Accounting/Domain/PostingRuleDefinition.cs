namespace MDGsm.Accounting.Domain;

public enum PostingRuleStatus { Draft, Accepted, Blocked, Inactive }

/// <summary>Runtime projection of every 7B field. Evidence must match the represented value.</summary>
public sealed record PostingRuleDefinition(
    string FinancialEventId, int Version, PostingRuleStatus Status,
    string DebitAccountCode, string CreditAccountCode, string AmountBasis,
    string RecognitionTime, string SourceReference, string DecisionReference,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IdempotencyRequired,
    bool AtomicityRequired, bool CreatesTaxLine,
    IReadOnlyCollection<FieldEvidence>? AcceptanceEvidence = null)
{
    public string? EventName { get; init; }
    public string? SourceEvent { get; init; }
    public string? Trigger { get; init; }
    public string? Preconditions { get; init; }
    public string? SourceDocument { get; init; }
    public string? DebitCondition { get; init; }
    public string? CreditCondition { get; init; }
    public string? CostBasis { get; init; }
    public string? TaxModeDefinition { get; init; }
    public string? SubledgerUpdates { get; init; }
    public string? Dimensions { get; init; }
    public string? InventoryEffect { get; init; }
    public string? PartyEffect { get; init; }
    public string? PostingIdentityDefinition { get; init; }
    public string? IdempotencyKeyDefinition { get; init; }
    public string? ReversalRule { get; init; }
    public string? PeriodRule { get; init; }
    public string? AtomicityBoundaryDefinition { get; init; }
    public string? RetryBehavior { get; init; }
    public string? Authorization { get; init; }
    public string? AuditEvents { get; init; }
    public string? TestCaseIds { get; init; }
    public string? EvidenceRequirements { get; init; }
    public string? DesignStatus { get; init; }
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

    public static IReadOnlyList<string> RequiredVerifiedAcceptanceFields { get; } = RequiredAcceptanceFields;

    public bool HasCompleteAcceptanceEvidence =>
        FieldEvidenceValidator.IsComplete(AcceptanceEvidence, RequiredAcceptanceFields, RequiredVerifiedAcceptanceFields) &&
        RequiredAcceptanceFields.All(fieldName => FieldEvidenceValidator.Matches(AcceptanceEvidence, fieldName, ExpectedEvidenceValue(fieldName)));

    public bool IsExecutableOn(DateOnly date) =>
        Status == PostingRuleStatus.Accepted && Version > 0 &&
        IsAccountCode(DebitAccountCode) && IsAccountCode(CreditAccountCode) &&
        !string.IsNullOrWhiteSpace(FinancialEventId) &&
        !string.IsNullOrWhiteSpace(EventName) &&
        string.Equals(DesignStatus, "ACCEPTED", StringComparison.Ordinal) &&
        OpenIssueIds is { Count: 0 } &&
        !string.IsNullOrWhiteSpace(AmountBasis) &&
        !string.IsNullOrWhiteSpace(RecognitionTime) &&
        !string.IsNullOrWhiteSpace(SourceReference) &&
        !string.IsNullOrWhiteSpace(DecisionReference) &&
        !string.IsNullOrWhiteSpace(TaxModeDefinition) &&
        !string.IsNullOrWhiteSpace(PostingIdentityDefinition) &&
        !string.IsNullOrWhiteSpace(IdempotencyKeyDefinition) &&
        !string.IsNullOrWhiteSpace(AtomicityBoundaryDefinition) &&
        IdempotencyRequired && AtomicityRequired &&
        HasCompleteAcceptanceEvidence &&
        date >= EffectiveFrom && (!EffectiveTo.HasValue || date <= EffectiveTo.Value);

    public string? GetExpectedEvidenceValueForTesting(string fieldName) => ExpectedEvidenceValue(fieldName);

    private string? ExpectedEvidenceValue(string fieldName) => fieldName switch
    {
        "FEId" => FinancialEventId,
        "EventName" => EventName,
        "SourceEvent" => SourceEvent,
        "Trigger" => Trigger,
        "Preconditions" => Preconditions,
        "RecognitionTime" => RecognitionTime,
        "SourceDocument" => SourceDocument,
        "DebitAccountCode" => DebitAccountCode,
        "DebitCondition" => DebitCondition,
        "CreditAccountCode" => CreditAccountCode,
        "CreditCondition" => CreditCondition,
        "AmountBasis" => AmountBasis,
        "CostBasis" => CostBasis,
        "TaxMode" => TaxModeDefinition,
        "SubledgerUpdates" => SubledgerUpdates,
        "Dimensions" => Dimensions,
        "InventoryEffect" => InventoryEffect,
        "PartyEffect" => PartyEffect,
        "PostingIdentity" => PostingIdentityDefinition,
        "IdempotencyKey" => IdempotencyKeyDefinition,
        "ReversalRule" => ReversalRule,
        "PeriodRule" => PeriodRule,
        "AtomicityBoundary" => AtomicityBoundaryDefinition,
        "RetryBehavior" => RetryBehavior,
        "Authorization" => Authorization,
        "AuditEvents" => AuditEvents,
        "TestCaseIDs" => TestCaseIds,
        "EvidenceRequirements" => EvidenceRequirements,
        "SourceRefs" => SourceReference,
        "DesignStatus" => DesignStatus,
        "OpenIssueIDs" => OpenIssueIds is { Count: 0 } ? "NONE" : string.Join("\u001f", OpenIssueIds),
        "DecisionReferences" => DecisionReference,
        _ => null
    };

    private static bool IsAccountCode(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(char.IsAsciiDigit) &&
        !string.Equals(value, "110809", StringComparison.Ordinal);
}

public sealed record TaxPolicySnapshot(int TaxMode, string? MatrixVersion, string? SourceReference,
    DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool SpecialistApproved)
{
    public bool AllowsTaxLine(DateOnly date) =>
        TaxMode > 0 && !string.IsNullOrWhiteSpace(MatrixVersion) &&
        !string.IsNullOrWhiteSpace(SourceReference) && EffectiveFrom.HasValue &&
        date >= EffectiveFrom.Value && (!EffectiveTo.HasValue || date <= EffectiveTo.Value) &&
        SpecialistApproved;
}
