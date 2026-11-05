using System.Data.Common;

namespace MDGsm.Persons.Contracts;

/// <summary>
/// Read-only identity boundary for modules that hold a durable PartyId reference.
/// The caller supplies its transaction so a same-operation identity creation can be
/// observed atomically. Inactive parties still exist; deactivation does not invalidate history.
/// </summary>
public interface IPartyIdentityLookup
{
    Task<bool> ExistsAsync(
        Guid partyId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
