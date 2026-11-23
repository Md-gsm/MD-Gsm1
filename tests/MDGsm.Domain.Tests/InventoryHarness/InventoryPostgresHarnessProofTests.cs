using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.InventoryHarness;

public sealed class InventoryPostgresHarnessProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Observer_correlates_a_real_postgresql_row_lock_wait_between_independent_sessions()
    {
        await using var harness = await InventoryPostgresHarness.CreateAsync();
        var resourceId = await harness.SeedLockRowAsync("observer-proof");
        var barrierAHasLock = new DeterministicBarrier();
        var barrierBAttempting = new DeterministicBarrier();
        var timeline = new List<string>();
        var repositorySha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "LOCAL-UNSPECIFIED";

        await using var a = await harness.OpenParticipantAsync("writer-a");
        await using var b = await harness.OpenParticipantAsync("writer-b");
        var pidA = await BackendPidAsync(a);
        var pidB = await BackendPidAsync(b);
        Assert.NotEqual(pidA, pidB);

        await using var txA = await a.BeginTransactionAsync();
        await using var txB = await b.BeginTransactionAsync();
        await SetTimeoutsAsync(a, txA);
        await SetTimeoutsAsync(b, txB);

        await LockRowAsync(a, txA, harness.QuotedSchema, resourceId);
        timeline.Add("A:row-lock-acquired");
        barrierAHasLock.Release();

        var writerB = Task.Run(async () =>
        {
            await barrierAHasLock.WaitAsync();
            barrierBAttempting.Release();
            await LockRowAsync(b, txB, harness.QuotedSchema, resourceId);
            timeline.Add("B:row-lock-acquired-after-A-release");
        });

        await barrierBAttempting.WaitAsync();
        var observation = await harness.ObserveWaitAsync(pidB, pidA, TimeSpan.FromSeconds(8));
        timeline.Add("observer:expected-blocker-confirmed");
        Assert.Contains(pidA, observation.BlockingPids);
        Assert.Equal("Lock", observation.WaitEventType);

        await txA.CommitAsync();
        timeline.Add("A:committed");
        await writerB.WaitAsync(TimeSpan.FromSeconds(8));
        await txB.CommitAsync();
        timeline.Add("B:committed");

        var version = await ReadVersionAsync(a, harness.QuotedSchema, resourceId);
        Assert.Equal(1L, version);

        var evidence = new HarnessEvidence(
            "H0-H1-OBSERVER-ROW-LOCK",
            harness.FixtureKind,
            repositorySha,
            await ServerVersionAsync(a),
            a.Database,
            harness.Schema,
            pidA,
            pidB,
            observation,
            timeline,
            "PASS",
            "Synthetic lock_probe only. This proves harness observer mechanics, not Inventory production participation, Inventory Test Spec acceptance, or a global cross-module lock order.");

        var evidenceHash = await harness.WriteEvidenceAsync(evidence);
        Assert.Matches("^[a-f0-9]{64}$", evidenceHash);
    }

    [Fact]
    public void Lock_plan_recorder_enforces_inventory_local_rank_and_same_rank_key_order()
    {
        var recorder = new LockPlanRecorder();
        recorder.Record(0, "IdempotencyReceipt", "actor|op|key");
        recorder.Record(1, "SourceAggregate", "Inventory|Movement|0001");
        recorder.Record(4, "StockPosition", "warehouse|variant|bin");
        recorder.Record(4, "StockPosition", "warehouse|variant|bin-2");
        recorder.Record(7, "WarehouseCommitSequence", "warehouse-01");
        recorder.Record(8, "ProjectionPointer", "projection|active");
        Assert.Equal(6, recorder.Entries.Count);

        Assert.Throws<InvalidOperationException>(() =>
            recorder.Record(3, "ParentShortageScope", "warehouse|variant"));
        Assert.Throws<InvalidOperationException>(() =>
            recorder.Record(4, "StockPosition", "warehouse|variant|bin-0"));
    }

    [Theory]
    [InlineData("40P01", "DEADLOCK_DETECTED")]
    [InlineData("40001", "SERIALIZATION_FAILURE")]
    [InlineData("55P03", "LOCK_NOT_AVAILABLE")]
    [InlineData("57014", "QUERY_CANCELED_OR_TIMEOUT")]
    [InlineData("23505", "NON_TRANSIENT_OR_UNCLASSIFIED")]
    [InlineData(null, "UNKNOWN_OR_TRANSPORT_FAILURE")]
    public void Sqlstate_classification_is_stable(string? sqlState, string expected) =>
        Assert.Equal(expected, PostgreSqlFailureClassifier.Classify(sqlState));

    private static async Task<int> BackendPidAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT pg_backend_pid();", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ServerVersionAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SHOW server_version;", connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SetTimeoutsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand(
            "SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='10s';", connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task LockRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string schema, Guid id)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT resource_id FROM {schema}.lock_probe WHERE resource_id=@id FOR UPDATE;",
            connection, transaction);
        command.Parameters.AddWithValue("id", id);
        Assert.NotNull(await command.ExecuteScalarAsync());
    }

    private static async Task<long> ReadVersionAsync(
        NpgsqlConnection connection, string schema, Guid id)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT row_version FROM {schema}.lock_probe WHERE resource_id=@id;", connection);
        command.Parameters.AddWithValue("id", id);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
