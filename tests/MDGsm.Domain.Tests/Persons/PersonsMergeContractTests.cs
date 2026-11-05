using System.Data.Common;
using MDGsm.Finance.Infrastructure.Accounts;
using MDGsm.Infrastructure.Persons;
using MDGsm.Persons.Contracts;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsMergeContractTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC42_preview_is_read_only_and_counts_transferable_finance_references()
    {
        await using var dataSource = OpenDataSource();
        var source = await CreatePartyAsync(dataSource, "Source");
        var target = await CreatePartyAsync(dataSource, "Target");
        var accountId = await CreateFinanceAccountAsync(dataSource, source);
        var service = new PostgresPartyMergeService(dataSource, [new PostgresPartyAccountMergeParticipant()]);

        var preview = await service.PreviewAsync(source, target, "ci-merge-actor");

        Assert.True(preview.CanMerge);
        Assert.Equal(1, preview.TransferableReferences);
        Assert.Equal(0, preview.HistoricalReferences);
        Assert.Equal(0, preview.BlockingConflicts);
        Assert.Single(preview.Participants);
        Assert.Equal(accountId, await FindAccountIdAsync(dataSource, source));
        Assert.True(await IsActiveAsync(dataSource, source));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC41_AC44_merge_transfers_registered_reference_atomically_and_replays_idempotently()
    {
        await using var dataSource = OpenDataSource();
        var source = await CreatePartyAsync(dataSource, "Source");
        var target = await CreatePartyAsync(dataSource, "Target");
        var accountId = await CreateFinanceAccountAsync(dataSource, source);
        var createdAt = await FindAccountCreatedAtAsync(dataSource, source);
        var service = new PostgresPartyMergeService(dataSource, [new PostgresPartyAccountMergeParticipant()]);
        var operation = Guid.CreateVersion7();
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var first = await service.MergeAsync(source, target, operation, "ci-merge-actor", now);
        var replay = await service.MergeAsync(source, target, operation, "ci-merge-actor", now.AddMinutes(1));

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.False(await IsActiveAsync(dataSource, source));
        Assert.Equal(target, await FindMergedIntoAsync(dataSource, source));
        Assert.Equal(accountId, await FindAccountIdAsync(dataSource, target));
        Assert.Equal(createdAt, await FindAccountCreatedAtAsync(dataSource, target));
        Assert.Equal(1, await CountMergeRecordsAsync(dataSource, operation));
        Assert.Null(await FindAccountIdAsync(dataSource, source));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC43_two_existing_finance_accounts_are_a_blocking_conflict_and_no_state_changes()
    {
        await using var dataSource = OpenDataSource();
        var source = await CreatePartyAsync(dataSource, "Source");
        var target = await CreatePartyAsync(dataSource, "Target");
        var sourceAccount = await CreateFinanceAccountAsync(dataSource, source);
        var targetAccount = await CreateFinanceAccountAsync(dataSource, target);
        var service = new PostgresPartyMergeService(dataSource, [new PostgresPartyAccountMergeParticipant()]);
        var preview = await service.PreviewAsync(source, target, "ci-merge-actor");

        Assert.False(preview.CanMerge);
        Assert.Equal(1, preview.BlockingConflicts);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MergeAsync(
            source, target, Guid.CreateVersion7(), "ci-merge-actor", DateTimeOffset.UtcNow));

        Assert.True(await IsActiveAsync(dataSource, source));
        Assert.True(await IsActiveAsync(dataSource, target));
        Assert.Equal(sourceAccount, await FindAccountIdAsync(dataSource, source));
        Assert.Equal(targetAccount, await FindAccountIdAsync(dataSource, target));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC44_failure_after_apply_rolls_back_every_participant_and_runs_recovery()
    {
        await using var dataSource = OpenDataSource();
        var source = await CreatePartyAsync(dataSource, "Source");
        var target = await CreatePartyAsync(dataSource, "Target");
        var sourceAccount = await CreateFinanceAccountAsync(dataSource, source);
        var failing = new FailingMergeParticipant();
        var service = new PostgresPartyMergeService(dataSource,
            [new PostgresPartyAccountMergeParticipant(), failing]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MergeAsync(
            source, target, Guid.CreateVersion7(), "ci-merge-actor", DateTimeOffset.UtcNow));

        Assert.Equal(1, failing.RecoveryCalls);
        Assert.True(await IsActiveAsync(dataSource, source));
        Assert.Null(await FindMergedIntoAsync(dataSource, source));
        Assert.Equal(sourceAccount, await FindAccountIdAsync(dataSource, source));
        Assert.Null(await FindAccountIdAsync(dataSource, target));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AC44_concurrent_merges_of_one_source_allow_exactly_one_winner()
    {
        await using var dataSource = OpenDataSource();
        var source = await CreatePartyAsync(dataSource, "Source");
        var firstTarget = await CreatePartyAsync(dataSource, "First");
        var secondTarget = await CreatePartyAsync(dataSource, "Second");
        await CreateFinanceAccountAsync(dataSource, source);
        var service = new PostgresPartyMergeService(dataSource, [new PostgresPartyAccountMergeParticipant()]);
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        async Task<bool> Attempt(Guid target, Guid operation)
        {
            try
            {
                await service.MergeAsync(source, target, operation, "ci-merge-actor", now);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(
            Attempt(firstTarget, Guid.CreateVersion7()),
            Attempt(secondTarget, Guid.CreateVersion7()));

        Assert.Single(results.Where(x => x));
        Assert.False(await IsActiveAsync(dataSource, source));
        Assert.Contains(await FindMergedIntoAsync(dataSource, source), new Guid?[] { firstTarget, secondTarget });
        Assert.Equal(1, await FindAccountCountForPartyAsync(dataSource, firstTarget) +
                        await FindAccountCountForPartyAsync(dataSource, secondTarget));
    }

    private static async Task<long> FindAccountCountForPartyAsync(NpgsqlDataSource dataSource, Guid partyId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM finance_party_accounts WHERE party_id = @party;", connection);
        command.Parameters.AddWithValue("party", partyId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static NpgsqlDataSource OpenDataSource()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "The integration test requires the marked disposable PostgreSQL database supplied by CI.");
        return NpgsqlDataSource.Create(connectionString!);
    }

    private static async Task<Guid> CreatePartyAsync(NpgsqlDataSource dataSource, string label)
    {
        var id = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO persons
                (party_id, party_type, business_roles, given_name, family_name, created_by, updated_by)
            VALUES (@id, 1, 1, @given, @family, 'ci-merge', 'ci-merge');
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("given", label);
        command.Parameters.AddWithValue("family", "Test");
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task<Guid> CreateFinanceAccountAsync(NpgsqlDataSource dataSource, Guid partyId)
    {
        var accountId = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO finance_party_accounts (account_id, party_id, created_by)
            VALUES (@account, @party, 'ci-merge');
            """, connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("party", partyId);
        await command.ExecuteNonQueryAsync();
        return accountId;
    }

    private static async Task<Guid?> FindAccountIdAsync(NpgsqlDataSource dataSource, Guid partyId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT account_id FROM finance_party_accounts WHERE party_id = @party;", connection);
        command.Parameters.AddWithValue("party", partyId);
        var value = await command.ExecuteScalarAsync();
        return value is Guid id ? id : null;
    }

    private static async Task<DateTime> FindAccountCreatedAtAsync(NpgsqlDataSource dataSource, Guid partyId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT created_at_utc FROM finance_party_accounts WHERE party_id = @party;", connection);
        command.Parameters.AddWithValue("party", partyId);
        return (DateTime)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> IsActiveAsync(NpgsqlDataSource dataSource, Guid partyId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT is_active FROM persons WHERE party_id = @party;", connection);
        command.Parameters.AddWithValue("party", partyId);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid?> FindMergedIntoAsync(NpgsqlDataSource dataSource, Guid partyId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT merged_into_party_id FROM persons WHERE party_id = @party;", connection);
        command.Parameters.AddWithValue("party", partyId);
        var value = await command.ExecuteScalarAsync();
        return value is Guid id ? id : null;
    }

    private static async Task<long> CountMergeRecordsAsync(NpgsqlDataSource dataSource, Guid operationId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM persons_party_merge_records WHERE operation_id = @operation;", connection);
        command.Parameters.AddWithValue("operation", operationId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class FailingMergeParticipant : IPartyMergeParticipant
    {
        public string Module => "TestFailure";
        public string ContractVersion => "1.0";
        public string MergeProtocolVersion => PostgresPartyMergeService.ProtocolVersion;
        public int RecoveryCalls { get; private set; }

        public Task<PartyMergeParticipantPreview> PreviewAsync(
            PartyMergeContext context, DbConnection connection, CancellationToken cancellationToken) =>
            Task.FromResult(new PartyMergeParticipantPreview(Module, 0, 0, 0));

        public Task PrepareAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ValidateAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ApplyAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task VerifyAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Injected participant verification failure.");

        public Task RecoverAsync(PartyMergeContext context, DbConnection connection, DbTransaction transaction,
            CancellationToken cancellationToken)
        {
            RecoveryCalls++;
            return Task.CompletedTask;
        }
    }
}
