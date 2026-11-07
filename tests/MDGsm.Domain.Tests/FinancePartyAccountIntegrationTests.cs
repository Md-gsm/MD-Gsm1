using System.Data.Common;
using MDGsm.Finance.Contracts;
using MDGsm.Finance.Infrastructure.Accounts;
using MDGsm.Infrastructure.Persons;
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
        var cs = GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(cs);
        var party = new PartyReference(Guid.CreateVersion7());

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async worker =>
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var provisioner = CreateProvisioner($"finance-ci-{worker}");
            var result = await provisioner.GetOrCreateAsync(party, connection, transaction);
            await transaction.CommitAsync();
            return result;
        }));

        Assert.Single(results.Select(x => x.AccountId).Distinct());
        Assert.All(results, x => Assert.Equal(party.PartyId, x.PartyId));
        await using var verifyConnection = await dataSource.OpenConnectionAsync();
        await using var verify = new NpgsqlCommand(
            "SELECT count(*) FROM finance_party_accounts WHERE party_id = @party_id", verifyConnection);
        verify.Parameters.AddWithValue("party_id", party.PartyId);
        Assert.Equal(1L, (long)(await verify.ExecuteScalarAsync())!);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Account_creation_participates_in_the_callers_transaction()
    {
        var cs = GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(cs);
        var party = new PartyReference(Guid.CreateVersion7());
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var account = await CreateProvisioner("finance-rollback-test")
            .GetOrCreateAsync(party, connection, transaction);
        Assert.Equal(party.PartyId, account.PartyId);
        await transaction.RollbackAsync();

        await using var verifyConnection = await dataSource.OpenConnectionAsync();
        await using var verify = new NpgsqlCommand(
            "SELECT count(*) FROM finance_party_accounts WHERE party_id = @party_id", verifyConnection);
        verify.Parameters.AddWithValue("party_id", party.PartyId);
        Assert.Equal(0L, (long)(await verify.ExecuteScalarAsync())!);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Different_parties_receive_different_account_ids()
    {
        var cs = GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(cs);
        var first = await ProvisionAsync(dataSource, new PartyReference(Guid.CreateVersion7()), "finance-ci-a");
        var second = await ProvisionAsync(dataSource, new PartyReference(Guid.CreateVersion7()), "finance-ci-b");
        Assert.NotEqual(first.AccountId, second.AccountId);
        Assert.NotEqual(first.PartyId, second.PartyId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Unknown_or_inactive_party_is_rejected_before_account_creation()
    {
        var cs = GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(cs);
        var party = new PartyReference(Guid.CreateVersion7());
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var provisioner = new PostgresPartyAccountProvisioner(
            new FixedPartyReferenceValidator(false), new FixedFinanceActorContext("finance-validation-test"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provisioner.GetOrCreateAsync(party, connection, transaction));
        Assert.Contains("does not exist or is inactive", exception.Message, StringComparison.Ordinal);
        await transaction.RollbackAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Persons_validator_accepts_active_party_and_rejects_deactivated_party()
    {
        var cs = GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(cs);
        var partyId = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var insert = new NpgsqlCommand("""
            INSERT INTO persons
                (party_id, party_type, business_roles, given_name, family_name, created_by, updated_by)
            VALUES (@party_id, 1, 1, 'Finance', 'Validation', 'finance-test', 'finance-test');
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("party_id", partyId);
            await insert.ExecuteNonQueryAsync();
        }

        var validator = new PostgresPartyReferenceValidator();
        Assert.True(await validator.IsActiveAsync(partyId, connection, transaction));

        await using (var deactivate = new NpgsqlCommand(
            "UPDATE persons SET is_active = false WHERE party_id = @party_id;", connection, transaction))
        {
            deactivate.Parameters.AddWithValue("party_id", partyId);
            await deactivate.ExecuteNonQueryAsync();
        }

        Assert.False(await validator.IsActiveAsync(partyId, connection, transaction));
        await transaction.RollbackAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Replay_preserves_original_account_and_creation_audit_metadata()
    {
        var cs = GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(cs);
        var party = new PartyReference(Guid.CreateVersion7());
        var first = await ProvisionAsync(dataSource, party, "finance-original-actor");
        var replay = await ProvisionAsync(dataSource, party, "finance-replay-actor");

        Assert.Equal(first.AccountId, replay.AccountId);
        Assert.Equal(first.CreatedAtUtc, replay.CreatedAtUtc);

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT created_by FROM finance_party_accounts WHERE party_id = @party_id;", connection);
        command.Parameters.AddWithValue("party_id", party.PartyId);
        Assert.Equal("finance-original-actor", (string)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Missing_or_overlength_actor_context_is_rejected()
    {
        var cs = GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(cs);
        var party = new PartyReference(Guid.CreateVersion7());

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var missingActor = new PostgresPartyAccountProvisioner(
                new FixedPartyReferenceValidator(true), new FixedFinanceActorContext(null));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => missingActor.GetOrCreateAsync(party, connection, transaction));
            await transaction.RollbackAsync();
        }

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var longActor = new PostgresPartyAccountProvisioner(
                new FixedPartyReferenceValidator(true), new FixedFinanceActorContext(new string('a', 129)));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => longActor.GetOrCreateAsync(party, connection, transaction));
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Finance_party_account_schema_does_not_claim_unused_row_version()
    {
        var cs = GetConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(cs);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*)
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = 'finance_party_accounts'
              AND column_name = 'row_version';
            """, connection);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value!;
    }

    private static PostgresPartyAccountProvisioner CreateProvisioner(string actor) =>
        new(new FixedPartyReferenceValidator(true), new FixedFinanceActorContext(actor));

    private static async Task<PartyAccountReference> ProvisionAsync(
        NpgsqlDataSource dataSource, PartyReference party, string actor)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await CreateProvisioner(actor).GetOrCreateAsync(party, connection, transaction);
        await transaction.CommitAsync();
        return result;
    }

    private sealed class FixedPartyReferenceValidator(bool isActive) : IPartyReferenceValidator
    {
        public Task<bool> IsActiveAsync(
            Guid partyId, DbConnection connection, DbTransaction transaction,
            CancellationToken cancellationToken = default) => Task.FromResult(isActive);
    }

    private sealed class FixedFinanceActorContext(string? actorId) : IFinanceActorContext
    {
        public string? ActorId => actorId;
    }
}
