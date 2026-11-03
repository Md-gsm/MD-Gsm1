namespace MDGsm.Persons.Contracts;

public sealed record PartyResponse(
    string PartyId,
    string PartyType,
    string DisplayName,
    string[] Roles,
    string? NationalIdentifier,
    string? EconomicCode,
    string? ResponsiblePersonName,
    IReadOnlyList<PartyContactContract> Contacts,
    PartyAddressContract? PrimaryAddress,
    PartyFinancialProfileContract Financial,
    PartyCommunicationPreferencesContract Communication,
    string? AcquaintanceSource,
    DateTimeOffset CreatedAtUtc);

public sealed record PartyContactContract(string Kind, string Value, bool IsPrimary, bool IsMessagingAllowed);

public sealed record PartyAddressContract(
    string CountryCode,
    string Province,
    string City,
    string PostalCode,
    string AddressLine);

public sealed record PartyFinancialProfileContract(
    long CreditLimitRial,
    decimal DefaultDiscountPercent,
    string DefaultPaymentMethod,
    string? BankCardLastFour);

public sealed record PartyCommunicationPreferencesContract(
    bool RepairStatusSmsEnabled,
    bool ReadyForDeliverySmsEnabled,
    bool PromotionsSmsEnabled);

public sealed record PartyListResponse(
    IReadOnlyList<PartyListItemContract> Items,
    int Limit,
    bool HasMore);

public sealed record PartyListItemContract(
    string PartyId,
    string PartyType,
    string DisplayName,
    string[] Roles,
    string? PrimaryMobile,
    string? City,
    DateTimeOffset CreatedAtUtc);
