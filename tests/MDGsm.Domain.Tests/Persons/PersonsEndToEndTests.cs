using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsEndToEndTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Registered_party_contacts_and_address_are_written_atomically_to_postgresql()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "The integration test requires a disposable PostgreSQL database supplied by CI.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var partyId = Guid.CreateVersion7();

        try
        {
            await using (var party = new NpgsqlCommand("""
                INSERT INTO persons
                  (party_id, party_type, business_roles, given_name, family_name, created_by, updated_by)
                VALUES (@id, 1, 1, 'CI', 'Integration Test', 'ci-test-user', 'ci-test-user');
                """, connection, transaction))
            {
                party.Parameters.AddWithValue("id", partyId);
                Assert.Equal(1, await party.ExecuteNonQueryAsync());
            }

            await using (var contact = new NpgsqlCommand("""
                INSERT INTO persons_contact_points
                  (contact_point_id, party_id, kind, value, is_primary, is_messaging_allowed, created_by)
                VALUES (@contactId, @partyId, 1, '+989121234567', true, true, 'ci-test-user');
                """, connection, transaction))
            {
                contact.Parameters.AddWithValue("contactId", Guid.CreateVersion7());
                contact.Parameters.AddWithValue("partyId", partyId);
                Assert.Equal(1, await contact.ExecuteNonQueryAsync());
            }

            await using (var address = new NpgsqlCommand("""
                INSERT INTO persons_addresses
                  (address_id, party_id, country_code, province, city, postal_code, address_line, is_primary, created_by)
                VALUES (@addressId, @partyId, 'IR', 'تهران', 'تهران', '1234567890', 'آدرس آزمایشی CI', true, 'ci-test-user');
                """, connection, transaction))
            {
                address.Parameters.AddWithValue("addressId", Guid.CreateVersion7());
                address.Parameters.AddWithValue("partyId", partyId);
                Assert.Equal(1, await address.ExecuteNonQueryAsync());
            }

            await using var verify = new NpgsqlCommand("""
                SELECT count(*) FROM persons p
                JOIN persons_contact_points c ON c.party_id=p.party_id
                JOIN persons_addresses a ON a.party_id=p.party_id
                WHERE p.party_id=@id AND p.is_active AND c.is_primary AND a.is_primary;
                """, connection, transaction);
            verify.Parameters.AddWithValue("id", partyId);
            Assert.Equal(1L, (long)(await verify.ExecuteScalarAsync())!);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }
}
