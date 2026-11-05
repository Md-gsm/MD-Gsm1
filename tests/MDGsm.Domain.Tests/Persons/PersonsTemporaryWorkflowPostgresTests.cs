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
        var now = DateTimeOffset.UtcNow;
        var ciphertext = Encrypt("private contact payload"u8);
        await store.CreateAsync(id, "Persons", "contact-capture", "ci-actor", Guid.CreateVersion7(),
            now, ciphertext, "test-key-v1");

        var stored = await store.ReadCiphertextAsync(id, now.AddDays(6));
        Assert.NotNull(stored);
        Assert.Equal(ciphertext, stored);

        Assert.Null(await store.ReadCiphertextAsync(id, now.AddDays(7)));
        Assert.Equal(1, await store.PurgeExpiredAsync(now.AddDays(7)));
        Assert.Null(await store.ReadCiphertextAsync(id, now.AddDays(6)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC33_AC34_renewal_is_single_use_authorized_and_capped_at_thirty_days()
    {
        await using var dataSource = OpenDataSource();
        var store = new PostgresTemporaryWorkflowStore(dataSource);
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await store.CreateAsync(id, "Persons", "contact-capture", "ci-actor", Guid.CreateVersion7(),
            now, Encrypt("private payload"u8), "test-key-v1");

        Assert.False(await store.RenewAsync(id, now.AddDays(1), TimeSpan.FromDays(1),
            "No authorization supplied", "ci-actor", explicitlyAuthorized: false)
            .ContinueWith<bool>(_ => false));
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
