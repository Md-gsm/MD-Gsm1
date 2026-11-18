using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// PostgreSQL persistence boundary for temporary workflows. The caller must supply authenticated
/// ciphertext; this type deliberately has no API that accepts plaintext PII.
/// Every payload read is guarded by the expiry predicate in the database.
/// </summary>
public sealed class PostgresTemporaryWorkflowStore(NpgsqlDataSource dataSource)
{
    private const int DefaultTtlDays = 7;
    private const int AbsoluteMaxAgeDays = 30;
    private const int MaxReasonLength = 500;

    public async Task CreateAsync(
        Guid workflowRecordId,
        string ownerModule,
        string purposeCode,
        string createdBy,
        Guid idempotencyKey,
        DateTimeOffset nowUtc,
        ReadOnlyMemory<byte> authenticatedCiphertext,
        string encryptionKeyVersion,
        CancellationToken cancellationToken = default)
    {
        if (workflowRecordId == Guid.Empty) throw new ArgumentException("Workflow id is required.", nameof(workflowRecordId));
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(purposeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptionKeyVersion);
        if (ownerModule.Trim().Length > 80 || purposeCode.Trim().Length > 80 ||
            createdBy.Trim().Length > 128 || encryptionKeyVersion.Trim().Length > 80)
            throw new ArgumentException("Temporary workflow metadata exceeds its declared database field length.");
        if (authenticatedCiphertext.Length < 28)
            throw new ArgumentException("An authenticated-encryption ciphertext (including nonce and tag) is required.", nameof(authenticatedCiphertext));

        var created = nowUtc.ToUniversalTime();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var metadata = new NpgsqlCommand("""
            INSERT INTO persons_temporary_workflows
                (workflow_record_id, owner_module, purpose_code, created_by, created_at_utc,
                 expires_at_utc, absolute_expires_at_utc, idempotency_key)
            VALUES
                (@id, @owner, @purpose, @actor, @created, @expires, @absolute, @idempotency);
            """, connection, transaction))
        {
            metadata.Parameters.AddWithValue("id", workflowRecordId);
            metadata.Parameters.AddWithValue("owner", ownerModule.Trim());
            metadata.Parameters.AddWithValue("purpose", purposeCode.Trim());
            metadata.Parameters.AddWithValue("actor", createdBy.Trim());
            metadata.Parameters.AddWithValue("created", created);
            metadata.Parameters.AddWithValue("expires", created.AddDays(DefaultTtlDays));
            metadata.Parameters.AddWithValue("absolute", created.AddDays(AbsoluteMaxAgeDays));
            metadata.Parameters.AddWithValue("idempotency", idempotencyKey);
            await metadata.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var payload = new NpgsqlCommand("""
            INSERT INTO persons_temporary_workflow_payloads
                (workflow_record_id, encrypted_payload, encryption_key_version)
            VALUES (@id, @payload, @key_version);
            """, connection, transaction))
        {
            payload.Parameters.AddWithValue("id", workflowRecordId);
            payload.Parameters.Add("payload", NpgsqlDbType.Bytea).Value = authenticatedCiphertext.ToArray();
            payload.Parameters.AddWithValue("key_version", encryptionKeyVersion.Trim());
            await payload.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<byte[]?> ReadCiphertextAsync(
        Guid workflowRecordId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT payload.encrypted_payload
            FROM persons_temporary_workflows AS workflow
            INNER JOIN persons_temporary_workflow_payloads AS payload
                ON payload.workflow_record_id = workflow.workflow_record_id
            WHERE workflow.workflow_record_id = @id
              AND workflow.lifecycle_status = 'open'
              AND workflow.expires_at_utc > @now;
            """, connection);
        command.Parameters.AddWithValue("id", workflowRecordId);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is byte[] bytes ? bytes : null;
    }

    public async Task<bool> RenewAsync(
        Guid workflowRecordId,
        DateTimeOffset nowUtc,
        TimeSpan extension,
        string reason,
        string authorizedActor,
        bool explicitlyAuthorized,
        CancellationToken cancellationToken = default)
    {
        if (!explicitlyAuthorized) throw new UnauthorizedAccessException("Explicit renewal authorization is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedActor);
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 8 || reason.Length > MaxReasonLength)
            throw new ArgumentException("A renewal reason between 8 and 500 characters is required.", nameof(reason));
        if (extension < TimeSpan.FromSeconds(1)) throw new ArgumentOutOfRangeException(nameof(extension));

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE persons_temporary_workflows
            SET expires_at_utc = LEAST(
                    absolute_expires_at_utc,
                    expires_at_utc + (@extension_seconds * interval '1 second')),
                renewal_count = 1,
                renewal_reason = @reason,
                renewed_by = @actor,
                renewed_at_utc = @now,
                row_version = row_version + 1
            WHERE workflow_record_id = @id
              AND lifecycle_status = 'open'
              AND EXISTS (
                  SELECT 1 FROM persons_temporary_workflow_payloads AS payload
                  WHERE payload.workflow_record_id = persons_temporary_workflows.workflow_record_id
              )
              AND expires_at_utc > @now
              AND renewal_count = 0
              AND LEAST(absolute_expires_at_utc,
                    expires_at_utc + (@extension_seconds * interval '1 second')) > expires_at_utc;
            """, connection);
        command.Parameters.AddWithValue("id", workflowRecordId);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        command.Parameters.AddWithValue("extension_seconds", checked((long)extension.TotalSeconds));
        command.Parameters.AddWithValue("reason", reason.Trim());
        command.Parameters.AddWithValue("actor", authorizedActor.Trim());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }


    public async Task<bool> CloseAsync(
        Guid workflowRecordId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE persons_temporary_workflows
            SET lifecycle_status = 'closed',
                row_version = row_version + 1
            WHERE workflow_record_id = @id
              AND lifecycle_status = 'open'
              AND expires_at_utc > @now
              AND EXISTS (
                  SELECT 1 FROM persons_temporary_workflow_payloads AS payload
                  WHERE payload.workflow_record_id = persons_temporary_workflows.workflow_record_id
              );
            """, connection);
        command.Parameters.AddWithValue("id", workflowRecordId);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    /// <summary>
    /// Idempotently removes ciphertext for expired rows. FOR UPDATE SKIP LOCKED permits
    /// multiple workers without double-processing the same row.
    /// </summary>
    public async Task<int> PurgeExpiredAsync(
        DateTimeOffset nowUtc, int batchSize = 250, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            WITH due AS (
                SELECT workflow.workflow_record_id
                FROM persons_temporary_workflows AS workflow
                WHERE workflow.expires_at_utc <= @now
                  AND workflow.lifecycle_status IN ('open', 'closed')
                  AND EXISTS (
                      SELECT 1 FROM persons_temporary_workflow_payloads AS payload
                      WHERE payload.workflow_record_id = workflow.workflow_record_id
                  )
                ORDER BY workflow.expires_at_utc, workflow.workflow_record_id
                LIMIT @batch_size
                FOR UPDATE OF workflow SKIP LOCKED
            ),
            deleted_payloads AS (
                DELETE FROM persons_temporary_workflow_payloads AS payload
                USING due
                WHERE payload.workflow_record_id = due.workflow_record_id
                RETURNING payload.workflow_record_id
            )
            UPDATE persons_temporary_workflows AS workflow
            SET lifecycle_status = 'purged',
                purged_at_utc = @now,
                row_version = row_version + 1
            FROM deleted_payloads
            WHERE workflow.workflow_record_id = deleted_payloads.workflow_record_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        command.Parameters.AddWithValue("batch_size", batchSize);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affected;
    }
}
