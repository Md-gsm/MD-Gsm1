namespace MDGsm.Persons.Contracts;

public sealed record TemporaryWorkflowPromotionRequest(
    bool CreateNewParty,
    string? SelectedPartyId = null,
    bool ConfirmDistinctParty = false);

public sealed record TemporaryWorkflowPromotionResponse(
    string Outcome,
    string? PartyId,
    bool Created,
    bool Replayed,
    IReadOnlyList<PartyQuickRegistrationCandidateContract> Candidates);
