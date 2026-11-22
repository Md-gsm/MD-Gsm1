using System.Text;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL proof for owner-accepted Organization policy decisions OD-01..03.
/// This is a policy PoC only; it does not create or authorize a production Organization module.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class O20OrganizationPolicyPostgresProofTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Od01_dotnet_and_postgresql_canonicalization_are_equivalent_and_collision_safe()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_code_" + Guid.NewGuid().ToString("N")[..10];
        var q = Q(schema);
        try
        {
            await ExecuteAsync(cs, $"""
                CREATE SCHEMA {q};
                CREATE FUNCTION {q}.canonical_code(p_input text)
                RETURNS text LANGUAGE plpgsql IMMUTABLE STRICT AS $fn$
                DECLARE
                    v text;
                    ch text;
                    cp integer;
                    i integer;
                    whitespace_chars text := chr(9)||chr(10)||chr(11)||chr(12)||chr(13)||chr(32)||
                        chr(133)||chr(160)||chr(5760)||chr(8192)||chr(8193)||chr(8194)||
                        chr(8195)||chr(8196)||chr(8197)||chr(8198)||chr(8199)||chr(8200)||
                        chr(8201)||chr(8202)||chr(8232)||chr(8233)||chr(8239)||chr(8287)||chr(12288);
                BEGIN
                    v := btrim(normalize(p_input, NFC), whitespace_chars);
                    IF v = '' THEN
                        RAISE EXCEPTION 'business code is empty' USING ERRCODE='22023';
                    END IF;
                    FOR i IN 1..char_length(v) LOOP
                        ch := substr(v, i, 1);
                        cp := ascii(ch);
                        IF cp <= 31 OR cp BETWEEN 127 AND 159 OR
                           cp IN (173,1564,1807,6158,2274,69821,69837,65279,917505)
                           OR cp BETWEEN 1536 AND 1541 OR cp BETWEEN 2192 AND 2193
                           OR cp BETWEEN 8203 AND 8207 OR cp BETWEEN 8234 AND 8238
                           OR cp BETWEEN 8288 AND 8292 OR cp BETWEEN 8294 AND 8303
                           OR cp BETWEEN 65529 AND 65531 OR cp BETWEEN 78896 AND 78911
                           OR cp BETWEEN 113824 AND 113827 OR cp BETWEEN 119155 AND 119162
                           OR cp BETWEEN 917536 AND 917631
                           OR position(ch in whitespace_chars) > 0 THEN
                            RAISE EXCEPTION 'business code contains forbidden scalar U+%', to_hex(cp)
                                USING ERRCODE='22023';
                        END IF;
                    END LOOP;
                    RETURN v;
                END;
                $fn$;

                CREATE TABLE {q}.branch_code_reservation
                (
                    branch_id uuid PRIMARY KEY,
                    canonical_code text COLLATE "C" NOT NULL UNIQUE
                );
                CREATE TABLE {q}.warehouse_code_reservation
                (
                    branch_id uuid NOT NULL,
                    warehouse_id uuid NOT NULL,
                    canonical_code text COLLATE "C" NOT NULL,
                    PRIMARY KEY(branch_id, canonical_code),
                    UNIQUE(warehouse_id, branch_id)
                );
                """);

            var vectors = new[]
            {
                "  BR-01  ",
                "\u00A0WH-02\u2003",
                "Cafe\u0301",
                "\u2003شعبه۱۲\u00A0",
                "A-B_09",
                "a",
                "A",
                "\u0085X\u3000",
                "\u202Fکد\u202F"
            };

            foreach (var input in vectors)
            {
                var expected = CanonicalizeDotNet(input);
                var actual = await ScalarStringAsync(cs,
                    $"SELECT {q}.canonical_code(@value);", ("value", input));
                Assert.Equal(expected, actual);
                Assert.Equal(
                    Encoding.UTF8.GetBytes(expected),
                    Encoding.UTF8.GetBytes(actual));
            }

            var invalid = new[]
            {
                "   ",
                "\u00A0\u2003",
                "A B",
                "A\tB",
                "A\u0001B",
                "A\u200BB",
                "A\u202EB"
            };
            foreach (var input in invalid)
            {
                Assert.Throws<ArgumentException>(() => CanonicalizeDotNet(input));
                var error = await Assert.ThrowsAsync<PostgresException>(() =>
                    ScalarStringAsync(cs, $"SELECT {q}.canonical_code(@value);", ("value", input)));
                Assert.Equal("22023", error.SqlState);
            }

            var branchId = Guid.NewGuid();
            await ExecuteAsync(cs,
                $"INSERT INTO {q}.branch_code_reservation(branch_id,canonical_code) VALUES (@id,{q}.canonical_code(@value));",
                ("id", branchId), ("value", "Caf\u00E9"));
            var collision = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(cs,
                    $"INSERT INTO {q}.branch_code_reservation(branch_id,canonical_code) VALUES (@id,{q}.canonical_code(@value));",
                    ("id", Guid.NewGuid()), ("value", "Cafe\u0301")));
            Assert.Equal("23505", collision.SqlState);

            // C collation remains bytewise and case-sensitive; Persian/Arabic digit forms are distinct.
            var distinct = new[]
            {
                ("A", Guid.NewGuid()),
                ("a", Guid.NewGuid()),
                ("\u06F1", Guid.NewGuid()),
                ("\u0661", Guid.NewGuid()),
                ("A-B", Guid.NewGuid()),
                ("AB", Guid.NewGuid())
            };
            foreach (var (code, id) in distinct)
                await ExecuteAsync(cs,
                    $"INSERT INTO {q}.branch_code_reservation(branch_id,canonical_code) VALUES (@id,{q}.canonical_code(@value));",
                    ("id", id), ("value", code));

            var count = await ScalarInt64Async(cs, $"SELECT count(*) FROM {q}.branch_code_reservation;");
            Assert.Equal(7L, count);
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Od02_od03_warehouse_identity_reassignment_and_terminal_archive_are_enforced()
    {
        var cs = await OpenMarkedDisposableDatabaseAsync();
        var schema = "o20org_lifecycle_" + Guid.NewGuid().ToString("N")[..10];
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
                CREATE TABLE {q}.account_warehouse_assignment
                (
                    assignment_id uuid PRIMARY KEY,
                    account_id uuid NOT NULL,
                    warehouse_id uuid NOT NULL REFERENCES {q}.warehouse(warehouse_id),
                    state text NOT NULL CHECK (state IN ('ACTIVE','REVOKED'))
                );
                CREATE FUNCTION {q}.deny_archived_reactivation() RETURNS trigger
                LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF OLD.state='ARCHIVED' AND NEW.state<>'ARCHIVED' THEN
                        RAISE EXCEPTION 'ARCHIVED is terminal' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END;
                $fn$;
                CREATE TRIGGER warehouse_archive_terminal
                BEFORE UPDATE ON {q}.warehouse FOR EACH ROW
                EXECUTE FUNCTION {q}.deny_archived_reactivation();
                CREATE FUNCTION {q}.deny_physical_delete() RETURNS trigger
                LANGUAGE plpgsql AS $fn$
                BEGIN
                    RAISE EXCEPTION 'physical deletion is prohibited' USING ERRCODE='23514';
                END;
                $fn$;
                CREATE TRIGGER warehouse_no_delete
                BEFORE DELETE ON {q}.warehouse FOR EACH ROW
                EXECUTE FUNCTION {q}.deny_physical_delete();
                CREATE FUNCTION {q}.reassign_warehouse(p_warehouse uuid,p_destination uuid)
                RETURNS void LANGUAGE plpgsql AS $fn$
                DECLARE current_state text;
                BEGIN
                    SELECT state INTO current_state FROM {q}.warehouse
                    WHERE warehouse_id=p_warehouse FOR UPDATE;
                    IF NOT FOUND OR current_state<>'INACTIVE' THEN
                        RAISE EXCEPTION 'warehouse must be inactive' USING ERRCODE='23514';
                    END IF;
                    PERFORM 1 FROM {q}.branch WHERE branch_id=p_destination AND state='ACTIVE' FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'destination branch must be active' USING ERRCODE='23514';
                    END IF;
                    UPDATE {q}.warehouse
                    SET branch_id=p_destination,row_version=row_version+1
                    WHERE warehouse_id=p_warehouse;
                END;
                $fn$;
                CREATE FUNCTION {q}.warehouse_scope(p_account uuid,p_warehouse uuid)
                RETURNS boolean LANGUAGE sql STABLE AS $fn$
                    SELECT EXISTS (
                        SELECT 1
                        FROM {q}.account_warehouse_assignment a
                        JOIN {q}.warehouse w ON w.warehouse_id=a.warehouse_id
                        JOIN {q}.branch b ON b.branch_id=w.branch_id
                        WHERE a.account_id=p_account AND a.warehouse_id=p_warehouse
                          AND a.state='ACTIVE' AND w.state='ACTIVE' AND b.state='ACTIVE'
                    );
                $fn$;
                CREATE FUNCTION {q}.archive_warehouse(p_warehouse uuid)
                RETURNS void LANGUAGE plpgsql AS $fn$
                BEGIN
                    UPDATE {q}.warehouse SET state='ARCHIVED',row_version=row_version+1
                    WHERE warehouse_id=p_warehouse AND state='INACTIVE'
                      AND NOT EXISTS (
                        SELECT 1 FROM {q}.account_warehouse_assignment a
                        WHERE a.warehouse_id=p_warehouse AND a.state='ACTIVE'
                      );
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'archive preconditions failed' USING ERRCODE='23514';
                    END IF;
                END;
                $fn$;
                """);

            var sourceBranch = Guid.NewGuid();
            var destinationBranch = Guid.NewGuid();
            var inactiveBranch = Guid.NewGuid();
            var warehouse = Guid.NewGuid();
            var account = Guid.NewGuid();
            var assignment = Guid.NewGuid();
            await ExecuteAsync(cs, $"INSERT INTO {q}.branch(branch_id,state) VALUES (@id,'ACTIVE');", ("id", sourceBranch));
            await ExecuteAsync(cs, $"INSERT INTO {q}.branch(branch_id,state) VALUES (@id,'ACTIVE');", ("id", destinationBranch));
            await ExecuteAsync(cs, $"INSERT INTO {q}.branch(branch_id,state) VALUES (@id,'INACTIVE');", ("id", inactiveBranch));
            await ExecuteAsync(cs, $"INSERT INTO {q}.warehouse(warehouse_id,branch_id,state) VALUES (@warehouse,@branch,'ACTIVE');",
                ("warehouse", warehouse), ("branch", sourceBranch));
            await ExecuteAsync(cs, $"INSERT INTO {q}.account_warehouse_assignment(assignment_id,account_id,warehouse_id,state) VALUES (@assignment,@account,@warehouse,'ACTIVE');",
                ("assignment", assignment), ("account", account), ("warehouse", warehouse));

            Assert.True(await ScalarBoolAsync(cs,
                $"SELECT {q}.warehouse_scope(@account,@warehouse);",
                ("account", account), ("warehouse", warehouse)));

            var activeReassign = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(cs, $"SELECT {q}.reassign_warehouse(@warehouse,@destination);",
                    ("warehouse", warehouse), ("destination", destinationBranch)));
            Assert.Equal("23514", activeReassign.SqlState);

            await ExecuteAsync(cs, $"UPDATE {q}.warehouse SET state='INACTIVE' WHERE warehouse_id=@warehouse;",
                ("warehouse", warehouse));
            Assert.False(await ScalarBoolAsync(cs,
                $"SELECT {q}.warehouse_scope(@account,@warehouse);",
                ("account", account), ("warehouse", warehouse)));

            await ExecuteAsync(cs, $"SELECT {q}.reassign_warehouse(@warehouse,@destination);",
                ("warehouse", warehouse), ("destination", destinationBranch));
            var retained = await ScalarInt64Async(cs,
                $"SELECT count(*) FROM {q}.account_warehouse_assignment WHERE assignment_id=@assignment AND warehouse_id=@warehouse AND state='ACTIVE';",
                ("assignment", assignment), ("warehouse", warehouse));
            Assert.Equal(1L, retained);

            await ExecuteAsync(cs, $"UPDATE {q}.warehouse SET state='ACTIVE' WHERE warehouse_id=@warehouse;",
                ("warehouse", warehouse));
            Assert.True(await ScalarBoolAsync(cs,
                $"SELECT {q}.warehouse_scope(@account,@warehouse);",
                ("account", account), ("warehouse", warehouse)));

            // The same WarehouseId assignment does not bypass the active-current-Branch predicate.
            await ExecuteAsync(cs, $"UPDATE {q}.branch SET state='INACTIVE' WHERE branch_id=@destination;",
                ("destination", destinationBranch));
            Assert.False(await ScalarBoolAsync(cs,
                $"SELECT {q}.warehouse_scope(@account,@warehouse);",
                ("account", account), ("warehouse", warehouse)));
            await ExecuteAsync(cs, $"UPDATE {q}.branch SET state='ACTIVE' WHERE branch_id=@destination;",
                ("destination", destinationBranch));

            // Archive is blocked while an active assignment remains.
            var blockedArchive = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(cs, $"SELECT {q}.archive_warehouse(@warehouse);", ("warehouse", warehouse)));
            Assert.Equal("23514", blockedArchive.SqlState);

            await ExecuteAsync(cs, $"UPDATE {q}.warehouse SET state='INACTIVE' WHERE warehouse_id=@warehouse;", ("warehouse", warehouse));
            await ExecuteAsync(cs, $"UPDATE {q}.account_warehouse_assignment SET state='REVOKED' WHERE assignment_id=@assignment;", ("assignment", assignment));
            await ExecuteAsync(cs, $"SELECT {q}.archive_warehouse(@warehouse);", ("warehouse", warehouse));

            var reactivate = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(cs, $"UPDATE {q}.warehouse SET state='ACTIVE' WHERE warehouse_id=@warehouse;",
                    ("warehouse", warehouse)));
            Assert.Equal("23514", reactivate.SqlState);

            var delete = await Assert.ThrowsAsync<PostgresException>(() =>
                ExecuteAsync(cs, $"DELETE FROM {q}.warehouse WHERE warehouse_id=@warehouse;",
                    ("warehouse", warehouse)));
            Assert.Equal("23514", delete.SqlState);
        }
        finally
        {
            await DropSchemaAsync(cs, q);
        }
    }

    private static string CanonicalizeDotNet(string input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        var normalized = input.Normalize(NormalizationForm.FormC);
        var runes = normalized.EnumerateRunes().ToList();
        var start = 0;
        var end = runes.Count;
        while (start < end && IsUnicodeWhiteSpace(runes[start].Value)) start++;
        while (end > start && IsUnicodeWhiteSpace(runes[end - 1].Value)) end--;
        if (start == end) throw new ArgumentException("Business code is empty.", nameof(input));

        var result = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            var scalar = runes[i].Value;
            if (IsUnicodeWhiteSpace(scalar) || IsUnicodeControl(scalar) || IsUnicodeFormat(scalar))
                throw new ArgumentException("Business code contains a forbidden scalar.", nameof(input));
            result.Append(runes[i].ToString());
        }
        return result.ToString();
    }

    // Pin the accepted canonicalization alphabet to Unicode White_Space / Cc / Cf
    // ranges instead of inheriting the host runtime's locale or Unicode-table behavior.
    private static bool IsUnicodeWhiteSpace(int cp) =>
        cp is >= 0x0009 and <= 0x000D or 0x0020 or 0x0085 or 0x00A0 or 0x1680 or
        >= 0x2000 and <= 0x200A or 0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;

    private static bool IsUnicodeControl(int cp) =>
        cp is >= 0x0000 and <= 0x001F or >= 0x007F and <= 0x009F;

    private static bool IsUnicodeFormat(int cp) =>
        cp is 0x00AD or >= 0x0600 and <= 0x0605 or 0x061C or 0x070F or
        >= 0x0890 and <= 0x0891 or 0x08E2 or 0x180E or
        >= 0x200B and <= 0x200F or >= 0x202A and <= 0x202E or
        >= 0x2060 and <= 0x2064 or >= 0x2066 and <= 0x206F or
        0xFEFF or >= 0xFFF9 and <= 0xFFFB or 0x110BD or 0x110CD or
        >= 0x13430 and <= 0x1343F or >= 0x1BCA0 and <= 0x1BCA3 or
        >= 0x1D173 and <= 0x1D17A or 0xE0001 or >= 0xE0020 and <= 0xE007F;

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

    private static async Task ExecuteAsync(
        string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarStringAsync(
        string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> ScalarInt64Async(
        string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> ScalarBoolAsync(
        string cs, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (bool)(await command.ExecuteScalarAsync())!;
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
