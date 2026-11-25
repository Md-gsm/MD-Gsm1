using MDGsm.Persons.Contracts;

namespace MDGsm.Persons.Application;

public sealed record PartyMergePreview(
    Guid SourcePartyId,
    Guid TargetPartyId,
    IReadOnlyList<PartyMergeParticipantPreview> Participants,
    long ActiveReferences,
    long TransferableReferences,
    long HistoricalReferences,
    long BlockingConflicts)
{
    public bool CanMerge => BlockingConflicts == 0;
}

public sealed record PartyMergeResult(
    Guid OperationId,
    Guid SourcePartyId,
    Guid TargetPartyId,
    bool Replayed,
    DateTimeOffset CompletedAtUtc);

/// <summary>Application-facing port for the transactional, registered-consumer merge coordinator.</summary>
public interface IPartyMergeService
{
    Task<PartyMergePreview> PreviewAsync(
        Guid sourcePartyId, Guid targetPartyId, string actorId,
        CancellationToken cancellationToken = default);

    Task<PartyMergeResult> MergeAsync(
        Guid sourcePartyId, Guid targetPartyId, Guid operationId, string actorId,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
