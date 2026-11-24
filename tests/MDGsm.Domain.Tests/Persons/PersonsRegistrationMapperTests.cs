using MDGsm.Api.Persons;
using MDGsm.Domain.Persons;
using MDGsm.Persons.Contracts;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsRegistrationMapperTests
{
    [Fact]
    public void Natural_registration_maps_multiple_roles_and_address_country()
    {
        var request = new CreateNaturalPartyRequest(
            "علی", "رضایی", ["Customer", "Colleague"], "0123456789",
            "09125550123", null, null, "ali@example.test",
            new PartyAddressContract("IR", "تهران", "تهران", "1234567890", "خیابان نمونه"),
            new PartyCommunicationPreferencesContract(true, true, false),
            "PersonalReferral", "یادداشت داخلی", "Technician");

        var mapped = PersonsRegistrationMapper.Map(request);
        Assert.Equal(PartyBusinessRoles.Customer | PartyBusinessRoles.Colleague, mapped.Roles);
        Assert.Equal(PartyAcquaintanceSource.PersonalReferral, mapped.AcquaintanceSource);
        Assert.Equal(WorkClassification.Technician, mapped.WorkClassification);
        Assert.Equal("IR", mapped.CountryCode);
        Assert.Equal("تهران", mapped.City);
        Assert.False(mapped.CommunicationPreferences.PromotionsSmsEnabled);
    }

    [Fact]
    public void Legal_registration_maps_identity_without_work_classification()
    {
        var request = new CreateLegalPartyRequest(
            "شرکت نمونه", "12345678901", "12345678901234", "مسئول نمونه",
            ["Customer"], null, null, null, "office@example.test",
            new PartyAddressContract("IR", "تهران", "کرج", "1234567890", "نشانی نمونه"),
            new PartyCommunicationPreferencesContract(false, true, false), "WalkIn", null);

        var mapped = PersonsRegistrationMapper.Map(request);
        Assert.Equal(PartyBusinessRoles.Customer, mapped.Roles);
        Assert.Equal(PartyAcquaintanceSource.WalkIn, mapped.AcquaintanceSource);
        Assert.Null(mapped.WorkClassification);
        Assert.Equal("IR", mapped.CountryCode);
    }

    [Fact]
    public void Mapper_rejects_unknown_or_duplicate_roles_and_numeric_enum_values()
    {
        var request = new CreateNaturalPartyRequest(
            "علی", "رضایی", ["Customer"], null, null, null, null, null, null,
            new PartyCommunicationPreferencesContract(true, true, false), null, null);

        Assert.Throws<ArgumentException>(() => PersonsRegistrationMapper.Map(request with { Roles = ["Customer", "Customer"] }));
        Assert.Throws<ArgumentException>(() => PersonsRegistrationMapper.Map(request with { Roles = ["Administrator"] }));
        Assert.Throws<ArgumentException>(() => PersonsRegistrationMapper.Map(request with { AcquaintanceSource = "4" }));
    }

    [Fact]
    public void Legal_registration_rejects_work_classification()
    {
        var request = new CreateLegalPartyRequest(
            "شرکت نمونه", null, null, null, ["Customer"], null, null, null, null, null,
            new PartyCommunicationPreferencesContract(true, true, false), null, null, "Technician");
        Assert.Throws<ArgumentException>(() => PersonsRegistrationMapper.Map(request));
    }
}
