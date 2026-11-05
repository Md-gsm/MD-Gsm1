using System.Data.Common;

namespace MDGsm.Persons.Contracts;

/// <summary>Immutable context shared by every registered participant in one Party merge operation.</summary>
public sealed record PartyMergeContext(
    Guid OperationId,
    Guid SourcePartyId,
    Guid TargetPartyId,
    string ActorId,
    DateTimeOffset OccurredAtUtc);

/// <summary>PII-free counts returned by a participant's read-only merge preview.</summary>
public sealed record PartyMergeParticipantPreview(
    string Module,
    long ActiveReferences,
    long TransferableReferences,
    long HistoricalReferences,
    long BlockingConflicts);

/// <summary>
/// Versioned participant contract. Implementations must use the supplied connection/transaction
/// and must not commit, open independent transactions, or mutate historical snapshots.
/// </summary>
public interface IPartyMergeParticipant
{
    string Module { get; }
    string ContractVersion { get; }
    string MergeProtocolVersion { get; }

    Task<PartyMergeParticipantPreview> PreviewAsync(
        PartyMergeContext context, DbConnection connection, CancellationToken cancellationToken);

    Task PrepareAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);

    Task ValidateAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);

    Task ApplyAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);

    Task VerifyAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);

    Task RecoverAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
}
