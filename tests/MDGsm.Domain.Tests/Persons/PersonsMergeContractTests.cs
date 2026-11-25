using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using MDGsm.Finance.Infrastructure.Accounts;
using MDGsm.Persons.Infrastructure;
using MDGsm.Persons.Contracts;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsMergeContractTests
{
    [Fact]
    public void AC39_AC40_AllExternalPartyIdConsumersAreRegisteredAndClassifiedExactlyOnce()
    {
        var root = FindRepositoryRoot();
        using var registryDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "docs", "persons", "consumer-registry.json")));
        var registry = registryDocument.RootElement;
        var owners = registry.GetProperty("ownerPaths").EnumerateArray()
            .Select(item => item.GetString()!.Replace('\\', '/').TrimEnd('/'))
            .ToArray();
        var consumers = registry.GetProperty("registeredConsumers").EnumerateArray().ToArray();
        var paths = consumers.Select(item => item.GetProperty("path").GetString()!.Replace('\\', '/')).ToArray();

        Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());

        var allowed = registry.GetProperty("registryPolicy").GetProperty("allowedMergeClassifications")
            .EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(allowed);
        foreach (var consumer in consumers)
        {
            Assert.Equal(1, consumer.EnumerateObject().Count(property => property.Name == "mergeClassification"));
            var classification = consumer.GetProperty("mergeClassification").GetString();
            Assert.False(string.IsNullOrWhiteSpace(classification));
            Assert.Contains(classification!, allowed);
        }

        var referencePattern = new Regex(
            @"\bPartyId\b|\bparty_id\b|REFERENCES\s+parties\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var extensions = new HashSet<string>(
            [".cs", ".sql", ".xaml", ".json", ".yml", ".yaml", ".xml", ".props", ".csproj"],
            StringComparer.OrdinalIgnoreCase);
        var discovered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scanRoot in new[] { Path.Combine(root, "src"), Path.Combine(root, "database", "migrations") })
        {
            if (!Directory.Exists(scanRoot)) continue;
            foreach (var file in Directory.EnumerateFiles(scanRoot, "*", SearchOption.AllDirectories))
            {
                if (!extensions.Contains(Path.GetExtension(file))) continue;
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Split('/').Any(segment => segment is "bin" or "obj")) continue;
                if (owners.Any(owner => relative == owner || relative.StartsWith(owner + "/", StringComparison.Ordinal))) continue;
                var source = File.ReadAllText(file);
                if (referencePattern.IsMatch(source)) discovered.Add(relative);
            }
        }

        Assert.Equal(discovered.OrderBy(x => x, StringComparer.Ordinal),
            paths.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void AC41_FinanceMergeParticipantImplementsVersionedProtocolHooks()
    {
        var participant = new PostgresPartyAccountMergeParticipant();
        var contract = typeof(IPartyMergeParticipant);
        var implementation = typeof(PostgresPartyAccountMergeParticipant);

        Assert.Contains(contract, implementation.GetInterfaces());
        Assert.Equal("Finance", participant.Module);
        Assert.Equal("1.1", participant.ContractVersion);
        Assert.Equal(PostgresPartyMergeService.ProtocolVersion, participant.MergeProtocolVersion);

        var requiredHooks = new[]
        {
            "PreviewAsync", "PrepareAsync", "ValidateAsync", "ApplyAsync", "VerifyAsync", "RecoverAsync"
        };
        var interfaceMap = implementation.GetInterfaceMap(contract);
        var implementedHooks = interfaceMap.InterfaceMethods
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var hook in requiredHooks)
            Assert.Contains(hook, implementedHooks);

        var root = FindRepositoryRoot();
        using var registryDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "docs", "persons", "consumer-registry.json")));
        var participantEntry = registryDocument.RootElement.GetProperty("registeredConsumers")
            .EnumerateArray()
            .Single(item => item.GetProperty("path").GetString() ==
                "src/Modules/Finance/Infrastructure/Accounts/PostgresPartyAccountMergeParticipant.cs");
        Assert.Equal("PERSISTED_ACTIVE_REFERENCE",
            participantEntry.GetProperty("mergeClassification").GetString());
        Assert.True(participantEntry.GetProperty("mergeParticipant").GetBoolean());
        var declaredParticipants = registryDocument.RootElement.GetProperty("registeredConsumers")
            .EnumerateArray()
            .Where(item => item.GetProperty("mergeParticipant").GetBoolean())
            .Select(item => item.GetProperty("path").GetString())
            .ToArray();
        Assert.Equal(new[]
        {
            "src/Modules/Finance/Infrastructure/Accounts/PostgresPartyAccountMergeParticipant.cs"
        }, declaredParticipants);
    }

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
        Assert.Equal(1, preview.ActiveReferences);
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
        Assert.Equal(1L, await CountMergeRecordsAsync(dataSource, operation));
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
    public async Task AC44_MergeIsAtomicConcurrentAndIdempotentWithFailureRollback()
    {
        // Compose all three independent failure modes under one AC-44 acceptance test:
        // replay idempotency, transaction rollback/recovery, and competing merge concurrency.
        await AC41_AC44_merge_transfers_registered_reference_atomically_and_replays_idempotently();
        await AC44_failure_after_apply_rolls_back_every_participant_and_runs_recovery();
        await AC44_concurrent_merges_of_one_source_allow_exactly_one_winner();
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

        Assert.Single(results, succeeded => succeeded);
        Assert.False(await IsActiveAsync(dataSource, source));
        Assert.Contains(await FindMergedIntoAsync(dataSource, source), new Guid?[] { firstTarget, secondTarget });
        Assert.Equal(1L, await FindAccountCountForPartyAsync(dataSource, firstTarget) +
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

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")) &&
                File.Exists(Path.Combine(directory.FullName, "docs", "persons", "consumer-registry.json")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the MD-Gsm repository root for the Persons merge contract tests.");
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
            Task.FromResult(new PartyMergeParticipantPreview(Module, 0, 0, 0, 0));

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
