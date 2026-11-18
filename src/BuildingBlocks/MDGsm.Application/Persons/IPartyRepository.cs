using System.Data.Common;

namespace MDGsm.Application.Persons;

public static class PartySearchLimits
{
    /// <summary>Owner-approved ceiling for search results (ADR design on PR #11).</summary>
    public const int MaxResults = 10;
    public const int MinNameTermLength = 2;
    public const int MaxTermLength = 160;
}

/// <summary>Server-side authorization for sensitive Persons data; implementations resolve current IdentityAccess permissions and fail closed.</summary>
public interface IPartySensitiveDataAuthorizer
{
    Task<bool> CanReadSensitivePartyDataAsync(string actorId, CancellationToken cancellationToken);
}

/// <summary>Safe fallback until IdentityAccess permission evaluation is explicitly wired.</summary>
public sealed class DenyByDefaultPartySensitiveDataAuthorizer : IPartySensitiveDataAuthorizer
{
    public Task<bool> CanReadSensitivePartyDataAsync(string actorId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}

public interface IPartyRepository
{
    /// <summary>
    /// Resolves an existing party by normalized mobile inside a caller-owned transaction.
    /// Ambiguous or different-name matches require explicit selection; forceNew is an explicit override.
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
    /// Sensitive fields (national identifier, economic code, credit, card digits, note, full contact values)
    /// are returned only when <paramref name="includeSensitive"/> is true; the caller must have verified that permission server-side.
    /// </summary>
    Task<PartyDetails?> GetAsync(string partyId, string actorId, bool includeSensitive, DbTransaction? transaction,
        CancellationToken cancellationToken);
}
