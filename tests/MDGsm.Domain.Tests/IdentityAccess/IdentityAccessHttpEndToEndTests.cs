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

            var recoveryStore = new PostgresIdentityAccessStores(db);
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

        // Provision the canonical N.6 runtime_app LOGIN only inside this marked,
        // disposable PostgreSQL instance. Authentication is local trust-only; no
        // reusable password or credential enters source control, CI variables or logs.
        var runtimeConnectionString = await PrepareRuntimePrincipalAsync(connectionString);

        using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(web =>
            {
                web.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:MDGsmDatabase"] = runtimeConnectionString,
                        ["IdentityAccess:PrivacyHmacKey"] = privacyKey,
                        ["IdentityAccess:HttpLoginEnabled"] = "true"
                    }));
                // Override both EF and the raw-Npgsql adapter data source so the real
                // host is guaranteed to use this test's newly-created isolated database.
                web.ConfigureTestServices(services =>
                {
                    services.RemoveAll<DbContextOptions<MdGsmDbContext>>();
                    services.RemoveAll<MdGsmDbContext>();
                    services.AddDbContext<MdGsmDbContext>(options => options.UseNpgsql(runtimeConnectionString));
                    services.RemoveAll<NpgsqlDataSource>();
                    services.AddSingleton(_ => NpgsqlDataSource.Create(runtimeConnectionString));
                    // Deliberately skew the host clock by two days. IdentityAccess
                    // login/session validation must still use PostgreSQL clock_timestamp().
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(DateTimeOffset.UtcNow.AddDays(-2)));
                });
            });
        using var client = factory.CreateClient();

        var partyUrl = "/api/v2/persons/" + Guid.CreateVersion7().ToString("D");
        var noToken = await client.GetAsync(partyUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

        var heartbeatWithoutToken = await client.PostAsync("/api/v1/identity/heartbeat", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, heartbeatWithoutToken.StatusCode);

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
        var heartbeat = await client.PostAsync("/api/v1/identity/heartbeat", content: null);
        Assert.Equal(HttpStatusCode.NoContent, heartbeat.StatusCode);

        // This proof is intentionally scoped to IdentityAccess. Persons/Finance grants
        // are not inferred from the current IdentityAccess-only runtime grant script.

        client.DefaultRequestHeaders.Authorization = null;
        var wrongPassword = await client.PostAsJsonAsync("/api/v1/identity/login",
            new LoginRequest(username, "incorrect-password", Guid.CreateVersion7()));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var secondLogin = await client.PostAsJsonAsync("/api/v1/identity/login",
            new LoginRequest(username, changedPassword, Guid.CreateVersion7()));
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
        var secondSession = await secondLogin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(secondSession);
        Assert.False(secondSession!.MustChangePassword);

        // Logout must revoke only the presented session, not every session for the account.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", active.AccessToken);
        var logout = await client.PostAsync("/api/v1/identity/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var revokedAfterLogout = await client.GetAsync(partyUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedAfterLogout.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondSession.AccessToken);
        var secondSessionStillValid = await client.PostAsync("/api/v1/identity/heartbeat", content: null);
        Assert.Equal(HttpStatusCode.NoContent, secondSessionStillValid.StatusCode);

        var secondLogout = await client.PostAsync("/api/v1/identity/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, secondLogout.StatusCode);
        var secondSessionRevoked = await client.PostAsync("/api/v1/identity/heartbeat", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, secondSessionRevoked.StatusCode);

        var repeatedLogout = await client.PostAsync("/api/v1/identity/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, repeatedLogout.StatusCode);
    }
    private static async Task<string> PrepareRuntimePrincipalAsync(string adminConnectionString)
    {
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        // Runtime and support roles are disposable test principals. In production,
        // LOGIN creation, secret delivery and membership remain DBA/deployment work.
        await using (var createRoles = new NpgsqlCommand("""
            DO $
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mdgsm_runtime') THEN
                    CREATE ROLE mdgsm_runtime NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'runtime_app') THEN
                    CREATE ROLE runtime_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE INHERIT;
                END IF;
            END
            $;
            GRANT mdgsm_runtime TO runtime_app;
            """, admin))
        {
            await createRoles.ExecuteNonQueryAsync();
        }

        var root = FindRepositoryRoot();
        var grantScript = await File.ReadAllTextAsync(Path.Combine(root,
            "database", "operations", "identityaccess-runtime-role-grants.sql"));
        await using (var applyGrants = new NpgsqlCommand(grantScript, admin))
        {
            await applyGrants.ExecuteNonQueryAsync();
        }

        var runtimeBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "runtime_app",
            Password = null,
            IncludeErrorDetail = false
        };
        var runtimeConnectionString = runtimeBuilder.ConnectionString;

        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync();
        await using (var identity = new NpgsqlCommand("""
            SELECT current_user, rolcanlogin, rolsuper, rolcreatedb, rolcreaterole,
                   rolreplication, rolbypassrls, rolinherit
            FROM pg_roles WHERE rolname = current_user;
            """, runtime))
        await using (var reader = await identity.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("runtime_app", reader.GetString(0));
            Assert.True(reader.GetBoolean(1));
            Assert.False(reader.GetBoolean(2));
            Assert.False(reader.GetBoolean(3));
            Assert.False(reader.GetBoolean(4));
            Assert.False(reader.GetBoolean(5));
            Assert.False(reader.GetBoolean(6));
            Assert.True(reader.GetBoolean(7));
        }

        // Positive permission checks and deliberately denied escalation/direct writes.
        await using (var allowedRead = new NpgsqlCommand(
            "SELECT count(*) FROM identity.accounts; SELECT count(*) FROM identity.sessions;", runtime))
        {
            await allowedRead.ExecuteNonQueryAsync();
        }
        await AssertDeniedAsync(runtime, "CREATE TABLE identity.runtime_privilege_probe(id integer);");
        await AssertDeniedAsync(runtime, "ALTER TABLE identity.accounts ADD COLUMN runtime_probe text;");
        await AssertDeniedAsync(runtime, "DROP TABLE identity.accounts;");
        await AssertDeniedAsync(runtime, "TRUNCATE TABLE identity.sessions;");
        await AssertDeniedAsync(runtime, "INSERT INTO identity.accounts DEFAULT VALUES;");
        await AssertDeniedAsync(runtime, "DELETE FROM identity.sessions;");
        await AssertDeniedAsync(runtime, "UPDATE identity.accounts SET email = email;");
        await AssertDeniedAsync(runtime, "UPDATE identity.security_audit_events SET event_type = event_type;");
        await AssertDeniedAsync(runtime, "CREATE ROLE runtime_privilege_probe LOGIN;");
        await AssertDeniedAsync(runtime, "ALTER ROLE runtime_app SUPERUSER;");
        await AssertDeniedAsync(runtime, "GRANT SELECT ON identity.accounts TO PUBLIC;");
        await AssertDeniedAsync(runtime, "SET ROLE postgres;");
        await AssertDeniedAsync(runtime, "SELECT * FROM persons LIMIT 0;");

        return runtimeConnectionString;
    }

    private static async Task AssertDeniedAsync(NpgsqlConnection connection, string sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("42501", exception.SqlState);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root from the IdentityAccess integration-test output directory.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state,
            TimeSpan dueTime, TimeSpan period) =>
            TimeProvider.System.CreateTimer(callback, state, dueTime, period);
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
