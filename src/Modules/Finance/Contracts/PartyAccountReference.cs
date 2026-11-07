namespace MDGsm.Finance.Contracts;

/// <summary>Reference to a Finance-owned Party subledger account, not a balance or GL account code.</summary>
public sealed record PartyAccountReference(Guid AccountId, Guid PartyId, DateTime CreatedAtUtc);
