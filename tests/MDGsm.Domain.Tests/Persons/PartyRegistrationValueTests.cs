using MDGsm.Domain.Persons;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PartyRegistrationValueTests
{
    [Fact]
    public void Iranian_digits_are_normalized_to_ascii()
    {
        Assert.Equal("09121234567", PartyIdentifiers.NormalizeDigits("۰۹۱۲۱۲۳۴۵۶۷"));
        Assert.Equal("09121234567", PartyIdentifiers.NormalizeDigits("٠٩١٢١٢٣٤٥٦٧"));
    }

    [Fact]
    public void Iranian_mobile_is_normalized_to_e164()
    {
        var contact = PartyContactPoint.Mobile("۰۹۱۲۱۲۳۴۵۶۷", isPrimary: true, isMessagingAllowed: true);

        Assert.Equal(ContactPointKind.Mobile, contact.Kind);
        Assert.Equal("+989121234567", contact.Value);
        Assert.True(contact.IsPrimary);
        Assert.True(contact.IsMessagingAllowed);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("09ABC123456")]
    [InlineData("")]
    public void Invalid_mobile_is_rejected(string value)
    {
        Assert.Throws<ArgumentException>(() => PartyContactPoint.Mobile(value));
    }

    [Fact]
    public void Iranian_national_id_accepts_ten_digits_and_normalizes_digits()
    {
        Assert.Equal("0123456789", PartyIdentifiers.NormalizeIranianNationalId("۰۱۲۳۴۵۶۷۸۹"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345678901")]
    [InlineData("abcdefghij")]
    public void Invalid_national_id_format_is_rejected(string value)
    {
        Assert.Throws<ArgumentException>(() => PartyIdentifiers.NormalizeIranianNationalId(value));
    }

    [Fact]
    public void Legal_national_id_requires_eleven_digits()
    {
        Assert.Equal("01234567890", PartyIdentifiers.NormalizeIranianLegalNationalId("۰۱۲۳۴۵۶۷۸۹۰"));
    }

    [Fact]
    public void Economic_code_accepts_ten_to_fourteen_digits()
    {
        Assert.Equal("1234567890", PartyIdentifiers.NormalizeEconomicCode("۱۲۳۴۵۶۷۸۹۰"));
        Assert.Throws<ArgumentException>(() => PartyIdentifiers.NormalizeEconomicCode("123"));
    }

    [Fact]
    public void Address_defaults_to_iran_and_normalizes_postal_code()
    {
        var address = PartyAddress.Create("تهران", "تهران", "۱۲۳۴۵۶۷۸۹۰", "خیابان نمونه");

        Assert.Equal("IR", address.CountryCode);
        Assert.Equal("1234567890", address.PostalCode);
    }

    [Fact]
    public void Financial_profile_keeps_only_last_four_card_digits()
    {
        var profile = PartyFinancialProfile.Create(2_500_000, 5.5m, DefaultPaymentMethod.CardToCard, "۱۲۳۴");

        Assert.Equal(2_500_000, profile.CreditLimitRial);
        Assert.Equal(5.5m, profile.DefaultDiscountPercent);
        Assert.Equal("1234", profile.BankCardLastFour);
    }

    [Fact]
    public void Financial_profile_rejects_full_card_number_and_invalid_ranges()
    {
        Assert.Throws<ArgumentException>(() => PartyFinancialProfile.Create(bankCardLastFour: "1234567890123456"));
        Assert.Throws<ArgumentOutOfRangeException>(() => PartyFinancialProfile.Create(creditLimitRial: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PartyFinancialProfile.Create(defaultDiscountPercent: 101));
    }

    [Fact]
    public void Promotions_sms_are_disabled_by_default()
    {
        Assert.False(PartyCommunicationPreferences.Default.PromotionsSmsEnabled);
        Assert.True(PartyCommunicationPreferences.Default.RepairStatusSmsEnabled);
        Assert.True(PartyCommunicationPreferences.Default.ReadyForDeliverySmsEnabled);
    }

    [Fact]
    public void Registration_requires_at_least_one_supported_role()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PartyRegistrationRules.ValidateRoles(PartyBusinessRoles.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => PartyRegistrationRules.ValidateRoles((PartyBusinessRoles)8));
    }
}
