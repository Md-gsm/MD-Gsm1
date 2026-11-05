using MDGsm.Finance.Application;
using MDGsm.Finance.Contracts;
using MDGsm.Finance.Infrastructure;
using MDGsm.Persons.Contracts;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Finance;

public sealed class FinancePartyAccountIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task First_financial_use_creates_one_account_and_replays_return_the_same_account()
    {
        await using var connection = await OpenMarkedConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var partyId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner();

        Assert.Equal(0L, await CountAsync(connection, transaction, partyId));

        var first = await provisioner.EnsureAsync(partyId, transaction);
        var replay = await provisioner.EnsureAsync(partyId, transaction);

        Assert.Equal(first.AccountId, replay.AccountId);
        Assert.Equal(partyId, first.PartyId);
        Assert.Equal(AccountsReceivableAccountStatus.Active, first.Status);
        Assert.Equal(first.CreatedAtUtc, replay.CreatedAtUtc);
        Assert.Equal(1L, await CountAsync(connection, transaction, partyId));

        await transaction.CommitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Unknown_party_is_rejected_before_an_account_is_created()
    {
        await using var connection = await OpenMarkedConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var partyId = Guid.CreateVersion7();
        var provisioner = new AccountsReceivableAccountProvisioner(
            new PostgresPartyAccountRepository(),
            new FixedFinanceActorContext(),
            new FixedPartyIdentityLookup(exists: false));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => provisioner.EnsureAsync(partyId, transaction));
        Assert.Equal(0L, await CountAsync(connection, transaction, partyId));
        await transaction.RollbackAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Account_creation_rolls_back_with_the_callers_financial_transaction()
    {
        await using var connection = await OpenMarkedConnectionAsync();
        var partyId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner();

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await provisioner.EnsureAsync(partyId, transaction);
            await transaction.RollbackAsync();
        }

        Assert.Equal(0L, await CountAsync(connection, null, partyId));

        await using var retry = await connection.BeginTransactionAsync();
        var created = await provisioner.EnsureAsync(partyId, retry);
        await retry.CommitAsync();

        Assert.Equal(partyId, created.PartyId);
        Assert.Equal(1L, await CountAsync(connection, null, partyId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_first_use_for_the_same_party_returns_one_stable_account_id()
    {
        var partyId = Guid.CreateVersion7();

        var first = EnsureAndCommitOnOwnConnectionAsync(partyId);
        var second = EnsureAndCommitOnOwnConnectionAsync(partyId);
        var results = await Task.WhenAll(first, second);

        Assert.Equal(results[0].AccountId, results[1].AccountId);
        Assert.Equal(partyId, results[0].PartyId);
        Assert.Equal(1L, await CountOnNewConnectionAsync(partyId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Closing_an_account_does_not_release_party_id_or_create_a_second_account()
    {
        await using var connection = await OpenMarkedConnectionAsync();
        var partyId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner();
        PartyAccountReference first;

        await using (var create = await connection.BeginTransactionAsync())
        {
            first = await provisioner.EnsureAsync(partyId, create);
            await create.CommitAsync();
        }

        await using var transaction = await connection.BeginTransactionAsync();
        await using (var close = new NpgsqlCommand(
            "UPDATE finance.party_accounts SET account_status = 3, row_version = row_version + 1 WHERE party_id = @party_id",
            connection, transaction))
        {
            close.Parameters.AddWithValue("party_id", partyId);
            Assert.Equal(1, await close.ExecuteNonQueryAsync());
        }

        var replay = await provisioner.EnsureAsync(partyId, transaction);
        Assert.Equal(first.AccountId, replay.AccountId);
        Assert.Equal(AccountsReceivableAccountStatus.Closed, replay.Status);
        Assert.Equal(1L, await CountAsync(connection, transaction, partyId));
        await transaction.CommitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Finance_account_rows_cannot_be_physically_deleted()
    {
        await using var connection = await OpenMarkedConnectionAsync();
        var partyId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner();

        await using (var create = await connection.BeginTransactionAsync())
        {
            await provisioner.EnsureAsync(partyId, create);
            await create.CommitAsync();
        }

        await using var transaction = await connection.BeginTransactionAsync();
        await using var delete = new NpgsqlCommand(
            "DELETE FROM finance.party_accounts WHERE party_id = @party_id",
            connection, transaction);
        delete.Parameters.AddWithValue("party_id", partyId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
        Assert.Equal("P0001", exception.SqlState);
        await transaction.RollbackAsync();
        Assert.Equal(1L, await CountAsync(connection, null, partyId));
    }

    private static AccountsReceivableAccountProvisioner CreateProvisioner() =>
        new(new PostgresPartyAccountRepository(), new FixedFinanceActorContext(), new FixedPartyIdentityLookup(exists: true));

    private static async Task<PartyAccountReference> EnsureAndCommitOnOwnConnectionAsync(Guid partyId)
    {
        await using var connection = await OpenMarkedConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await CreateProvisioner().EnsureAsync(partyId, transaction);
        await transaction.CommitAsync();
        return result;
    }

    private static async Task<NpgsqlConnection> OpenMarkedConnectionAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase")
            ?? throw new InvalidOperationException("Finance integration tests require the marked disposable PostgreSQL database supplied by CI.");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Finance integration tests require the marked disposable PostgreSQL database supplied by CI.");

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var marker = new NpgsqlCommand(
            "SELECT coalesce(shobj_description(oid, 'pg_database'), '') FROM pg_database WHERE datname = current_database()",
            connection);
        var comment = (string?)await marker.ExecuteScalarAsync();
        Assert.Contains("MDGSM_DISPOSABLE_TEST_DB", comment ?? string.Empty);
        return connection;
    }

    private static async Task<long> CountOnNewConnectionAsync(Guid partyId)
    {
        await using var connection = await OpenMarkedConnectionAsync();
        return await CountAsync(connection, null, partyId);
    }

    private static async Task<long> CountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid partyId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM finance.party_accounts WHERE party_id = @party_id",
            connection,
            transaction);
        command.Parameters.AddWithValue("party_id", partyId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class FixedFinanceActorContext : IFinanceActorContext
    {
        public string ActorId => "ci-finance-test-user";
    }

    private sealed class FixedPartyIdentityLookup(bool exists) : IPartyIdentityLookup
    {
        public Task<bool> ExistsAsync(Guid partyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(exists);
    }
}
