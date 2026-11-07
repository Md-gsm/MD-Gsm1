namespace MDGsm.Persons.Contracts;

public sealed record CreateNaturalPartyRequest(
    string GivenName,
    string FamilyName,
    string[] Roles,
    string? NationalId,
    string? PrimaryMobile,
    string? AlternateMobile,
    string? Landline,
    string? Email,
    PartyAddressContract? Address,
    PartyCommunicationPreferencesContract Communication,
    string? AcquaintanceSource,
    string? InternalNote,
    string? WorkClassification = null);
