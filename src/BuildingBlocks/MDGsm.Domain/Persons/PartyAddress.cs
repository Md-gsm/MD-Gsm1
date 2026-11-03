namespace MDGsm.Domain.Persons;

public sealed record PartyAddress
{
    private PartyAddress(string countryCode, string province, string city, string postalCode, string addressLine, bool isPrimary)
    {
        CountryCode = countryCode;
        Province = province;
        City = city;
        PostalCode = postalCode;
        AddressLine = addressLine;
        IsPrimary = isPrimary;
    }

    public string CountryCode { get; }
    public string Province { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string AddressLine { get; }
    public bool IsPrimary { get; }

    public static PartyAddress Create(
        string province, string city, string postalCode, string addressLine,
        string countryCode = "IR", bool isPrimary = true)
    {
        static string Required(string? value, string name, int max)
        {
            var text = value?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length > max)
                throw new ArgumentException($"A value between 1 and {max} characters is required.", name);
            return text;
        }

        var country = Required(countryCode, nameof(countryCode), 2).ToUpperInvariant();
        if (country.Length != 2 || country.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Country code must be ISO alpha-2.", nameof(countryCode));

        var postal = PartyIdentifiers.NormalizeDigits(Required(postalCode, nameof(postalCode), 20)).Replace(" ", "");
        if (postal.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Postal code must contain digits only.", nameof(postalCode));

        return new PartyAddress(country,
            Required(province, nameof(province), 100),
            Required(city, nameof(city), 100),
            postal,
            Required(addressLine, nameof(addressLine), 1000),
            isPrimary);
    }
}
