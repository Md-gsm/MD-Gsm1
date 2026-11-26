namespace MDGsm.Persons.Contracts;

public sealed record QuickRegisterNaturalPartyRequest(CreateNaturalPartyRequest Party, bool ForceNew = false, string? SelectedPartyId = null, bool ConfirmDistinctParty = false);
public sealed record QuickRegisterLegalPartyRequest(CreateLegalPartyRequest Party, bool ForceNew = false, string? SelectedPartyId = null, bool ConfirmDistinctParty = false);

public sealed record PartyQuickRegistrationCandidateContract(
    string PartyId, string PartyType, string DisplayName, IReadOnlyList<string> Roles,
    bool PartyIsActive, bool IsMerged, bool HasActiveMobile, string MaskedMobile);

public sealed record PartyQuickRegistrationResponse(
    string Outcome, string? PartyId, bool Created, IReadOnlyList<PartyQuickRegistrationCandidateContract> Candidates);
