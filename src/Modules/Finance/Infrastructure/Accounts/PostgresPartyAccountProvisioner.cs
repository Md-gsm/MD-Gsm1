using System.Data;
using MDGsm.Finance.Contracts;
using MDGsm.Persons.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Finance.Infrastructure.Accounts;

/// <summary>
/// Idempotently creates the single Finance-owned subledger account inside the caller's transaction.
/// Party existence/activity and actor identity are mandatory trusted boundary dependencies.
/// </summary>
public sealed class PostgresPartyAccountProvisioner
{
    private readonly IPartyReferenceValidator _partyReferenceValidator;
    private readonly IFinanceActorContext _actorContext;

    public PostgresPartyAccountProvisioner(
        IPartyReferenceValidator partyReferenceValidator,
        IFinanceActorContext actorContext)
    {
        _partyReferenceValidator = partyReferenceValidator
            ?? throw new ArgumentNullException(nameof(partyReferenceValidator));
        _actorContext = actorContext
            ?? throw new ArgumentNullException(nameof(actorContext));
    }

    public async Task<PartyAccountReference> GetOrCreateAsync(
        PartyReference party,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("The Finance connection must already be open.");
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("The transaction must belong to the supplied connection.", nameof(transaction));
        if (transaction.IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException(
                "Party account provisioning requires READ COMMITTED isolation so a concurrent winner is visible to the replay SELECT.");

        var actorId = _actorContext.ActorId;
        if (string.IsNullOrWhiteSpace(actorId) ||
            actorId.Length > 128 ||
            !string.Equals(actorId, actorId.Trim(), StringComparison.Ordinal) ||
            actorId.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                "A trusted authenticated Finance actor identifier of 1-128 non-control characters is required.");
        }

        var partyIsActive = await _partyReferenceValidator.IsActiveAsync(
            party.PartyId, connection, transaction, cancellationToken);
        if (!partyIsActive)
            throw new InvalidOperationException("The referenced Party does not exist or is inactive.");

        // DO NOTHING avoids touching created_at/created_by or generating a new row version on replay.
        // The SELECT is a separate statement: under READ COMMITTED it sees a concurrent transaction
        // that won the unique-key race after INSERT waited for that transaction to finish.
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO finance_party_accounts (account_id, party_id, created_by)
            VALUES (@account_id, @party_id, @created_by)
            ON CONFLICT (party_id) DO NOTHING;
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("account_id", NpgsqlDbType.Uuid, Guid.CreateVersion7());
            insert.Parameters.AddWithValue("party_id", NpgsqlDbType.Uuid, party.PartyId);
            insert.Parameters.AddWithValue("created_by", NpgsqlDbType.Varchar, actorId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var select = new NpgsqlCommand("""
            SELECT account_id, party_id, created_at_utc
            FROM finance_party_accounts
            WHERE party_id = @party_id;
            """, connection, transaction);
        select.Parameters.AddWithValue("party_id", NpgsqlDbType.Uuid, party.PartyId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Finance account provisioning returned no account row.");

        return new PartyAccountReference(reader.GetGuid(0), reader.GetGuid(1),
            DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc));
    }
}
