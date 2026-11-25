using System.Security.Cryptography;
using System.Text;
using MDGsm.Persons.Application;
using MDGsm.Persons.Infrastructure;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

[CollectionDefinition("PersonsTemporaryWorkflowPostgres", DisableParallelization = true)]
public sealed class PersonsTemporaryWorkflowPostgresCollection
{
}

[Collection("PersonsTemporaryWorkflowPostgres")]
public sealed class PersonsTemporaryWorkflowPostgresTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC32_ciphertext_is_stored_and_expiry_blocks_reads_before_cleanup()
    {
        await using var dataSource = OpenDataSource();
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var ciphertext = Encrypt("private contact payload"u8);
        await store.CreateAsync(id, "Persons", "contact-capture", "ci-actor", Guid.CreateVersion7(),
            now, ciphertext, "test-key-v1");

        var stored = await store.ReadCiphertextAsync(id, now.AddDays(6));
        Assert.NotNull(stored);
        Assert.Equal(ciphertext, stored);

        Assert.Null(await store.ReadCiphertextAsync(id, now.AddDays(7)));

        // PurgeExpiredAsync is intentionally database-wide. Other serialized tests may have
        // expired rows in the same disposable database, so assert this record's lifecycle,
        // not a global number of rows deleted.
        await store.PurgeExpiredAsync(now.AddDays(7));
        Assert.Null(await store.ReadCiphertextAsync(id, now.AddDays(6)));
        Assert.Equal(0L, await CountPayloadRowsAsync(dataSource, id));
        Assert.Equal("purged", await ReadLifecycleStatusAsync(dataSource, id));

        await store.PurgeExpiredAsync(now.AddDays(8));
        Assert.Equal(0L, await CountPayloadRowsAsync(dataSource, id));
        Assert.Equal("purged", await ReadLifecycleStatusAsync(dataSource, id));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC36_temporary_workflow_payload_does_not_appear_in_Persons_search_or_search_route()
    {
        await using var dataSource = OpenDataSource();
        await AssertDisposableDatabaseAsync(dataSource);

        const string privateTemporaryName = "AC36-TEMPORARY-PRIVATE-SEARCH-NEVER-EXPOSE";
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        var workflowId = Guid.CreateVersion7();
        await store.CreateAsync(
            workflowId,
            "Persons",
            "contact-capture",
            "ci-actor",
            Guid.CreateVersion7(),
            now,
            Encrypt(Encoding.UTF8.GetBytes(privateTemporaryName)),
            "test-key-v1");

        // Global Persons search reads only committed Party identities and active contacts;
        // it must never query the encrypted temporary-workflow payload store.
        var repository = new PostgresPartyRepository(dataSource);
        var results = await repository.SearchAsync(
            privateTemporaryName,
            PartySearchLimits.MaxResults,
            "ci-temp-search-actor",
            default);
        Assert.Empty(results);

        // The current host exposes only Party details; no search/autocomplete HTTP route is registered.
        var root = FindRepositoryRoot();
        var program = await File.ReadAllTextAsync(Path.Combine(root, "src", "Host", "Api", "Program.cs"));
        Assert.DoesNotContain("app.MapGet(\"/api/v2/persons\",", program, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchAsync(", program, StringComparison.Ordinal);
        Assert.DoesNotContain("autocomplete", program, StringComparison.OrdinalIgnoreCase);

        await using var payloadConnection = await dataSource.OpenConnectionAsync();
        await using var payloadCommand = new NpgsqlCommand(
            "SELECT encrypted_payload FROM persons_temporary_workflow_payloads WHERE workflow_record_id = @id;",
            payloadConnection);
        payloadCommand.Parameters.AddWithValue("id", workflowId);
        var storedCiphertext = (byte[])(await payloadCommand.ExecuteScalarAsync())!;
        Assert.NotEmpty(storedCiphertext);
        var plaintextMarker = Encoding.UTF8.GetBytes(privateTemporaryName);
        Assert.True(storedCiphertext.AsSpan().IndexOf(plaintextMarker) < 0,
            "Temporary workflow storage must contain ciphertext, never the raw search phrase.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC35_closed_workflow_is_not_readable_and_is_purged_after_expiry()
    {
        await using var dataSource = OpenDataSource();
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await store.CreateAsync(id, "Persons", "contact-capture", "ci-actor", Guid.CreateVersion7(),
            now, Encrypt("private payload"u8), "test-key-v1");

        Assert.True(await store.CloseAsync(id, now.AddDays(1)));
        Assert.False(await store.CloseAsync(id, now.AddDays(1)));
        Assert.Null(await store.ReadCiphertextAsync(id, now.AddDays(1)));

        await store.PurgeExpiredAsync(now.AddDays(7));
        Assert.Equal(0L, await CountPayloadRowsAsync(dataSource, id));
        Assert.Equal("purged", await ReadLifecycleStatusAsync(dataSource, id));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC33_AC34_renewal_is_single_use_authorized_and_capped_at_thirty_days()
    {
        await using var dataSource = OpenDataSource();
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await store.CreateAsync(id, "Persons", "contact-capture", "ci-actor", Guid.CreateVersion7(),
            now, Encrypt("private payload"u8), "test-key-v1");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.RenewAsync(id,
            now.AddDays(1), TimeSpan.FromDays(1), "No authorization supplied", "ci-actor",
            explicitlyAuthorized: false));

        Assert.True(await store.RenewAsync(id, now.AddDays(1), TimeSpan.FromDays(40),
            "Owner approved temporary extension", "ci-actor", explicitlyAuthorized: true));

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT expires_at_utc, renewal_count
            FROM persons_temporary_workflows
            WHERE workflow_record_id = @id;
            """, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(now.AddDays(30), reader.GetFieldValue<DateTimeOffset>(0));
        Assert.Equal((short)1, reader.GetInt16(1));
        await reader.DisposeAsync();

        Assert.False(await store.RenewAsync(id, now.AddDays(2), TimeSpan.FromDays(1),
            "Second renewal attempt rejected", "ci-actor", explicitlyAuthorized: true));
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

    private static async Task AssertDisposableDatabaseAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT coalesce(shobj_description(oid, 'pg_database'), '') FROM pg_database WHERE datname = current_database();",
            connection);
        var marker = (string?)await command.ExecuteScalarAsync();
        Assert.Contains("MDGSM_DISPOSABLE_TEST_DB", marker ?? string.Empty);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "Host", "Api", "Program.cs")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate MD-Gsm repository root for AC-36.");
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
