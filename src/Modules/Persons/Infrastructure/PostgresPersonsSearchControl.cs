using System.Data;
using MDGsm.Persons.Application;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MDGsm.Persons.Infrastructure;

public sealed class PostgresPersonsSearchControl(
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider,
    ILogger<PostgresPersonsSearchControl> logger) : IPersonsSearchControl
{
    private const string SearchPurpose = "persons.search";
    private const int PerMinuteLimit = 10;
    private const int PerDayLimit = 100;
    private const long GlobalWarningThreshold = 1000;
    private const int AbuseThreshold = 5;
    private static readonly TimeSpan AbuseWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public async Task<PersonsSearchControlDecision> TryConsumeAsync(string actorId, string purpose, CancellationToken cancellationToken)
    {
        var actor = ValidateActor(actorId, nameof(actorId));
        if (!string.Equals(purpose, SearchPurpose, StringComparison.Ordinal))
            throw new ArgumentException("Only a server-defined Persons search purpose is accepted.", nameof(purpose));
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var minuteStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockActorAsync(connection, transaction, actor, cancellationToken);

        var lockRow = await ReadLockAsync(connection, transaction, actor, cancellationToken);
        if (lockRow is not null)
        {
            if (lockRow.ExpiresAtUtc > now)
            {
                await transaction.CommitAsync(cancellationToken);
                return PersonsSearchControlDecision.Locked((int)Math.Ceiling((lockRow.ExpiresAtUtc - now).TotalSeconds));
            }
            await using var delete = new NpgsqlCommand(
                "DELETE FROM persons_search_actor_locks WHERE actor_id = @actor AND lock_id = @lock;", connection, transaction);
            delete.Parameters.AddWithValue("actor", actor);
            delete.Parameters.AddWithValue("lock", lockRow.LockId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
            await InsertLockAuditAsync(connection, transaction, lockRow.LockId, actor, "expired", now,
                null, "automatic-expiry", "search-expiry", cancellationToken);
        }

        await CleanupCountersAsync(connection, transaction, actor, minuteStart, dayStart, cancellationToken);
        var minuteCount = await IncrementActorCounterAsync(connection, transaction, actor, purpose, "minute", minuteStart, now, cancellationToken);
        var dayCount = await IncrementActorCounterAsync(connection, transaction, actor, purpose, "day", dayStart, now, cancellationToken);
        var globalCount = await IncrementGlobalCounterAsync(connection, transaction, dayStart, now, cancellationToken);

        var shouldWarn = false;
        if (globalCount >= GlobalWarningThreshold)
        {
            await using var warning = new NpgsqlCommand("""
                UPDATE persons_search_global_daily_counters
                SET warning_emitted = true
                WHERE utc_day = @day AND request_count >= @threshold AND warning_emitted = false
                RETURNING request_count;
                """, connection, transaction);
            warning.Parameters.AddWithValue("day", DateOnly.FromDateTime(dayStart.UtcDateTime));
            warning.Parameters.AddWithValue("threshold", GlobalWarningThreshold);
            shouldWarn = await warning.ExecuteScalarAsync(cancellationToken) is not null;
        }
        await transaction.CommitAsync(cancellationToken);

        if (shouldWarn)
            logger.LogWarning("Persons search global daily warning threshold reached. UtcDay={UtcDay}; Threshold={Threshold}",
                DateOnly.FromDateTime(dayStart.UtcDateTime), GlobalWarningThreshold);

        if (minuteCount > PerMinuteLimit)
            return PersonsSearchControlDecision.RateLimited(60 - now.Second);
        if (dayCount > PerDayLimit)
            return PersonsSearchControlDecision.RateLimited((int)Math.Ceiling((dayStart.AddDays(1) - now).TotalSeconds));
        return PersonsSearchControlDecision.Permit();
    }

    public async Task RecordSecurityViolationAsync(string actorId, PersonsSearchSecurityViolation violation,
        string requestId, CancellationToken cancellationToken)
    {
        var actor = ValidateActor(actorId, nameof(actorId));
        var request = ValidateRequestId(requestId);
        var code = violation switch
        {
            PersonsSearchSecurityViolation.PermissionDenied => "permission_denied",
            PersonsSearchSecurityViolation.ActorIdentityTampering => "actor_identity_tampering",
            PersonsSearchSecurityViolation.PurposeTampering => "purpose_tampering",
            PersonsSearchSecurityViolation.AuthorizationBypass => "authorization_bypass",
            _ => throw new ArgumentOutOfRangeException(nameof(violation))
        };
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var violationId = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockActorAsync(connection, transaction, actor, cancellationToken);

        await using (var audit = new NpgsqlCommand("""
            INSERT INTO persons_search_security_violation_audit
                (violation_id, actor_id, violation_code, request_id, occurred_at_utc)
            VALUES (@id, @actor, @code, @request, @occurred);
            """, connection, transaction))
        {
            audit.Parameters.AddWithValue("id", violationId);
            audit.Parameters.AddWithValue("actor", actor);
            audit.Parameters.AddWithValue("code", code);
            audit.Parameters.AddWithValue("request", request);
            audit.Parameters.AddWithValue("occurred", now);
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var insertWindow = new NpgsqlCommand("""
            INSERT INTO persons_search_abuse_window (actor_id, violation_id, occurred_at_utc)
            VALUES (@actor, @id, @occurred);
            """, connection, transaction))
        {
            insertWindow.Parameters.AddWithValue("actor", actor);
            insertWindow.Parameters.AddWithValue("id", violationId);
            insertWindow.Parameters.AddWithValue("occurred", now);
            await insertWindow.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var cleanup = new NpgsqlCommand("""
            DELETE FROM persons_search_abuse_window WHERE actor_id = @actor AND occurred_at_utc < @cutoff;
            """, connection, transaction))
        {
            cleanup.Parameters.AddWithValue("actor", actor);
            cleanup.Parameters.AddWithValue("cutoff", now - AbuseWindow);
            await cleanup.ExecuteNonQueryAsync(cancellationToken);
        }

        long count;
        await using (var countCommand = new NpgsqlCommand("""
            SELECT count(*) FROM persons_search_abuse_window
            WHERE actor_id = @actor AND occurred_at_utc >= @cutoff;
            """, connection, transaction))
        {
            countCommand.Parameters.AddWithValue("actor", actor);
            countCommand.Parameters.AddWithValue("cutoff", now - AbuseWindow);
            count = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        var current = await ReadLockAsync(connection, transaction, actor, cancellationToken);
        if (count >= AbuseThreshold && (current is null || current.ExpiresAtUtc <= now))
        {
            if (current is not null)
            {
                await using var deleteExpired = new NpgsqlCommand(
                    "DELETE FROM persons_search_actor_locks WHERE actor_id = @actor AND lock_id = @lock;", connection, transaction);
                deleteExpired.Parameters.AddWithValue("actor", actor);
                deleteExpired.Parameters.AddWithValue("lock", current.LockId);
                await deleteExpired.ExecuteNonQueryAsync(cancellationToken);
                await InsertLockAuditAsync(connection, transaction, current.LockId, actor, "expired", now,
                    null, "automatic-expiry", request, cancellationToken);
            }
            var lockId = Guid.CreateVersion7();
            await using (var insertLock = new NpgsqlCommand("""
                INSERT INTO persons_search_actor_locks
                    (actor_id, lock_id, locked_at_utc, expires_at_utc, qualifying_violation_count)
                VALUES (@actor, @lock, @locked, @expires, @count);
                """, connection, transaction))
            {
                insertLock.Parameters.AddWithValue("actor", actor);
                insertLock.Parameters.AddWithValue("lock", lockId);
                insertLock.Parameters.AddWithValue("locked", now);
                insertLock.Parameters.AddWithValue("expires", now + LockDuration);
                insertLock.Parameters.AddWithValue("count", count);
                await insertLock.ExecuteNonQueryAsync(cancellationToken);
            }
            await InsertLockAuditAsync(connection, transaction, lockId, actor, "activated", now,
                null, $"security-violations:{count}", request, cancellationToken);
            logger.LogWarning("Persons search Abuse Lock activated. RequestId={RequestId}; QualifyingViolations={Count}", request, count);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AdminUnlockAsync(string actorId, string adminActorId, string reason, string requestId,
        CancellationToken cancellationToken)
    {
        var actor = ValidateActor(actorId, nameof(actorId));
        var admin = ValidateActor(adminActorId, nameof(adminActorId));
        var normalizedReason = ValidateReason(reason);
        var request = ValidateRequestId(requestId);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockActorAsync(connection, transaction, actor, cancellationToken);
        var current = await ReadLockAsync(connection, transaction, actor, cancellationToken);
        if (current is null)
        {
            await InsertLockAuditAsync(connection, transaction, Guid.CreateVersion7(), actor, "admin_unlock_noop",
                now, admin, normalizedReason, request, cancellationToken);
        }
        else
        {
            await using var delete = new NpgsqlCommand(
                "DELETE FROM persons_search_actor_locks WHERE actor_id = @actor AND lock_id = @lock;", connection, transaction);
            delete.Parameters.AddWithValue("actor", actor);
            delete.Parameters.AddWithValue("lock", current.LockId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
            await InsertLockAuditAsync(connection, transaction, current.LockId, actor, "admin_unlocked",
                now, admin, normalizedReason, request, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CleanupCountersAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string actor, DateTimeOffset minuteStart, DateTimeOffset dayStart, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            DELETE FROM persons_search_actor_purpose_counters
            WHERE actor_id = @actor AND
              ((window_kind = 'minute' AND window_start_utc < @minute_cutoff)
               OR (window_kind = 'day' AND window_start_utc < @day_cutoff));
            """, connection, transaction);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("minute_cutoff", minuteStart.AddDays(-1));
        command.Parameters.AddWithValue("day_cutoff", dayStart.AddDays(-90));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> IncrementActorCounterAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string actor, string purpose, string windowKind, DateTimeOffset windowStart, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO persons_search_actor_purpose_counters
                (actor_id, purpose, window_kind, window_start_utc, request_count, updated_at_utc)
            VALUES (@actor, @purpose, @kind, @start, 1, @updated)
            ON CONFLICT (actor_id, purpose, window_kind, window_start_utc)
            DO UPDATE SET request_count = persons_search_actor_purpose_counters.request_count + 1,
                           updated_at_utc = EXCLUDED.updated_at_utc
            RETURNING request_count;
            """, connection, transaction);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("purpose", purpose);
        command.Parameters.AddWithValue("kind", windowKind);
        command.Parameters.AddWithValue("start", windowStart);
        command.Parameters.AddWithValue("updated", now);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task<long> IncrementGlobalCounterAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        DateTimeOffset dayStart, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO persons_search_global_daily_counters
                (utc_day, request_count, warning_emitted, updated_at_utc)
            VALUES (@day, 1, false, @updated)
            ON CONFLICT (utc_day)
            DO UPDATE SET request_count = persons_search_global_daily_counters.request_count + 1,
                           updated_at_utc = EXCLUDED.updated_at_utc
            RETURNING request_count;
            """, connection, transaction);
        command.Parameters.AddWithValue("day", DateOnly.FromDateTime(dayStart.UtcDateTime));
        command.Parameters.AddWithValue("updated", now);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task<ActorLock?> ReadLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string actor, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT lock_id, expires_at_utc FROM persons_search_actor_locks WHERE actor_id = @actor FOR UPDATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("actor", actor);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ActorLock(reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1))
            : null;
    }

    private static async Task LockActorAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string actor, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@actor, 0));", connection, transaction);
        command.Parameters.AddWithValue("actor", actor);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task InsertLockAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid lockId, string actor, string eventType, DateTimeOffset occurred, string? adminActor,
        string reason, string request, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO persons_search_lock_audit
                (audit_id, lock_id, actor_id, event_type, occurred_at_utc, admin_actor_id, reason, request_id)
            VALUES (@id, @lock, @actor, @event, @occurred, @admin, @reason, @request);
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("lock", lockId);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("event", eventType);
        command.Parameters.AddWithValue("occurred", occurred);
        command.Parameters.AddWithValue("admin", (object?)adminActor ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("request", request);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ValidateActor(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) || value.Any(char.IsControl))
            throw new ArgumentException("A trusted actor identifier of 1-128 non-control characters is required.", parameter);
        return value;
    }

    private static string ValidateRequestId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl))
            throw new ArgumentException("A non-empty request identifier is required.", nameof(value));
        return value;
    }

    private static string ValidateReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("An administrator unlock reason is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 300 || normalized.Any(char.IsControl))
            throw new ArgumentException("Unlock reason must be 1-300 non-control characters.", nameof(value));
        return normalized;
    }

    private sealed record ActorLock(Guid LockId, DateTimeOffset ExpiresAtUtc);
}
