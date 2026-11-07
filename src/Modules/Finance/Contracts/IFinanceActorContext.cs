namespace MDGsm.Finance.Contracts;

/// <summary>
/// Server-side authenticated actor context. Implementations must derive ActorId from
/// the authenticated principal, never from request payloads or client-controlled headers.
/// </summary>
public interface IFinanceActorContext
{
    string? ActorId { get; }
}
