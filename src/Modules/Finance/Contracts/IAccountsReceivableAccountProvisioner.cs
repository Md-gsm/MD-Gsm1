using System.Data.Common;

namespace MDGsm.Finance.Contracts;

/// <summary>
/// Finance-owned, lazy account provisioning contract. Callers must pass a PartyId
/// obtained from the Persons module and the caller-owned transaction used by the
/// first financial operation. Person registration and role assignment must not call this contract.
/// </summary>
public interface IAccountsReceivableAccountProvisioner
{
    /// <summary>
    /// Returns the unique receivables account for a Party, creating it only when first needed.
    /// The supplied transaction remains owned by the caller and is never committed or disposed here.
    /// </summary>
    Task<PartyAccountReference> EnsureAsync(
        Guid partyId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
