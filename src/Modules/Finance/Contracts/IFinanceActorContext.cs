namespace MDGsm.Finance.Contracts;

/// <summary>Actor identity derived from the authenticated server-side principal.</summary>
public interface IFinanceActorContext
{
    string? ActorId { get; }
}
