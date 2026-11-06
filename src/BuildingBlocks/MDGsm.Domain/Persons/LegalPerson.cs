namespace MDGsm.Domain.Persons;

/// <summary>Legal-entity identity attributes. Registration identifiers are a separately validated concern.</summary>
public sealed class LegalPerson : Party
{
    private LegalPerson(
        PartyId id,
        string legalName,
        PartyBusinessRoles businessRoles)
        : base(id, businessRoles)
    {
        if (string.IsNullOrWhiteSpace(legalName))
        {
            throw new ArgumentException("A non-empty legal name is required.", nameof(legalName));
        }

        LegalName = PersianText.Normalize(legalName);
        if (LegalName.Length == 0 || LegalName.Length > 300)
        {
            throw new ArgumentException("A legal name between 1 and 300 characters is required.", nameof(legalName));
        }
    }

    public override PartyType Type => PartyType.LegalPerson;

    public string LegalName { get; }

    public static LegalPerson Create(
        string legalName,
        PartyBusinessRoles businessRoles = PartyBusinessRoles.Customer) =>
        new(PartyId.New(), legalName, businessRoles);

    public static LegalPerson Reconstitute(
        PartyId id,
        string legalName,
        PartyBusinessRoles businessRoles = PartyBusinessRoles.Customer) =>
        new(id, legalName, businessRoles);
}
