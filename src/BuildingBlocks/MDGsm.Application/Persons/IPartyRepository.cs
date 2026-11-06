using System.Data.Common;

namespace MDGsm.Application.Persons;

public static class PartySearchLimits
{
    /// <summary>Owner-approved ceiling for search results (ADR design on PR #11).</summary>
    public const int MaxResults = 10;
    public const int MinNameTermLength = 2;
    public const int MaxTermLength = 160;
}

public interface IPartyRepository
{
    /// <summary>
    /// Quick registration resolves an existing exact mobile under a transaction-scoped mobile lock.
    /// Ambiguous or different-name matches are returned for explicit user choice; forceNew is an explicit override.
    /// </summary>
    Task<PartyQuickRegistrationResult> QuickRegisterNaturalAsync(RegisterNaturalParty request, string actorId,
        Guid operationId, bool forceNew, string? selectedPartyId, DbTransaction transaction, CancellationToken cancellationToken);

    Task<PartyQuickRegistrationResult> QuickRegisterLegalAsync(RegisterLegalParty request, string actorId,
        Guid operationId, bool forceNew, string? selectedPartyId, DbTransaction transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Registers a natural person idempotently by <paramref name="operationId"/> (V1: OperationId only, no payload comparison).
    /// When <paramref name="transaction"/> is supplied the write joins that caller-owned transaction (ADR-0005) and is not committed here.
    /// </summary>
    Task<PartyRegistrationResult> AddNaturalAsync(RegisterNaturalParty request, string actorId, Guid operationId,
        DbTransaction? transaction, CancellationToken cancellationToken);

    Task<PartyRegistrationResult> AddLegalAsync(RegisterLegalParty request, string actorId, Guid operationId,
        DbTransaction? transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Search by name (at least two characters) or by the last four digits of a mobile number.
    /// A term is required, at most <see cref="PartySearchLimits.MaxResults"/> results are returned, and mobile numbers are masked.
    /// </summary>
    Task<IReadOnlyList<PartyListItem>> SearchAsync(string term, int limit, DbTransaction? transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one party. Every read is recorded in the append-only access audit with <paramref name="actorId"/>.
    /// Sensitive fields are returned only with a server-created authorization capability. Callers cannot
    /// manufacture that capability; use PartyDetailsQueryService so policy is checked before repository access.
    /// </summary>
    Task<PartyDetails?> GetAsync(string partyId, string actorId, PartyReadAuthorization authorization,
        DbTransaction? transaction, CancellationToken cancellationToken);
}
