using System.Data;
using System.Text.Json;
using MDGsm.Persons.Application;
using Npgsql;

namespace MDGsm.Persons.Infrastructure;

/// <summary>Reads sensitive immutable lifecycle history and records a separate access-audit entry.</summary>
public sealed class PostgresPartyLifecycleHistoryReader(NpgsqlDataSource dataSource) : IPartyLifecycleHistoryReader
{
    public async Task<PartyLifecycleHistoryPage> GetAsync(Guid partyId, int limit,
        PartyLifecycleHistoryCursor? before, string actorId, CancellationToken cancellationToken)
    {
        if (partyId == Guid.Empty) throw new ArgumentException("Party id is required.", nameof(partyId));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        if (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 128 || actorId.Any(char.IsControl))
            throw new ArgumentException("A trusted actor identifier is required.", nameof(actorId));
        if (before is not null && before.HistoryId == Guid.Empty)
            throw new ArgumentException("History cursor id is invalid.", nameof(before));

        var items = new List<PartyLifecycleHistoryItem>(limit);
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = new NpgsqlCommand("""
            WITH RECURSIVE lineage(party_id) AS (
                SELECT @party
                UNION
                SELECT p.party_id FROM persons p JOIN lineage l ON p.merged_into_party_id=l.party_id
            )
            SELECT h.history_id, h.entity_type, h.entity_id, h.party_id, h.row_version,
                   h.change_type, h.actor_id, h.occurred_at_utc, h.reason,
                   h.before_snapshot, h.after_snapshot, h.operation_id
            FROM persons_lifecycle_history h
            WHERE h.party_id IN (SELECT party_id FROM lineage)
              AND (@before_time IS NULL OR (h.occurred_at_utc, h.history_id) < (@before_time, @before_id))
            ORDER BY h.occurred_at_utc DESC, h.history_id DESC
            LIMIT @limit;
            """, connection))
        {
            command.Parameters.AddWithValue("party", partyId);
            command.Parameters.AddWithValue("before_time", (object?)before?.OccurredAtUtc ?? DBNull.Value);
            command.Parameters.AddWithValue("before_id", (object?)before?.HistoryId ?? DBNull.Value);
            command.Parameters.AddWithValue("limit", limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                JsonElement? beforeSnapshot = reader.IsDBNull(9)
                    ? null
                    : JsonDocument.Parse(reader.GetString(9)).RootElement.Clone();
                var afterSnapshot = JsonDocument.Parse(reader.GetString(10)).RootElement.Clone();
                items.Add(new PartyLifecycleHistoryItem(
                    reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetGuid(3),
                    reader.GetInt64(4), reader.GetString(5), reader.GetString(6),
                    reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8),
                    beforeSnapshot, afterSnapshot, reader.IsDBNull(11) ? null : reader.GetGuid(11)));
            }
        }

        await AppendAccessAuditAsync(partyId, items.Select(x => x.PartyId).Append(partyId).Distinct().ToArray(),
            actorId.Trim(), cancellationToken);
        var next = items.Count == limit
            ? new PartyLifecycleHistoryCursor(items[^1].OccurredAtUtc, items[^1].HistoryId)
            : null;
        return new PartyLifecycleHistoryPage(items, next);
    }

    private async Task AppendAccessAuditAsync(Guid requestedParty, IReadOnlyCollection<Guid> sourceParties,
        string actor, CancellationToken cancellationToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString) { Pooling = false }.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        foreach (var party in sourceParties)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO persons_access_audit (audit_id, party_id, actor, action, sensitive_included)
                VALUES (@audit, @party, @actor, 'history-read', true);
                """, connection, transaction);
            command.Parameters.AddWithValue("audit", Guid.CreateVersion7());
            command.Parameters.AddWithValue("party", party);
            command.Parameters.AddWithValue("actor", actor);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
