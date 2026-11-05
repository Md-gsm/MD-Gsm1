using System.Security.Cryptography;
using MDGsm.Infrastructure.Persons;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsTemporaryWorkflowPostgresTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC32_ciphertext_is_stored_and_expiry_blocks_reads_before_cleanup()
    {
        await using var dataSource = OpenDataSource();
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        var id = Guid.CreateVersion7();
        var ciphertext = Encrypt("private contact payload"u8);
        await store.CreateAsync(id, "Persons", "contact-capture", "ci-actor", Guid.CreateVersion7(),
            ciphertext, "test-key-v1");

        var stored = await store.ReadCiphertextAsync(id);
        Assert.NotNull(stored);
        Assert.Equal(ciphertext, stored);

        // Force expiry in PostgreSQL while preserving the table's created < expiry invariant.
        await SetExpiryAsync(dataSource, id);
        Assert.Null(await store.ReadCiphertextAsync(id));
        Assert.Equal(1, await store.PurgeExpiredAsync(250));
        Assert.Null(await store.ReadCiphertextAsync(id));
        Assert.Equal(0L, await CountPayloadRowsAsync(dataSource, id));
        Assert.Equal("purged", await ReadLifecycleStatusAsync(dataSource, id));
        Assert.Equal(0, await store.PurgeExpiredAsync(250));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC32_worker_drains_expired_payloads_across_multiple_database_batches()
    {
        await using var dataSource = OpenDataSource();
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        const string purpose = "retention-batch-test";

        for (var index = 0; index < 251; index++)
        {
            await store.CreateAsync(Guid.CreateVersion7(), "Persons", purpose, "ci-actor",
                Guid.CreateVersion7(), Encrypt("temporary private data"u8), "test-key-v1");
        }

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            WITH clock AS (SELECT clock_timestamp() AS now_utc)
            UPDATE persons_temporary_workflows AS workflow
            SET created_at_utc = clock.now_utc - interval '2 days',
                expires_at_utc = clock.now_utc - interval '1 second',
                absolute_expires_at_utc = clock.now_utc + interval '28 days',
                row_version = workflow.row_version + 1
            FROM clock
            WHERE workflow.owner_module = 'Persons' AND workflow.purpose_code = @purpose;
            """, connection))
        {
            command.Parameters.AddWithValue("purpose", purpose);
            Assert.Equal(251, await command.ExecuteNonQueryAsync());
        }

        var logger = new CapturingLogger<TemporaryWorkflowRetentionSweep>();
        var alertLogger = new CapturingLogger<StructuredLogTemporaryWorkflowRetentionAlert>();
        var alert = new StructuredLogTemporaryWorkflowRetentionAlert(alertLogger);
        var sweep = new TemporaryWorkflowRetentionSweep(store, alert, logger);

        Assert.Equal(TimeSpan.FromMinutes(5), TemporaryWorkflowRetentionWorker.SweepInterval);
        Assert.True(await sweep.RunOnceAsync());

        await using var verify = await dataSource.OpenConnectionAsync();
        await using var verifyCommand = new NpgsqlCommand("""
            SELECT count(*) FILTER (WHERE payload.workflow_record_id IS NOT NULL),
                   count(*) FILTER (WHERE workflow.lifecycle_status = 'purged')
            FROM persons_temporary_workflows AS workflow
            LEFT JOIN persons_temporary_workflow_payloads AS payload
              ON payload.workflow_record_id = workflow.workflow_record_id
            WHERE workflow.owner_module = 'Persons' AND workflow.purpose_code = @purpose;
            """, verify);
        verifyCommand.Parameters.AddWithValue("purpose", purpose);
        await using var reader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0L, reader.GetInt64(0));
        Assert.Equal(251L, reader.GetInt64(1));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC36_person_search_does_not_discover_encrypted_temporary_workflow_payloads()
    {
        await using var dataSource = OpenDataSource();
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        var term = "نام موقت غیرقابل جستجو";
        await store.CreateAsync(Guid.CreateVersion7(), "Persons", "search-isolation-test", "ci-actor",
            Guid.CreateVersion7(), Encrypt(System.Text.Encoding.UTF8.GetBytes(term)), "test-key-v1");

        var repository = new PostgresPartyRepository(dataSource);
        var results = await repository.SearchAsync(term, 10, null, default);

        Assert.Empty(results);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC35_closed_workflow_is_not_readable_and_is_purged_after_expiry()
    {
        await using var dataSource = OpenDataSource();
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        var id = Guid.CreateVersion7();
        await store.CreateAsync(id, "Persons", "contact-capture", "ci-actor", Guid.CreateVersion7(),
            Encrypt("private payload"u8), "test-key-v1");

        Assert.True(await store.CloseAsync(id));
        Assert.False(await store.CloseAsync(id));
        Assert.Null(await store.ReadCiphertextAsync(id));
        await SetExpiryAsync(dataSource, id);
        Assert.Equal(1, await store.PurgeExpiredAsync(250));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC33_AC34_renewal_is_single_use_authorized_and_capped_at_thirty_days()
    {
        await using var dataSource = OpenDataSource();
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        var id = Guid.CreateVersion7();
        await store.CreateAsync(id, "Persons", "contact-capture", "ci-actor", Guid.CreateVersion7(),
            Encrypt("private payload"u8), "test-key-v1");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.RenewAsync(id,
            TimeSpan.FromDays(1), "No authorization supplied", "ci-actor",
            explicitlyAuthorized: false));

        Assert.True(await store.RenewAsync(id, TimeSpan.FromDays(40),
            "Owner approved temporary extension", "ci-actor", explicitlyAuthorized: true));

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT created_at_utc, expires_at_utc, renewal_count
            FROM persons_temporary_workflows
            WHERE workflow_record_id = @id;
            """, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var createdAt = reader.GetFieldValue<DateTimeOffset>(0);
        Assert.Equal(createdAt.AddDays(30), reader.GetFieldValue<DateTimeOffset>(1));
        Assert.Equal((short)1, reader.GetInt16(2));
        await reader.DisposeAsync();

        Assert.False(await store.RenewAsync(id, TimeSpan.FromDays(1),
            "Second renewal attempt rejected", "ci-actor", explicitlyAuthorized: true));
    }

    private static async Task SetExpiryAsync(NpgsqlDataSource dataSource, Guid workflowId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            WITH clock AS (SELECT clock_timestamp() AS now_utc)
            UPDATE persons_temporary_workflows AS workflow
            SET created_at_utc = clock.now_utc - interval '2 days',
                expires_at_utc = clock.now_utc - interval '1 second',
                absolute_expires_at_utc = clock.now_utc + interval '28 days',
                row_version = workflow.row_version + 1
            FROM clock
            WHERE workflow.workflow_record_id = @id;
            """, connection);
        command.Parameters.AddWithValue("id", workflowId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task<long> CountPayloadRowsAsync(NpgsqlDataSource dataSource, Guid workflowId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM persons_temporary_workflow_payloads WHERE workflow_record_id = @id;", connection);
        command.Parameters.AddWithValue("id", workflowId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> ReadLifecycleStatusAsync(NpgsqlDataSource dataSource, Guid workflowId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT lifecycle_status FROM persons_temporary_workflows WHERE workflow_record_id = @id;", connection);
        command.Parameters.AddWithValue("id", workflowId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static NpgsqlDataSource OpenDataSource()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "The integration test requires the marked disposable PostgreSQL database supplied by CI.");
        return NpgsqlDataSource.Create(connectionString!);
    }

    private static byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return [.. nonce, .. tag, .. ciphertext];
    }
}
