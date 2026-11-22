using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL two-writer policy proof for command idempotency and unique active
/// assignment invariants. It is not a production Organization implementation or the
/// Platform-wide lock-order registry.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OrganizationIdempotencyConcurrencyPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_same_key_replay_and_distinct_key_same_assignment_resolve_to_one_fact()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_race_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.account_warehouse_assignment
                (
                    assignment_id uuid PRIMARY KEY,
                    subject_account_id uuid NOT NULL,
                    warehouse_id uuid NOT NULL,
                    state text NOT NULL CHECK (state IN ('ACTIVE','REVOKED')),
                    created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                    UNIQUE(subject_account_id,warehouse_id)
                );
                CREATE TABLE {q}.command_receipt
                (
                    idempotency_key text PRIMARY KEY,
                    request_hash bytea NOT NULL CHECK (octet_length(request_hash)=32),
                    assignment_id uuid NOT NULL,
                    created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
                );

                CREATE FUNCTION {q}.apply_assignment(
                    p_key text,p_hash bytea,p_subject uuid,p_warehouse uuid)
                RETURNS uuid LANGUAGE plpgsql AS $fn$
                DECLARE
                    target_assignment uuid;
                    stored_hash bytea;
                    inserted_count integer;
                BEGIN
                    IF p_key IS NULL OR length(btrim(p_key))=0 OR octet_length(p_hash)<>32 THEN
                        RAISE EXCEPTION 'idempotency key and SHA-256 request hash are required'
                            USING ERRCODE='22023';
                    END IF;

                    INSERT INTO {q}.command_receipt(idempotency_key,request_hash,assignment_id)
                    VALUES (p_key,p_hash,gen_random_uuid())
                    ON CONFLICT (idempotency_key) DO NOTHING;

                    SELECT request_hash,assignment_id
                    INTO stored_hash,target_assignment
                    FROM {q}.command_receipt
                    WHERE idempotency_key=p_key
                    FOR UPDATE;

                    IF stored_hash<>p_hash THEN
                        RAISE EXCEPTION 'idempotency key reused with a different request fingerprint'
                            USING ERRCODE='23505';
                    END IF;

                    INSERT INTO {q}.account_warehouse_assignment
                        (assignment_id,subject_account_id,warehouse_id,state)
                    VALUES (target_assignment,p_subject,p_warehouse,'ACTIVE')
                    ON CONFLICT DO NOTHING;

                    GET DIAGNOSTICS inserted_count = ROW_COUNT;
                    IF inserted_count=0 THEN
                        SELECT assignment_id INTO target_assignment
                        FROM {q}.account_warehouse_assignment
                        WHERE subject_account_id=p_subject AND warehouse_id=p_warehouse
                        FOR UPDATE;
                        UPDATE {q}.command_receipt
                        SET assignment_id=target_assignment
                        WHERE idempotency_key=p_key;
                    END IF;

                    RETURN target_assignment;
                END;
                $fn$;
                """);

            var subject = Guid.NewGuid();
            var warehouse = Guid.NewGuid();
            var fingerprint = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("grant|subject|warehouse|reason-v1"));
            var key = "o20-race-same-key-001";

            var sameKeyResults = await RunTwoWritersAsync(
                () => ScalarGuidAsync(cs, $"SELECT {q}.apply_assignment(@key,@hash,@subject,@warehouse);",
                    ("key", key), ("hash", fingerprint), ("subject", subject), ("warehouse", warehouse)),
                () => ScalarGuidAsync(cs, $"SELECT {q}.apply_assignment(@key,@hash,@subject,@warehouse);",
                    ("key", key), ("hash", fingerprint), ("subject", subject), ("warehouse", warehouse)));

            Assert.Equal(sameKeyResults[0], sameKeyResults[1]);
            Assert.Equal(1L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.command_receipt WHERE idempotency_key=@key;", ("key", key)));
            Assert.Equal(1L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.account_warehouse_assignment WHERE subject_account_id=@subject AND warehouse_id=@warehouse AND state='ACTIVE';",
                ("subject", subject), ("warehouse", warehouse)));

            // A replay with the same idempotency key but altered request bytes must fail closed.
            var alteredFingerprint = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("grant|subject|warehouse|different-reason"));
            var mismatch = await Assert.ThrowsAsync<PostgresException>(() =>
                ScalarGuidAsync(cs, $"SELECT {q}.apply_assignment(@key,@hash,@subject,@warehouse);",
                    ("key", key), ("hash", alteredFingerprint), ("subject", subject), ("warehouse", warehouse)));
            Assert.Equal("23505", mismatch.SqlState);

            // Separate idempotency keys for the same business assignment still converge on one stable assignment.
            var distinctKeyResults = await RunTwoWritersAsync(
                () => ScalarGuidAsync(cs, $"SELECT {q}.apply_assignment(@key,@hash,@subject,@warehouse);",
                    ("key", "o20-race-distinct-key-A"), ("hash", fingerprint), ("subject", subject), ("warehouse", warehouse)),
                () => ScalarGuidAsync(cs, $"SELECT {q}.apply_assignment(@key,@hash,@subject,@warehouse);",
                    ("key", "o20-race-distinct-key-B"), ("hash", fingerprint), ("subject", subject), ("warehouse", warehouse)));

            Assert.Equal(sameKeyResults[0], distinctKeyResults[0]);
            Assert.Equal(sameKeyResults[0], distinctKeyResults[1]);
            Assert.Equal(3L, await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.command_receipt;"));
            Assert.Equal(1L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.account_warehouse_assignment WHERE subject_account_id=@subject AND warehouse_id=@warehouse;",
                ("subject", subject), ("warehouse", warehouse)));
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }

    private static async Task<Guid[]> RunTwoWritersAsync(Func<Task<Guid>> first, Func<Task<Guid>> second)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task1 = Task.Run(async () =>
        {
            await gate.Task;
            return await first();
        });
        var task2 = Task.Run(async () =>
        {
            await gate.Task;
            return await second();
        });
        gate.SetResult();
        return await Task.WhenAll(task1, task2);
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

    private static async Task<Guid> ScalarGuidAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> ScalarInt64Async(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
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
