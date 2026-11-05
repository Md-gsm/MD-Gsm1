using MDGsm.Finance.Contracts;
using MDGsm.Persons.Contracts;

namespace MDGsm.Finance.Application;

/// <summary>
/// Idempotent use case for lazy receivables-account creation. The first financial use
/// supplies the caller transaction; no account is created during Persons registration.
/// </summary>
public sealed class AccountsReceivableAccountProvisioner(
    IPartyAccountRepository repository,
    IFinanceActorContext actorContext,
    IPartyIdentityLookup partyIdentityLookup) : IAccountsReceivableAccountProvisioner
{
    public async Task<PartyAccountReference> EnsureAsync(
        Guid partyId,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Empty)
            throw new ArgumentException("A non-empty canonical PartyId is required.", nameof(partyId));

        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction.Connection is not { State: System.Data.ConnectionState.Open })
            throw new ArgumentException("The caller-owned transaction must be active on an open connection.", nameof(transaction));

        var actorId = actorContext.ActorId;
        if (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 128)
            throw new InvalidOperationException("A valid authenticated Finance actor context is required.");

        if (!await partyIdentityLookup.ExistsAsync(partyId, cancellationToken))
            throw new KeyNotFoundException("The supplied PartyId does not exist in the Persons identity boundary.");

        var account = await repository.GetOrCreateAsync(
            partyId, actorId, transaction, cancellationToken);

        return new PartyAccountReference(
            account.AccountId,
            account.PartyId,
            (AccountsReceivableAccountStatus)account.Status,
            account.CreatedAtUtc);
    }
}
