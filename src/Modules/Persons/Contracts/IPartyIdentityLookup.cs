namespace MDGsm.Persons.Contracts;

/// <summary>
/// Read-only identity boundary for modules that hold a durable PartyId reference.
/// Implementations must treat inactive parties as existing identities; deactivation
/// must not invalidate historical financial references.
/// </summary>
public interface IPartyIdentityLookup
{
    Task<bool> ExistsAsync(Guid partyId, CancellationToken cancellationToken = default);
}
