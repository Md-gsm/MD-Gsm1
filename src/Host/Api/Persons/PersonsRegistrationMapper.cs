using MDGsm.Application.Persons;
using MDGsm.Domain.Persons;
using MDGsm.Persons.Contracts;

namespace MDGsm.Api.Persons;

/// <summary>Strictly maps versioned HTTP registration contracts to application commands.</summary>
public static class PersonsRegistrationMapper
{
    public static RegisterNaturalParty Map(CreateNaturalPartyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new RegisterNaturalParty(
            request.GivenName, request.FamilyName, ParseRoles(request.Roles), request.NationalId,
            request.PrimaryMobile, request.AlternateMobile, request.Landline, request.Email,
            request.Address?.Province, request.Address?.City, request.Address?.PostalCode, request.Address?.AddressLine,
            ParseEnum(request.AcquaintanceSource, PartyAcquaintanceSource.Unknown),
            request.InternalNote, MapCommunication(request.Communication),
            ParseOptionalEnum<WorkClassification>(request.WorkClassification),
            request.Address?.CountryCode ?? "IR");
    }

    public static RegisterLegalParty Map(CreateLegalPartyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.IsNullOrWhiteSpace(request.WorkClassification))
            throw new ArgumentException("Work classification is not valid for a legal person.", nameof(request));

        return new RegisterLegalParty(
            request.LegalName, request.LegalNationalId, request.EconomicCode, request.ResponsiblePersonName,
            ParseRoles(request.Roles), request.PrimaryMobile, request.AlternateMobile, request.Landline, request.Email,
            request.Address?.Province, request.Address?.City, request.Address?.PostalCode, request.Address?.AddressLine,
            ParseEnum(request.AcquaintanceSource, PartyAcquaintanceSource.Unknown),
            request.InternalNote, MapCommunication(request.Communication), null,
            request.Address?.CountryCode ?? "IR");
    }

    private static PartyCommunicationPreferences MapCommunication(
        PartyCommunicationPreferencesContract? communication) =>
        communication is null
            ? PartyCommunicationPreferences.Default
            : new PartyCommunicationPreferences(
                communication.RepairStatusSmsEnabled,
                communication.ReadyForDeliverySmsEnabled,
                communication.PromotionsSmsEnabled);

    private static PartyBusinessRoles ParseRoles(string[]? values)
    {
        if (values is null || values.Length == 0)
            throw new ArgumentException("At least one supported commercial role is required.", nameof(values));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var roles = PartyBusinessRoles.None;
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                throw new ArgumentException("Commercial roles must be non-empty and unique.", nameof(values));

            roles |= value switch
            {
                "Customer" => PartyBusinessRoles.Customer,
                "Colleague" => PartyBusinessRoles.Colleague,
                _ => throw new ArgumentException("An unknown commercial role was supplied.", nameof(values))
            };
        }
        return roles;
    }

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum =>
        ParseOptionalEnum<T>(value) ?? fallback;

    private static T? ParseOptionalEnum<T>(string? value) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        foreach (var name in Enum.GetNames<T>())
            if (string.Equals(name, value, StringComparison.Ordinal))
                return Enum.Parse<T>(value, ignoreCase:false);
        throw new ArgumentException("An unknown enumeration value was supplied.", nameof(value));
    }
}
