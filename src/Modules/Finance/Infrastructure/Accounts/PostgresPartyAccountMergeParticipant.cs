using System.Data.Common;
using MDGsm.Persons.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Finance.Infrastructure.Accounts;

/// <summary>
/// Finance's registered merge participant. The current Finance PartyId consumer is the unique
/// subledger-account identity. A source account transfers only when the target has no account;
/// two existing accounts are an explicit blocking conflict. Ledger history is linked by AccountId
/// and is never rewritten by this participant.
/// </summary>
public sealed class PostgresPartyAccountMergeParticipant : IPartyMergeParticipant
{
    public string Module => "Finance";
    public string ContractVersion => "1.1";
    public string MergeProtocolVersion => "1.0";

    public async Task<PartyMergeParticipantPreview> PreviewAsync(
        PartyMergeContext context, DbConnection connection, CancellationToken cancellationToken)
    {
        var (source, target) = await ReadCountsAsync(connection, null, context, cancellationToken);
        var blockers = source > 0 && target > 0 ? 1L : 0L;
        var transferable = source > 0 && target == 0 ? source : 0L;
        return new PartyMergeParticipantPreview(Module, transferable, 0, blockers);
    }

    public async Task PrepareAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var (postgres, tx) = RequirePostgres(connection, transaction);
        await using var command = new NpgsqlCommand("""
            CREATE TEMP TABLE persons_merge_finance_context (
                operation_id uuid PRIMARY KEY,
                source_account_count bigint NOT NULL,
                target_account_count bigint NOT NULL
            ) ON COMMIT DROP;
            """, postgres, tx);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var counts = await ReadCountsAsync(postgres, tx, context, cancellationToken);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO persons_merge_finance_context
                (operation_id, source_account_count, target_account_count)
            VALUES (@operation, @source_count, @target_count);
            """, postgres, tx);
        insert.Parameters.AddWithValue("operation", context.OperationId);
        insert.Parameters.AddWithValue("source_count", counts.Source);
        insert.Parameters.AddWithValue("target_count", counts.Target);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ValidateAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var (postgres, tx) = RequirePostgres(connection, transaction);
        await using var command = new NpgsqlCommand("""
            SELECT source_account_count, target_account_count
            FROM persons_merge_finance_context
            WHERE operation_id = @operation;
            """, postgres, tx);
        command.Parameters.AddWithValue("operation", context.OperationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Finance merge preparation evidence is missing.");
        if (reader.GetInt64(0) > 0 && reader.GetInt64(1) > 0)
            throw new InvalidOperationException("Merge blocked: both Parties already have Finance subledger accounts.");
        if (reader.GetInt64(0) > 1 || reader.GetInt64(1) > 1)
            throw new InvalidOperationException("Finance account cardinality violates the one-account-per-Party contract.");
    }

    public async Task ApplyAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var (postgres, tx) = RequirePostgres(connection, transaction);
        await using var command = new NpgsqlCommand("""
            UPDATE finance_party_accounts AS account
            SET party_id = @target,
                row_version = row_version + 1
            WHERE account.party_id = @source
              AND EXISTS (
                  SELECT 1 FROM persons_merge_finance_context AS prepared
                  WHERE prepared.operation_id = @operation
                    AND prepared.source_account_count = 1
                    AND prepared.target_account_count = 0
              );
            """, postgres, tx);
        command.Parameters.AddWithValue("source", context.SourcePartyId);
        command.Parameters.AddWithValue("target", context.TargetPartyId);
        command.Parameters.AddWithValue("operation", context.OperationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task VerifyAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var (postgres, tx) = RequirePostgres(connection, transaction);
        await using var command = new NpgsqlCommand("""
            SELECT
                (SELECT count(*) FROM finance_party_accounts WHERE party_id = @source),
                (SELECT count(*) FROM finance_party_accounts WHERE party_id = @target),
                (SELECT source_account_count + target_account_count
                   FROM persons_merge_finance_context WHERE operation_id = @operation);
            """, postgres, tx);
        command.Parameters.AddWithValue("source", context.SourcePartyId);
        command.Parameters.AddWithValue("target", context.TargetPartyId);
        command.Parameters.AddWithValue("operation", context.OperationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) ||
            reader.GetInt64(0) != 0 || reader.GetInt64(1) != reader.GetInt64(2))
            throw new InvalidOperationException("Finance Party account references did not reconcile after merge.");
    }

    public Task RecoverAsync(
        PartyMergeContext context, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        // All Finance mutations use the coordinator's transaction; rollback is the recovery mechanism.
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static async Task<(long Source, long Target)> ReadCountsAsync(
        DbConnection connection, DbTransaction? transaction, PartyMergeContext context, CancellationToken cancellationToken)
    {
        if (connection is not NpgsqlConnection postgres)
            throw new ArgumentException("Finance merge participant requires PostgreSQL.", nameof(connection));
        await using var command = new NpgsqlCommand("""
            SELECT
                (SELECT count(*) FROM finance_party_accounts WHERE party_id = @source),
                (SELECT count(*) FROM finance_party_accounts WHERE party_id = @target);
            """, postgres, transaction as NpgsqlTransaction);
        command.Parameters.AddWithValue("source", NpgsqlDbType.Uuid, context.SourcePartyId);
        command.Parameters.AddWithValue("target", NpgsqlDbType.Uuid, context.TargetPartyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Finance merge reference discovery returned no result.");
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static (NpgsqlConnection Connection, NpgsqlTransaction Transaction) RequirePostgres(
        DbConnection connection, DbTransaction transaction)
    {
        if (connection is not NpgsqlConnection postgres ||
            transaction is not NpgsqlTransaction tx ||
            !ReferenceEquals(tx.Connection, postgres))
            throw new ArgumentException("Finance merge participant requires the matching PostgreSQL connection and transaction.");
        return (postgres, tx);
    }
}
