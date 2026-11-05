using System.Data.Common;

namespace MDGsm.Application.Persons;

/// <summary>Supplies the authenticated actor established by the server authentication boundary.</summary>
public interface IPartyActorContext
{
    string ActorId { get; }
}

/// <summary>Server-side policy boundary for access to personally identifiable and financial details.</summary>
public interface IPartySensitiveReadAuthorizer
{
    Task<bool> CanReadSensitiveAsync(string authenticatedActorId, string partyId, CancellationToken cancellationToken);
}

/// <summary>
/// A short-lived, actor- and party-bound capability. Its constructor is internal so API/client code
/// cannot turn a client-supplied boolean into authorization.
/// </summary>
public sealed class PartyReadAuthorization
{
    internal PartyReadAuthorization(string actorId, string partyId, bool includeSensitive)
    {
        ActorId = actorId;
        PartyId = partyId;
        IncludeSensitive = includeSensitive;
    }

    public string ActorId { get; }
    public string PartyId { get; }
    public bool IncludeSensitive { get; }
}

/// <summary>
/// The only application entry point for party detail reads. Missing/failed policy evaluation never
/// grants sensitive access. The authorization capability is bound to both the authenticated actor
/// and requested party before it reaches Infrastructure.
/// </summary>
public sealed class PartyDetailsQueryService(
    IPartyRepository repository,
    IPartySensitiveReadAuthorizer authorizer,
    IPartyActorContext actorContext)
{
    public async Task<PartyDetails?> GetAsync(
        string partyId,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        var authenticatedActorId = actorContext.ActorId;
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedActorId);

        var allowed = await authorizer.CanReadSensitiveAsync(
            authenticatedActorId, partyId, cancellationToken);

        var authorization = new PartyReadAuthorization(
            authenticatedActorId, partyId, allowed);

        return await repository.GetAsync(
            partyId, authenticatedActorId, authorization, transaction, cancellationToken);
    }
}
