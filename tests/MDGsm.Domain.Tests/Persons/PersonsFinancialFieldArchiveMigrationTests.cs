using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsFinancialFieldArchiveMigrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Migration_0016_preserves_every_legacy_value_and_provenance_then_removes_persons_columns()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Migration 0016 integration test requires the disposable PostgreSQL database supplied by CI.");

        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        await using var connection = await dataSource.OpenConnectionAsync();

        await using (var marker = new NpgsqlCommand(
            "SELECT coalesce(shobj_description(oid, 'pg_database'), '') FROM pg_database WHERE datname=current_database();",
            connection))
        {
            var comment = (string?)await marker.ExecuteScalarAsync();
            Assert.Contains("MDGSM_DISPOSABLE_TEST_DB", comment ?? string.Empty);
        }

        // The CI migration login is explicitly allowed to SET ROLE to this NOLOGIN schema owner.
        // This keeps the fixture and archive owned by the same least-privilege role as real migrations.
        await ExecuteAsync(connection, "SET ROLE mdgsm_schema_owner;");

        var sourceSchema = "persons_archive_source_" + Guid.NewGuid().ToString("N");
        var archiveSchema = "persons_archive_private_" + Guid.NewGuid().ToString("N");

        try
        {
            await ExecuteAsync(connection, "CREATE SCHEMA " + sourceSchema + ";");
            await ExecuteAsync(connection, "SET search_path TO " + sourceSchema + ", public;");

            await ExecuteAsync(connection, """
                CREATE TABLE persons (
                    party_id uuid PRIMARY KEY,
                    credit_limit_rial bigint NOT NULL DEFAULT 0 CHECK (credit_limit_rial >= 0),
                    default_discount_percent numeric(5,2) NOT NULL DEFAULT 0
                        CHECK (default_discount_percent BETWEEN 0 AND 100),
                    default_payment_method smallint NOT NULL DEFAULT 1
                        CHECK (default_payment_method BETWEEN 1 AND 5),
                    bank_card_last_four char(4),
                    created_at_utc timestamptz NOT NULL,
                    created_by varchar(128) NOT NULL,
                    updated_at_utc timestamptz NOT NULL,
                    updated_by varchar(128) NOT NULL,
                    row_version bigint NOT NULL CHECK (row_version >= 1),
                    CONSTRAINT ck_persons_card_last_four_format
                        CHECK (bank_card_last_four IS NULL OR bank_card_last_four ~ '^[0-9]{4}$')
                );
                """);

            var fixtures = new[]
            {
                new ArchiveFixture(Guid.CreateVersion7(), 987654321L, 7.25m, 3, "4123",
                    new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), "seed-created-a",
                    new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.Zero), "seed-updated-a", 4),
                new ArchiveFixture(Guid.CreateVersion7(), 0L, 0m, 1, null,
                    new DateTimeOffset(2023, 6, 7, 8, 9, 10, TimeSpan.Zero), "seed-created-b",
                    new DateTimeOffset(2023, 6, 7, 8, 9, 10, TimeSpan.Zero), "seed-created-b", 1),
                new ArchiveFixture(Guid.CreateVersion7(), 125000L, 12.50m, 5, "0007",
                    new DateTimeOffset(2022, 10, 11, 12, 13, 14, TimeSpan.Zero), "seed-created-c",
                    new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero), "seed-updated-c", 9)
            };

            foreach (var fixture in fixtures)
            {
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO persons
                        (party_id, credit_limit_rial, default_discount_percent, default_payment_method,
                         bank_card_last_four, created_at_utc, created_by, updated_at_utc, updated_by, row_version)
                    VALUES
                        (@party, @credit, @discount, @payment, @last_four,
                         @created_at, @created_by, @updated_at, @updated_by, @row_version);
                    """, connection);
                insert.Parameters.AddWithValue("party", fixture.PartyId);
                insert.Parameters.AddWithValue("credit", fixture.CreditLimitRial);
                insert.Parameters.AddWithValue("discount", fixture.DefaultDiscountPercent);
                insert.Parameters.AddWithValue("payment", fixture.DefaultPaymentMethod);
                insert.Parameters.AddWithValue("last_four", (object?)fixture.BankCardLastFour ?? DBNull.Value);
                insert.Parameters.AddWithValue("created_at", fixture.CreatedAtUtc);
                insert.Parameters.AddWithValue("created_by", fixture.CreatedBy);
                insert.Parameters.AddWithValue("updated_at", fixture.UpdatedAtUtc);
                insert.Parameters.AddWithValue("updated_by", fixture.UpdatedBy);
                insert.Parameters.AddWithValue("row_version", fixture.RowVersion);
                await insert.ExecuteNonQueryAsync();
            }

            var root = FindRepositoryRoot();
            var migrationPath = Path.Combine(root, "database", "migrations", "0016_persons_financial_field_archive.sql");
            var migrationSql = await File.ReadAllTextAsync(migrationPath);
            Assert.Contains("CREATE SCHEMA mdgsm_migration_archive;", migrationSql, StringComparison.Ordinal);
            migrationSql = migrationSql.Replace("mdgsm_migration_archive", archiveSchema, StringComparison.Ordinal);
            await ExecuteAsync(connection, migrationSql);

            await using (var count = new NpgsqlCommand(
                "SELECT count(*) FROM " + archiveSchema + ".persons_financial_field_archive;", connection))
            {
                Assert.Equal(fixtures.Length, Convert.ToInt32(await count.ExecuteScalarAsync()));
            }

            var expected = fixtures.ToDictionary(item => item.PartyId);
            await using (var read = new NpgsqlCommand($"""
                SELECT party_id, credit_limit_rial, default_discount_percent, default_payment_method,
                       bank_card_last_four, source_created_at_utc, source_created_by,
                       source_updated_at_utc, source_updated_by, source_row_version,
                       credit_limit_disposition, default_discount_disposition,
                       default_payment_method_disposition, bank_card_last_four_disposition,
                       archive_migration_version
                FROM {archiveSchema}.persons_financial_field_archive
                ORDER BY party_id;
                """, connection))
            await using (var reader = await read.ExecuteReaderAsync())
            {
                var observed = 0;
                while (await reader.ReadAsync())
                {
                    observed++;
                    var partyId = reader.GetGuid(0);
                    Assert.True(expected.TryGetValue(partyId, out var fixture),
                        "Archive contains an unexpected or duplicated PartyId.");
                    Assert.Equal(fixture!.CreditLimitRial, reader.GetInt64(1));
                    Assert.Equal(fixture.DefaultDiscountPercent, reader.GetDecimal(2));
                    Assert.Equal(fixture.DefaultPaymentMethod, reader.GetInt16(3));
                    var lastFour = reader.IsDBNull(4) ? null : reader.GetString(4).TrimEnd();
                    Assert.Equal(fixture.BankCardLastFour, lastFour);
                    Assert.Equal(fixture.CreatedAtUtc, reader.GetFieldValue<DateTimeOffset>(5));
                    Assert.Equal(fixture.CreatedBy, reader.GetString(6));
                    Assert.Equal(fixture.UpdatedAtUtc, reader.GetFieldValue<DateTimeOffset>(7));
                    Assert.Equal(fixture.UpdatedBy, reader.GetString(8));
                    Assert.Equal(fixture.RowVersion, reader.GetInt64(9));
                    Assert.Equal("PARTY_ONLY_SCOPE_REJECTED", reader.GetString(10));
                    Assert.Equal("PRICING_RULE_UNMAPPED", reader.GetString(11));
                    Assert.Equal("TREASURY_ENUM_UNMAPPED", reader.GetString(12));
                    Assert.Equal("TREASURY_LAST4_REFERENCE_ONLY", reader.GetString(13));
                    Assert.Equal((short)16, reader.GetInt16(14));
                }

                Assert.Equal(fixtures.Length, observed);
            }

            await using (var removedColumns = new NpgsqlCommand("""
                SELECT count(*)
                FROM information_schema.columns
                WHERE table_schema=@source_schema AND table_name='persons'
                  AND column_name = ANY(@legacy_columns);
                """, connection))
            {
                removedColumns.Parameters.AddWithValue("source_schema", sourceSchema);
                removedColumns.Parameters.AddWithValue("legacy_columns", new[]
                {
                    "credit_limit_rial", "default_discount_percent",
                    "default_payment_method", "bank_card_last_four"
                });
                Assert.Equal(0, Convert.ToInt32(await removedColumns.ExecuteScalarAsync()));
            }

            await using (var guards = new NpgsqlCommand("""
                SELECT
                    NOT EXISTS (
                        SELECT 1
                        FROM pg_namespace n
                        CROSS JOIN LATERAL aclexplode(COALESCE(n.nspacl, acldefault('n', n.nspowner))) acl
                        WHERE n.nspname=@archive_schema AND acl.grantee=0
                          AND acl.privilege_type IN ('USAGE', 'CREATE')),
                    NOT EXISTS (
                        SELECT 1
                        FROM pg_class c
                        JOIN pg_namespace n ON n.oid=c.relnamespace
                        CROSS JOIN LATERAL aclexplode(COALESCE(c.relacl, acldefault('r', c.relowner))) acl
                        WHERE n.nspname=@archive_schema AND c.relname='persons_financial_field_archive'
                          AND acl.grantee=0),
                    NOT EXISTS (
                        SELECT 1
                        FROM pg_proc p
                        JOIN pg_namespace n ON n.oid=p.pronamespace
                        CROSS JOIN LATERAL aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) acl
                        WHERE n.nspname=@archive_schema
                          AND p.proname='reject_persons_financial_archive_mutation'
                          AND acl.grantee=0),
                    (SELECT count(*) FROM pg_trigger t
                     JOIN pg_class c ON c.oid=t.tgrelid
                     JOIN pg_namespace n ON n.oid=c.relnamespace
                     WHERE n.nspname=@archive_schema
                       AND c.relname='persons_financial_field_archive' AND NOT t.tgisinternal);
                """, connection))
            {
                guards.Parameters.AddWithValue("archive_schema", archiveSchema);
                await using var reader = await guards.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.True(reader.GetBoolean(0), "PUBLIC has USAGE or CREATE on the private archive schema.");
                Assert.True(reader.GetBoolean(1), "PUBLIC has a table privilege on the private archive.");
                Assert.True(reader.GetBoolean(2), "PUBLIC can execute the archive mutation guard function.");
                Assert.Equal(3L, reader.GetInt64(3));
            }

            await AssertMutationRejectedAsync(connection,
                "UPDATE " + archiveSchema + ".persons_financial_field_archive SET credit_limit_rial=1;");
            await AssertMutationRejectedAsync(connection,
                "DELETE FROM " + archiveSchema + ".persons_financial_field_archive;");
            await AssertMutationRejectedAsync(connection,
                "TRUNCATE TABLE " + archiveSchema + ".persons_financial_field_archive;");

            await using var finalCount = new NpgsqlCommand(
                "SELECT count(*) FROM " + archiveSchema + ".persons_financial_field_archive;", connection);
            Assert.Equal(fixtures.Length, Convert.ToInt32(await finalCount.ExecuteScalarAsync()));
        }
        finally
        {
            try
            {
                await ExecuteAsync(connection, "RESET search_path;");
                await ExecuteAsync(connection, "DROP SCHEMA IF EXISTS " + archiveSchema + " CASCADE;");
                await ExecuteAsync(connection, "DROP SCHEMA IF EXISTS " + sourceSchema + " CASCADE;");
                await ExecuteAsync(connection, "RESET ROLE;");
            }
            catch
            {
                // The database itself is uniquely named and disposable; CI drops it after this test.
            }
        }
    }

    private static async Task AssertMutationRejectedAsync(NpgsqlConnection connection, string sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("55000", exception.SqlState);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("MDGsm.sln was not found above the test output directory.");
    }

    private sealed record ArchiveFixture(
        Guid PartyId,
        long CreditLimitRial,
        decimal DefaultDiscountPercent,
        short DefaultPaymentMethod,
        string? BankCardLastFour,
        DateTimeOffset CreatedAtUtc,
        string CreatedBy,
        DateTimeOffset UpdatedAtUtc,
        string UpdatedBy,
        long RowVersion);
}
