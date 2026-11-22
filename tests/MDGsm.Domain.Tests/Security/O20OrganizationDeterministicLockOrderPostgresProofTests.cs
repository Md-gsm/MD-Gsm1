using System;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL experiment for deterministic acquisition of multiple
/// Organization-owned aggregate rows. It validates one candidate local ordering only;
/// it does not approve the Platform-wide cross-module Lock-Order Registry.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OrganizationDeterministicLockOrderPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Reversed_caller_input_order_uses_one_deterministic_uuid_lock_order()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_lock_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.aggregate_lock_probe
                (
                    aggregate_id uuid PRIMARY KEY,
                    row_version bigint NOT NULL DEFAULT 1
                );
                """);

            var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            foreach (var id in ids)
                await ExecuteAsync(cs,
                    $"INSERT INTO {q}.aggregate_lock_probe(aggregate_id) VALUES (@id);",
                    ("id", id));

            // The two callers deliberately submit opposite input order. The ordering key
            // is canonical lowercase UUID text (equivalent to PostgreSQL uuid byte order).
            var forward = ids.ToArray();
            var reverse = ids.Reverse().ToArray();
            var firstOrder = CanonicalOrder(forward);
            var secondOrder = CanonicalOrder(reverse);
            Assert.Equal(firstOrder, secondOrder);

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstWriter = Task.Run(async () =>
            {
                await start.Task;
                return await LockAndIncrementAsync(cs, q, forward, "writer-forward");
            });
            var secondWriter = Task.Run(async () =>
            {
                await start.Task;
                return await LockAndIncrementAsync(cs, q, reverse, "writer-reverse");
            });
            start.SetResult();

            var acquired = await Task.WhenAll(firstWriter, secondWriter);
            Assert.Equal(firstOrder, acquired[0]);
            Assert.Equal(secondOrder, acquired[1]);

            Assert.Equal((long)ids.Length,
                await ScalarInt64Async(cs,
                    $"SELECT count(*) FROM {q}.aggregate_lock_probe WHERE row_version=3;"));
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }

    private static string[] CanonicalOrder(Guid[] values) =>
        values.Select(x => x.ToString("N"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    private static async Task<string[]> LockAndIncrementAsync(
        string cs, string quotedSchema, Guid[] callerOrder, string applicationName)
    {
        var ordered = callerOrder.OrderBy(x => x.ToString("N"), StringComparer.Ordinal).ToArray();
        var recorded = ordered.Select(x => x.ToString("N")).ToArray();
        var builder = new NpgsqlConnectionStringBuilder(cs) { ApplicationName = applicationName };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var settings = new NpgsqlCommand(
            "SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='15s';",
            connection, transaction))
        {
            await settings.ExecuteNonQueryAsync();
        }

        for (var i = 0; i < ordered.Length; i++)
        {
            await using var command = new NpgsqlCommand(
                $"SELECT aggregate_id FROM {quotedSchema}.aggregate_lock_probe WHERE aggregate_id=@id FOR UPDATE;",
                connection, transaction);
            command.Parameters.AddWithValue("id", ordered[i]);
            var locked = (Guid)(await command.ExecuteScalarAsync())!;
            Assert.Equal(ordered[i], locked);

            // Let the competing connection reach the same first lock while this
            // transaction retains its existing locks. Since both use the same order,
            // it waits on the first row instead of forming a lock cycle.
            if (i == 0)
                await Task.Delay(75);
        }

        foreach (var id in ordered)
        {
            await using var update = new NpgsqlCommand(
                $"UPDATE {quotedSchema}.aggregate_lock_probe SET row_version=row_version+1 WHERE aggregate_id=@id;",
                connection, transaction);
            update.Parameters.AddWithValue("id", id);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }

        await transaction.CommitAsync();
        return recorded;
    }

    private static async Task<string> OpenMarkedDisposableDatabaseAsync()
    {
        var cs = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(cs));
        var builder = new NpgsqlConnectionStringBuilder(cs);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", builder.Database);
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var marker = new NpgsqlCommand(
            "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname=current_database();",
            connection);
        Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB;", (string?)await marker.ExecuteScalarAsync());
        return cs!;
    }

    private static async Task ExecuteAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarInt64Async(string cs, string sql)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task DropSchemaAsync(string cs, string quotedSchema)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
