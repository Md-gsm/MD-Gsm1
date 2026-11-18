namespace MDGsm.Domain.Persons;

/// <summary>
/// Shared identity root for natural persons and legal entities.
/// Addresses, contact points, financial settings and workflow data are separate concerns.
/// </summary>
public abstract class Party
{
    private const PartyBusinessRoles AllowedRoles =
        PartyBusinessRoles.Customer | PartyBusinessRoles.Colleague;

    protected Party(PartyId id, PartyBusinessRoles businessRoles)
    {
        if (id == default)
        {
            throw new ArgumentException("A valid PartyId is required.", nameof(id));
        }

        if (businessRoles == PartyBusinessRoles.None || (businessRoles & ~AllowedRoles) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(businessRoles),
                businessRoles,
                "At least one supported business role must be supplied; unsupported role bits are forbidden.");
        }

        Id = id;
        BusinessRoles = businessRoles;
    }

    public PartyId Id { get; }

    public abstract PartyType Type { get; }

    public PartyBusinessRoles BusinessRoles { get; }

    public bool IsCustomer => BusinessRoles.HasFlag(PartyBusinessRoles.Customer);

    public bool IsColleague => BusinessRoles.HasFlag(PartyBusinessRoles.Colleague);
}
