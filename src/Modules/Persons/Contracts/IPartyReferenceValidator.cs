using System.Data.Common;

namespace MDGsm.Persons.Contracts;

/// <summary>
/// Persons-owned authority for checking that a Party exists and is active.
/// The check joins the caller's transaction and locks the Party row until that transaction ends.
/// </summary>
public interface IPartyReferenceValidator
{
    Task<bool> IsActiveAsync(
        Guid partyId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
