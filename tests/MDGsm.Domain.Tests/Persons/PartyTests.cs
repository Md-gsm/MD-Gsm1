using MDGsm.Domain.Persons;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PartyTests
{
    [Fact]
    public void Natural_person_has_party_identity_and_natural_type()
    {
        var party = NaturalPerson.Create(" علی ", " رضایی ");

        Assert.NotEqual(default, party.Id);
        Assert.Equal(PartyType.NaturalPerson, party.Type);
        Assert.Equal("علی", party.GivenName);
        Assert.Equal("رضایی", party.FamilyName);
        Assert.Equal("علی رضایی", party.FullName);
        Assert.True(party.IsCustomer);
        Assert.False(party.IsColleague);
    }

    [Fact]
    public void Legal_person_is_a_party_and_can_be_customer_and_colleague()
    {
        var party = LegalPerson.Create(
            " شرکت نمونه ",
            PartyBusinessRoles.Customer | PartyBusinessRoles.Colleague);

        Assert.Equal(PartyType.LegalPerson, party.Type);
        Assert.Equal("شرکت نمونه", party.LegalName);
        Assert.True(party.IsCustomer);
        Assert.True(party.IsColleague);
    }

    [Fact]
    public void Natural_person_can_be_a_colleague_without_being_a_customer()
    {
        var party = NaturalPerson.Create(
            "علی",
            "رضایی",
            PartyBusinessRoles.Colleague);

        Assert.False(party.IsCustomer);
        Assert.True(party.IsColleague);
    }

    [Theory]
    [InlineData("", "رضایی")]
    [InlineData("علی", " ")]
    public void Natural_person_rejects_missing_name_parts(string givenName, string familyName)
    {
        Assert.Throws<ArgumentException>(() => NaturalPerson.Create(givenName, familyName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Legal_person_rejects_missing_legal_name(string legalName)
    {
        Assert.Throws<ArgumentException>(() => LegalPerson.Create(legalName));
    }

    [Fact]
    public void Reconstitution_rejects_default_party_id()
    {
        Assert.Throws<ArgumentException>(() =>
            NaturalPerson.Reconstitute(default, "علی", "رضایی"));
    }

    [Fact]
    public void Create_rejects_no_business_role_for_both_party_types()
    {
        Assert.Throws<ArgumentException>(() =>
            NaturalPerson.Create("علی", "رضایی", PartyBusinessRoles.None));
        Assert.Throws<ArgumentException>(() =>
            LegalPerson.Create("شرکت نمونه", PartyBusinessRoles.None));
    }

    [Fact]
    public void Reconstitute_rejects_no_business_role_for_both_party_types()
    {
        var id = PartyId.New();
        Assert.Throws<ArgumentException>(() =>
            NaturalPerson.Reconstitute(id, "علی", "رضایی", PartyBusinessRoles.None));
        Assert.Throws<ArgumentException>(() =>
            LegalPerson.Reconstitute(id, "شرکت نمونه", PartyBusinessRoles.None));
    }

    [Fact]
    public void Party_rejects_undefined_business_role_bits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NaturalPerson.Create("علی", "رضایی", (PartyBusinessRoles)8));
    }

    [Fact]
    public void Identity_is_immutable_after_creation()
    {
        var party = NaturalPerson.Create("علی", "رضایی");
        var originalId = party.Id;

        Assert.Equal(originalId, party.Id);
    }
}
