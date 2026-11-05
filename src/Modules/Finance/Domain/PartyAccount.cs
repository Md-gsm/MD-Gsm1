namespace MDGsm.Finance.Domain;

/// <summary>Finance-owned dedicated receivables detail account for one canonical Party.</summary>
public sealed record PartyAccount(
    Guid AccountId,
    Guid PartyId,
    PartyAccountStatus Status,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy);

public enum PartyAccountStatus : short
{
    Active = 1,
    MergeTransferPending = 2,
    Closed = 3
}
