namespace MDGsm.Domain.Persons;

/// <summary>Natural-person identity attributes. Contact and financial data are not stored here.</summary>
public sealed class NaturalPerson : Party
{
    private NaturalPerson(
        PartyId id,
        string givenName,
        string familyName,
        PartyBusinessRoles businessRoles)
        : base(id, businessRoles)
    {
        GivenName = RequireText(givenName, nameof(givenName));
        FamilyName = RequireText(familyName, nameof(familyName));
    }

    public override PartyType Type => PartyType.NaturalPerson;

    public string GivenName { get; }

    public string FamilyName { get; }

    public string FullName => $"{GivenName} {FamilyName}";

    public static NaturalPerson Create(
        string givenName,
        string familyName,
        PartyBusinessRoles businessRoles = PartyBusinessRoles.Customer) =>
        new(PartyId.New(), givenName, familyName, businessRoles);

    public static NaturalPerson Reconstitute(
        PartyId id,
        string givenName,
        string familyName,
        PartyBusinessRoles businessRoles = PartyBusinessRoles.Customer) =>
        new(id, givenName, familyName, businessRoles);

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }
}
