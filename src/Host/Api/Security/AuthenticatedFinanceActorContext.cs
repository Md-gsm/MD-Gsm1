using MDGsm.Finance.Contracts;

namespace MDGsm.Api.Security;

/// <summary>Maps the validated session account to Finance's trusted actor boundary.</summary>
public sealed class AuthenticatedFinanceActorContext(IAuthenticatedActorContext authenticatedActor)
    : IFinanceActorContext
{
    public string? ActorId => authenticatedActor.AccountId.ToString("D");
}
