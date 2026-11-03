namespace MDGsm.Application.Persons;

public interface IPartyRepository
{
    Task<string> AddNaturalAsync(RegisterNaturalParty request, string actorId, CancellationToken cancellationToken);
    Task<string> AddLegalAsync(RegisterLegalParty request, string actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PartyListItem>> SearchAsync(string? query, int limit, CancellationToken cancellationToken);
    Task<PartyDetails?> GetAsync(string partyId, CancellationToken cancellationToken);
}
