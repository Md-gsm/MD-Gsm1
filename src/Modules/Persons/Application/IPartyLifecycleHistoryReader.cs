using System.Text.Json;

namespace MDGsm.Persons.Application;

public sealed record PartyLifecycleHistoryCursor(DateTimeOffset OccurredAtUtc, Guid HistoryId);

public sealed record PartyLifecycleHistoryItem(
    Guid HistoryId, string EntityType, Guid EntityId, Guid PartyId, long RowVersion,
    string ChangeType, string ActorId, DateTimeOffset OccurredAtUtc, string Reason,
    JsonElement? BeforeSnapshot, JsonElement AfterSnapshot, Guid? OperationId);

public sealed record PartyLifecycleHistoryPage(
    IReadOnlyList<PartyLifecycleHistoryItem> Items, PartyLifecycleHistoryCursor? NextCursor);

public interface IPartyLifecycleHistoryReader
{
    Task<PartyLifecycleHistoryPage> GetAsync(
        Guid partyId, int limit, PartyLifecycleHistoryCursor? before, string actorId,
        CancellationToken cancellationToken);
}
