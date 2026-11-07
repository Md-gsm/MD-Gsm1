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
    [Fact]
    public void Persons_details_contract_does_not_expose_finance_owned_fields()
    {
        var properties = typeof(PartyDetails).GetProperties().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("CreditLimitRial", properties);
        Assert.DoesNotContain("DefaultDiscountPercent", properties);
        Assert.DoesNotContain("DefaultPaymentMethod", properties);
        Assert.DoesNotContain("BankCardLastFour", properties);
    }

    private const string Actor = "ci-test-user";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Quick_registration_same_mobile_and_same_name_reuses_party_and_adds_role()
    {
        await using var scope = await TestScope.OpenAsync();
        var mobile = "09125550123";
        var first = await scope.Repository.AddNaturalAsync(
            NewNatural(mobile, NewNationalId()), Actor, Guid.CreateVersion7(), scope.Transaction, default);

        var quick = await scope.Repository.QuickRegisterNaturalAsync(
            NewNatural(mobile, NewNationalId()) with { Roles = PartyBusinessRoles.Customer | PartyBusinessRoles.Colleague },
            Actor, Guid.CreateVersion7(), forceNew: false, selectedPartyId: null, scope.Transaction, default);

        Assert.Equal(PartyQuickRegistrationOutcome.ExistingPartyMatched, quick.Outcome);
        Assert.Equal(first.PartyId, quick.PartyId);
        Assert.False(quick.Created);
        var matchedCandidate = Assert.Single(quick.Candidates);
        Assert.Equal(first.PartyId, matchedCandidate.PartyId);
        Assert.False(matchedCandidate.IsMerged);
        Assert.Equal(3L, await ScalarAsync(scope,
            "SELECT business_roles::bigint FROM persons WHERE party_id = @id", Guid.Parse(first.PartyId)));
        Assert.Equal(1L, await ScalarAsync(scope,
            "SELECT count(*) FROM persons_contact_points WHERE kind = 1 AND value = '+989125550123' AND is_active AND @id = @id", Guid.Empty));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Quick_registration_requires_explicit_choice_for_different_name_and_supports_force_new()
    {
        await using var scope = await TestScope.OpenAsync();
        var mobile = "09125550124";
        var first = await scope.Repository.AddNaturalAsync(
            NewNatural(mobile, NewNationalId()), Actor, Guid.CreateVersion7(), scope.Transaction, default);
        var operation = Guid.CreateVersion7();
        var differentName = NewNatural(mobile, NewNationalId()) with
        {
            GivenName = "سارا",
            FamilyName = "محمدی",
            Roles = PartyBusinessRoles.Colleague
        };

        var ambiguous = await scope.Repository.QuickRegisterNaturalAsync(
            differentName, Actor, operation, forceNew: false, selectedPartyId: null, scope.Transaction, default);

        Assert.Equal(PartyQuickRegistrationOutcome.SelectionRequired, ambiguous.Outcome);
        Assert.Null(ambiguous.PartyId);
        var candidate = Assert.Single(ambiguous.Candidates);
        Assert.Equal(first.PartyId, candidate.PartyId);
        Assert.Equal(1L, await ScalarAsync(scope,
            "SELECT count(*) FROM persons_contact_points WHERE kind = 1 AND value = '+989125550124' AND is_active AND @id = @id", Guid.Empty));

        var selected = await scope.Repository.QuickRegisterNaturalAsync(
            differentName, Actor, operation, forceNew: false, selectedPartyId: candidate.PartyId, scope.Transaction, default);
        Assert.Equal(PartyQuickRegistrationOutcome.ExistingPartyMatched, selected.Outcome);
        Assert.Equal(first.PartyId, selected.PartyId);
        Assert.Equal(3L, await ScalarAsync(scope,
            "SELECT business_roles::bigint FROM persons WHERE party_id = @id", Guid.Parse(first.PartyId)));

        var forced = await scope.Repository.QuickRegisterNaturalAsync(
            differentName, Actor, Guid.CreateVersion7(), forceNew: true, selectedPartyId: null, scope.Transaction, default);
        Assert.Equal(PartyQuickRegistrationOutcome.Created, forced.Outcome);
        Assert.True(forced.Created);
        Assert.NotEqual(first.PartyId, forced.PartyId);
        Assert.Equal(2L, await ScalarAsync(scope,
            "SELECT count(*) FROM persons_contact_points WHERE kind = 1 AND value = '+989125550124' AND is_active AND @id = @id", Guid.Empty));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Quick_registration_rejects_selection_when_the_matching_mobile_is_inactive()
    {
        await using var scope = await TestScope.OpenAsync();
        const string mobile = "09125550126";
        var first = await scope.Repository.AddNaturalAsync(
            NewNatural(mobile, NewNationalId()), Actor, Guid.CreateVersion7(), scope.Transaction, default);

        await using (var deactivate = new NpgsqlCommand("""
            UPDATE persons_contact_points
            SET is_active = false
            WHERE party_id = @party AND kind = 1 AND value = '+989125550126';
            """, scope.Connection, scope.Transaction))
        {
            deactivate.Parameters.AddWithValue("party", Guid.Parse(first.PartyId));
            Assert.Equal(1, await deactivate.ExecuteNonQueryAsync());
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Repository.QuickRegisterNaturalAsync(
                NewNatural(mobile, NewNationalId()), Actor, Guid.CreateVersion7(),
                forceNew: false, selectedPartyId: first.PartyId, scope.Transaction, default));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_quick_registration_of_same_mobile_creates_exactly_one_party()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var dataSource = NpgsqlDataSource.Create(connectionString!);
        await using var markerConnection = await dataSource.OpenConnectionAsync();
        await using var marker = new NpgsqlCommand(
            "SELECT coalesce(shobj_description(oid, 'pg_database'), '') FROM pg_database WHERE datname = current_database()",
            markerConnection);
        var databaseMarker = (string?)await marker.ExecuteScalarAsync();
        Assert.Contains("MDGSM_DISPOSABLE_TEST_DB", databaseMarker ?? string.Empty);

        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mobile = "09125550125";
        async Task<PartyQuickRegistrationResult> RegisterAsync(string actor)
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await start.Task;
            var result = await new PostgresPartyRepository(dataSource).QuickRegisterNaturalAsync(
                NewNatural(mobile, NewNationalId()), actor, Guid.CreateVersion7(),
                forceNew: false, selectedPartyId: null, transaction, default);
            await transaction.CommitAsync();
            return result;
        }

        var firstTask = RegisterAsync("ci-quick-station-1");
        var secondTask = RegisterAsync("ci-quick-station-2");
        start.SetResult(true);
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Single(results, x => x.Outcome == PartyQuickRegistrationOutcome.Created);
        Assert.Single(results, x => x.Outcome == PartyQuickRegistrationOutcome.ExistingPartyMatched);
        Assert.Single(results.Select(x => x.PartyId).Distinct());
        await using var verifyConnection = await dataSource.OpenConnectionAsync();
        await using var verify = new NpgsqlCommand("""
            SELECT count(DISTINCT p.party_id)
            FROM persons p
            JOIN persons_contact_points c ON c.party_id = p.party_id
            WHERE c.kind = 1 AND c.value = '+989125550125' AND c.is_active;
            """, verifyConnection);
        Assert.Equal(1L, (long)(await verify.ExecuteScalarAsync())!);
    }

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

        var details = await scope.Repository.GetAsync(first.PartyId, Actor, includeSensitive: true, scope.Transaction, default);
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
            NewNatural("09121234567", nationalId), Actor, Guid.CreateVersion7(), scope.Transaction, default);

        var masked = await scope.Repository.GetAsync(created.PartyId, Actor, includeSensitive: false, scope.Transaction, default);
        var full = await scope.Repository.GetAsync(created.PartyId, Actor, includeSensitive: true, scope.Transaction, default);

        Assert.NotNull(masked);
        Assert.False(masked.SensitiveFieldsIncluded);
        Assert.Null(masked.InternalNote);
        Assert.Null(masked.EconomicCode);
        Assert.EndsWith(nationalId[^3..], masked.NationalIdentifier);
        Assert.DoesNotContain(nationalId, masked.NationalIdentifier);
        Assert.All(masked.Contacts, c => Assert.DoesNotContain("9121234", c.Value));
        Assert.Equal("***", masked.PrimaryAddress!.AddressLine);

        Assert.NotNull(full);
        Assert.True(full.SensitiveFieldsIncluded);
        Assert.Equal(nationalId, full.NationalIdentifier);
        Assert.NotNull(full.AcquaintanceSource);
        Assert.NotNull(full.CommunicationPreferences);

        Assert.Equal(2L, await ScalarAsync(scope, "SELECT count(*) FROM persons_access_audit WHERE party_id = @id", Guid.Parse(created.PartyId)));
        Assert.Equal(1L, await ScalarAsync(scope, "SELECT count(*) FROM persons_access_audit WHERE party_id = @id AND sensitive_included", Guid.Parse(created.PartyId)));
        Assert.Null(masked.AcquaintanceSource);
        Assert.Null(masked.CommunicationPreferences);
        Assert.Equal("م*** م****", PartyIdentifiers.MaskPersonName("مدیر مسئول"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Sensitive_read_is_denied_without_server_authorization()
    {
        await using var scope = await TestScope.OpenAsync();
        var created = await scope.Repository.AddNaturalAsync(NewNatural("09125555555", NewNationalId()), Actor, Guid.CreateVersion7(), scope.Transaction, default);
        var denied = new PostgresPartyRepository(scope.DataSource);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            denied.GetAsync(created.PartyId, Actor, includeSensitive: true, scope.Transaction, default));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Read_audit_survives_rollback_of_the_callers_transaction()
    {
        await using var scope = await TestScope.OpenAsync();
        var created = await scope.Repository.AddNaturalAsync(NewNatural("09126666666", NewNationalId()), Actor, Guid.CreateVersion7(), scope.Transaction, default);
        await scope.Repository.GetAsync(created.PartyId, Actor, includeSensitive: false, scope.Transaction, default);
        await scope.RollbackTransactionAsync();
        await using var verify = await scope.DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM persons_access_audit WHERE party_id=@id AND actor=@actor", verify);
        command.Parameters.AddWithValue("id", Guid.Parse(created.PartyId));
        command.Parameters.AddWithValue("actor", Actor);
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
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
    public async Task Database_rejects_cross_type_identity_columns()
    {
        await using var scope = await TestScope.OpenAsync();
        await using var connection = await scope.DataSource.OpenConnectionAsync();

        await using (var legalTx = await connection.BeginTransactionAsync())
        {
            await using var legal = new NpgsqlCommand("""
                INSERT INTO persons (party_id, party_type, business_roles, legal_name, national_id, created_by, updated_by)
                VALUES (@id, 2, 1, 'شرکت آزمایشی', '1234567890', 'ci', 'ci');
                """, connection, legalTx);
            legal.Parameters.AddWithValue("id", Guid.CreateVersion7());
            var failure = await Assert.ThrowsAsync<PostgresException>(() => legal.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal("ck_persons_party_identity", failure.ConstraintName);
            await legalTx.RollbackAsync();
        }

        await using (var naturalTx = await connection.BeginTransactionAsync())
        {
            await using var natural = new NpgsqlCommand("""
                INSERT INTO persons (party_id, party_type, business_roles, given_name, family_name, economic_code, created_by, updated_by)
                VALUES (@id, 1, 1, 'علی', 'رضایی', '1234567890', 'ci', 'ci');
                """, connection, naturalTx);
            natural.Parameters.AddWithValue("id", Guid.CreateVersion7());
            var failure = await Assert.ThrowsAsync<PostgresException>(() => natural.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal("ck_persons_party_identity", failure.ConstraintName);
            await naturalTx.RollbackAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Registration_allows_walk_in_party_without_mobile_or_other_contacts()
    {
        await using var scope = await TestScope.OpenAsync();
        var request = NewNatural(null, NewNationalId()) with
        {
            Province = null,
            City = null,
            PostalCode = null,
            AddressLine = null
        };
        var created = await scope.Repository.AddNaturalAsync(
            request, Actor, Guid.CreateVersion7(), scope.Transaction, default);
        var details = await scope.Repository.GetAsync(
            created.PartyId, Actor, includeSensitive: false, scope.Transaction, default);
        Assert.NotNull(details);
        Assert.Empty(details.Contacts);
        Assert.Null(details.PrimaryAddress);
        var results = await scope.Repository.SearchAsync(PersianTestData.AliRezaeiCanonical, 10, scope.Transaction, default);
        Assert.Null(Assert.Single(results).MaskedPrimaryMobile);
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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Natural_and_legal_national_identifiers_remain_reserved_after_deactivation()
    {
        await using (var naturalScope = await TestScope.OpenAsync())
        {
            var nationalId = NewNationalId();
            var first = await naturalScope.Repository.AddNaturalAsync(NewNatural("09127777777", nationalId), Actor, Guid.CreateVersion7(), naturalScope.Transaction, default);
            await using (var deactivate = new NpgsqlCommand("UPDATE persons SET is_active = false WHERE party_id = @id", naturalScope.Connection, naturalScope.Transaction))
            {
                deactivate.Parameters.AddWithValue("id", Guid.Parse(first.PartyId));
                Assert.Equal(1, await deactivate.ExecuteNonQueryAsync());
            }
            await using (var savepoint = new NpgsqlCommand("SAVEPOINT before_natural_identifier_reuse", naturalScope.Connection, naturalScope.Transaction))
                await savepoint.ExecuteNonQueryAsync();
            var duplicate = await Assert.ThrowsAsync<DuplicatePartyIdentifierException>(() => naturalScope.Repository.AddNaturalAsync(NewNatural("09128888888", nationalId), Actor, Guid.CreateVersion7(), naturalScope.Transaction, default));
            Assert.DoesNotContain(nationalId, duplicate.Message, StringComparison.Ordinal);
            await using var rollback = new NpgsqlCommand("ROLLBACK TO SAVEPOINT before_natural_identifier_reuse", naturalScope.Connection, naturalScope.Transaction);
            await rollback.ExecuteNonQueryAsync();
        }
        await using (var legalScope = await TestScope.OpenAsync())
        {
            var legalNationalId = Random.Shared.NextInt64(10_000_000_000L, 99_999_999_999L).ToString();
            var first = await legalScope.Repository.AddLegalAsync(NewLegal(legalNationalId), Actor, Guid.CreateVersion7(), legalScope.Transaction, default);
            await using (var deactivate = new NpgsqlCommand("UPDATE persons SET is_active = false WHERE party_id = @id", legalScope.Connection, legalScope.Transaction))
            {
                deactivate.Parameters.AddWithValue("id", Guid.Parse(first.PartyId));
                Assert.Equal(1, await deactivate.ExecuteNonQueryAsync());
            }
            await using (var savepoint = new NpgsqlCommand("SAVEPOINT before_legal_identifier_reuse", legalScope.Connection, legalScope.Transaction))
                await savepoint.ExecuteNonQueryAsync();
            var duplicate = await Assert.ThrowsAsync<DuplicatePartyIdentifierException>(() => legalScope.Repository.AddLegalAsync(NewLegal(legalNationalId), Actor, Guid.CreateVersion7(), legalScope.Transaction, default));
            Assert.DoesNotContain(legalNationalId, duplicate.Message, StringComparison.Ordinal);
            await using var rollback = new NpgsqlCommand("ROLLBACK TO SAVEPOINT before_legal_identifier_reuse", legalScope.Connection, legalScope.Transaction);
            await rollback.ExecuteNonQueryAsync();
        }
    }

    private static RegisterLegalParty NewLegal(string legalNationalId) =>
        new("شرکت آزمایشی", legalNationalId, "1234567890", "نام مسئول شرکت",
            PartyBusinessRoles.Customer, "09126666666", null, null, null,
            "تهران", "تهران", "1234567890", "خیابان آزمایشی",
            PartyAcquaintanceSource.Unknown, "یادداشت داخلی", PartyCommunicationPreferences.Default);

    private static string NewNationalId() => Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L).ToString();

    private static RegisterNaturalParty NewNatural(string? mobile, string? nationalId) =>
        new(PersianTestData.AliArabicYeh, PersianTestData.RezaeiArabicYeh, PartyBusinessRoles.Customer, nationalId, mobile, null, null, null,
            "تهران", "تهران", "1234567890", "خیابان آزمایشی ۱۲",
            PartyAcquaintanceSource.Unknown, "یادداشت داخلی", PartyCommunicationPreferences.Default);

    private static async Task<long> ScalarAsync(TestScope scope, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, scope.Connection, scope.Transaction);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class AllowSensitivePartyDataAuthorizer : IPartySensitiveDataAuthorizer
    {
        public Task<bool> CanReadSensitivePartyDataAsync(string actorId, CancellationToken cancellationToken) => Task.FromResult(actorId == Actor);
    }

    private sealed class TestScope : IAsyncDisposable
    {
        private readonly NpgsqlDataSource _dataSource;

        private TestScope(NpgsqlDataSource dataSource, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            _dataSource = dataSource;
            Connection = connection;
            Transaction = transaction;
            Repository = new PostgresPartyRepository(dataSource, new AllowSensitivePartyDataAuthorizer());
        }

        private bool _transactionRolledBack;
        public NpgsqlDataSource DataSource => _dataSource;
        public async Task RollbackTransactionAsync()
        {
            await Transaction.RollbackAsync();
            _transactionRolledBack = true;
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
            if (!_transactionRolledBack) await Transaction.RollbackAsync();
            await Transaction.DisposeAsync();
            await Connection.DisposeAsync();
            await _dataSource.DisposeAsync();
        }
    }
}
