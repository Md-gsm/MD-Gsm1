using MDGsm.Application.Persons;
using MDGsm.Domain.Persons;
using MDGsm.Infrastructure.Persons;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

/// <summary>
/// Runs the real <see cref="PostgresPartyRepository"/> against the disposable CI database.
/// Every test works inside one caller-owned transaction (ADR-0005) that is always rolled back.
/// </summary>
public sealed class PersonsEndToEndTests
{
    private const string Actor = "ci-test-user";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Registration_is_atomic_idempotent_and_normalizes_persian_text()
    {
        await using var scope = await TestScope.OpenAsync();
        var operationId = Guid.CreateVersion7();
        var request = NewNatural("۰۹۱۲۱۲۳۴۵۶۷", NewNationalId());

        var first = await scope.Repository.AddNaturalAsync(request, Actor, operationId, scope.Transaction, default);
        var replay = await scope.Repository.AddNaturalAsync(request, Actor, operationId, scope.Transaction, default);

        Assert.True(first.Created);
        Assert.False(replay.Created);
        Assert.Equal(first.PartyId, replay.PartyId);
        Assert.True(PartyId.TryParse(first.PartyId, out _));
        Assert.Equal(1L, await ScalarAsync(scope, "SELECT count(*) FROM persons_operations WHERE operation_id = @id", operationId));

        var details = await scope.QueryFor(allowSensitive: true).GetAsync(first.PartyId, Actor, scope.Transaction, default);
        Assert.NotNull(details);
        Assert.Equal(PersianTestData.AliRezaeiCanonical, details.DisplayName);   // Arabic yeh typed on input, Persian yeh stored
        Assert.Contains(details.Contacts, c => c.Kind == "Mobile" && c.Value == "+989121234567" && c.IsPrimary);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Detail_read_masks_sensitive_fields_and_is_audited()
    {
        await using var scope = await TestScope.OpenAsync();
        var nationalId = NewNationalId();
        var created = await scope.Repository.AddNaturalAsync(
            NewNatural("09121234567", nationalId, creditLimitRial: 5_000_000, lastFour: "4321"), Actor, Guid.CreateVersion7(), scope.Transaction, default);

        var masked = await scope.QueryFor(allowSensitive: true).GetAsync(created.PartyId, Actor, scope.Transaction, default);
        var full = await scope.QueryFor(allowSensitive: true).GetAsync(created.PartyId, Actor, scope.Transaction, default);

        Assert.NotNull(masked);
        Assert.False(masked.SensitiveFieldsIncluded);
        Assert.Null(masked.CreditLimitRial);
        Assert.Null(masked.BankCardLastFour);
        Assert.Null(masked.InternalNote);
        Assert.Null(masked.EconomicCode);
        Assert.EndsWith(nationalId[^3..], masked.NationalIdentifier);
        Assert.DoesNotContain(nationalId, masked.NationalIdentifier);
        Assert.All(masked.Contacts, c => Assert.DoesNotContain("9121234", c.Value));
        Assert.Equal("***", masked.PrimaryAddress!.AddressLine);

        Assert.NotNull(full);
        Assert.True(full.SensitiveFieldsIncluded);
        Assert.Equal((long?)5_000_000, full.CreditLimitRial);
        Assert.Equal("4321", full.BankCardLastFour);
        Assert.Equal(nationalId, full.NationalIdentifier);
        Assert.Equal("نام مسئول", full.ResponsiblePersonName);

        Assert.Equal(2L, await ScalarAsync(scope, "SELECT count(*) FROM persons_access_audit WHERE party_id = @id", Guid.Parse(created.PartyId)));
        Assert.Equal(1L, await ScalarAsync(scope, "SELECT count(*) FROM persons_access_audit WHERE party_id = @id AND sensitive_included", Guid.Parse(created.PartyId)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Search_requires_a_term_masks_mobiles_and_treats_wildcards_literally()
    {
        await using var scope = await TestScope.OpenAsync();
        var suffix = Random.Shared.Next(1000, 9999).ToString();
        var mobile = "0912000" + suffix;                       // 11 digits, ends with the four-digit suffix
        await scope.Repository.AddNaturalAsync(NewNatural(mobile, NewNationalId()), Actor, Guid.CreateVersion7(), scope.Transaction, default);

        var bySuffix = await scope.Repository.SearchAsync(suffix, 10, scope.Transaction, default);
        var item = Assert.Single(bySuffix, x => x.DisplayName == PersianTestData.AliRezaeiCanonical);
        Assert.Equal(new string('*', 11) + suffix[^2..], item.MaskedPrimaryMobile);

        var byArabicSpelling = await scope.Repository.SearchAsync(PersianTestData.AliArabicYeh, 10, scope.Transaction, default);
        Assert.Contains(byArabicSpelling, x => x.DisplayName == PersianTestData.AliRezaeiCanonical);

        var literalPercent = await scope.Repository.SearchAsync("%%", 10, scope.Transaction, default);
        Assert.Empty(literalPercent);

        await Assert.ThrowsAsync<ArgumentException>(() => scope.Repository.SearchAsync("", 10, scope.Transaction, default));
        await Assert.ThrowsAsync<ArgumentException>(() => scope.Repository.SearchAsync("\u0639", 10, scope.Transaction, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => scope.Repository.SearchAsync(PersianTestData.AliArabicYeh, 11, scope.Transaction, default));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Duplicate_national_identifier_is_reported_without_leaking_database_details()
    {
        await using var scope = await TestScope.OpenAsync();
        var nationalId = NewNationalId();
        await scope.Repository.AddNaturalAsync(NewNatural("09121111111", nationalId), Actor, Guid.CreateVersion7(), scope.Transaction, default);

        var exception = await Assert.ThrowsAsync<DuplicatePartyIdentifierException>(() =>
            scope.Repository.AddNaturalAsync(NewNatural("09122222222", nationalId), Actor, Guid.CreateVersion7(), scope.Transaction, default));

        Assert.DoesNotContain("ux_persons", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nationalId, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Database_rejects_malformed_mobile_values()
    {
        await using var scope = await TestScope.OpenAsync();
        var created = await scope.Repository.AddNaturalAsync(NewNatural("09123333333", NewNationalId()), Actor, Guid.CreateVersion7(), scope.Transaction, default);

        await using var insert = new NpgsqlCommand(
            "INSERT INTO persons_contact_points (contact_point_id, party_id, kind, value, created_by) VALUES (@c, @p, 1, '+982112345678x', 'ci')",
            scope.Connection, scope.Transaction);
        insert.Parameters.AddWithValue("c", Guid.CreateVersion7());
        insert.Parameters.AddWithValue("p", Guid.Parse(created.PartyId));
        var failure = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Access_audit_is_append_only()
    {
        await using var scope = await TestScope.OpenAsync();
        var created = await scope.Repository.AddNaturalAsync(NewNatural("09124444444", NewNationalId()), Actor, Guid.CreateVersion7(), scope.Transaction, default);
        await scope.Repository.GetAsync(created.PartyId, Actor, includeSensitive: false, scope.Transaction, default);

        await using var update = new NpgsqlCommand("UPDATE persons_access_audit SET actor = 'tampered'", scope.Connection, scope.Transaction);
        await update.ExecuteNonQueryAsync();
        await using var delete = new NpgsqlCommand("DELETE FROM persons_access_audit", scope.Connection, scope.Transaction);
        await delete.ExecuteNonQueryAsync();

        Assert.Equal(1L, await ScalarAsync(scope, "SELECT count(*) FROM persons_access_audit WHERE party_id = @id AND actor = 'ci-test-user'", Guid.Parse(created.PartyId)));
    }

    private static string NewNationalId() => Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L).ToString();

    private static RegisterNaturalParty NewNatural(string mobile, string? nationalId, long creditLimitRial = 0, string? lastFour = null) =>
        new(PersianTestData.AliArabicYeh, PersianTestData.RezaeiArabicYeh, PartyBusinessRoles.Customer, nationalId, mobile, null, null, null,
            "تهران", "تهران", "1234567890", "خیابان آزمایشی ۱۲",
            creditLimitRial, 0m, DefaultPaymentMethod.Cash, lastFour, PartyAcquaintanceSource.Unknown, "یادداشت داخلی",
            PartyCommunicationPreferences.Default);

    private static async Task<long> ScalarAsync(TestScope scope, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, scope.Connection, scope.Transaction);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class FixedSensitiveReadAuthorizer(bool allowSensitive) : IPartySensitiveReadAuthorizer
    {
        public Task<bool> CanReadSensitiveAsync(string authenticatedActorId, string partyId, CancellationToken cancellationToken) =>
            Task.FromResult(allowSensitive);
    }

    private sealed class TestScope : IAsyncDisposable
    {
        private readonly NpgsqlDataSource _dataSource;

        private TestScope(NpgsqlDataSource dataSource, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            _dataSource = dataSource;
            Connection = connection;
            Transaction = transaction;
            Repository = new PostgresPartyRepository(dataSource);
        }

        public NpgsqlConnection Connection { get; }
        public NpgsqlTransaction Transaction { get; }
        public PostgresPartyRepository Repository { get; }


        public static async Task<TestScope> OpenAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
            Assert.False(string.IsNullOrWhiteSpace(connectionString),
                "The integration test requires a disposable PostgreSQL database supplied by CI.");

            var dataSource = NpgsqlDataSource.Create(connectionString!);
            var connection = await dataSource.OpenConnectionAsync();
            await using (var marker = new NpgsqlCommand(
                "SELECT coalesce(shobj_description(oid, 'pg_database'), '') FROM pg_database WHERE datname = current_database()", connection))
            {
                var comment = (string?)await marker.ExecuteScalarAsync();
                Assert.Contains("MDGSM_DISPOSABLE_TEST_DB", comment ?? string.Empty);
            }

            var transaction = await connection.BeginTransactionAsync();
            return new TestScope(dataSource, connection, transaction);
        }

        public async ValueTask DisposeAsync()
        {
            await Transaction.RollbackAsync();
            await Transaction.DisposeAsync();
            await Connection.DisposeAsync();
            await _dataSource.DisposeAsync();
        }
    }
}
