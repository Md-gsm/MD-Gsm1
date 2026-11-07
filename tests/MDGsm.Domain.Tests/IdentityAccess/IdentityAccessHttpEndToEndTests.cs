using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Infrastructure.IdentityAccess;
using MDGsm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

[CollectionDefinition("IdentityAccessPostgres", DisableParallelization = true)]
public sealed class IdentityAccessPostgresCollection { }

/// <summary>
/// Exercises the real API host, HTTP login/token issuance, the session middleware,
/// mandatory password change, and a protected Persons request against disposable PostgreSQL.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class IdentityAccessHttpEndToEndTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Http_login_issues_opaque_session_and_forced_password_change_unlocks_only_after_relogin()
    {
        var connectionString = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "HTTP IdentityAccess test requires a dedicated disposable PostgreSQL database.");
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(parsed.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", parsed.Database);

        await using (var guard = new NpgsqlConnection(connectionString))
        {
            await guard.OpenAsync();
            await using var marker = new NpgsqlCommand(
                "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = current_database();",
                guard);
            var comment = (string?)await marker.ExecuteScalarAsync();
            Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB; IdentityAccess integration test only", comment);
        }

        await using var disposableDatabase = await DisposableDatabase.CreateAsync(connectionString);
        connectionString = disposableDatabase.ConnectionString;

        var privacyKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(x => (byte)x).ToArray());
        var options = new DbContextOptionsBuilder<MdGsmDbContext>().UseNpgsql(connectionString).Options;
        var username = "httpflow" + Guid.NewGuid().ToString("N")[..12];
        const string initialPassword = "Initial-Password-2026!";
        const string changedPassword = "Changed-Password-2026!";
        var accountId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        // Exercise the same ordered SQL migration path as staging before using EF.
        // EnsureCreated must not run here: it would pre-create IdentityAccess tables
        // and mask collisions or drift in migration 0003.
        await new DatabaseMigrationRunner().ApplyPendingAsync(
            connectionString, requireDisposableMarker: true);

        await using (var db = new MdGsmDbContext(options))
        {
            Assert.True(await db.IdentitySystemSettings.AnyAsync(x => x.SettingsId == 1),
                "Migration 0003 must seed the required singleton session-policy row.");

            var recoveryStore = new PostgresIdentityAccessStores(db, TimeProvider.System);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                recoveryStore.CreateChallengeAsync(
                    AccountId.From(Guid.CreateVersion7()), new byte[32], "mobile",
                    DateTimeOffset.UtcNow.AddMinutes(10), CancellationToken.None));

            var hasher = new PlatformPasswordHasher();
            var parsedUsername = Username.Parse(username);
            db.IdentityAccounts.Add(new IdentityAccountRow
            {
                AccountId = accountId,
                LoginName = parsedUsername.DisplayValue,
                LoginNameNormalized = parsedUsername.NormalizedValue,
                PasswordHash = hasher.Hash(initialPassword),
                PasswordHashVersion = hasher.CurrentVersion,
                MustChangePassword = true,
                IsEnabled = true,
                IsBootstrapAccount = false,
                CredentialVersion = 1,
                FailedAttemptCount = 0,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            db.IdentityRoles.Add(new IdentityRoleRow
            {
                RoleId = roleId,
                RoleKey = "http_flow_reader_" + Guid.NewGuid().ToString("N")[..8],
                DisplayName = "HTTP flow reader",
                Description = "Disposable HTTP integration test role",
                IsSystemRole = false,
                IsAssignable = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            var readPermission = await db.IdentityPermissions
                .SingleOrDefaultAsync(x => x.PermissionKey == "persons.read");
            if (readPermission is null)
            {
                readPermission = new IdentityPermissionRow
                {
                    PermissionId = Guid.CreateVersion7(),
                    PermissionKey = "persons.read",
                    DisplayName = "Read Persons",
                    Description = "Read Persons records",
                    RiskLevel = "standard",
                    IsActive = true
                };
                db.IdentityPermissions.Add(readPermission);
            }
            else
            {
                readPermission.IsActive = true;
            }

            db.IdentityAccountRoles.Add(new IdentityAccountRoleRow
            {
                AccountId = accountId,
                RoleId = roleId,
                AssignedAtUtc = now,
                AssignmentReason = "http-e2e-test"
            });
            db.IdentityRolePermissions.Add(new IdentityRolePermissionRow
            {
                RoleId = roleId,
                PermissionId = readPermission.PermissionId
            });
            await db.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(web =>
            {
                web.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:MDGsmDatabase"] = connectionString,
                        ["IdentityAccess:PrivacyHmacKey"] = privacyKey,
                        ["IdentityAccess:HttpLoginEnabled"] = "true"
                    }));
                // Override both EF and the raw-Npgsql adapter data source so the real
                // host is guaranteed to use this test's newly-created isolated database.
                web.ConfigureTestServices(services =>
                {
                    services.RemoveAll<DbContextOptions<MdGsmDbContext>>();
                    services.RemoveAll<MdGsmDbContext>();
                    services.AddDbContext<MdGsmDbContext>(options => options.UseNpgsql(connectionString));
                    services.RemoveAll<NpgsqlDataSource>();
                    services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
                });
            });
        using var client = factory.CreateClient();

        var partyUrl = "/api/v1/persons/" + Guid.CreateVersion7().ToString("D");
        var noToken = await client.GetAsync(partyUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/identity/login",
            new LoginRequest(username, initialPassword, Guid.CreateVersion7()));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var issued = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(issued);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", issued!.AccessToken);
        Assert.True(issued.MustChangePassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.AccessToken);
        var blockedUntilPasswordChange = await client.GetAsync(partyUrl);
        Assert.Equal(HttpStatusCode.Forbidden, blockedUntilPasswordChange.StatusCode);

        var changed = await client.PostAsJsonAsync("/api/v1/identity/password/change",
            new ChangePasswordRequest(initialPassword, changedPassword));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        var revokedOldSession = await client.GetAsync(partyUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedOldSession.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var relogin = await client.PostAsJsonAsync("/api/v1/identity/login",
            new LoginRequest(username, changedPassword, Guid.CreateVersion7()));
        Assert.Equal(HttpStatusCode.OK, relogin.StatusCode);
        var active = await relogin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(active);
        Assert.False(active!.MustChangePassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", active.AccessToken);
        var authorizedPersonsRead = await client.GetAsync(partyUrl);
        Assert.Equal(HttpStatusCode.NotFound, authorizedPersonsRead.StatusCode);

        // A broad persons.read grant must not bypass the fail-closed technician
        // row-scope guard, including when the same account has another role.
        await using (var db = new MdGsmDbContext(options))
        {
            var technicianRoleId = Guid.CreateVersion7();
            db.IdentityRoles.Add(new IdentityRoleRow
            {
                RoleId = technicianRoleId,
                RoleKey = RoleCatalog.Technician,
                DisplayName = "Technician",
                Description = "Technician role for HTTP authorization regression test",
                IsSystemRole = true,
                IsAssignable = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            db.IdentityAccountRoles.Add(new IdentityAccountRoleRow
            {
                AccountId = accountId,
                RoleId = technicianRoleId,
                AssignedAtUtc = now,
                AssignmentReason = "technician-scope-http-e2e-test"
            });
            await db.SaveChangesAsync();
        }

        var technicianWithAdditionalRole = await client.GetAsync(partyUrl);
        Assert.Equal(HttpStatusCode.Forbidden, technicianWithAdditionalRole.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var wrongPassword = await client.PostAsJsonAsync("/api/v1/identity/login",
            new LoginRequest(username, "incorrect-password", Guid.CreateVersion7()));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
    }
    private sealed class DisposableDatabase(string adminConnectionString, string databaseName, string connectionString)
        : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;

        public static async Task<DisposableDatabase> CreateAsync(string sourceConnectionString)
        {
            var source = new NpgsqlConnectionStringBuilder(sourceConnectionString);
            var databaseName = $"mdgsm_identityaccess_disposable_{Environment.ProcessId}_{DateTime.UtcNow.Ticks}";
            var admin = new NpgsqlConnectionStringBuilder(sourceConnectionString) { Database = "postgres" };
            await using (var connection = new NpgsqlConnection(admin.ConnectionString))
            {
                await connection.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
                await create.ExecuteNonQueryAsync();
                await using var comment = new NpgsqlCommand(
                    $"COMMENT ON DATABASE \"{databaseName}\" IS 'MDGSM_DISPOSABLE_TEST_DB; IdentityAccess HTTP end-to-end test'",
                    connection);
                await comment.ExecuteNonQueryAsync();
            }

            source.Database = databaseName;
            return new DisposableDatabase(admin.ConnectionString, databaseName, source.ConnectionString);
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

}
