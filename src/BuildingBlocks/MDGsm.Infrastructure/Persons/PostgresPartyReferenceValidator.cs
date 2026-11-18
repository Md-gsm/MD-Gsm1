using System.Data.Common;
using MDGsm.Persons.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Transitional Persons-owned PostgreSQL adapter. It validates and share-locks an active Party
/// in the caller's transaction, preventing deactivation while a Finance operation is in flight.
/// </summary>
public sealed class PostgresPartyReferenceValidator : IPartyReferenceValidator
{
    public async Task<bool> IsActiveAsync(
        Guid partyId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Empty)
            return false;
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (connection is not NpgsqlConnection postgresConnection ||
            transaction is not NpgsqlTransaction postgresTransaction ||
            !ReferenceEquals(postgresTransaction.Connection, postgresConnection))
        {
            throw new ArgumentException("Party validation requires the matching PostgreSQL connection and transaction.");
        }

        if (postgresConnection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("The Persons connection must already be open.");

        await using var command = new NpgsqlCommand(
            "SELECT 1 FROM persons WHERE party_id = @party_id AND is_active FOR SHARE;",
            postgresConnection,
            postgresTransaction);
        command.Parameters.AddWithValue("party_id", NpgsqlDbType.Uuid, partyId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }
}
