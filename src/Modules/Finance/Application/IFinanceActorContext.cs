namespace MDGsm.Finance.Application;

/// <summary>Authenticated server-side actor identity supplied by the host composition root.</summary>
public interface IFinanceActorContext
{
    string ActorId { get; }
}
