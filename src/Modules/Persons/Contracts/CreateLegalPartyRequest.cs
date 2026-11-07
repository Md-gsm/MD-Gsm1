namespace MDGsm.Persons.Contracts;

public sealed record CreateLegalPartyRequest(
    string LegalName,
    string? LegalNationalId,
    string? EconomicCode,
    string? ResponsiblePersonName,
    string[] Roles,
    string? PrimaryMobile,
    string? AlternateMobile,
    string? Landline,
    string? Email,
    PartyAddressContract? Address,
    PartyCommunicationPreferencesContract Communication,
    string? AcquaintanceSource,
    string? InternalNote,
    string? WorkClassification = null);
