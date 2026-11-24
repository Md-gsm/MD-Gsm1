using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace MDGsm.Domain.Tests.InventoryHarness;

/// <summary>
/// Test-only PostgreSQL harness primitives. This code never opens an unmarked or
/// non-loopback database and never references production Inventory persistence.
/// </summary>
internal sealed class InventoryPostgresHarness : IAsyncDisposable
{
    private const string ConnectionVariable = "IdentityAccessTestConnectionString";
    private readonly string _connectionString;
    private readonly string _schema;
    private bool _disposed;

    private InventoryPostgresHarness(string connectionString, string schema, string runId)
    {
        _connectionString = connectionString;
        _schema = schema;
        RunId = runId;
        QuotedSchema = QuoteIdentifier(schema);
    }

    public string RunId { get; }
    public string Schema => _schema;
    public string QuotedSchema { get; }
    public string FixtureKind => "SYNTHETIC_HARNESS_MECHANICS_ONLY";

    public static async Task<InventoryPostgresHarness> CreateAsync(CancellationToken cancellationToken = default)
    {
        var raw = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException($"Required disposable PostgreSQL variable '{ConnectionVariable}' is absent.");

        var builder = new NpgsqlConnectionStringBuilder(raw)
        {
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 10
        };

        if (!new[] { "127.0.0.1", "localhost", "::1" }.Contains(builder.Host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Harness refuses non-loopback PostgreSQL hosts.");
        if (string.IsNullOrWhiteSpace(builder.Database) ||
            !System.Text.RegularExpressions.Regex.IsMatch(
                builder.Database, "^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$"))
            throw new InvalidOperationException("Harness refuses databases outside the approved disposable naming convention.");

        await using (var verify = new NpgsqlConnection(builder.ConnectionString))
        {
            await verify.OpenAsync(cancellationToken);
            await using var marker = new NpgsqlCommand(
                "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname=current_database();",
                verify);
            var description = (string?)await marker.ExecuteScalarAsync(cancellationToken);
            if (description is null || !description.StartsWith("MDGSM_DISPOSABLE_TEST_DB;", StringComparison.Ordinal))
                throw new InvalidOperationException("Harness refuses a database without the MDGSM_DISPOSABLE_TEST_DB marker.");
        }

        var runId = Guid.NewGuid().ToString("N");
        var schema = "inv_harness_" + runId[..16];
        var harness = new InventoryPostgresHarness(builder.ConnectionString, schema, runId);
        await harness.ExecuteAsync($"CREATE SCHEMA {harness.QuotedSchema};", cancellationToken);
        await harness.ExecuteAsync($"""
            CREATE TABLE {harness.QuotedSchema}.lock_probe
            (
                resource_id uuid PRIMARY KEY,
                resource_label text NOT NULL,
                row_version bigint NOT NULL DEFAULT 1
            );
            """, cancellationToken);
        return harness;
    }

    public async Task<NpgsqlConnection> OpenParticipantAsync(string participant, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InventoryPostgresHarness));
        if (string.IsNullOrWhiteSpace(participant) || participant.Length > 20 ||
            participant.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Participant label must be a short ASCII identifier.", nameof(participant));

        var builder = new NpgsqlConnectionStringBuilder(_connectionString)
        {
            ApplicationName = $"invh-{RunId[..8]}-{participant}"
        };
        var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task<Guid> SeedLockRowAsync(string label, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenParticipantAsync("fixture", cancellationToken);
        await using var command = new NpgsqlCommand(
            $"INSERT INTO {QuotedSchema}.lock_probe(resource_id,resource_label) VALUES (@id,@label);", connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("label", label);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public async Task<LockObservation> ObserveWaitAsync(
        Guid logicalResourceId, int waiterPid, int expectedBlockerPid, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        await using var observer = await OpenParticipantAsync("observer", cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var command = new NpgsqlCommand("""
                SELECT a.pid,
                       a.application_name,
                       a.wait_event_type,
                       a.wait_event,
                       pg_blocking_pids(a.pid),
                       COALESCE((
                           SELECT jsonb_agg(jsonb_build_object(
                               'locktype', l.locktype,
                               'mode', l.mode,
                               'granted', l.granted,
                               'relation', l.relation::regclass::text,
                               'transactionid', l.transactionid::text))
                           FROM pg_locks l WHERE l.pid=a.pid
                       ), '[]'::jsonb)::text
                FROM pg_stat_activity a
                WHERE a.pid=@waiter;
                """, observer);
            command.Parameters.AddWithValue("waiter", waiterPid);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var blockers = reader.GetFieldValue<int[]>(4);
                var lockJson = reader.GetString(5);
                if (blockers.Contains(expectedBlockerPid))
                {
                    return new LockObservation(
                        logicalResourceId,
                        waiterPid,
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3),
                        blockers,
                        lockJson,
                        DateTimeOffset.UtcNow);
                }
            }
            await reader.DisposeAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException(
            $"Expected PostgreSQL blocker {expectedBlockerPid} for waiter {waiterPid} was not observed within {timeout}.");
    }

    public async Task<string> WriteEvidenceAsync(HarnessEvidence evidence, CancellationToken cancellationToken = default)
    {
        var directory = Environment.GetEnvironmentVariable("MDGSM_INVENTORY_HARNESS_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "InventoryHarness");
        Directory.CreateDirectory(directory);

        var payload = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(payload);
        var path = Path.Combine(directory, $"{RunId}-{evidence.CaseId}.json");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var sidecar = $"{hash}  {Path.GetFileName(path)}{Environment.NewLine}";
        await File.WriteAllTextAsync(path + ".sha256", sidecar, Encoding.ASCII, cancellationToken);
        return hash;
    }

    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = await OpenParticipantAsync("setup", cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"") + "\"";

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {QuotedSchema} CASCADE;", connection);
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed record LockObservation(
    Guid LogicalResourceId,
    int WaiterPid,
    string ApplicationName,
    string? WaitEventType,
    string? WaitEvent,
    int[] BlockingPids,
    string PgLocksJson,
    DateTimeOffset ObservedAtUtc);

internal sealed record HarnessEvidence(
    string CaseId,
    string FixtureKind,
    string RepositorySha,
    string WorkflowTestedSha,
    string PostgreSqlVersion,
    string DatabaseName,
    string Schema,
    int ParticipantAPid,
    int ParticipantBPid,
    LockObservation WaitObservation,
    IReadOnlyList<string> BarrierEvents,
    string Verdict,
    string Limitations);

internal sealed class DeterministicBarrier
{
    private readonly TaskCompletionSource _signal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken cancellationToken = default) =>
        _signal.Task.WaitAsync(cancellationToken);

    public void Release() => _signal.TrySetResult();
}

internal sealed class LockPlanRecorder
{
    private readonly List<LockPlanEntry> _entries = [];
    public IReadOnlyList<LockPlanEntry> Entries => _entries;

    public void Record(int localRank, string resourceClass, string canonicalKey)
    {
        if (localRank is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(localRank));
        if (string.IsNullOrWhiteSpace(resourceClass) || string.IsNullOrWhiteSpace(canonicalKey))
            throw new ArgumentException("Resource class and canonical key are required.");

        if (_entries.Count > 0)
        {
            var previous = _entries[^1];
            if (localRank < previous.LocalRank ||
                (localRank == previous.LocalRank &&
                 StringComparer.Ordinal.Compare(canonicalKey, previous.CanonicalKey) < 0))
                throw new InvalidOperationException("Lock plan descends from the accepted Inventory-local rank/key order.");
        }

        _entries.Add(new LockPlanEntry(localRank, resourceClass, canonicalKey));
    }
}

internal sealed record LockPlanEntry(int LocalRank, string ResourceClass, string CanonicalKey);

internal static class PostgreSqlFailureClassifier
{
    public static string Classify(string? sqlState) => sqlState switch
    {
        "40P01" => "DEADLOCK_DETECTED",
        "40001" => "SERIALIZATION_FAILURE",
        "55P03" => "LOCK_NOT_AVAILABLE",
        "57014" => "QUERY_CANCELED_OR_TIMEOUT",
        null or "" => "UNKNOWN_OR_TRANSPORT_FAILURE",
        _ => "NON_TRANSIENT_OR_UNCLASSIFIED"
    };
}
