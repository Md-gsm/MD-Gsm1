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
    [InlineData("09121234567")]
    [InlineData("+989121234567")]
    [InlineData("00989121234567")]
    [InlineData("989121234567")]
    [InlineData("0912 123 4567")]
    [InlineData("0912-123-4567")]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷")]
    public void Iranian_mobile_forms_normalize_to_the_same_e164_value(string value)
    {
        Assert.Equal("+989121234567", PartyContactPoint.Mobile(value).Value);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("09ABC123456")]
    [InlineData("")]
    [InlineData("02112345678")]      // Tehran landline must not be accepted as a mobile
    [InlineData("+982112345678")]
    [InlineData("00982112345678")]
    [InlineData("9121234567")]       // missing leading 0 / country code
    [InlineData("0912123456")]       // too short
    [InlineData("091212345678")]     // too long
    [InlineData("+9812")]
    public void Invalid_mobile_is_rejected(string value)
    {
        Assert.Throws<ArgumentException>(() => PartyContactPoint.Mobile(value));
    }

    [Theory]
    [InlineData("+14155552671", "+14155552671")]
    [InlineData("0014155552671", "+14155552671")]
    public void Valid_international_mobile_is_kept_in_e164(string value, string expected)
    {
        Assert.Equal(expected, PartyContactPoint.Mobile(value).Value);
    }

    [Fact]
    public void Persian_text_unifies_arabic_variants_and_removes_invisible_marks()
    {
        Assert.Equal(PersianTestData.AliRezaeiCanonical,
            PersianText.Normalize("  " + PersianTestData.AliArabicYeh + "\u200F   " + PersianTestData.RezaeiArabicYeh + " "));
        Assert.Equal(PersianTestData.KarimCanonical, PersianText.Normalize(PersianTestData.KarimArabic));
        Assert.Equal(PersianTestData.WithZwnj, PersianText.Normalize(PersianTestData.WithZwnj));
        Assert.Equal(PersianTestData.MohammadCanonical, PersianText.Normalize(PersianTestData.MohammadWithTatweel));
    }

    [Fact]
    public void Natural_and_legal_names_are_stored_in_persian_canonical_form()
    {
        Assert.Equal(PersianTestData.AliRezaeiCanonical,
            NaturalPerson.Create(PersianTestData.AliArabicYeh, PersianTestData.RezaeiArabicYeh).FullName);
        Assert.Equal(PersianTestData.CompanyCanonical, LegalPerson.Create(PersianTestData.CompanyArabic).LegalName);
    }

    [Fact]
    public void Masks_keep_only_the_documented_suffix()
    {
        Assert.Equal("***********67", PartyIdentifiers.MaskPhone("+989121234567"));
        Assert.Equal("*******789", PartyIdentifiers.MaskIdentifier("0123456789"));
        Assert.Equal("***@example.com", PartyIdentifiers.MaskEmail("user@example.com"));
        Assert.Equal("**", PartyIdentifiers.MaskPhone("12"));
    }

    [Fact]
    public void Work_classification_is_limited_to_natural_colleagues()
    {
        PartyRegistrationRules.ValidateWorkClassification(PartyType.NaturalPerson, PartyBusinessRoles.Colleague, WorkClassification.Technician);
        PartyRegistrationRules.ValidateWorkClassification(PartyType.NaturalPerson, PartyBusinessRoles.Customer, null);
        Assert.Throws<ArgumentException>(() =>
            PartyRegistrationRules.ValidateWorkClassification(PartyType.NaturalPerson, PartyBusinessRoles.Customer, WorkClassification.Staff));
        Assert.Throws<ArgumentException>(() =>
            PartyRegistrationRules.ValidateWorkClassification(PartyType.LegalPerson, PartyBusinessRoles.Colleague, WorkClassification.Staff));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PartyRegistrationRules.ValidateWorkClassification(PartyType.NaturalPerson, PartyBusinessRoles.Colleague, (WorkClassification)99));
    }

    [Fact]
    public void Discount_with_more_than_two_decimals_is_rejected_instead_of_being_rounded()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PartyFinancialProfile.Create(defaultDiscountPercent: 12.345m));
        Assert.Equal(12.34m, PartyFinancialProfile.Create(defaultDiscountPercent: 12.34m).DefaultDiscountPercent);
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
