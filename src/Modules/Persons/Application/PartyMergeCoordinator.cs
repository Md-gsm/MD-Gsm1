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

/// <summary>Application-owned boundary for the transactional Persons merge coordinator.</summary>
public interface IPartyMergeCoordinator
{
    Task<PartyMergePreview> PreviewAsync(
        Guid sourcePartyId, Guid targetPartyId, string actorId,
        CancellationToken cancellationToken = default);

    Task<PartyMergeResult> MergeAsync(
        Guid sourcePartyId, Guid targetPartyId, Guid operationId, string actorId,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
