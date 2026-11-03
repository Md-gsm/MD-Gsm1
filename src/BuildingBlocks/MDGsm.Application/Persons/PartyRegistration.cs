using MDGsm.Domain.Persons;

namespace MDGsm.Application.Persons;

public sealed record RegisterNaturalParty(
    string GivenName,
    string FamilyName,
    PartyBusinessRoles Roles,
    string? NationalId,
    string PrimaryMobile,
    string? AlternateMobile,
    string? Landline,
    string? Email,
    string? Province,
    string? City,
    string? PostalCode,
    string? AddressLine,
    long CreditLimitRial,
    decimal DefaultDiscountPercent,
    DefaultPaymentMethod DefaultPaymentMethod,
    string? BankCardLastFour,
    PartyAcquaintanceSource AcquaintanceSource,
    string? InternalNote,
    PartyCommunicationPreferences CommunicationPreferences);

public sealed record RegisterLegalParty(
    string LegalName,
    string? LegalNationalId,
    string? EconomicCode,
    string? ResponsiblePersonName,
    PartyBusinessRoles Roles,
    string PrimaryMobile,
    string? AlternateMobile,
    string? Landline,
    string? Email,
    string? Province,
    string? City,
    string? PostalCode,
    string? AddressLine,
    long CreditLimitRial,
    decimal DefaultDiscountPercent,
    DefaultPaymentMethod DefaultPaymentMethod,
    string? BankCardLastFour,
    PartyAcquaintanceSource AcquaintanceSource,
    string? InternalNote,
    PartyCommunicationPreferences CommunicationPreferences);

public sealed record PartyListItem(
    string PartyId, PartyType Type, string DisplayName, PartyBusinessRoles Roles,
    string? PrimaryMobile, string? City, DateTimeOffset CreatedAtUtc);

public sealed record PartyDetails(
    string PartyId, PartyType Type, string DisplayName, PartyBusinessRoles Roles,
    string? NationalIdentifier, string? EconomicCode, string? ResponsiblePersonName,
    IReadOnlyList<ContactPointDto> Contacts, AddressDto? PrimaryAddress,
    long CreditLimitRial, decimal DefaultDiscountPercent,
    DefaultPaymentMethod DefaultPaymentMethod, string? BankCardLastFour,
    PartyAcquaintanceSource AcquaintanceSource, string? InternalNote,
    PartyCommunicationPreferences CommunicationPreferences, DateTimeOffset CreatedAtUtc);

public sealed record ContactPointDto(string Kind, string Value, bool IsPrimary, bool IsMessagingAllowed);
public sealed record AddressDto(string CountryCode, string Province, string City, string PostalCode, string AddressLine);
