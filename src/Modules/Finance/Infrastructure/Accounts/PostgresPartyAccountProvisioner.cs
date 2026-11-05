using MDGsm.Finance.Contracts;
using MDGsm.Persons.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Finance.Infrastructure.Accounts;

/// <summary>Idempotently creates the single Finance-owned subledger account inside the caller's transaction.</summary>
public sealed class PostgresPartyAccountProvisioner
{
    private const string ProvisionSql = """
        INSERT INTO finance_party_accounts (account_id, party_id, created_by)
        VALUES (@account_id, @party_id, @created_by)
        ON CONFLICT (party_id) DO UPDATE
            SET party_id = EXCLUDED.party_id
        RETURNING account_id, party_id, created_at_utc;
        """;

    public async Task<PartyAccountReference> GetOrCreateAsync(
        PartyReference party, string actorId, NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("The Finance connection must already be open.");
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("The transaction must belong to the supplied connection.", nameof(transaction));

        await using var command = new NpgsqlCommand(ProvisionSql, connection, transaction);
        command.Parameters.AddWithValue("account_id", NpgsqlDbType.Uuid, Guid.CreateVersion7());
        command.Parameters.AddWithValue("party_id", NpgsqlDbType.Uuid, party.PartyId);
        command.Parameters.AddWithValue("created_by", NpgsqlDbType.Varchar, actorId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Finance account provisioning returned no account row.");

        return new PartyAccountReference(reader.GetGuid(0), reader.GetGuid(1),
            DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc));
    }
}
