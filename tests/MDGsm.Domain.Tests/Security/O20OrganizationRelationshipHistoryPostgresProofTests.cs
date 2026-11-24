using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL proof for the owner-accepted requirement to retain immutable
/// Warehouse-to-Branch relationship history during reassignment. This is a policy PoC,
/// not a production Organization schema or migration.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OrganizationRelationshipHistoryPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Warehouse_branch_reassignment_appends_immutable_history_atomically()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_history_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE TABLE {q}.branch
                (
                    branch_id uuid PRIMARY KEY,
                    state text NOT NULL CHECK (state IN ('ACTIVE','INACTIVE','ARCHIVED'))
                );
                CREATE TABLE {q}.warehouse
                (
                    warehouse_id uuid PRIMARY KEY,
                    branch_id uuid NOT NULL REFERENCES {q}.branch(branch_id),
                    state text NOT NULL CHECK (state IN ('ACTIVE','INACTIVE','ARCHIVED')),
                    row_version bigint NOT NULL DEFAULT 1
                );
                CREATE TABLE {q}.warehouse_branch_history
                (
                    warehouse_id uuid NOT NULL REFERENCES {q}.warehouse(warehouse_id),
                    relationship_version bigint NOT NULL CHECK (relationship_version > 0),
                    branch_id uuid NOT NULL REFERENCES {q}.branch(branch_id),
                    event_type text NOT NULL CHECK (event_type IN ('CREATED','BRANCH_REASSIGNED')),
                    reason text NOT NULL CHECK (length(btrim(reason)) > 0),
                    recorded_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                    PRIMARY KEY (warehouse_id, relationship_version)
                );
                CREATE FUNCTION {q}.deny_history_mutation() RETURNS trigger
                LANGUAGE plpgsql AS $fn$
                BEGIN
                    RAISE EXCEPTION 'warehouse branch history is append-only' USING ERRCODE='23514';
                END;
                $fn$;
                CREATE TRIGGER warehouse_branch_history_immutable
                BEFORE UPDATE OR DELETE ON {q}.warehouse_branch_history
                FOR EACH ROW EXECUTE FUNCTION {q}.deny_history_mutation();

                CREATE FUNCTION {q}.reassign_warehouse(p_warehouse uuid, p_destination uuid, p_reason text)
                RETURNS void LANGUAGE plpgsql AS $fn$
                DECLARE
                    current_state text;
                    current_branch uuid;
                    current_version bigint;
                BEGIN
                    SELECT state, branch_id, row_version
                    INTO current_state, current_branch, current_version
                    FROM {q}.warehouse
                    WHERE warehouse_id=p_warehouse
                    FOR UPDATE;

                    IF NOT FOUND OR current_state <> 'INACTIVE' THEN
                        RAISE EXCEPTION 'warehouse must be inactive' USING ERRCODE='23514';
                    END IF;
                    IF p_reason IS NULL OR length(btrim(p_reason))=0 THEN
                        RAISE EXCEPTION 'reassignment reason is required' USING ERRCODE='23514';
                    END IF;
                    IF current_branch=p_destination THEN
                        RAISE EXCEPTION 'warehouse already belongs to destination branch' USING ERRCODE='23514';
                    END IF;

                    PERFORM 1
                    FROM {q}.branch
                    WHERE branch_id=p_destination AND state='ACTIVE'
                    FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'destination branch must be active' USING ERRCODE='23514';
                    END IF;

                    UPDATE {q}.warehouse
                    SET branch_id=p_destination, row_version=row_version+1
                    WHERE warehouse_id=p_warehouse;

                    INSERT INTO {q}.warehouse_branch_history
                        (warehouse_id, relationship_version, branch_id, event_type, reason)
                    VALUES
                        (p_warehouse, current_version+1, p_destination, 'BRANCH_REASSIGNED', p_reason);
                END;
                $fn$;
                """);

            var sourceBranch = Guid.NewGuid();
            var destinationBranch = Guid.NewGuid();
            var inactiveBranch = Guid.NewGuid();
            var warehouse = Guid.NewGuid();

            await ExecuteAsync(cs, $"INSERT INTO {q}.branch(branch_id,state) VALUES (@id,'ACTIVE');", ("id", sourceBranch));
            await ExecuteAsync(cs, $"INSERT INTO {q}.branch(branch_id,state) VALUES (@id,'ACTIVE');", ("id", destinationBranch));
            await ExecuteAsync(cs, $"INSERT INTO {q}.branch(branch_id,state) VALUES (@id,'INACTIVE');", ("id", inactiveBranch));
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.warehouse(warehouse_id,branch_id,state) VALUES (@warehouse,@branch,'ACTIVE');",
                ("warehouse", warehouse), ("branch", sourceBranch));
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.warehouse_branch_history(warehouse_id,relationship_version,branch_id,event_type,reason) VALUES (@warehouse,1,@branch,'CREATED','initial registration');",
                ("warehouse", warehouse), ("branch", sourceBranch));

            var activeReassignment = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(cs, $"SELECT {q}.reassign_warehouse(@warehouse,@destination,@reason);",
                    ("warehouse", warehouse), ("destination", destinationBranch), ("reason", "approved test change")));
            Assert.Equal("23514", activeReassignment.SqlState);

            await ExecuteAsync(cs, $"UPDATE {q}.warehouse SET state='INACTIVE' WHERE warehouse_id=@warehouse;", ("warehouse", warehouse));

            var inactiveDestination = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(cs, $"SELECT {q}.reassign_warehouse(@warehouse,@destination,@reason);",
                    ("warehouse", warehouse), ("destination", inactiveBranch), ("reason", "approved test change")));
            Assert.Equal("23514", inactiveDestination.SqlState);

            // A rejected reassignment must leave both current state and history untouched.
            Assert.Equal(sourceBranch,
                await ScalarGuidAsync(cs, $"SELECT branch_id FROM {q}.warehouse WHERE warehouse_id=@warehouse;", ("warehouse", warehouse)));
            Assert.Equal(1L,
                await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.warehouse_branch_history WHERE warehouse_id=@warehouse;", ("warehouse", warehouse)));

            await ExecuteAsync(cs, $"SELECT {q}.reassign_warehouse(@warehouse,@destination,@reason);",
                ("warehouse", warehouse), ("destination", destinationBranch), ("reason", "owner-accepted controlled reassignment"));

            Assert.Equal(destinationBranch,
                await ScalarGuidAsync(cs, $"SELECT branch_id FROM {q}.warehouse WHERE warehouse_id=@warehouse;", ("warehouse", warehouse)));
            Assert.Equal(2L,
                await ScalarInt64Async(cs, $"SELECT row_version FROM {q}.warehouse WHERE warehouse_id=@warehouse;", ("warehouse", warehouse)));
            Assert.Equal(2L,
                await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.warehouse_branch_history WHERE warehouse_id=@warehouse;", ("warehouse", warehouse)));

            var history = await ReadHistoryAsync(cs, q, warehouse);
            Assert.Collection(
                history,
                first =>
                {
                    Assert.Equal(1L, first.Version);
                    Assert.Equal(sourceBranch, first.BranchId);
                    Assert.Equal("CREATED", first.EventType);
                },
                second =>
                {
                    Assert.Equal(2L, second.Version);
                    Assert.Equal(destinationBranch, second.BranchId);
                    Assert.Equal("BRANCH_REASSIGNED", second.EventType);
                });

            var updateHistory = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(cs,
                    $"UPDATE {q}.warehouse_branch_history SET reason='tampered' WHERE warehouse_id=@warehouse AND relationship_version=1;",
                    ("warehouse", warehouse)));
            Assert.Equal("23514", updateHistory.SqlState);

            var deleteHistory = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(cs,
                    $"DELETE FROM {q}.warehouse_branch_history WHERE warehouse_id=@warehouse AND relationship_version=1;",
                    ("warehouse", warehouse)));
            Assert.Equal("23514", deleteHistory.SqlState);
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
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

    private static async Task<long> ScalarInt64Async(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<Guid> ScalarGuidAsync(string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<List<(long Version, Guid BranchId, string EventType)>> ReadHistoryAsync(
        string cs, string quotedSchema, Guid warehouse)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT relationship_version, branch_id, event_type FROM {quotedSchema}.warehouse_branch_history WHERE warehouse_id=@warehouse ORDER BY relationship_version;",
            connection);
        command.Parameters.AddWithValue("warehouse", warehouse);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(long Version, Guid BranchId, string EventType)>();
        while (await reader.ReadAsync())
            rows.Add((reader.GetInt64(0), reader.GetGuid(1), reader.GetString(2)));
        return rows;
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
