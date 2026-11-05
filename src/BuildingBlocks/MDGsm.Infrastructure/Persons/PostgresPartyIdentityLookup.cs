using System.Data;
using System.Data.Common;
using MDGsm.Persons.Contracts;
using Npgsql;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Persons-owned read adapter for durable PartyId references. It deliberately does not
/// filter by is_active: deactivation must not invalidate historical financial ownership.
/// The supplied transaction is reused to preserve the caller's atomic boundary.
/// </summary>
public sealed class PostgresPartyIdentityLookup : IPartyIdentityLookup
{
    public async Task<bool> ExistsAsync(
        Guid partyId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Empty)
            return false;

        if (transaction is not NpgsqlTransaction postgresTransaction)
            throw new ArgumentException("Party identity lookup requires an Npgsql transaction.", nameof(transaction));

        var connection = postgresTransaction.Connection;
        if (connection is null || connection.State != ConnectionState.Open)
            throw new ArgumentException("Party identity lookup requires an active Npgsql connection.", nameof(transaction));

        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM persons WHERE party_id = @party_id);",
            connection,
            postgresTransaction);
        command.Parameters.AddWithValue("party_id", partyId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
