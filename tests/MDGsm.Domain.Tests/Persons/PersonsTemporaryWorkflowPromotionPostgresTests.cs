using System.Security.Cryptography;
using System.Text;
using MDGsm.Application.Persons;
using MDGsm.Domain.Persons;
using MDGsm.Infrastructure.Persons;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsTemporaryWorkflowPromotionPostgresTests
{
    private const string ActorText = "f0f0f0f0-1111-7111-8111-111111111111";
    private static readonly Guid Actor = Guid.Parse(ActorText);
    private static readonly byte[] EncryptionKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private const string KeyVersion = "ci-test-v1";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC37_new_party_promotion_commits_party_tombstone_payload_purge_and_outbox_atomically()
    {
        await using var dataSource = OpenDataSource();
        var mobile = NewMobile();
        var workflowId = await CreateWorkflowAsync(dataSource, mobile);
        var operationId = Guid.CreateVersion7();
        var service = CreateService(dataSource);

        var first = await service.PromoteAsync(
            NewCommand(workflowId, operationId, createNew: true), default);
        var replay = await service.PromoteAsync(
            NewCommand(workflowId, operationId, createNew: true), default);

        Assert.Equal(TemporaryWorkflowPromotionOutcome.Created, first.Outcome);
        Assert.True(first.Created);
        Assert.False(first.Replayed);
        Assert.Equal(TemporaryWorkflowPromotionOutcome.Replayed, replay.Outcome);
        Assert.True(replay.Replayed);
        Assert.Equal(first.PartyId, replay.PartyId);
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_operations WHERE operation_id = @id;", operationId));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_promotions WHERE workflow_record_id = @id AND outcome = 'Created';",
            workflowId));
        Assert.Equal(0L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_payloads WHERE workflow_record_id = @id;", workflowId));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key = @id AND event_type = 'PartyRegistered';",
            operationId));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_deliveries d JOIN persons_outbox_events e USING (event_id) WHERE e.idempotency_key = @id AND d.delivery_status = 'pending';",
            operationId));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_access_audit WHERE workflow_record_id = @id AND action = 'promotion_succeeded';",
            workflowId));
        Assert.Equal("promoted", await ScalarTextAsync(dataSource,
            "SELECT lifecycle_status FROM persons_temporary_workflows WHERE workflow_record_id = @id;", workflowId));
        Assert.Equal(0L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE aggregate_id = @id AND event_type = 'PartyMerged';",
            Guid.Parse(first.PartyId!)));

        await Assert.ThrowsAsync<TemporaryWorkflowAlreadyPromotedException>(() =>
            service.PromoteAsync(NewCommand(workflowId, Guid.CreateVersion7(), createNew: true), default));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC37_preview_is_owner_scoped_masked_and_audited()
    {
        await using var dataSource = OpenDataSource();
        var mobile = NewMobile();
        var workflowId = await CreateWorkflowAsync(dataSource, mobile);
        var preview = await CreateService(dataSource).PreviewAsync(workflowId, Actor, Guid.CreateVersion7(), default);

        Assert.Equal(workflowId, preview.WorkflowRecordId);
        Assert.Equal("NaturalPerson", preview.PartyType);
        Assert.Equal("سارا محمدی", preview.DisplayName);
        Assert.NotNull(preview.MaskedPrimaryMobile);
        Assert.DoesNotContain(mobile, preview.MaskedPrimaryMobile, StringComparison.Ordinal);
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_access_audit WHERE workflow_record_id = @id AND action = 'preview';",
            workflowId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC37_closed_and_expired_workflows_are_rejected_without_deleting_payload()
    {
        await using var dataSource = OpenDataSource();
        var service = CreateService(dataSource);
        var closedId = await CreateWorkflowAsync(dataSource, NewMobile());
        await ExecuteAsync(dataSource,
            "UPDATE persons_temporary_workflows SET lifecycle_status = 'closed', row_version = row_version + 1 WHERE workflow_record_id = @id;",
            closedId);

        await Assert.ThrowsAsync<TemporaryWorkflowUnavailableException>(() =>
            service.PromoteAsync(NewCommand(closedId, Guid.CreateVersion7(), createNew: true), default));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_payloads WHERE workflow_record_id = @id;", closedId));

        var expiredId = await CreateWorkflowAsync(
            dataSource, NewMobile(), createdAtUtc: DateTimeOffset.UtcNow.AddDays(-8));
        await Assert.ThrowsAsync<TemporaryWorkflowUnavailableException>(() =>
            service.PromoteAsync(NewCommand(expiredId, Guid.CreateVersion7(), createNew: true), default));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_payloads WHERE workflow_record_id = @id;", expiredId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC37_workflow_owner_mismatch_and_cross_actor_replay_are_rejected()
    {
        await using var dataSource = OpenDataSource();
        var workflowId = await CreateWorkflowAsync(dataSource, NewMobile());
        var otherActor = Guid.CreateVersion7();
        var service = CreateService(dataSource);

        await Assert.ThrowsAsync<TemporaryWorkflowOwnershipException>(() =>
            service.PromoteAsync(new TemporaryWorkflowPromotionCommand(
                workflowId, Guid.CreateVersion7(), otherActor, null, true, false), default));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_payloads WHERE workflow_record_id = @id;", workflowId));

        var operationId = Guid.CreateVersion7();
        var created = await service.PromoteAsync(NewCommand(workflowId, operationId, createNew: true), default);
        await Assert.ThrowsAsync<TemporaryWorkflowOwnershipException>(() =>
            service.PromoteAsync(new TemporaryWorkflowPromotionCommand(
                workflowId, operationId, otherActor, null, true, false), default));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key = @id;", operationId));
        Assert.NotNull(created.PartyId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC37_concurrent_same_actor_same_operation_returns_one_creation_and_one_replay()
    {
        await using var dataSource = OpenDataSource();
        var workflowId = await CreateWorkflowAsync(dataSource, NewMobile());
        var command = NewCommand(workflowId, Guid.CreateVersion7(), createNew: true);
        var service = CreateService(dataSource);

        var results = await Task.WhenAll(
            service.PromoteAsync(command, default),
            service.PromoteAsync(command, default));

        Assert.Single(results, result => result.Outcome == TemporaryWorkflowPromotionOutcome.Created);
        Assert.Single(results, result => result.Outcome == TemporaryWorkflowPromotionOutcome.Replayed);
        Assert.Single(results.Select(result => result.PartyId).Distinct());
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key = @id;", command.OperationId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC37_tombstone_and_outbox_envelopes_exclude_descriptive_PII_columns()
    {
        await using var dataSource = OpenDataSource();
        var tombstoneColumns = await ColumnNamesAsync(dataSource, "persons_temporary_workflow_promotions");
        var eventColumns = await ColumnNamesAsync(dataSource, "persons_outbox_events");

        foreach (var forbidden in new[] { "given_name", "family_name", "legal_name", "national_id", "mobile", "email", "address", "encrypted_payload" })
        {
            Assert.DoesNotContain(forbidden, tombstoneColumns, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, eventColumns, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC37_explicit_existing_party_selection_creates_tombstone_without_PartyRegistered()
    {
        await using var dataSource = OpenDataSource();
        var mobile = NewMobile();
        var existingPartyId = await CreateExistingPartyAsync(dataSource, mobile);
        var workflowId = await CreateWorkflowAsync(dataSource, mobile, givenName: "نام متفاوت");
        var operationId = Guid.CreateVersion7();
        var service = CreateService(dataSource);

        var result = await service.PromoteAsync(
            new TemporaryWorkflowPromotionCommand(
                workflowId, operationId, Actor, existingPartyId.ToString("D"),
                CreateNewParty: false, ConfirmDistinctParty: false), default);

        Assert.Equal(TemporaryWorkflowPromotionOutcome.Existing, result.Outcome);
        Assert.False(result.Created);
        Assert.Equal(existingPartyId.ToString("D"), result.PartyId);
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_promotions WHERE workflow_record_id = @id AND outcome = 'Existing';",
            workflowId));
        Assert.Equal(0L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key = @id;", operationId));
        Assert.Equal(0L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_payloads WHERE workflow_record_id = @id;", workflowId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC37_ForceNew_returns_candidates_until_explicit_confirmation_and_audits_the_override()
    {
        await using var dataSource = OpenDataSource();
        var mobile = NewMobile();
        var existingPartyId = await CreateExistingPartyAsync(dataSource, mobile);
        var workflowId = await CreateWorkflowAsync(dataSource, mobile, givenName: "شخص مستقل");
        var operationId = Guid.CreateVersion7();
        var service = CreateService(dataSource);
        var command = NewCommand(workflowId, operationId, createNew: true);

        var preview = await service.PromoteAsync(command, default);

        Assert.Equal(TemporaryWorkflowPromotionOutcome.DuplicateReviewRequired, preview.Outcome);
        Assert.Single(preview.Candidates);
        Assert.Equal(existingPartyId.ToString("D"), preview.Candidates[0].PartyId);
        Assert.Equal("open", await ScalarTextAsync(dataSource,
            "SELECT lifecycle_status FROM persons_temporary_workflows WHERE workflow_record_id = @id;", workflowId));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_payloads WHERE workflow_record_id = @id;", workflowId));
        Assert.Equal(0L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_temporary_workflow_promotions WHERE workflow_record_id = @id;", workflowId));

        var confirmed = await service.PromoteAsync(command with { ConfirmDistinctParty = true }, default);

        Assert.Equal(TemporaryWorkflowPromotionOutcome.Created, confirmed.Outcome);
        Assert.NotEqual(existingPartyId.ToString("D"), confirmed.PartyId);
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_access_audit WHERE party_id = @id AND action = 'force-new-confirmed';",
            existingPartyId));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key = @id AND event_type = 'PartyRegistered';",
            operationId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC37_failed_outbox_insert_rolls_back_party_workflow_tombstone_and_payload_deletion()
    {
        await using var dataSource = OpenDataSource();
        var mobile = NewMobile();
        var workflowId = await CreateWorkflowAsync(dataSource, mobile);
        var operationId = Guid.CreateVersion7();
        var functionName = "ci_fail_outbox_" + operationId.ToString("N");
        var triggerName = "trg_ci_fail_outbox_" + operationId.ToString("N");

        await ExecuteAsync(dataSource, $"""
            CREATE FUNCTION public.{functionName}() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.idempotency_key = '{operationId:D}' THEN
                    RAISE EXCEPTION 'injected AC-37 Outbox failure';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER {triggerName}
            BEFORE INSERT ON persons_outbox_events
            FOR EACH ROW EXECUTE FUNCTION public.{functionName}();
            """);

        try
        {
            var service = CreateService(dataSource);
            await Assert.ThrowsAsync<PostgresException>(() =>
                service.PromoteAsync(NewCommand(workflowId, operationId, createNew: true), default));

            Assert.Equal("open", await ScalarTextAsync(dataSource,
                "SELECT lifecycle_status FROM persons_temporary_workflows WHERE workflow_record_id = @id;", workflowId));
            Assert.Equal(1L, await ScalarAsync(dataSource,
                "SELECT count(*) FROM persons_temporary_workflow_payloads WHERE workflow_record_id = @id;", workflowId));
            Assert.Equal(0L, await ScalarAsync(dataSource,
                "SELECT count(*) FROM persons_temporary_workflow_promotions WHERE workflow_record_id = @id;", workflowId));
            Assert.Equal(0L, await ScalarAsync(dataSource,
                "SELECT count(*) FROM persons_operations WHERE operation_id = @id;", operationId));
            Assert.Equal(0L, await ScalarAsync(dataSource,
                "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key = @id;", operationId));
        }
        finally
        {
            await ExecuteAsync(dataSource, $"""
                DROP TRIGGER IF EXISTS {triggerName} ON persons_outbox_events;
                DROP FUNCTION IF EXISTS public.{functionName}();
                """);
        }
    }

    private static PostgresTemporaryWorkflowPromotionService CreateService(NpgsqlDataSource dataSource) =>
        new(dataSource, new PostgresPartyRepository(dataSource),
            new AesGcmTemporaryWorkflowPayloadProtector(new TestKeyProvider()),
            new AllowAllPromotionAuthorizer(), TimeProvider.System);

    private static TemporaryWorkflowPromotionCommand NewCommand(Guid workflowId, Guid operationId, bool createNew) =>
        new(workflowId, operationId, Actor, null, createNew, false, Guid.CreateVersion7());

    private static async Task<Guid> CreateWorkflowAsync(
        NpgsqlDataSource dataSource, string mobile, string givenName = "سارا", DateTimeOffset? createdAtUtc = null)
    {
        var workflowId = Guid.CreateVersion7();
        var plaintext = Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": 1,
              "partyType": "NaturalPerson",
              "roles": ["Customer"],
              "givenName": "{{givenName}}",
              "familyName": "محمدی",
              "primaryMobile": "{{mobile}}",
              "acquaintanceSource": "WalkIn"
            }
            """);
        var ciphertext = Encrypt(workflowId, KeyVersion, EncryptionKey, plaintext);
        await new PostgresTemporaryWorkflowStore(dataSource).CreateAsync(
            workflowId, "Persons", "PartyRegistration", ActorText, Guid.CreateVersion7(),
            createdAtUtc ?? DateTimeOffset.UtcNow, ciphertext, KeyVersion);
        CryptographicOperations.ZeroMemory(plaintext);
        return workflowId;
    }

    private static async Task<Guid> CreateExistingPartyAsync(NpgsqlDataSource dataSource, string mobile)
    {
        var repository = new PostgresPartyRepository(dataSource);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var request = new RegisterNaturalParty(
            "شخص", "موجود", PartyBusinessRoles.Customer, null,
            mobile, null, null, null, null, null, null, null,
            PartyAcquaintanceSource.Unknown, null, PartyCommunicationPreferences.Default);
        var result = await repository.AddNaturalAsync(
            request, ActorText, Guid.CreateVersion7(), transaction, default);
        await transaction.CommitAsync();
        return Guid.Parse(result.PartyId);
    }

    private static string NewMobile() =>
        "0912" + Random.Shared.Next(1_000_000, 9_999_999).ToString("D7");

    private static byte[] Encrypt(Guid workflowId, string keyVersion, byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var encrypted = new byte[plaintext.Length];
        var aad = AesGcmTemporaryWorkflowPayloadProtector.CreateAssociatedData(workflowId, keyVersion);
        using (var aes = new AesGcm(key, tagSizeInBytes: 16))
            aes.Encrypt(nonce, plaintext, encrypted, tag, aad);
        CryptographicOperations.ZeroMemory(aad);
        return [.. nonce, .. tag, .. encrypted];
    }

    private static NpgsqlDataSource OpenDataSource()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "AC-37 PostgreSQL tests require the disposable CI database.");
        return NpgsqlDataSource.Create(connectionString!);
    }

    private static async Task<long> ScalarAsync(NpgsqlDataSource dataSource, string sql, Guid id)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarTextAsync(NpgsqlDataSource dataSource, string sql, Guid id)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql, Guid id)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string[]> ColumnNamesAsync(NpgsqlDataSource dataSource, string table)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table;",
            connection);
        command.Parameters.AddWithValue("table", table);
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names.ToArray();
    }

    private sealed class TestKeyProvider : ITemporaryWorkflowEncryptionKeyProvider
    {
        public ValueTask<byte[]> GetKeyAsync(string keyVersion, CancellationToken cancellationToken) =>
            ValueTask.FromResult(EncryptionKey.ToArray());
    }

    private sealed class AllowAllPromotionAuthorizer : IPersonsWorkflowPromotionAuthorizer
    {
        public Task<bool> HasPermissionAsync(Guid actorId, string permissionKey, CancellationToken cancellationToken) =>
            Task.FromResult(actorId != Guid.Empty &&
                permissionKey is "persons.workflow.promote" or "persons.read" or "persons.create" or "persons.update");
    }
}
