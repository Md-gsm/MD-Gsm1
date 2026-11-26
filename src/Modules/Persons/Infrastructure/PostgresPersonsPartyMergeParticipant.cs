using System.Data.Common;
using MDGsm.Persons.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Persons.Infrastructure;

/// <summary>
/// Persons-owned merge participant for ContactPoint and Address references.
/// Retires exact duplicates without deleting history, transfers non-duplicates,
/// retains origin_party_id and never broadens messaging consent.
/// </summary>
public sealed class PostgresPersonsPartyMergeParticipant : IPartyMergeParticipant
{
    public string Module => "Persons";
    public string ContractVersion => "1.0";
    public string MergeProtocolVersion => "1.0";

    public async Task<PartyMergeParticipantPreview> PreviewAsync(
        PartyMergeContext context, DbConnection connection, CancellationToken cancellationToken)
    {
        var postgres = RequireConnection(connection);
        await using var command = new NpgsqlCommand("""
            SELECT
              (SELECT count(*) FROM persons_contact_points WHERE party_id=@source AND is_active)
                + (SELECT count(*) FROM persons_addresses WHERE party_id=@source AND is_active),
              (SELECT count(*) FROM persons_contact_points WHERE party_id=@source AND is_active)
                + (SELECT count(*) FROM persons_addresses WHERE party_id=@source AND is_active),
              (SELECT count(*) FROM persons_lifecycle_history WHERE party_id=@source);
            """, postgres);
        command.Parameters.AddWithValue("source", context.SourcePartyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Persons merge reference discovery returned no result.");
        var active = reader.GetInt64(0);
        return new PartyMergeParticipantPreview(Module, active, active, reader.GetInt64(2), 0);
    }

    public async Task PrepareAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var (postgres, tx) = RequireTransaction(connection, transaction);
        await SetContextAsync(postgres, tx, context, "party-merge", cancellationToken);
        await LockContactRowsAsync(postgres, tx, context, cancellationToken);
        await LockAddressRowsAsync(postgres, tx, context, cancellationToken);
        await using var create = new NpgsqlCommand("""
            CREATE TEMP TABLE persons_merge_lifecycle_context (
                operation_id uuid PRIMARY KEY,
                source_contact_count bigint NOT NULL,
                source_address_count bigint NOT NULL
            ) ON COMMIT DROP;
            """, postgres, tx);
        await create.ExecuteNonQueryAsync(cancellationToken);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO persons_merge_lifecycle_context
                (operation_id, source_contact_count, source_address_count)
            SELECT @operation,
                (SELECT count(*) FROM persons_contact_points WHERE party_id=@source AND is_active),
                (SELECT count(*) FROM persons_addresses WHERE party_id=@source AND is_active);
            """, postgres, tx);
        insert.Parameters.AddWithValue("operation", context.OperationId);
        insert.Parameters.AddWithValue("source", context.SourcePartyId);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ValidateAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var (postgres, tx) = RequireTransaction(connection, transaction);
        await using var command = new NpgsqlCommand("""
            SELECT source_contact_count, source_address_count
            FROM persons_merge_lifecycle_context WHERE operation_id=@operation;
            """, postgres, tx);
        command.Parameters.AddWithValue("operation", context.OperationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Persons merge preparation evidence is missing.");
        var preparedContacts = reader.GetInt64(0);
        var preparedAddresses = reader.GetInt64(1);
        await reader.DisposeAsync();

        await using var verify = new NpgsqlCommand("""
            SELECT
              (SELECT count(*) FROM persons_contact_points WHERE party_id=@source AND is_active),
              (SELECT count(*) FROM persons_addresses WHERE party_id=@source AND is_active),
              (SELECT count(*) FROM persons_contact_points WHERE party_id=@target AND is_active AND is_primary),
              (SELECT count(*) FROM persons_addresses WHERE party_id=@target AND is_active AND is_primary);
            """, postgres, tx);
        verify.Parameters.AddWithValue("source", context.SourcePartyId);
        verify.Parameters.AddWithValue("target", context.TargetPartyId);
        await using var result = await verify.ExecuteReaderAsync(cancellationToken);
        if (!await result.ReadAsync(cancellationToken) ||
            result.GetInt64(0) != preparedContacts || result.GetInt64(1) != preparedAddresses ||
            result.GetInt64(2) > 3 || result.GetInt64(3) > 1)
            throw new InvalidOperationException("Persons contact/address merge state changed or violates primary cardinality.");
    }

    public async Task ApplyAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var (postgres, tx) = RequireTransaction(connection, transaction);
        await SetReasonAsync(postgres, tx, "merge-deduplicated-contact", cancellationToken);
        await using (var dedupeContacts = new NpgsqlCommand("""
            WITH source_ids AS (
                SELECT s.contact_point_id AS source_id, s.kind, s.is_primary, s.is_messaging_allowed
                FROM persons_contact_points s
                JOIN persons_contact_points t
                  ON t.party_id=@target AND t.kind=s.kind AND t.is_active
                 AND lower(btrim(t.value))=lower(btrim(s.value))
                WHERE s.party_id=@source AND s.is_active
            ), duplicates AS (
                SELECT t.contact_point_id AS target_id, s.kind,
                       bool_or(s.is_primary) AS source_primary,
                       bool_and(s.is_messaging_allowed) AS source_messaging,
                       EXISTS (SELECT 1 FROM persons_contact_points p
                           WHERE p.party_id=@target AND p.kind=s.kind AND p.is_active AND p.is_primary) AS target_has_primary
                FROM persons_contact_points t
                JOIN source_ids s ON s.kind=t.kind
                JOIN persons_contact_points source_row ON source_row.contact_point_id=s.source_id
                    AND lower(btrim(source_row.value))=lower(btrim(t.value))
                WHERE t.party_id=@target AND t.is_active
                GROUP BY t.contact_point_id, s.kind
            ), update_target AS (
                UPDATE persons_contact_points t
                SET is_messaging_allowed = t.is_messaging_allowed AND d.source_messaging,
                    is_primary = CASE WHEN NOT d.target_has_primary AND d.source_primary
                        AND d.target_id = (SELECT d2.target_id FROM duplicates d2 WHERE d2.kind=d.kind ORDER BY d2.target_id LIMIT 1)
                    THEN true ELSE t.is_primary END
                FROM duplicates d WHERE t.contact_point_id=d.target_id
                RETURNING t.contact_point_id
            )
            UPDATE persons_contact_points s
            SET is_active=false, is_primary=false
            WHERE s.contact_point_id IN (SELECT source_id FROM source_ids);
            """, postgres, tx))
        {
            dedupeContacts.Parameters.AddWithValue("source", context.SourcePartyId);
            dedupeContacts.Parameters.AddWithValue("target", context.TargetPartyId);
            await dedupeContacts.ExecuteNonQueryAsync(cancellationToken);
        }

        await SetReasonAsync(postgres, tx, "merge-deduplicated-address", cancellationToken);
        await using (var dedupeAddresses = new NpgsqlCommand("""
            WITH source_ids AS (
                SELECT s.address_id AS source_id, s.is_primary,
                       s.country_code, s.province, s.city, s.postal_code, s.address_line
                FROM persons_addresses s
                JOIN persons_addresses t
                  ON t.party_id=@target AND t.is_active
                 AND t.country_code=s.country_code
                 AND lower(btrim(t.province))=lower(btrim(s.province))
                 AND lower(btrim(t.city))=lower(btrim(s.city))
                 AND btrim(t.postal_code)=btrim(s.postal_code)
                 AND lower(btrim(t.address_line))=lower(btrim(s.address_line))
                WHERE s.party_id=@source AND s.is_active
            ), duplicates AS (
                SELECT t.address_id AS target_id,
                       bool_or(s.is_primary) AS source_primary,
                       EXISTS (SELECT 1 FROM persons_addresses p
                           WHERE p.party_id=@target AND p.is_active AND p.is_primary) AS target_has_primary
                FROM persons_addresses t
                JOIN source_ids s ON t.country_code=s.country_code
                    AND lower(btrim(t.province))=lower(btrim(s.province))
                    AND lower(btrim(t.city))=lower(btrim(s.city))
                    AND btrim(t.postal_code)=btrim(s.postal_code)
                    AND lower(btrim(t.address_line))=lower(btrim(s.address_line))
                WHERE t.party_id=@target AND t.is_active
                GROUP BY t.address_id
            ), update_target AS (
                UPDATE persons_addresses t
                SET is_primary = CASE WHEN NOT d.target_has_primary AND d.source_primary
                        AND d.target_id = (SELECT d2.target_id FROM duplicates d2 ORDER BY d2.target_id LIMIT 1)
                    THEN true ELSE t.is_primary END
                FROM duplicates d WHERE t.address_id=d.target_id
                RETURNING t.address_id
            )
            UPDATE persons_addresses s
            SET is_active=false, is_primary=false
            WHERE s.address_id IN (SELECT source_id FROM source_ids);
            """, postgres, tx))
        {
            dedupeAddresses.Parameters.AddWithValue("source", context.SourcePartyId);
            dedupeAddresses.Parameters.AddWithValue("target", context.TargetPartyId);
            await dedupeAddresses.ExecuteNonQueryAsync(cancellationToken);
        }

        await SetReasonAsync(postgres, tx, "merge-transfer", cancellationToken);
        await using (var transferContacts = new NpgsqlCommand("""
            UPDATE persons_contact_points s
            SET party_id=@target,
                is_primary = CASE WHEN s.is_primary AND NOT EXISTS (
                    SELECT 1 FROM persons_contact_points p
                    WHERE p.party_id=@target AND p.kind=s.kind AND p.is_active AND p.is_primary)
                    THEN true ELSE false END
            WHERE s.party_id=@source AND s.is_active
              AND NOT EXISTS (
                  SELECT 1 FROM persons_contact_points t
                  WHERE t.party_id=@target AND t.kind=s.kind AND t.is_active
                    AND lower(btrim(t.value))=lower(btrim(s.value))
              );
            """, postgres, tx))
        {
            transferContacts.Parameters.AddWithValue("source", context.SourcePartyId);
            transferContacts.Parameters.AddWithValue("target", context.TargetPartyId);
            await transferContacts.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var transferAddresses = new NpgsqlCommand("""
            UPDATE persons_addresses s
            SET party_id=@target,
                is_primary = CASE WHEN s.is_primary AND NOT EXISTS (
                    SELECT 1 FROM persons_addresses p
                    WHERE p.party_id=@target AND p.is_active AND p.is_primary)
                    THEN true ELSE false END
            WHERE s.party_id=@source AND s.is_active
              AND NOT EXISTS (
                  SELECT 1 FROM persons_addresses t
                  WHERE t.party_id=@target AND t.is_active
                    AND t.country_code=s.country_code
                    AND lower(btrim(t.province))=lower(btrim(s.province))
                    AND lower(btrim(t.city))=lower(btrim(s.city))
                    AND btrim(t.postal_code)=btrim(s.postal_code)
                    AND lower(btrim(t.address_line))=lower(btrim(s.address_line))
              );
            """, postgres, tx))
        {
            transferAddresses.Parameters.AddWithValue("source", context.SourcePartyId);
            transferAddresses.Parameters.AddWithValue("target", context.TargetPartyId);
            await transferAddresses.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task VerifyAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var (postgres, tx) = RequireTransaction(connection, transaction);
        await using var command = new NpgsqlCommand("""
            SELECT
              (SELECT count(*) FROM persons_contact_points WHERE party_id=@source AND is_active),
              (SELECT count(*) FROM persons_addresses WHERE party_id=@source AND is_active),
              (SELECT count(*) FROM persons_lifecycle_history
                WHERE operation_id=@operation AND change_type IN ('merge-source','merge-transfer','merge-deduplicated'));
            """, postgres, tx);
        command.Parameters.AddWithValue("source", context.SourcePartyId);
        command.Parameters.AddWithValue("operation", context.OperationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetInt64(0) != 0 ||
            reader.GetInt64(1) != 0 || reader.GetInt64(2) == 0)
            throw new InvalidOperationException("Persons merge transfer/history reconciliation failed.");
    }

    public Task RecoverAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        // The coordinator rolls back all source-module and consumer mutations in its shared transaction.
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static async Task LockContactRowsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PartyMergeContext context, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT contact_point_id FROM persons_contact_points
            WHERE party_id=ANY(@parties) ORDER BY contact_point_id FOR UPDATE;
            """, connection, transaction);
        command.Parameters.Add("parties", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value =
            new[] { context.SourcePartyId, context.TargetPartyId };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) { }
    }

    private static async Task LockAddressRowsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PartyMergeContext context, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT address_id FROM persons_addresses
            WHERE party_id=ANY(@parties) ORDER BY address_id FOR UPDATE;
            """, connection, transaction);
        command.Parameters.Add("parties", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value =
            new[] { context.SourcePartyId, context.TargetPartyId };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) { }
    }

    private static async Task SetContextAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PartyMergeContext context, string reason, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT set_config('persons.lifecycle_actor',@actor,true),
                   set_config('persons.lifecycle_reason',@reason,true),
                   set_config('persons.lifecycle_operation',@operation,true),
                   set_config('persons.lifecycle_occurred_at_utc',@occurred,true);
            """, connection, transaction);
        command.Parameters.AddWithValue("actor", context.ActorId);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("operation", context.OperationId.ToString("D"));
        command.Parameters.AddWithValue("occurred", context.OccurredAtUtc.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetReasonAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string reason, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('persons.lifecycle_reason',@reason,true);", connection, transaction);
        command.Parameters.AddWithValue("reason", reason);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlConnection RequireConnection(DbConnection connection) =>
        connection as NpgsqlConnection ?? throw new ArgumentException("Persons merge requires PostgreSQL.", nameof(connection));

    private static (NpgsqlConnection Connection, NpgsqlTransaction Transaction) RequireTransaction(
        DbConnection connection, DbTransaction transaction)
    {
        if (connection is not NpgsqlConnection postgres || transaction is not NpgsqlTransaction tx ||
            !ReferenceEquals(tx.Connection, postgres))
            throw new ArgumentException("Persons merge requires the matching PostgreSQL connection and transaction.");
        return (postgres, tx);
    }
}
