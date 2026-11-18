namespace MDGsm.Persons.Contracts;

/// <summary>Stable identity reference issued by the Persons boundary for other modules.</summary>
public sealed record PartyReference
{
    public Guid PartyId { get; }

    public PartyReference(Guid partyId)
    {
        if (partyId == Guid.Empty)
            throw new ArgumentException("PartyId must not be empty.", nameof(partyId));
        PartyId = partyId;
    }
}
