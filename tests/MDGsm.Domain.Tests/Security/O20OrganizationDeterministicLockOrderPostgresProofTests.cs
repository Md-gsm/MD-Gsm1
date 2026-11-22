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


    [Fact]
    [Trait("Category", "Integration")]
    public async Task Candidate_branch_warehouse_assignment_rank_order_avoids_reversed_multi_table_cycle()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_rank_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.branch_probe
                (
                    branch_id uuid PRIMARY KEY,
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.warehouse_probe
                (
                    warehouse_id uuid PRIMARY KEY,
                    branch_id uuid NOT NULL REFERENCES {q}.branch_probe(branch_id),
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.assignment_probe
                (
                    assignment_id uuid PRIMARY KEY,
                    warehouse_id uuid NOT NULL REFERENCES {q}.warehouse_probe(warehouse_id),
                    row_version bigint NOT NULL DEFAULT 1
                );
                """);

            var branches = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var warehouses = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var assignments = new[] { Guid.NewGuid(), Guid.NewGuid() };

            await ExecuteAsync(cs,
                $"INSERT INTO {q}.branch_probe(branch_id) VALUES (@id1),(@id2);",
                ("id1", branches[0]), ("id2", branches[1]));
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.warehouse_probe(warehouse_id,branch_id) VALUES (@id1,@branch1),(@id2,@branch2);",
                ("id1", warehouses[0]), ("branch1", branches[0]),
                ("id2", warehouses[1]), ("branch2", branches[1]));
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.assignment_probe(assignment_id,warehouse_id) VALUES (@id1,@warehouse1),(@id2,@warehouse2);",
                ("id1", assignments[0]), ("warehouse1", warehouses[0]),
                ("id2", assignments[1]), ("warehouse2", warehouses[1]));

            var expectedOrder = CandidateRankedOrder(branches, warehouses, assignments);
            var reversedBranches = branches.Reverse().ToArray();
            var reversedWarehouses = warehouses.Reverse().ToArray();
            var reversedAssignments = assignments.Reverse().ToArray();
            Assert.Equal(expectedOrder, CandidateRankedOrder(
                reversedBranches, reversedWarehouses, reversedAssignments));

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = Task.Run(async () =>
            {
                await start.Task;
                return await LockRankedRowsAsync(
                    cs, q, branches, warehouses, assignments, "rank-forward");
            });
            var second = Task.Run(async () =>
            {
                await start.Task;
                return await LockRankedRowsAsync(
                    cs, q, reversedBranches, reversedWarehouses, reversedAssignments, "rank-reverse");
            });
            start.SetResult();

            var acquired = await Task.WhenAll(first, second);
            Assert.Equal(expectedOrder, acquired[0]);
            Assert.Equal(expectedOrder, acquired[1]);

            Assert.Equal(2L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.branch_probe WHERE row_version=3;"));
            Assert.Equal(2L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.warehouse_probe WHERE row_version=3;"));
            Assert.Equal(2L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.assignment_probe WHERE row_version=3;"));
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }


    [Fact]
    [Trait("Category", "Integration")]
    public async Task Candidate_transaction_order_persists_receipt_approval_aggregates_history_and_outbox_atomically()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_totalorder_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.command_receipt
                (
                    receipt_id uuid PRIMARY KEY,
                    request_fingerprint bytea NOT NULL,
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.approval_request
                (
                    request_id uuid PRIMARY KEY,
                    request_fingerprint bytea NOT NULL,
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.branch_probe
                (
                    branch_id uuid PRIMARY KEY,
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.warehouse_probe
                (
                    warehouse_id uuid PRIMARY KEY,
                    branch_id uuid NOT NULL REFERENCES {q}.branch_probe(branch_id),
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.assignment_probe
                (
                    assignment_id uuid PRIMARY KEY,
                    warehouse_id uuid NOT NULL REFERENCES {q}.warehouse_probe(warehouse_id),
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.domain_history
                (
                    event_id uuid PRIMARY KEY,
                    aggregate_id uuid NOT NULL,
                    aggregate_version bigint NOT NULL,
                    event_type text NOT NULL
                );
                CREATE TABLE {q}.audit_outbox
                (
                    event_id uuid PRIMARY KEY,
                    payload_hash bytea NOT NULL CHECK (octet_length(payload_hash)=32),
                    state text NOT NULL CHECK (state='PENDING')
                );
                """);

            var branches = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var warehouses = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var assignments = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var receiptIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var approvalIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var historyIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var outboxIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var fingerprint = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("fixed-candidate-order-request"));

            foreach (var id in receiptIds)
                await ExecuteAsync(cs,
                    $"INSERT INTO {q}.command_receipt(receipt_id,request_fingerprint) VALUES (@id,@hash);",
                    ("id", id), ("hash", fingerprint));
            foreach (var id in approvalIds)
                await ExecuteAsync(cs,
                    $"INSERT INTO {q}.approval_request(request_id,request_fingerprint) VALUES (@id,@hash);",
                    ("id", id), ("hash", fingerprint));
            foreach (var id in branches)
                await ExecuteAsync(cs, $"INSERT INTO {q}.branch_probe(branch_id) VALUES (@id);", ("id", id));
            for (var i = 0; i < warehouses.Length; i++)
                await ExecuteAsync(cs,
                    $"INSERT INTO {q}.warehouse_probe(warehouse_id,branch_id) VALUES (@id,@branch);",
                    ("id", warehouses[i]), ("branch", branches[i]));
            for (var i = 0; i < assignments.Length; i++)
                await ExecuteAsync(cs,
                    $"INSERT INTO {q}.assignment_probe(assignment_id,warehouse_id) VALUES (@id,@warehouse);",
                    ("id", assignments[i]), ("warehouse", warehouses[i]));

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var forward = Task.Run(async () =>
            {
                await start.Task;
                return await RunCandidateTransactionAsync(
                    cs, q, receiptIds[0], approvalIds[0], branches, warehouses, assignments,
                    historyIds[0], outboxIds[0], "candidate-order-forward");
            });
            var reverse = Task.Run(async () =>
            {
                await start.Task;
                return await RunCandidateTransactionAsync(
                    cs, q, receiptIds[1], approvalIds[1], branches.Reverse().ToArray(),
                    warehouses.Reverse().ToArray(), assignments.Reverse().ToArray(),
                    historyIds[1], outboxIds[1], "candidate-order-reverse");
            });
            start.SetResult();

            var logs = await Task.WhenAll(forward, reverse);
            Assert.Equal(CandidateTotalOrder(receiptIds[0], approvalIds[0], branches, warehouses, assignments),
                logs[0]);
            Assert.Equal(CandidateTotalOrder(receiptIds[1], approvalIds[1], branches, warehouses, assignments),
                logs[1]);

            Assert.Equal(2L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.command_receipt WHERE row_version=2;"));
            Assert.Equal(2L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.approval_request WHERE row_version=2;"));
            Assert.Equal(2L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.branch_probe WHERE row_version=3;"));
            Assert.Equal(2L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.warehouse_probe WHERE row_version=3;"));
            Assert.Equal(2L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.assignment_probe WHERE row_version=3;"));
            Assert.Equal(4L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.domain_history;"));
            Assert.Equal(2L, await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.audit_outbox WHERE state='PENDING';"));
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }

    private static string[] CandidateTotalOrder(
        Guid receipt, Guid approval, Guid[] branches, Guid[] warehouses, Guid[] assignments) =>
        new[] { $"00:receipt:{receipt:N}", $"20:approval:{approval:N}" }
            .Concat(branches.OrderBy(x => x.ToString("N"), StringComparer.Ordinal)
                .Select(x => $"30:branch:{x:N}"))
            .Concat(warehouses.OrderBy(x => x.ToString("N"), StringComparer.Ordinal)
                .Select(x => $"40:warehouse:{x:N}"))
            .Concat(assignments.OrderBy(x => x.ToString("N"), StringComparer.Ordinal)
                .Select(x => $"50:assignment:{x:N}"))
            .Concat(new[] { "60:domain-history:append", "70:audit-outbox:append" })
            .ToArray();

    private static async Task<string[]> RunCandidateTransactionAsync(
        string cs,
        string quotedSchema,
        Guid receiptId,
        Guid approvalId,
        Guid[] branches,
        Guid[] warehouses,
        Guid[] assignments,
        Guid historyEventId,
        Guid outboxEventId,
        string applicationName)
    {
        var branchOrder = branches.OrderBy(x => x.ToString("N"), StringComparer.Ordinal).ToArray();
        var warehouseOrder = warehouses.OrderBy(x => x.ToString("N"), StringComparer.Ordinal).ToArray();
        var assignmentOrder = assignments.OrderBy(x => x.ToString("N"), StringComparer.Ordinal).ToArray();
        var acquired = new System.Collections.Generic.List<string>();
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

        await LockOneAsync(connection, transaction,
            $"SELECT receipt_id FROM {quotedSchema}.command_receipt WHERE receipt_id=@id FOR UPDATE;", receiptId);
        acquired.Add($"00:receipt:{receiptId:N}");

        await LockOneAsync(connection, transaction,
            $"SELECT request_id FROM {quotedSchema}.approval_request WHERE request_id=@id FOR UPDATE;", approvalId);
        acquired.Add($"20:approval:{approvalId:N}");

        foreach (var id in branchOrder)
        {
            await LockOneAsync(connection, transaction,
                $"SELECT branch_id FROM {quotedSchema}.branch_probe WHERE branch_id=@id FOR UPDATE;", id);
            acquired.Add($"30:branch:{id:N}");
            if (acquired.Count == 3) await Task.Delay(75);
        }
        foreach (var id in warehouseOrder)
        {
            await LockOneAsync(connection, transaction,
                $"SELECT warehouse_id FROM {quotedSchema}.warehouse_probe WHERE warehouse_id=@id FOR UPDATE;", id);
            acquired.Add($"40:warehouse:{id:N}");
        }
        foreach (var id in assignmentOrder)
        {
            await LockOneAsync(connection, transaction,
                $"SELECT assignment_id FROM {quotedSchema}.assignment_probe WHERE assignment_id=@id FOR UPDATE;", id);
            acquired.Add($"50:assignment:{id:N}");
        }

        await IncrementOneAsync(connection, transaction,
            $"UPDATE {quotedSchema}.command_receipt SET row_version=row_version+1 WHERE receipt_id=@id;", receiptId);
        await IncrementOneAsync(connection, transaction,
            $"UPDATE {quotedSchema}.approval_request SET row_version=row_version+1 WHERE request_id=@id;", approvalId);
        foreach (var id in branchOrder)
            await IncrementOneAsync(connection, transaction,
                $"UPDATE {quotedSchema}.branch_probe SET row_version=row_version+1 WHERE branch_id=@id;", id);
        foreach (var id in warehouseOrder)
            await IncrementOneAsync(connection, transaction,
                $"UPDATE {quotedSchema}.warehouse_probe SET row_version=row_version+1 WHERE warehouse_id=@id;", id);
        foreach (var id in assignmentOrder)
            await IncrementOneAsync(connection, transaction,
                $"UPDATE {quotedSchema}.assignment_probe SET row_version=row_version+1 WHERE assignment_id=@id;", id);

        // Append-only domain history and local outbox are inserted after all aggregate locks,
        // in the same local transaction. No network delivery occurs while locks are held.
        await using (var history = new NpgsqlCommand(
            $"INSERT INTO {quotedSchema}.domain_history(event_id,aggregate_id,aggregate_version,event_type) VALUES (@event,@aggregate,2,'ASSIGNMENT_UPDATED');",
            connection, transaction))
        {
            history.Parameters.AddWithValue("event", historyEventId);
            history.Parameters.AddWithValue("aggregate", assignments[0]);
            Assert.Equal(1, await history.ExecuteNonQueryAsync());
        }

        var payloadHash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"event:{outboxEventId:N}"));
        await using (var outbox = new NpgsqlCommand(
            $"INSERT INTO {quotedSchema}.audit_outbox(event_id,payload_hash,state) VALUES (@event,@hash,'PENDING');",
            connection, transaction))
        {
            outbox.Parameters.AddWithValue("event", outboxEventId);
            outbox.Parameters.AddWithValue("hash", payloadHash);
            Assert.Equal(1, await outbox.ExecuteNonQueryAsync());
        }
        acquired.Add("60:domain-history:append");
        acquired.Add("70:audit-outbox:append");

        await transaction.CommitAsync();
        return acquired.ToArray();
    }

    private static string[] CandidateRankedOrder(
        Guid[] branches, Guid[] warehouses, Guid[] assignments) =>
        branches.OrderBy(x => x.ToString("N"), StringComparer.Ordinal)
            .Select(x => $"30:branch:{x:N}")
            .Concat(warehouses.OrderBy(x => x.ToString("N"), StringComparer.Ordinal)
                .Select(x => $"40:warehouse:{x:N}"))
            .Concat(assignments.OrderBy(x => x.ToString("N"), StringComparer.Ordinal)
                .Select(x => $"50:assignment:{x:N}"))
            .ToArray();

    private static async Task<string[]> LockRankedRowsAsync(
        string cs,
        string quotedSchema,
        Guid[] branches,
        Guid[] warehouses,
        Guid[] assignments,
        string applicationName)
    {
        var branchOrder = branches.OrderBy(x => x.ToString("N"), StringComparer.Ordinal).ToArray();
        var warehouseOrder = warehouses.OrderBy(x => x.ToString("N"), StringComparer.Ordinal).ToArray();
        var assignmentOrder = assignments.OrderBy(x => x.ToString("N"), StringComparer.Ordinal).ToArray();
        var acquired = new System.Collections.Generic.List<string>();
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

        foreach (var id in branchOrder)
        {
            await LockOneAsync(connection, transaction,
                $"SELECT branch_id FROM {quotedSchema}.branch_probe WHERE branch_id=@id FOR UPDATE;", id);
            acquired.Add($"30:branch:{id:N}");
            if (acquired.Count == 1) await Task.Delay(75);
        }
        foreach (var id in warehouseOrder)
        {
            await LockOneAsync(connection, transaction,
                $"SELECT warehouse_id FROM {quotedSchema}.warehouse_probe WHERE warehouse_id=@id FOR UPDATE;", id);
            acquired.Add($"40:warehouse:{id:N}");
        }
        foreach (var id in assignmentOrder)
        {
            await LockOneAsync(connection, transaction,
                $"SELECT assignment_id FROM {quotedSchema}.assignment_probe WHERE assignment_id=@id FOR UPDATE;", id);
            acquired.Add($"50:assignment:{id:N}");
        }

        foreach (var id in branchOrder)
            await IncrementOneAsync(connection, transaction,
                $"UPDATE {quotedSchema}.branch_probe SET row_version=row_version+1 WHERE branch_id=@id;", id);
        foreach (var id in warehouseOrder)
            await IncrementOneAsync(connection, transaction,
                $"UPDATE {quotedSchema}.warehouse_probe SET row_version=row_version+1 WHERE warehouse_id=@id;", id);
        foreach (var id in assignmentOrder)
            await IncrementOneAsync(connection, transaction,
                $"UPDATE {quotedSchema}.assignment_probe SET row_version=row_version+1 WHERE assignment_id=@id;", id);

        await transaction.CommitAsync();
        return acquired.ToArray();
    }

    private static async Task LockOneAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        Assert.Equal(id, (Guid)result!);
    }

    private static async Task IncrementOneAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static string[] CanonicalOrder(Guid[] values) =>
        values.Select(x => x.ToString("N"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    private static async Task<string[]> LockAndIncrementAsync(
        string cs, string quotedSchema, Guid[] callerOrder, string applicationName)
    {
        var ordered = callerOrder.OrderBy(x => x.ToString("N"), StringComparer.Ordinal).ToArray();
        var recorded = new System.Collections.Generic.List<string>(ordered.Length);
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
            recorded.Add(locked.ToString("N"));

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
        return recorded.ToArray();
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
