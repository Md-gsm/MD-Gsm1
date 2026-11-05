using MDGsm.Finance.Infrastructure.Accounts;
using MDGsm.Persons.Contracts;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class FinancePartyAccountIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_first_financial_use_creates_exactly_one_account_for_party()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(cs));
        await using var dataSource = NpgsqlDataSource.Create(cs!);
        var provisioner = new PostgresPartyAccountProvisioner();
        var party = new PartyReference(Guid.CreateVersion7());

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async worker =>
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var result = await provisioner.GetOrCreateAsync(party, $"finance-ci-{worker}", connection, transaction);
            await transaction.CommitAsync();
            return result;
        }));

        Assert.Single(results.Select(x => x.AccountId).Distinct());
        Assert.All(results, x => Assert.Equal(party.PartyId, x.PartyId));
        await using var verifyConnection = await dataSource.OpenConnectionAsync();
        await using var verify = new NpgsqlCommand("SELECT count(*) FROM finance_party_accounts WHERE party_id = @party_id", verifyConnection);
        verify.Parameters.AddWithValue("party_id", party.PartyId);
        Assert.Equal(1L, (long)(await verify.ExecuteScalarAsync())!);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Account_creation_participates_in_the_callers_transaction()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(cs));
        await using var dataSource = NpgsqlDataSource.Create(cs!);
        var party = new PartyReference(Guid.CreateVersion7());
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var account = await new PostgresPartyAccountProvisioner().GetOrCreateAsync(party, "finance-rollback-test", connection, transaction);
        Assert.Equal(party.PartyId, account.PartyId);
        await transaction.RollbackAsync();

        await using var verifyConnection = await dataSource.OpenConnectionAsync();
        await using var verify = new NpgsqlCommand("SELECT count(*) FROM finance_party_accounts WHERE party_id = @party_id", verifyConnection);
        verify.Parameters.AddWithValue("party_id", party.PartyId);
        Assert.Equal(0L, (long)(await verify.ExecuteScalarAsync())!);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Different_parties_receive_different_account_ids()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(cs));
        await using var dataSource = NpgsqlDataSource.Create(cs!);
        var provisioner = new PostgresPartyAccountProvisioner();
        var first = await ProvisionAsync(dataSource, provisioner, new PartyReference(Guid.CreateVersion7()), "finance-ci-a");
        var second = await ProvisionAsync(dataSource, provisioner, new PartyReference(Guid.CreateVersion7()), "finance-ci-b");
        Assert.NotEqual(first.AccountId, second.AccountId);
        Assert.NotEqual(first.PartyId, second.PartyId);
    }

    private static async Task<MDGsm.Finance.Contracts.PartyAccountReference> ProvisionAsync(
        NpgsqlDataSource dataSource, PostgresPartyAccountProvisioner provisioner, PartyReference party, string actor)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await provisioner.GetOrCreateAsync(party, actor, connection, transaction);
        await transaction.CommitAsync();
        return result;
    }
}
