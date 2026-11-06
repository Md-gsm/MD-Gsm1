using MDGsm.Domain.Persons;

namespace MDGsm.Application.Persons;

public enum PartyQuickRegistrationOutcome
{
    Created = 1,
    ExistingPartyMatched = 2,
    SelectionRequired = 3,
    Replayed = 4
}

public sealed record PartyQuickRegistrationCandidate(
    string PartyId,
    PartyType Type,
    string DisplayName,
    PartyBusinessRoles Roles,
    bool PartyIsActive,
    bool IsMerged,
    bool HasActiveMobile,
    string MaskedMobile);

public sealed record PartyQuickRegistrationResult(
    PartyQuickRegistrationOutcome Outcome,
    string? PartyId,
    bool Created,
    IReadOnlyList<PartyQuickRegistrationCandidate> Candidates);
