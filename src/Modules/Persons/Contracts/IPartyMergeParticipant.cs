using System.Data.Common;

namespace MDGsm.Persons.Contracts;

public sealed record PartyMergeContext(Guid OperationId, Guid SourcePartyId, Guid TargetPartyId,
    string ActorId, DateTimeOffset OccurredAtUtc);
public sealed record PartyMergeParticipantPreview(string Module, long ActiveReferences,
    long TransferableReferences, long HistoricalReferences, long BlockingConflicts);

public interface IPartyMergeParticipant
{
    string Module { get; }
    string ContractVersion { get; }
    string MergeProtocolVersion { get; }
    Task<PartyMergeParticipantPreview> PreviewAsync(PartyMergeContext context, DbConnection connection, CancellationToken cancellationToken);
    Task PrepareAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
    Task ValidateAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
    Task ApplyAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
    Task VerifyAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
    Task RecoverAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
}
