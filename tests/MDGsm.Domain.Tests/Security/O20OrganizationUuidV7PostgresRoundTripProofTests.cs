using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL proof for native UUID round-trip of RFC 9562 UUIDv7.
/// This verifies storage/transport fidelity only; it does not select the Platform-wide
/// identifier standard, prove distributed generator behavior, or authorize production DDL.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OrganizationUuidV7PostgresRoundTripProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Native_postgresql_uuid_round_trip_preserves_uuid_v7_bits_and_identity()
    {
        var connectionString = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20_uuidv7_" + Guid.NewGuid().ToString("N")[..10];
        var quotedSchema = QuoteIdentifier(schema);
        var expected = Enumerable.Range(0, 512)
            .Select(_ => Guid.CreateVersion7())
            .ToArray();

        Assert.Equal(expected.Length, expected.Distinct().Count());
        Assert.All(expected, AssertUuidV7);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using (var create = new NpgsqlCommand($"""
                CREATE SCHEMA {quotedSchema};
                CREATE TABLE {quotedSchema}.uuid_v7_round_trip
                (
                    id uuid PRIMARY KEY
                );
                """, connection))
            {
                await create.ExecuteNonQueryAsync();
            }

            await using (var transaction = await connection.BeginTransactionAsync())
            await using (var insert = new NpgsqlCommand(
                $"INSERT INTO {quotedSchema}.uuid_v7_round_trip(id) VALUES (@id);",
                connection,
                transaction))
            {
                insert.Parameters.Add(new NpgsqlParameter<Guid>("id"));
                foreach (var id in expected)
                {
                    insert.Parameters["id"].Value = id;
                    await insert.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }

            var actual = new List<Guid>();
            await using (var select = new NpgsqlCommand(
                $"SELECT id FROM {quotedSchema}.uuid_v7_round_trip;",
                connection))
            await using (var reader = await select.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    actual.Add(reader.GetGuid(0));
            }

            Assert.Equal(expected.Length, actual.Count);
            Assert.Equal(expected.ToHashSet(), actual.ToHashSet());
            Assert.All(actual, AssertUuidV7);
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;",
                cleanup);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string> OpenMarkedDisposableDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", builder.Database);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var marker = new NpgsqlCommand(
            "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname=current_database();",
            connection);
        Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB;", (string?)await marker.ExecuteScalarAsync());

        return connectionString!;
    }

    private static void AssertUuidV7(Guid value)
    {
        Assert.NotEqual(Guid.Empty, value);
        var canonical = value.ToString("N");
        Assert.Equal('7', canonical[12]);
        Assert.Contains(canonical[16], new[] { '8', '9', 'a', 'b' });
    }

    private static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
