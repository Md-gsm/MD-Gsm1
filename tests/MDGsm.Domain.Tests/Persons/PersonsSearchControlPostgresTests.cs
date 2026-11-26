using MDGsm.Persons.Application;
using MDGsm.Persons.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsSearchControlPostgresTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Actor_quota_is_atomic_and_excess_does_not_create_an_abuse_lock()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var control = new PostgresPersonsSearchControl(dataSource, TimeProvider.System,
            NullLogger<PostgresPersonsSearchControl>.Instance);
        var actor = Guid.CreateVersion7().ToString("D");

        for (var i = 0; i < 10; i++)
            Assert.True((await control.TryConsumeAsync(actor, "persons.search", default)).Allowed);

        var limited = await control.TryConsumeAsync(actor, "persons.search", default);
        Assert.False(limited.Allowed);
        Assert.Equal(PersonsSearchControlRejection.RateLimited, limited.Rejection);

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM persons_search_actor_locks WHERE actor_id = @actor),
                   (SELECT count(*) FROM persons_search_security_violation_audit WHERE actor_id = @actor);
            """, connection);
        command.Parameters.AddWithValue("actor", actor);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0L, reader.GetInt64(0));
        Assert.Equal(0L, reader.GetInt64(1));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Five_qualifying_violations_lock_only_the_actor_and_admin_unlock_is_audited()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var control = new PostgresPersonsSearchControl(dataSource, TimeProvider.System,
            NullLogger<PostgresPersonsSearchControl>.Instance);
        var actor = Guid.CreateVersion7().ToString("D");
        var admin = Guid.CreateVersion7().ToString("D");

        for (var i = 0; i < 5; i++)
            await control.RecordSecurityViolationAsync(actor, PersonsSearchSecurityViolation.PermissionDenied,
                Guid.CreateVersion7().ToString("D"), default);

        var locked = await control.TryConsumeAsync(actor, "persons.search", default);
        Assert.False(locked.Allowed);
        Assert.Equal(PersonsSearchControlRejection.ActorLocked, locked.Rejection);
        Assert.InRange(locked.RetryAfterSeconds, 1, 900);

        await control.AdminUnlockAsync(actor, admin, "Approved integration-test unlock", Guid.CreateVersion7().ToString("D"), default);
        await using (var stateConnection = await dataSource.OpenConnectionAsync())
        await using (var stateCommand = new NpgsqlCommand("""
            SELECT
              (SELECT count(*) FROM persons_search_abuse_window WHERE actor_id=@actor),
              (SELECT count(*) FROM persons_search_security_violation_audit WHERE actor_id=@actor);
            """, stateConnection))
        {
            stateCommand.Parameters.AddWithValue("actor", actor);
            await using var stateReader = await stateCommand.ExecuteReaderAsync();
            Assert.True(await stateReader.ReadAsync());
            Assert.Equal(0L, stateReader.GetInt64(0));
            Assert.Equal(5L, stateReader.GetInt64(1)); // Durable security evidence remains after the active window is reset.
        }
        Assert.True((await control.TryConsumeAsync(actor, "persons.search", default)).Allowed);
        for (var i = 0; i < 4; i++)
            await control.RecordSecurityViolationAsync(actor, PersonsSearchSecurityViolation.PermissionDenied,
                Guid.CreateVersion7().ToString("D"), default);
        Assert.True((await control.TryConsumeAsync(actor, "persons.search", default)).Allowed);
        await control.RecordSecurityViolationAsync(actor, PersonsSearchSecurityViolation.PermissionDenied,
            Guid.CreateVersion7().ToString("D"), default);
        var relocked = await control.TryConsumeAsync(actor, "persons.search", default);
        Assert.False(relocked.Allowed);
        Assert.Equal(PersonsSearchControlRejection.ActorLocked, relocked.Rejection);

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM persons_search_lock_audit
            WHERE actor_id = @actor AND event_type = 'admin_unlocked' AND admin_actor_id = @admin
              AND reason = 'Approved integration-test unlock';
            """, connection);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("admin", admin);
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Daily_quota_is_enforced_independently_of_the_minute_counter()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var actor = Guid.CreateVersion7().ToString("D");
        var now = DateTimeOffset.UtcNow;
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO persons_search_actor_purpose_counters
                (actor_id, purpose, window_kind, window_start_utc, request_count, updated_at_utc)
            VALUES (@actor, 'persons.search', 'day', @day, 100, @now);
            """, connection))
        {
            seed.Parameters.AddWithValue("actor", actor);
            seed.Parameters.AddWithValue("day", dayStart);
            seed.Parameters.AddWithValue("now", now);
            await seed.ExecuteNonQueryAsync();
        }

        var control = new PostgresPersonsSearchControl(dataSource, TimeProvider.System,
            NullLogger<PostgresPersonsSearchControl>.Instance);
        var decision = await control.TryConsumeAsync(actor, "persons.search", default);
        Assert.False(decision.Allowed);
        Assert.Equal(PersonsSearchControlRejection.RateLimited, decision.Rejection);
    }

    private static async Task<NpgsqlDataSource> OpenDisposableDataSourceAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Search control integration tests require the disposable PostgreSQL database supplied by CI.");
        var dataSource = NpgsqlDataSource.Create(connectionString!);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var marker = new NpgsqlCommand(
            "SELECT coalesce(shobj_description(oid, 'pg_database'), '') FROM pg_database WHERE datname = current_database();",
            connection);
        var comment = (string?)await marker.ExecuteScalarAsync();
        Assert.Contains("MDGSM_DISPOSABLE_TEST_DB", comment ?? string.Empty);
        return dataSource;
    }
}
