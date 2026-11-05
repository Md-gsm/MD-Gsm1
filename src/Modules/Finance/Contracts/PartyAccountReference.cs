namespace MDGsm.Finance.Contracts;

/// <summary>Stable reference to a Finance-owned receivables detail account.</summary>
public sealed record PartyAccountReference(
    Guid AccountId,
    Guid PartyId,
    AccountsReceivableAccountStatus Status,
    DateTimeOffset CreatedAtUtc);

/// <summary>Lifecycle state of a party's dedicated receivables account.</summary>
public enum AccountsReceivableAccountStatus : short
{
    Active = 1,
    MergeTransferPending = 2,
    Closed = 3
}
