using MDGsm.Domain.Persons;

namespace MDGsm.Application.Persons;

public interface IPartyRegistrationCommon
{
    string? PrimaryMobile { get; }
    string? AlternateMobile { get; }
    string? Landline { get; }
    string? Email { get; }
    string? Province { get; }
    string? City { get; }
    string? PostalCode { get; }
    string? AddressLine { get; }
    PartyAcquaintanceSource AcquaintanceSource { get; }
    string? InternalNote { get; }
    PartyCommunicationPreferences CommunicationPreferences { get; }
    WorkClassification? WorkClassification { get; }
}

public sealed record RegisterNaturalParty(
    string GivenName, string FamilyName, PartyBusinessRoles Roles, string? NationalId,
    string? PrimaryMobile, string? AlternateMobile, string? Landline, string? Email,
    string? Province, string? City, string? PostalCode, string? AddressLine,
    PartyAcquaintanceSource AcquaintanceSource, string? InternalNote,
    PartyCommunicationPreferences CommunicationPreferences,
    WorkClassification? WorkClassification = null) : IPartyRegistrationCommon;

public sealed record RegisterLegalParty(
    string LegalName, string? LegalNationalId, string? EconomicCode, string? ResponsiblePersonName,
    PartyBusinessRoles Roles, string PrimaryMobile, string? AlternateMobile, string? Landline, string? Email,
    string? Province, string? City, string? PostalCode, string? AddressLine,
    PartyAcquaintanceSource AcquaintanceSource, string? InternalNote,
    PartyCommunicationPreferences CommunicationPreferences,
    WorkClassification? WorkClassification = null) : IPartyRegistrationCommon;

public sealed record PartyListItem(
    string PartyId, PartyType Type, string DisplayName, PartyBusinessRoles Roles,
    string? MaskedPrimaryMobile, string? City, DateTimeOffset CreatedAtUtc);

public sealed record PartyDetails(
    string PartyId, PartyType Type, string DisplayName, PartyBusinessRoles Roles,
    string? NationalIdentifier, string? EconomicCode, string? ResponsiblePersonName,
    IReadOnlyList<ContactPointDto> Contacts, AddressDto? PrimaryAddress,
    PartyAcquaintanceSource AcquaintanceSource, string? InternalNote,
    PartyCommunicationPreferences CommunicationPreferences, DateTimeOffset CreatedAtUtc,
    bool SensitiveFieldsIncluded, WorkClassification? WorkClassification);

public sealed record ContactPointDto(string Kind, string Value, bool IsPrimary, bool IsMessagingAllowed);
public sealed record AddressDto(string CountryCode, string Province, string City, string PostalCode, string AddressLine);

/// <summary>Outcome of an idempotent registration. <see cref="Created"/> is false when the OperationId was already applied.</summary>
public sealed record PartyRegistrationResult(string PartyId, bool Created);

/// <summary>Raised when a unique identifier (for example a national identity number) is already registered. Carries no details by design.</summary>
public sealed class DuplicatePartyIdentifierException()
    : Exception("A party with the same unique identifier is already registered.");
