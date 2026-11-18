using System.Data.Common;

namespace MDGsm.Persons.Contracts;

public interface IPartyReferenceValidator
{
    Task<bool> IsActiveAsync(Guid partyId, DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
