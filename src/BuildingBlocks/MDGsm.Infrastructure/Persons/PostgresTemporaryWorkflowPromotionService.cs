using System.Data;
using System.Globalization;
using MDGsm.Application.Persons;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Executes AC-37 in one PostgreSQL transaction. The key provider is deliberately external;
/// this service cannot be activated until a deployment supplies an approved key-custody adapter.
/// </summary>
public sealed class PostgresTemporaryWorkflowPromotionService(
    NpgsqlDataSource dataSource,
    IPartyRepository parties,
    ITemporaryWorkflowPayloadProtector payloadProtector,
    IPersonsWorkflowPromotionAuthorizer authorizer,
    TimeProvider timeProvider) : ITemporaryWorkflowPromotionService
{
    private const string PromotePermission = "persons.workflow.promote";
    private const string ReadPermission = "persons.read";
    private const string CreatePermission = "persons.create";
    private const string UpdatePermission = "persons.update";
    private const string PartyRegisteredContractVersion = "1.0";

    public async Task<TemporaryWorkflowPromotionPreview> PreviewAsync(
        Guid workflowRecordId, Guid actorId, Guid? correlationId, CancellationToken cancellationToken)
    {
        ValidateActorAndWorkflow(workflowRecordId, actorId);
        await RequirePermissionAsync(actorId, PromotePermission, cancellationToken);
        await RequirePermissionAsync(actorId, ReadPermission, cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var row = await LockWorkflowAsync(connection, transaction, workflowRecordId, cancellationToken);
        EnsureOwnedAndOpen(row, actorId);
        var payload = await ReadPayloadAsync(connection, transaction, workflowRecordId, cancellationToken);
        var decoded = await DecodeAsync(workflowRecordId, payload, cancellationToken);
        var preview = new TemporaryWorkflowPromotionPreview(
            workflowRecordId,
            decoded.SchemaVersion,
            decoded.PartyType == MDGsm.Domain.Persons.PartyType.NaturalPerson ? "NaturalPerson" : "LegalEntity",
            decoded.DisplayName,
            decoded.MaskedPrimaryMobile,
            decoded.HasNationalIdentifier,
            decoded.HasAddress,
            row.ExpiresAtUtc);
        await InsertAuditAsync(connection, transaction, workflowRecordId, null, actorId,
            "preview", "previewed", correlationId, timeProvider.GetUtcNow(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return preview;
    }

    public async Task<TemporaryWorkflowPromotionResult> PromoteAsync(
        TemporaryWorkflowPromotionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateActorAndWorkflow(command.WorkflowRecordId, command.ActorId);
        if (command.OperationId == Guid.Empty)
            throw new ArgumentException("A non-empty OperationId is required.", nameof(command));
        if (command.CreateNewParty == !string.IsNullOrWhiteSpace(command.SelectedPartyId))
            throw new ArgumentException("Choose exactly one explicit path: selected Party or new Party.", nameof(command));
        if (command.ConfirmDistinctParty && !command.CreateNewParty)
            throw new ArgumentException("Distinct-party confirmation applies only to explicit new-Party creation.", nameof(command));
        if (!string.IsNullOrWhiteSpace(command.SelectedPartyId) &&
            (!Guid.TryParse(command.SelectedPartyId, out var selectedId) || selectedId == Guid.Empty))
            throw new ArgumentException("SelectedPartyId must be a valid UUID.", nameof(command));

        await RequirePermissionAsync(command.ActorId, PromotePermission, cancellationToken);
        await RequirePermissionAsync(command.ActorId, ReadPermission, cancellationToken);
        await RequirePermissionAsync(command.ActorId,
            command.CreateNewParty ? CreatePermission : UpdatePermission, cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var replay = await FindExistingTombstoneAsync(
                connection, transaction, command.WorkflowRecordId, command.OperationId, cancellationToken);
            if (replay is not null)
            {
                if (replay.WorkflowRecordId == command.WorkflowRecordId &&
                    replay.OperationId == command.OperationId &&
                    string.Equals(replay.ActorId, command.ActorId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                {
                    await InsertAuditAsync(connection, transaction, command.WorkflowRecordId, command.OperationId,
                        command.ActorId, "replay", "same_actor_same_operation", command.CorrelationId,
                        timeProvider.GetUtcNow(), cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return new TemporaryWorkflowPromotionResult(
                        TemporaryWorkflowPromotionOutcome.Replayed, replay.PartyId.ToString("D"),
                        replay.Outcome == "Created", true, Array.Empty<PartyQuickRegistrationCandidate>());
                }

                if (replay.OperationId == command.OperationId &&
                    !string.Equals(replay.ActorId, command.ActorId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                    throw new IdempotencyKeyConflictException();
                if (replay.WorkflowRecordId == command.WorkflowRecordId)
                    throw new TemporaryWorkflowAlreadyPromotedException();
                throw new IdempotencyKeyConflictException();
            }

            var workflow = await LockWorkflowAsync(
                connection, transaction, command.WorkflowRecordId, cancellationToken);
            EnsureOwnedAndOpen(workflow, command.ActorId);
            var encryptedPayload = await ReadPayloadAsync(
                connection, transaction, command.WorkflowRecordId, cancellationToken);
            var decoded = await DecodeAsync(command.WorkflowRecordId, encryptedPayload, cancellationToken);
            var now = timeProvider.GetUtcNow();

            PartyQuickRegistrationResult registration;
            if (decoded.Registration is RegisterNaturalParty natural)
            {
                registration = await parties.QuickRegisterNaturalAsync(
                    natural, command.ActorId.ToString("D"), command.OperationId,
                    command.CreateNewParty, command.SelectedPartyId, transaction, cancellationToken,
                    command.ConfirmDistinctParty);
            }
            else if (decoded.Registration is RegisterLegalParty legal)
            {
                registration = await parties.QuickRegisterLegalAsync(
                    legal, command.ActorId.ToString("D"), command.OperationId,
                    command.CreateNewParty, command.SelectedPartyId, transaction, cancellationToken,
                    command.ConfirmDistinctParty);
            }
            else
            {
                throw new TemporaryWorkflowPayloadException();
            }

            if (registration.Outcome == PartyQuickRegistrationOutcome.SelectionRequired)
            {
                await transaction.RollbackAsync(cancellationToken);
                await AppendAuditAsync(command.WorkflowRecordId, command.OperationId, command.ActorId,
                    "promotion_rejected", "duplicate_review_required", command.CorrelationId, now, cancellationToken);
                return new TemporaryWorkflowPromotionResult(
                    TemporaryWorkflowPromotionOutcome.DuplicateReviewRequired, null, false, false,
                    registration.Candidates);
            }

            if (registration.Outcome == PartyQuickRegistrationOutcome.Replayed ||
                string.IsNullOrWhiteSpace(registration.PartyId) ||
                registration.Created != (registration.Outcome == PartyQuickRegistrationOutcome.Created))
                throw new IdempotencyKeyConflictException();

            var partyId = Guid.Parse(registration.PartyId);
            await MarkWorkflowPromotedAsync(
                connection, transaction, command.WorkflowRecordId, partyId, now, cancellationToken);
            await DeleteEncryptedPayloadAsync(
                connection, transaction, command.WorkflowRecordId, cancellationToken);

            var outcome = registration.Created ? "Created" : "Existing";
            await InsertTombstoneAsync(
                connection, transaction, command, partyId, outcome, decoded.SchemaVersion, now, cancellationToken);

            if (registration.Created)
                await InsertPartyRegisteredOutboxAsync(
                    connection, transaction, partyId, command, now, cancellationToken);

            await InsertAuditAsync(connection, transaction, command.WorkflowRecordId, command.OperationId,
                command.ActorId, "promotion_succeeded", outcome.ToLowerInvariant(),
                command.CorrelationId, now, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new TemporaryWorkflowPromotionResult(
                registration.Created ? TemporaryWorkflowPromotionOutcome.Created : TemporaryWorkflowPromotionOutcome.Existing,
                partyId.ToString("D"), registration.Created, false,
                Array.Empty<PartyQuickRegistrationCandidate>());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (transaction.Connection is not null)
            {
                try { await transaction.RollbackAsync(cancellationToken); }
                catch (InvalidOperationException) { }
            }

            await AppendAuditAsync(command.WorkflowRecordId, command.OperationId, command.ActorId,
                "promotion_rejected", MapFailureCode(exception), command.CorrelationId,
                timeProvider.GetUtcNow(), cancellationToken);
            throw;
        }
    }

    private async Task RequirePermissionAsync(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        if (!await authorizer.HasPermissionAsync(actorId, permission, cancellationToken))
            throw new UnauthorizedAccessException("The actor lacks the required Persons workflow permission.");
    }

    private static void ValidateActorAndWorkflow(Guid workflowRecordId, Guid actorId)
    {
        if (workflowRecordId == Guid.Empty) throw new ArgumentException("Workflow id is required.", nameof(workflowRecordId));
        if (actorId == Guid.Empty) throw new UnauthorizedAccessException("A validated actor is required.");
    }

    private static void EnsureOwnedAndOpen(WorkflowRow row, Guid actorId)
    {
        if (!string.Equals(row.CreatedBy, actorId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            throw new TemporaryWorkflowOwnershipException();
        if (!string.Equals(row.LifecycleStatus, "open", StringComparison.Ordinal) ||
            row.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            throw new TemporaryWorkflowUnavailableException();
    }

    private async Task<WorkflowRow> LockWorkflowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workflowRecordId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT created_by, lifecycle_status, expires_at_utc
            FROM persons_temporary_workflows
            WHERE workflow_record_id = @workflow
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("workflow", workflowRecordId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new TemporaryWorkflowUnavailableException();
        return new WorkflowRow(
            reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2));
    }

    private static async Task<EncryptedPayload> ReadPayloadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workflowRecordId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT encrypted_payload, encryption_key_version
            FROM persons_temporary_workflow_payloads
            WHERE workflow_record_id = @workflow
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("workflow", workflowRecordId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new TemporaryWorkflowUnavailableException();
        return new EncryptedPayload(reader.GetFieldValue<byte[]>(0), reader.GetString(1));
    }

    private async Task<TemporaryWorkflowDecodedParty> DecodeAsync(
        Guid workflowRecordId, EncryptedPayload payload, CancellationToken cancellationToken)
    {
        try
        {
            var plaintext = await payloadProtector.UnprotectAsync(
                workflowRecordId, payload.KeyVersion, payload.Ciphertext, cancellationToken);
            try
            {
                return TemporaryWorkflowPayloadParser.Parse(plaintext);
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        catch (TemporaryWorkflowPayloadException)
        {
            throw;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or JsonException)
        {
            throw new TemporaryWorkflowPayloadException();
        }
    }

    private static async Task<Tombstone?> FindExistingTombstoneAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid workflowRecordId, Guid operationId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT workflow_record_id, operation_id, actor_id, party_id, outcome
            FROM persons_temporary_workflow_promotions
            WHERE workflow_record_id = @workflow OR operation_id = @operation
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("workflow", workflowRecordId);
        command.Parameters.AddWithValue("operation", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var tombstone = new Tombstone(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetString(4));
        if (await reader.ReadAsync(cancellationToken))
            throw new IdempotencyKeyConflictException();
        return tombstone;
    }

    private static async Task MarkWorkflowPromotedAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid workflowRecordId, Guid partyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE persons_temporary_workflows
            SET lifecycle_status = 'promoted',
                promoted_party_id = @party,
                promoted_at_utc = @now,
                row_version = row_version + 1
            WHERE workflow_record_id = @workflow
              AND lifecycle_status = 'open'
              AND expires_at_utc > @now;
            """, connection, transaction);
        command.Parameters.AddWithValue("workflow", workflowRecordId);
        command.Parameters.AddWithValue("party", partyId);
        command.Parameters.AddWithValue("now", now);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new TemporaryWorkflowUnavailableException();
    }

    private static async Task DeleteEncryptedPayloadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid workflowRecordId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            DELETE FROM persons_temporary_workflow_payloads
            WHERE workflow_record_id = @workflow;
            """, connection, transaction);
        command.Parameters.AddWithValue("workflow", workflowRecordId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new TemporaryWorkflowUnavailableException();
    }

    private static async Task InsertTombstoneAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        TemporaryWorkflowPromotionCommand command, Guid partyId, string outcome,
        int schemaVersion, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand("""
            INSERT INTO persons_temporary_workflow_promotions
                (operation_id, workflow_record_id, actor_id, party_id, completed_at_utc, outcome, contract_schema_version)
            VALUES (@operation, @workflow, @actor, @party, @completed, @outcome, @version);
            """, connection, transaction);
        insert.Parameters.AddWithValue("operation", command.OperationId);
        insert.Parameters.AddWithValue("workflow", command.WorkflowRecordId);
        insert.Parameters.AddWithValue("actor", command.ActorId.ToString("D"));
        insert.Parameters.AddWithValue("party", partyId);
        insert.Parameters.AddWithValue("completed", now);
        insert.Parameters.AddWithValue("outcome", outcome);
        insert.Parameters.AddWithValue("version", schemaVersion.ToString(CultureInfo.InvariantCulture));
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPartyRegisteredOutboxAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid partyId, TemporaryWorkflowPromotionCommand command,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var eventId = Guid.CreateVersion7();
        await using (var eventCommand = new NpgsqlCommand("""
            INSERT INTO persons_outbox_events
                (event_id, event_type, contract_version, aggregate_id, actor_reference,
                 occurred_at_utc, correlation_id, causation_id, idempotency_key)
            VALUES
                (@event, 'PartyRegistered', @version, @party, @actor,
                 @occurred, @correlation, @causation, @operation);
            """, connection, transaction))
        {
            eventCommand.Parameters.AddWithValue("event", eventId);
            eventCommand.Parameters.AddWithValue("version", PartyRegisteredContractVersion);
            eventCommand.Parameters.AddWithValue("party", partyId);
            eventCommand.Parameters.AddWithValue("actor", command.ActorId.ToString("D"));
            eventCommand.Parameters.AddWithValue("occurred", now);
            eventCommand.Parameters.Add(new NpgsqlParameter("correlation", NpgsqlDbType.Uuid)
            {
                Value = (object?)command.CorrelationId ?? DBNull.Value
            });
            eventCommand.Parameters.AddWithValue("causation", command.WorkflowRecordId);
            eventCommand.Parameters.AddWithValue("operation", command.OperationId);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var deliveryCommand = new NpgsqlCommand("""
            INSERT INTO persons_outbox_deliveries
                (event_id, delivery_status, attempt_count, next_attempt_at_utc)
            VALUES (@event, 'pending', 0, @now);
            """, connection, transaction);
        deliveryCommand.Parameters.AddWithValue("event", eventId);
        deliveryCommand.Parameters.AddWithValue("now", now);
        await deliveryCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task AppendAuditAsync(
        Guid workflowRecordId, Guid? operationId, Guid actorId,
        string action, string outcomeCode, Guid? correlationId,
        DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await InsertAuditAsync(connection, transaction, workflowRecordId, operationId, actorId,
            action, outcomeCode, correlationId, occurredAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid workflowRecordId, Guid? operationId, Guid actorId,
        string action, string outcomeCode, Guid? correlationId,
        DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO persons_temporary_workflow_access_audit
                (audit_id, workflow_record_id, operation_id, actor_id, action, outcome_code, correlation_id, occurred_at_utc)
            VALUES
                (@audit, @workflow, @operation, @actor, @action, @outcome, @correlation, @occurred);
            """, connection, transaction);
        command.Parameters.AddWithValue("audit", Guid.CreateVersion7());
        command.Parameters.AddWithValue("workflow", workflowRecordId);
        command.Parameters.Add(new NpgsqlParameter("operation", NpgsqlDbType.Uuid)
        {
            Value = (object?)operationId ?? DBNull.Value
        });
        command.Parameters.AddWithValue("actor", actorId.ToString("D"));
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("outcome", outcomeCode);
        command.Parameters.Add(new NpgsqlParameter("correlation", NpgsqlDbType.Uuid)
        {
            Value = (object?)correlationId ?? DBNull.Value
        });
        command.Parameters.AddWithValue("occurred", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string MapFailureCode(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "permission_denied",
        TemporaryWorkflowOwnershipException => "ownership_denied",
        TemporaryWorkflowUnavailableException => "workflow_unavailable",
        TemporaryWorkflowAlreadyPromotedException => "already_promoted",
        TemporaryWorkflowPayloadException => "payload_invalid",
        IdempotencyKeyConflictException => "idempotency_conflict",
        DuplicatePartyIdentifierException => "duplicate_identifier",
        _ => "promotion_failed"
    };

    private sealed record WorkflowRow(string CreatedBy, string LifecycleStatus, DateTimeOffset ExpiresAtUtc);
    private sealed record EncryptedPayload(byte[] Ciphertext, string KeyVersion);
    private sealed record Tombstone(Guid WorkflowRecordId, Guid OperationId, string ActorId, Guid PartyId, string Outcome);
}
