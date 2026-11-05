using System.Data.Common;
using MDGsm.Finance.Domain;

namespace MDGsm.Finance.Application;

/// <summary>Persistence port; implementations must use, not own, the supplied transaction.</summary>
public interface IPartyAccountRepository
{
    Task<PartyAccount> GetOrCreateAsync(
        Guid partyId,
        string actorId,
        DbTransaction transaction,
        CancellationToken cancellationToken);
}
