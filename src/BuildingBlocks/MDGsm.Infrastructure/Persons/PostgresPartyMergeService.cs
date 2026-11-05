using System.Data;
using MDGsm.Persons.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Infrastructure.Persons;

public sealed record PartyMergePreview(
    Guid SourcePartyId,
    Guid TargetPartyId,
    IReadOnlyList<PartyMergeParticipantPreview> Participants,
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

/// <summary>
/// Coordinates all registered consumers in one PostgreSQL transaction. Participants are invoked
/// in stable module-name order and may not commit or use another connection. Historical snapshots
/// remain immutable; only explicitly classified active references may be transferred.
/// </summary>
public sealed class PostgresPartyMergeService
{
    public const string ProtocolVersion = "1.0";
    private readonly NpgsqlDataSource _dataSource;
    private readonly IReadOnlyList<IPartyMergeParticipant> _participants;

    public PostgresPartyMergeService(
        NpgsqlDataSource dataSource,
        IEnumerable<IPartyMergeParticipant> participants)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _participants = (participants ?? throw new ArgumentNullException(nameof(participants)))
            .OrderBy(x => x.Module, StringComparer.Ordinal)
            .ToArray();

        if (_participants.Count == 0 ||
            !_participants.Any(x => string.Equals(x.Module, "Finance", StringComparison.Ordinal) &&
                                    string.Equals(x.ContractVersion, "1.1", StringComparison.Ordinal)) ||
            _participants.Any(x => string.IsNullOrWhiteSpace(x.Module) ||
                                   string.IsNullOrWhiteSpace(x.ContractVersion) ||
                                   !string.Equals(x.MergeProtocolVersion, ProtocolVersion, StringComparison.Ordinal)) ||
            _participants.Select(x => x.Module).Distinct(StringComparer.Ordinal).Count() != _participants.Count)
        {
            throw new InvalidOperationException("Party merge requires the registered Finance 1.1 participant and unique, version-compatible consumer contracts.");
        }
    }

    public async Task<PartyMergePreview> PreviewAsync(
        Guid sourcePartyId, Guid targetPartyId, string actorId,
        CancellationToken cancellationToken = default)
    {
        ValidatePartyPair(sourcePartyId, targetPartyId);
        var actor = ValidateActor(actorId);
        var context = new PartyMergeContext(Guid.CreateVersion7(), sourcePartyId, targetPartyId, actor, DateTimeOffset.UtcNow);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY;", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(cancellationToken);

        await EnsurePreviewPartiesAsync(connection, transaction, sourcePartyId, targetPartyId, cancellationToken);
        var previews = new List<PartyMergeParticipantPreview>(_participants.Count);
        foreach (var participant in _participants)
            previews.Add(await participant.PreviewAsync(context, connection, cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return new PartyMergePreview(sourcePartyId, targetPartyId, previews,
            previews.Sum(x => x.TransferableReferences),
            previews.Sum(x => x.HistoricalReferences),
            previews.Sum(x => x.BlockingConflicts));
    }

    public async Task<PartyMergeResult> MergeAsync(
        Guid sourcePartyId,
        Guid targetPartyId,
        Guid operationId,
        string actorId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidatePartyPair(sourcePartyId, targetPartyId);
        if (operationId == Guid.Empty) throw new ArgumentException("A non-empty merge OperationId is required.", nameof(operationId));
        var actor = ValidateActor(actorId);
        var occurred = nowUtc.ToUniversalTime();
        var context = new PartyMergeContext(operationId, sourcePartyId, targetPartyId, actor, occurred);
        var prepared = new List<IPartyMergeParticipant>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await LockPartiesInStableOrderAsync(connection, transaction, sourcePartyId, targetPartyId, cancellationToken);

            var replay = await FindOperationAsync(connection, transaction, operationId, cancellationToken);
            if (replay is not null)
            {
                if (replay.SourcePartyId != sourcePartyId || replay.TargetPartyId != targetPartyId ||
                    !string.Equals(replay.ActorId, actor, StringComparison.Ordinal))
                    throw new InvalidOperationException("The merge OperationId was already used with a different request.");
                await transaction.CommitAsync(cancellationToken);
                return new PartyMergeResult(operationId, sourcePartyId, targetPartyId, true, replay.CompletedAtUtc);
            }

            await EnsureMergeablePartiesAsync(connection, transaction, sourcePartyId, targetPartyId, cancellationToken);

            foreach (var participant in _participants)
            {
                prepared.Add(participant);
                await participant.PrepareAsync(context, connection, transaction, cancellationToken);
            }
            foreach (var participant in _participants)
                await participant.ValidateAsync(context, connection, transaction, cancellationToken);
            foreach (var participant in _participants)
                await participant.ApplyAsync(context, connection, transaction, cancellationToken);

            await using (var update = new NpgsqlCommand("""
                UPDATE persons
                SET is_active = false,
                    merged_into_party_id = @target,
                    updated_at_utc = @now,
                    updated_by = @actor,
                    row_version = row_version + 1
                WHERE party_id = @source
                  AND is_active = true
                  AND merged_into_party_id IS NULL;
                """, connection, transaction))
            {
                update.Parameters.AddWithValue("source", sourcePartyId);
                update.Parameters.AddWithValue("target", targetPartyId);
                update.Parameters.AddWithValue("now", occurred);
                update.Parameters.AddWithValue("actor", actor);
                if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                    throw new InvalidOperationException("The source Party changed while the merge was being applied.");
            }

            await using (var record = new NpgsqlCommand("""
                INSERT INTO persons_party_merge_records
                    (operation_id, source_party_id, target_party_id, actor_id, protocol_version, completed_at_utc)
                VALUES (@operation, @source, @target, @actor, @protocol, @completed);
                """, connection, transaction))
            {
                record.Parameters.AddWithValue("operation", operationId);
                record.Parameters.AddWithValue("source", sourcePartyId);
                record.Parameters.AddWithValue("target", targetPartyId);
                record.Parameters.AddWithValue("actor", actor);
                record.Parameters.AddWithValue("protocol", ProtocolVersion);
                record.Parameters.AddWithValue("completed", occurred);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var participant in _participants)
                await participant.VerifyAsync(context, connection, transaction, cancellationToken);

            await VerifySourceTransitionAsync(connection, transaction, sourcePartyId, targetPartyId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PartyMergeResult(operationId, sourcePartyId, targetPartyId, false, occurred);
        }
        catch (Exception mergeFailure)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch { /* The original failure remains primary; recovery is still attempted below. */ }

            if (prepared.Count > 0)
            {
                try
                {
                    await using var recoveryConnection = await _dataSource.OpenConnectionAsync(CancellationToken.None);
                    await using var recoveryTransaction = await recoveryConnection.BeginTransactionAsync(CancellationToken.None);
                    foreach (var participant in prepared.AsEnumerable().Reverse())
                        await participant.RecoverAsync(context, recoveryConnection, recoveryTransaction, CancellationToken.None);
                    await recoveryTransaction.CommitAsync(CancellationToken.None);
                }
                catch (Exception recoveryFailure)
                {
                    throw new AggregateException("Party merge failed and a participant recovery contract also failed.",
                        mergeFailure, recoveryFailure);
                }
            }
            throw;
        }
    }

    private static async Task LockPartiesInStableOrderAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid source, Guid target, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT party_id
            FROM persons
            WHERE party_id = ANY(@ids)
            ORDER BY party_id
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = new[] { source, target };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var found = new HashSet<Guid>();
        while (await reader.ReadAsync(cancellationToken)) found.Add(reader.GetGuid(0));
        if (found.Count != 2) throw new InvalidOperationException("Both source and target Parties must exist.");
    }

    private static async Task EnsurePreviewPartiesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid source, Guid target, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT count(*)
            FROM persons
            WHERE party_id = ANY(@ids) AND is_active = true AND merged_into_party_id IS NULL;
            """, connection, transaction);
        command.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = new[] { source, target };
        if ((long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L) != 2L)
            throw new InvalidOperationException("Preview requires two existing active, unmerged Parties.");
    }

    private static async Task EnsureMergeablePartiesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid source, Guid target, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT count(*)
            FROM persons
            WHERE party_id = ANY(@ids) AND is_active = true AND merged_into_party_id IS NULL;
            """, connection, transaction);
        command.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = new[] { source, target };
        if ((long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L) != 2L)
            throw new InvalidOperationException("Only two existing active, unmerged Parties can be merged.");
    }

    private static async Task<MergeOperationRow?> FindOperationAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT source_party_id, target_party_id, actor_id, completed_at_utc
            FROM persons_party_merge_records
            WHERE operation_id = @operation;
            """, connection, transaction);
        command.Parameters.AddWithValue("operation", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new MergeOperationRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3))
            : null;
    }

    private static async Task VerifySourceTransitionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid source, Guid target, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT is_active, merged_into_party_id
            FROM persons
            WHERE party_id = @source;
            """, connection, transaction);
        command.Parameters.AddWithValue("source", source);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetBoolean(0) ||
            reader.IsDBNull(1) || reader.GetGuid(1) != target)
            throw new InvalidOperationException("The source Party merge transition could not be verified.");
    }

    private static void ValidatePartyPair(Guid source, Guid target)
    {
        if (source == Guid.Empty || target == Guid.Empty)
            throw new ArgumentException("Source and target Party identifiers are required.");
        if (source == target)
            throw new ArgumentException("A Party cannot be merged into itself.");
    }

    private static string ValidateActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128 ||
            !string.Equals(actor, actor.Trim(), StringComparison.Ordinal) || actor.Any(char.IsControl))
            throw new ArgumentException("A trusted actor identifier of 1-128 non-control characters is required.", nameof(actor));
        return actor;
    }

    private sealed record MergeOperationRow(
        Guid SourcePartyId, Guid TargetPartyId, string ActorId, DateTimeOffset CompletedAtUtc);
}
