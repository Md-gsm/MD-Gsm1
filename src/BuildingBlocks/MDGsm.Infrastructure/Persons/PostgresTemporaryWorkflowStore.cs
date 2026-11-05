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
        if (authenticatedCiphertext.Length < 28)
            throw new ArgumentException("An authenticated-encryption ciphertext (including nonce and tag) is required.", nameof(authenticatedCiphertext));

        var created = nowUtc.ToUniversalTime();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO persons_temporary_workflows
                (workflow_record_id, owner_module, purpose_code, created_by, created_at_utc,
                 expires_at_utc, absolute_expires_at_utc, encrypted_payload, encryption_key_version,
                 idempotency_key)
            VALUES
                (@id, @owner, @purpose, @actor, @created, @expires, @absolute,
                 @payload, @key_version, @idempotency);
            """, connection);
        command.Parameters.AddWithValue("id", workflowRecordId);
        command.Parameters.AddWithValue("owner", ownerModule.Trim());
        command.Parameters.AddWithValue("purpose", purposeCode.Trim());
        command.Parameters.AddWithValue("actor", createdBy.Trim());
        command.Parameters.AddWithValue("created", created);
        command.Parameters.AddWithValue("expires", created.AddDays(DefaultTtlDays));
        command.Parameters.AddWithValue("absolute", created.AddDays(AbsoluteMaxAgeDays));
        command.Parameters.Add("payload", NpgsqlDbType.Bytea).Value = authenticatedCiphertext.ToArray();
        command.Parameters.AddWithValue("key_version", encryptionKeyVersion.Trim());
        command.Parameters.AddWithValue("idempotency", idempotencyKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<byte[]?> ReadCiphertextAsync(
        Guid workflowRecordId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT encrypted_payload
            FROM persons_temporary_workflows
            WHERE workflow_record_id = @id
              AND lifecycle_status = 'open'
              AND expires_at_utc > @now
              AND encrypted_payload IS NOT NULL;
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
        if (extension <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(extension));

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
              AND encrypted_payload IS NOT NULL
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
                SELECT workflow_record_id
                FROM persons_temporary_workflows
                WHERE expires_at_utc <= @now
                  AND encrypted_payload IS NOT NULL
                  AND lifecycle_status IN ('open', 'closed')
                ORDER BY expires_at_utc, workflow_record_id
                FOR UPDATE SKIP LOCKED
                LIMIT @batch_size
            )
            UPDATE persons_temporary_workflows AS target
            SET encrypted_payload = NULL,
                lifecycle_status = 'purged',
                purged_at_utc = @now,
                row_version = row_version + 1
            FROM due
            WHERE target.workflow_record_id = due.workflow_record_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("now", nowUtc.ToUniversalTime());
        command.Parameters.AddWithValue("batch_size", batchSize);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affected;
    }
}
