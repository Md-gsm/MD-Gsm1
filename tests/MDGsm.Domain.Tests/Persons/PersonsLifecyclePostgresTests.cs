using MDGsm.Finance.Infrastructure.Accounts;
using MDGsm.Persons.Application;
using MDGsm.Persons.Contracts;
using MDGsm.Persons.Domain;
using MDGsm.Persons.Infrastructure;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsLifecyclePostgresTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Contact_and_party_lifecycle_enforce_versions_and_capture_history()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var repository = new PostgresPartyRepository(dataSource);
        var actor = Guid.CreateVersion7().ToString("D");
        var party = await repository.AddNaturalAsync(NewNatural("09125550181"), actor, Guid.CreateVersion7(), null, default);
        var lifecycle = new PostgresPartyLifecycleService(dataSource, TimeProvider.System);

        Guid contactId;
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            SELECT contact_point_id FROM persons_contact_points
            WHERE party_id=@party AND kind=1 AND is_active;
            """, connection))
        {
            command.Parameters.AddWithValue("party", Guid.Parse(party.PartyId));
            contactId = (Guid)(await command.ExecuteScalarAsync())!;
        }

        var inactive = await lifecycle.SetContactPointActiveAsync(contactId, false, 1, actor, "customer requested pause", default);
        Assert.Equal(PartyLifecycleChangeStatus.Applied, inactive.Status);
        Assert.Equal(2L, inactive.CurrentRowVersion);

        var reactivated = await lifecycle.SetContactPointActiveAsync(contactId, true, 2, actor, "customer reactivated contact", default);
        Assert.Equal(PartyLifecycleChangeStatus.Applied, reactivated.Status);
        Assert.Equal(3L, reactivated.CurrentRowVersion);

        var updated = await lifecycle.UpdateContactPointValueAsync(contactId, "+989125550182", 3, actor, "corrected mobile", default);
        Assert.Equal(PartyLifecycleChangeStatus.Applied, updated.Status);
        Assert.Equal(4L, updated.CurrentRowVersion);

        var stale = await lifecycle.SetContactPointActiveAsync(contactId, false, 3, actor, "stale update", default);
        Assert.Equal(PartyLifecycleChangeStatus.VersionConflict, stale.Status);
        Assert.Equal(4L, stale.CurrentRowVersion);

        var partyInactive = await lifecycle.SetPartyActiveAsync(Guid.Parse(party.PartyId), false, 1, actor, "customer account inactive", default);
        Assert.Equal(PartyLifecycleChangeStatus.Applied, partyInactive.Status);
        Assert.Equal(2L, partyInactive.CurrentRowVersion);
        var partyActive = await lifecycle.SetPartyActiveAsync(Guid.Parse(party.PartyId), true, 2, actor, "customer account restored", default);
        Assert.Equal(PartyLifecycleChangeStatus.Applied, partyActive.Status);
        Assert.Equal(3L, partyActive.CurrentRowVersion);

        await using var historyConnection = await dataSource.OpenConnectionAsync();
        await using var history = new NpgsqlCommand("""
            SELECT count(*) FROM persons_lifecycle_history
            WHERE entity_type='contact_point' AND entity_id=@id AND actor_id=@actor
              AND change_type IN ('deactivated','reactivated','updated');
            """, historyConnection);
        history.Parameters.AddWithValue("id", contactId);
        history.Parameters.AddWithValue("actor", actor);
        Assert.Equal(3L, (long)(await history.ExecuteScalarAsync())!);

        Guid addressId;
        await using (var addressConnection = await dataSource.OpenConnectionAsync())
        await using (var addressCommand = new NpgsqlCommand("SELECT address_id FROM persons_addresses WHERE party_id=@party AND is_active;", addressConnection))
        {
            addressCommand.Parameters.AddWithValue("party", Guid.Parse(party.PartyId));
            addressId = (Guid)(await addressCommand.ExecuteScalarAsync())!;
        }
        var addressUpdated = await lifecycle.UpdateAddressAsync(addressId, "IR", "تهران", "تهران", "1234567890", "نشانی اصلاح‌شده",
            1, actor, "corrected address", default);
        Assert.Equal(PartyLifecycleChangeStatus.Applied, addressUpdated.Status);
        Assert.Equal(2L, addressUpdated.CurrentRowVersion);
        var addressInactive = await lifecycle.SetAddressActiveAsync(addressId, false, 2, actor, "address temporarily inactive", default);
        Assert.Equal(PartyLifecycleChangeStatus.Applied, addressInactive.Status);
        Assert.Equal(3L, addressInactive.CurrentRowVersion);
        var addressActive = await lifecycle.SetAddressActiveAsync(addressId, true, 3, actor, "address restored", default);
        Assert.Equal(PartyLifecycleChangeStatus.Applied, addressActive.Status);
        Assert.Equal(4L, addressActive.CurrentRowVersion);

        var historyReader = new PostgresPartyLifecycleHistoryReader(dataSource);
        var page = await historyReader.GetAsync(Guid.Parse(party.PartyId), 100, null, actor, default);
        Assert.Contains(page.Items, item => item.EntityId == contactId && item.ChangeType == "updated");
        Assert.Contains(page.Items, item => item.EntityId == addressId && item.ChangeType == "reactivated");
        Assert.Null(page.NextCursor);
        await using var auditConnection = await dataSource.OpenConnectionAsync();
        await using var auditCommand = new NpgsqlCommand("SELECT count(*) FROM persons_access_audit WHERE party_id=@party AND actor=@actor AND action='history-read' AND sensitive_included=true;", auditConnection);
        auditCommand.Parameters.AddWithValue("party", Guid.Parse(party.PartyId));
        auditCommand.Parameters.AddWithValue("actor", actor);
        Assert.True((long)(await auditCommand.ExecuteScalarAsync())! >= 1L);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Party_merge_transfers_nonduplicates_and_preserves_duplicate_provenance_and_restrictive_consent()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var repository = new PostgresPartyRepository(dataSource);
        var actor = Guid.CreateVersion7().ToString("D");
        var source = await repository.AddNaturalAsync(NewNatural("09125550191"), actor, Guid.CreateVersion7(), null, default);
        var target = await repository.AddNaturalAsync(NewNatural("09125550191"), actor, Guid.CreateVersion7(), null, default);
        var sourceId = Guid.Parse(source.PartyId);
        var targetId = Guid.Parse(target.PartyId);

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            UPDATE persons_contact_points SET is_messaging_allowed=false
            WHERE party_id=@target AND kind=1 AND is_active;
            """, connection))
        {
            command.Parameters.AddWithValue("target", targetId);
            await command.ExecuteNonQueryAsync();
        }

        // Add nonduplicate source-owned contact/address records so the merge test proves
        // both duplicate retention and provenance-preserving transfer.
        await using (var connection = await dataSource.OpenConnectionAsync())
        {
            await using var contact = new NpgsqlCommand("""
                INSERT INTO persons_contact_points
                    (contact_point_id, party_id, kind, value, is_primary, is_messaging_allowed, is_active, created_by)
                VALUES (@id, @source, @kind, 'merge-source@example.test', false, true, true, @actor);
                """, connection);
            contact.Parameters.AddWithValue("id", Guid.CreateVersion7());
            contact.Parameters.AddWithValue("source", sourceId);
            contact.Parameters.AddWithValue("kind", (short)2);
            contact.Parameters.AddWithValue("actor", actor);
            await contact.ExecuteNonQueryAsync();

            await using var address = new NpgsqlCommand("""
                INSERT INTO persons_addresses
                    (address_id, party_id, country_code, province, city, postal_code, address_line,
                     is_primary, is_active, created_by)
                VALUES (@id, @source, 'IR', 'تهران', 'تهران', '9876543210', 'نشانی غیرتکراری مبدأ',
                        false, true, @actor);
                """, connection);
            address.Parameters.AddWithValue("id", Guid.CreateVersion7());
            address.Parameters.AddWithValue("source", sourceId);
            address.Parameters.AddWithValue("actor", actor);
            await address.ExecuteNonQueryAsync();
        }

        var merge = new PostgresPartyMergeService(dataSource,
            [new PostgresPartyAccountMergeParticipant(), new PostgresPersonsPartyMergeParticipant()]);
        var operation = Guid.CreateVersion7();
        var result = await merge.MergeAsync(sourceId, targetId, operation, actor, TimeProvider.System.GetUtcNow(), default);
        Assert.False(result.Replayed);

        await using var verifyConnection = await dataSource.OpenConnectionAsync();
        await using var verify = new NpgsqlCommand("""
            SELECT
              (SELECT count(*) FROM persons_contact_points WHERE party_id=@source AND is_active),
              (SELECT count(*) FROM persons_contact_points WHERE party_id=@target AND kind=1 AND is_active),
              (SELECT is_messaging_allowed FROM persons_contact_points WHERE party_id=@target AND kind=1 AND is_active),
              (SELECT origin_party_id FROM persons_contact_points WHERE party_id=@target AND kind=1 AND is_active),
              (SELECT count(*) FROM persons_addresses WHERE party_id=@source AND is_active),
              (SELECT count(*) FROM persons_addresses WHERE party_id=@target AND is_active),
              (SELECT origin_party_id FROM persons_addresses WHERE party_id=@target AND is_active
                 AND address_line='نشانی آزمایشی'),
              (SELECT origin_party_id FROM persons_addresses WHERE party_id=@target AND is_active
                 AND address_line='نشانی غیرتکراری مبدأ'),
              (SELECT origin_party_id FROM persons_contact_points WHERE party_id=@target AND kind=2 AND is_active
                 AND value='merge-source@example.test');
            """, verifyConnection);
        verify.Parameters.AddWithValue("source", sourceId);
        verify.Parameters.AddWithValue("target", targetId);
        await using var reader = await verify.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.False(reader.GetBoolean(2));
        // The target's exact duplicate remains target-origin; only transferred nonduplicates retain source origin.
        var duplicateOrigin = reader.GetGuid(3);
        Assert.True(duplicateOrigin == targetId,
            $"Exact duplicate origin mismatch: source={sourceId}; target={targetId}; origin={duplicateOrigin}");
        Assert.Equal(0L, reader.GetInt64(4));
        Assert.Equal(2L, reader.GetInt64(5));
        Assert.Equal(targetId, reader.GetGuid(6)); // duplicate target address remains target-origin
        Assert.Equal(sourceId, reader.GetGuid(7)); // nonduplicate transferred address retains source origin
        Assert.Equal(sourceId, reader.GetGuid(8)); // nonduplicate transferred contact retains source origin
        await reader.DisposeAsync();

        await using var historyConnection = await dataSource.OpenConnectionAsync();
        await using var history = new NpgsqlCommand("""
            SELECT count(*) FROM persons_lifecycle_history
            WHERE operation_id=@operation AND change_type IN ('merge-source','merge-transfer','merge-deduplicated');
            """, historyConnection);
        history.Parameters.AddWithValue("operation", operation);
        Assert.True((long)(await history.ExecuteScalarAsync())! >= 3L);
    }

    private static RegisterNaturalParty NewNatural(string mobile) =>
        new("آزمایش", "تاریخی", PartyBusinessRoles.Customer, Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L).ToString(),
            mobile, null, null, null, "تهران", "تهران", "1234567890", "نشانی آزمایشی",
            PartyAcquaintanceSource.Unknown, "", PartyCommunicationPreferences.Default);

    private static async Task<NpgsqlDataSource> OpenDisposableDataSourceAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Lifecycle integration tests require the disposable PostgreSQL database supplied by CI.");
        var dataSource = NpgsqlDataSource.Create(connectionString!);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var marker = new NpgsqlCommand(
            "SELECT coalesce(shobj_description(oid, 'pg_database'), '') FROM pg_database WHERE datname=current_database();",
            connection);
        var comment = (string?)await marker.ExecuteScalarAsync();
        Assert.Contains("MDGSM_DISPOSABLE_TEST_DB", comment ?? string.Empty);
        return dataSource;
    }
}
