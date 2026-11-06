using System.Collections.Concurrent;
using System.Security.Cryptography;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Infrastructure.IdentityAccess;
using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class IdentityAccessPostgresIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Lockout_heartbeat_revocation_and_concurrent_login_capacity_are_enforced_by_postgresql()
    {
        var connectionString = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "IdentityAccess integration tests require a dedicated disposable PostgreSQL database.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", builder.Database);

        await using (var guard = new NpgsqlConnection(connectionString))
        {
            await guard.OpenAsync();
            await using var marker = new NpgsqlCommand(
                "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = current_database();",
                guard);
            var comment = (string?)await marker.ExecuteScalarAsync();
            Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB; IdentityAccess integration test only", comment);
        }

        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentityAccess:PrivacyHmacKey"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(x => (byte)x).ToArray()),
                ["ConnectionStrings:MDGsmDatabase"] = connectionString
            })
            .Build();

        services.AddMdGsmPersistence(configuration);
        services.AddIdentityAccessSecurity(configuration);
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<LoginHandler>();
        services.AddScoped<SessionHeartbeatHandler>();
        services.AddScoped<ChangeOwnPasswordHandler>();
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var tokenFactory = provider.GetRequiredService<IAccessTokenFactory>();
        var digest = provider.GetRequiredService<IPrivacyDigest>();
        var sharedPasswordHash = hasher.Hash("Correct-Password-2026!");

        await using (var setup = provider.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<MdGsmDbContext>();
            await db.Database.EnsureCreatedAsync();
            if (!await db.IdentitySystemSettings.AnyAsync(x => x.SettingsId == 1))
            {
                db.IdentitySystemSettings.Add(new IdentitySystemSettingsRow
                {
                    SettingsId = 1,
                    MaxActiveUsers = 5,
                    HeartbeatIntervalSeconds = 30,
                    SessionLeaseSeconds = 180,
                    UpdatedAtUtc = clock.GetUtcNow()
                });
            }

            if (!await db.IdentityAccounts.AnyAsync(x => x.LoginNameNormalized == "LOCKOUTUSER"))
                db.IdentityAccounts.Add(CreateAccount("lockoutuser", sharedPasswordHash, clock.GetUtcNow()));
            for (var i = 1; i <= 4; i++)
            {
                var normalized = $"CAPACITY{i}";
                if (!await db.IdentityAccounts.AnyAsync(x => x.LoginNameNormalized == normalized))
                    db.IdentityAccounts.Add(CreateAccount($"capacity{i}", sharedPasswordHash, clock.GetUtcNow()));
            }
            await db.SaveChangesAsync();
        }

        var lockoutSource = Digest(digest, "lockout-source");
        for (var attempt = 0; attempt < Account.FailedAttemptLimit; attempt++)
        {
            using var scope = provider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();
            var result = await handler.HandleAsync(
                new LoginCommand("lockoutuser", "wrong-password", Guid.CreateVersion7(),
                    lockoutSource, $"lockout-{attempt}"), CancellationToken.None);
            Assert.False(result.Succeeded);
            Assert.Equal(LoginFailure.InvalidCredentials, result.Failure);

            // Read back through a fresh scope after every attempt. This proves that
            // the counter is committed to PostgreSQL, not merely changed in memory.
            await AssertFailedAttemptCountPersistedAsync(provider, "LOCKOUTUSER", attempt + 1, clock);
        }

        clock.Advance(Account.LockDuration + TimeSpan.FromMinutes(1));
        LoginOutcome successful;
        using (var scope = provider.CreateScope())
        {
            successful = await scope.ServiceProvider.GetRequiredService<LoginHandler>()
                .HandleAsync(new LoginCommand("lockoutuser", "Correct-Password-2026!", Guid.CreateVersion7(),
                    Digest(digest, "lockout-source-after-window"), "lockout-recovery"), CancellationToken.None);
        }
        Assert.True(successful.Succeeded);
        Assert.NotNull(successful.AccessToken);
        Assert.NotNull(successful.LeaseExpiresAtUtc);

        var accountId = await ReadAccountId(provider, "LOCKOUTUSER");
        var token = successful.AccessToken!;
        var tokenDigest = tokenFactory.ComputeDigest(token);
        using (var scope = provider.CreateScope())
        {
            var heartbeat = scope.ServiceProvider.GetRequiredService<SessionHeartbeatHandler>();
            Assert.True(await heartbeat.HandleAsync(token, 1, CancellationToken.None));
        }

        await using (var verifyHeartbeat = provider.CreateAsyncScope())
        {
            var db = verifyHeartbeat.ServiceProvider.GetRequiredService<MdGsmDbContext>();
            var session = await db.IdentitySessions.SingleAsync(x => x.TokenDigest == tokenDigest);
            Assert.Equal(clock.GetUtcNow().Add(TimeSpan.FromSeconds(180)), session.LeaseExpiresAtUtc);
            Assert.Equal(clock.GetUtcNow(), session.LastSeenAtUtc);
        }

        using (var scope = provider.CreateScope())
        {
            var changePassword = scope.ServiceProvider.GetRequiredService<ChangeOwnPasswordHandler>();
            Assert.True(await changePassword.HandleAsync(accountId, "Correct-Password-2026!",
                "Changed-Password-2026!", "password-change-integration", CancellationToken.None));
        }

        using (var scope = provider.CreateScope())
        {
            var heartbeat = scope.ServiceProvider.GetRequiredService<SessionHeartbeatHandler>();
            Assert.False(await heartbeat.HandleAsync(token, 1, CancellationToken.None));
        }

        await using (var configureCapacity = provider.CreateAsyncScope())
        {
            var db = configureCapacity.ServiceProvider.GetRequiredService<MdGsmDbContext>();
            await db.IdentitySystemSettings.Where(x => x.SettingsId == 1)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaxActiveUsers, 2));
        }

        var outcomes = new ConcurrentBag<(string Username, LoginOutcome Outcome)>();
        var tasks = Enumerable.Range(1, 4).Select(async i =>
        {
            using var scope = provider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<LoginHandler>();
            var username = $"capacity{i}";
            var outcome = await handler.HandleAsync(
                new LoginCommand(username, "Correct-Password-2026!", Guid.CreateVersion7(),
                    Digest(digest, $"capacity-source-{i}"), $"capacity-login-{i}"),
                CancellationToken.None);
            outcomes.Add((username, outcome));
        });
        await Task.WhenAll(tasks);

        Assert.Equal(2, outcomes.Count(x => x.Outcome.Succeeded));
        Assert.Equal(2, outcomes.Count(x => x.Outcome.Failure == LoginFailure.SessionCapacityReached));

        var activeDistinctAccounts = 0;
        await using (var verifyCapacity = provider.CreateAsyncScope())
        {
            var db = verifyCapacity.ServiceProvider.GetRequiredService<MdGsmDbContext>();
            var now = clock.GetUtcNow();
            activeDistinctAccounts = await db.IdentitySessions
                .Where(x => x.RevokedAtUtc == null && x.LeaseExpiresAtUtc > now)
                .Select(x => x.AccountId).Distinct().CountAsync();
        }
        Assert.Equal(2, activeDistinctAccounts);

        var activeUsername = outcomes
            .Where(x => x.Outcome.Succeeded)
            .Select(x => x.Username)
            .OrderBy(x => x, StringComparer.Ordinal)
            .First();
        using (var secondClient = provider.CreateScope())
        {
            var result = await secondClient.ServiceProvider.GetRequiredService<LoginHandler>()
                .HandleAsync(new LoginCommand(activeUsername, "Correct-Password-2026!", Guid.CreateVersion7(),
                    Digest(digest, "same-account-second-client"), "same-account-second-client"),
                    CancellationToken.None);
            Assert.True(result.Succeeded,
                "A second session for an already-active account must not consume another distinct-user slot.");
        }

        await using (var finalCheck = provider.CreateAsyncScope())
        {
            var db = finalCheck.ServiceProvider.GetRequiredService<MdGsmDbContext>();
            var now = clock.GetUtcNow();
            var distinct = await db.IdentitySessions.Where(x => x.RevokedAtUtc == null && x.LeaseExpiresAtUtc > now)
                .Select(x => x.AccountId).Distinct().CountAsync();
            Assert.Equal(2, distinct);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Failed_login_attempt_counter_is_committed_and_read_back_from_postgresql()
    {
        var connectionString = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", builder.Database);

        await using (var guard = new NpgsqlConnection(connectionString))
        {
            await guard.OpenAsync();
            await using var marker = new NpgsqlCommand(
                "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = current_database();",
                guard);
            var comment = (string?)await marker.ExecuteScalarAsync();
            Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB; IdentityAccess integration test only", comment);
        }

        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentityAccess:PrivacyHmacKey"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(x => (byte)x).ToArray()),
                ["ConnectionStrings:MDGsmDatabase"] = connectionString
            })
            .Build();
        services.AddMdGsmPersistence(configuration);
        services.AddIdentityAccessSecurity(configuration);
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<LoginHandler>();
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var digest = provider.GetRequiredService<IPrivacyDigest>();
        var passwordHash = hasher.Hash("Correct-Password-2026!");
        await using (var setup = provider.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<MdGsmDbContext>();
            await db.Database.EnsureCreatedAsync();
            if (!await db.IdentitySystemSettings.AnyAsync(x => x.SettingsId == 1))
                db.IdentitySystemSettings.Add(new IdentitySystemSettingsRow
                {
                    SettingsId = 1,
                    MaxActiveUsers = 5,
                    HeartbeatIntervalSeconds = 30,
                    SessionLeaseSeconds = 180,
                    UpdatedAtUtc = clock.GetUtcNow()
                });
            if (!await db.IdentityAccounts.AnyAsync(x => x.LoginNameNormalized == "PERSISTCOUNTER"))
                db.IdentityAccounts.Add(CreateAccount("persistcounter", passwordHash, clock.GetUtcNow()));
            await db.SaveChangesAsync();
        }

        var sourceDigest = Digest(digest, "persist-counter-source");
        for (var attempt = 1; attempt <= Account.FailedAttemptLimit; attempt++)
        {
            using var scope = provider.CreateScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<LoginHandler>().HandleAsync(
                new LoginCommand("persistcounter", "wrong-password", Guid.CreateVersion7(),
                    sourceDigest, $"persist-counter-{attempt}"), CancellationToken.None);
            Assert.False(outcome.Succeeded);
            Assert.Equal(LoginFailure.InvalidCredentials, outcome.Failure);
            await AssertFailedAttemptCountPersistedAsync(provider, "PERSISTCOUNTER", attempt, clock);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Session_identity_resolution_rejects_expired_revoked_disabled_stale_credentials_and_mandatory_password_change()
    {
        var connectionString = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Session resolver integration test requires a dedicated disposable PostgreSQL database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(builder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", builder.Database);

        await using (var guard = new NpgsqlConnection(connectionString))
        {
            await guard.OpenAsync();
            await using var marker = new NpgsqlCommand(
                "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = current_database();",
                guard);
            var comment = (string?)await marker.ExecuteScalarAsync();
            Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB; IdentityAccess integration test only", comment);
        }

        var options = new DbContextOptionsBuilder<MdGsmDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new MdGsmDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var accountId = Guid.CreateVersion7();
        var account = CreateAccount($"sessionresolver{Guid.NewGuid():N}"[..30], "integration-only-placeholder", now);
        account.AccountId = accountId;
        var digest = SHA256.HashData(Guid.NewGuid().ToByteArray());
        var session = new IdentitySessionRow
        {
            SessionId = Guid.CreateVersion7(),
            AccountId = accountId,
            TokenDigest = digest,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            LeaseExpiresAtUtc = now.AddMinutes(3),
            CredentialVersionAtIssue = 1,
            ClientInstanceId = Guid.CreateVersion7()
        };
        db.IdentityAccounts.Add(account);
        db.IdentitySessions.Add(session);
        await db.SaveChangesAsync();

        var resolver = new PostgresSessionIdentityResolver(db);
        var valid = await resolver.ResolveAsync(digest, now, CancellationToken.None);
        Assert.NotNull(valid);
        Assert.Equal(AccountId.From(accountId), valid!.AccountId);
        Assert.Equal(1L, valid.CredentialVersion);

        session.LeaseExpiresAtUtc = now.AddSeconds(-1);
        await db.SaveChangesAsync();
        Assert.Null(await resolver.ResolveAsync(digest, now, CancellationToken.None));

        session.LeaseExpiresAtUtc = now.AddMinutes(3);
        session.RevokedAtUtc = now;
        await db.SaveChangesAsync();
        Assert.Null(await resolver.ResolveAsync(digest, now, CancellationToken.None));

        session.RevokedAtUtc = null;
        account.IsEnabled = false;
        await db.SaveChangesAsync();
        Assert.Null(await resolver.ResolveAsync(digest, now, CancellationToken.None));

        account.IsEnabled = true;
        account.CredentialVersion = 2;
        await db.SaveChangesAsync();
        Assert.Null(await resolver.ResolveAsync(digest, now, CancellationToken.None));

        session.CredentialVersionAtIssue = 2;
        account.MustChangePassword = true;
        await db.SaveChangesAsync();
        Assert.Null(await resolver.ResolveAsync(digest, now, CancellationToken.None));
    }

    private static IdentityAccountRow CreateAccount(string username, string passwordHash, DateTimeOffset now)
    {
        var parsed = Username.Parse(username);
        return new IdentityAccountRow
        {
        AccountId = Guid.CreateVersion7(),
        LoginName = parsed.DisplayValue,
        LoginNameNormalized = parsed.NormalizedValue,
        PasswordHash = passwordHash,
        PasswordHashVersion = 1,
        MustChangePassword = false,
        IsEnabled = true,
        IsBootstrapAccount = false,
        CredentialVersion = 1,
        FailedAttemptCount = 0,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
        };
    }

    private static async Task AssertFailedAttemptCountPersistedAsync(
        ServiceProvider provider, string normalizedUsername, int expectedCount, TimeProvider clock)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MdGsmDbContext>();
        var row = await db.IdentityAccounts.SingleAsync(x => x.LoginNameNormalized == normalizedUsername);
        Assert.Equal(expectedCount, row.FailedAttemptCount);

        if (expectedCount == Account.FailedAttemptLimit)
            Assert.True(row.LockedUntil > clock.GetUtcNow());
        else
            Assert.Null(row.LockedUntil);
    }

    private static async Task<AccountId> ReadAccountId(ServiceProvider provider, string normalizedUsername)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MdGsmDbContext>();
        var id = await db.IdentityAccounts.Where(x => x.LoginNameNormalized == normalizedUsername)
            .Select(x => x.AccountId).SingleAsync();
        return AccountId.From(id);
    }

    private static string Digest(IPrivacyDigest digest, string value) =>
        Convert.ToHexString(digest.ComputeSha256(value));

    private sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _now = initial;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }
}
