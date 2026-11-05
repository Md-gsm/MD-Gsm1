using System.Data;
using System.Data.Common;
using MDGsm.Finance.Application;
using MDGsm.Finance.Domain;
using Npgsql;

namespace MDGsm.Finance.Infrastructure;

/// <summary>
/// PostgreSQL adapter. The INSERT/ON CONFLICT statement is the concurrency boundary:
/// concurrent first-use calls for the same Party serialize on the unique PartyId constraint
/// and return the same immutable AccountId.
/// </summary>
public sealed class PostgresPartyAccountRepository : IPartyAccountRepository
{
    public async Task<PartyAccount> GetOrCreateAsync(
        Guid partyId,
        string actorId,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction is not NpgsqlTransaction postgresTransaction ||
            postgresTransaction.Connection is not { State: ConnectionState.Open } connection)
        {
            throw new ArgumentException(
                "Finance account provisioning requires an active Npgsql transaction.",
                nameof(transaction));
        }

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO finance.party_accounts
                (account_id, party_id, account_status, created_by)
            VALUES
                (@account_id, @party_id, 1, @created_by)
            ON CONFLICT (party_id)
            DO UPDATE SET party_id = EXCLUDED.party_id
            RETURNING account_id, party_id, account_status, created_at_utc, created_by;
            """,
            connection,
            postgresTransaction);

        command.Parameters.AddWithValue("account_id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("party_id", partyId);
        command.Parameters.AddWithValue("created_by", actorId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Finance account provisioning returned no account row.");

        return new PartyAccount(
            reader.GetGuid(0),
            reader.GetGuid(1),
            (PartyAccountStatus)reader.GetInt16(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetString(4));
    }
}
