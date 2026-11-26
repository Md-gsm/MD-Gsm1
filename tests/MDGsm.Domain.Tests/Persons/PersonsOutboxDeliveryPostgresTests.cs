using MDGsm.Persons.Application;
using MDGsm.Persons.Domain;
using MDGsm.Persons.Infrastructure;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsOutboxDeliveryPostgresTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Standard_party_creation_writes_one_PII_free_event_and_pending_delivery_atomically()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var repository = new PostgresPartyRepository(dataSource);
        var actor = Guid.CreateVersion7().ToString("D");
        var operation = Guid.CreateVersion7();
        var request = NewNatural(NewMobile());

        var first = await repository.AddNaturalAsync(request, actor, operation, null, default);
        var replay = await repository.AddNaturalAsync(request, actor, operation, null, default);

        Assert.True(first.Created);
        Assert.False(replay.Created);
        Assert.Equal(first.PartyId, replay.PartyId);
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key=@id AND event_type='PartyRegistered';",
            operation));
        Assert.Equal(1L, await ScalarAsync(dataSource, """
            SELECT count(*)
            FROM persons_outbox_events e
            JOIN persons_outbox_deliveries d USING (event_id)
            WHERE e.idempotency_key=@id
              AND d.delivery_status='pending'
              AND d.attempt_count=0;
            """, operation));
        Assert.Equal(0L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key=@id AND event_type='PartyMerged';",
            operation));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE aggregate_id=@party;",
            Guid.Parse(first.PartyId)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Outbox_insert_failure_rolls_back_standard_party_and_retry_uses_the_same_operation_id()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var mobile = NewMobile();
        var operation = Guid.CreateVersion7();
        var triggerFunction = "ci_fail_party_outbox_" + operation.ToString("N");
        var trigger = "trg_ci_fail_party_outbox_" + operation.ToString("N");
        await ExecuteAsync(dataSource, $"""
            CREATE FUNCTION public.{triggerFunction}() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.idempotency_key = '{operation:D}' THEN
                    RAISE EXCEPTION 'injected Persons Outbox failure';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER {trigger}
            BEFORE INSERT ON persons_outbox_events
            FOR EACH ROW EXECUTE FUNCTION public.{triggerFunction}();
            """);

        try
        {
            var repository = new PostgresPartyRepository(dataSource);
            await Assert.ThrowsAsync<PostgresException>(() =>
                repository.AddNaturalAsync(NewNatural(mobile), Actor(), operation, null, default));

            Assert.Equal(0L, await ScalarAsync(dataSource,
                "SELECT count(*) FROM persons_contact_points WHERE value=@mobile;", mobile));
            Assert.Equal(0L, await ScalarAsync(dataSource,
                "SELECT count(*) FROM persons_operations WHERE operation_id=@operation;", operation));
            Assert.Equal(0L, await ScalarAsync(dataSource,
                "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key=@operation;", operation));
            Assert.Equal(0L, await ScalarAsync(dataSource,
                "SELECT count(*) FROM persons_outbox_deliveries d JOIN persons_outbox_events e USING (event_id) WHERE e.idempotency_key=@operation;",
                operation));
        }
        finally
        {
            await ExecuteAsync(dataSource, $"""
                DROP TRIGGER IF EXISTS {trigger} ON persons_outbox_events;
                DROP FUNCTION IF EXISTS public.{triggerFunction}();
                """);
        }

        var retry = await new PostgresPartyRepository(dataSource)
            .AddNaturalAsync(NewNatural(mobile), Actor(), operation, null, default);
        Assert.True(retry.Created);
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_events WHERE idempotency_key=@operation;", operation));
        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_deliveries d JOIN persons_outbox_events e USING (event_id) WHERE e.idempotency_key=@operation AND d.delivery_status='pending';",
            operation));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delivery_lease_expiry_reclaims_same_event_and_fences_old_owner()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var operation = Guid.CreateVersion7();
        var party = await new PostgresPartyRepository(dataSource)
            .AddNaturalAsync(NewNatural(NewMobile()), Actor(), operation, null, default);
        var eventId = await EventIdForOperationAsync(dataSource, operation);
        var store = new PostgresPersonsOutboxDeliveryStore(dataSource);
        var startAt = DateTimeOffset.UtcNow;
        var workerA = "worker-a-" + Guid.CreateVersion7().ToString("N");
        var workerB = "worker-b-" + Guid.CreateVersion7().ToString("N");

        var firstLease = Assert.Single(await store.LeaseDueAsync(
            workerA, 1, startAt, startAt.AddMinutes(1), default, [eventId]));
        Assert.Equal(eventId, firstLease.Event.EventId);
        Assert.Equal(Guid.Parse(party.PartyId), firstLease.Event.AggregateId);
        Assert.Equal(1, firstLease.AttemptCount);

        var reclaimAt = startAt.AddMinutes(1).AddSeconds(1);
        var secondLease = Assert.Single(await store.LeaseDueAsync(
            workerB, 1, reclaimAt, reclaimAt.AddMinutes(1), default, [eventId]));
        Assert.Equal(firstLease.Event.EventId, secondLease.Event.EventId);
        Assert.Equal(2, secondLease.AttemptCount);

        Assert.False(await store.MarkDeliveredAsync(
            eventId, firstLease.LeaseVersion, workerA, reclaimAt.AddSeconds(1), default));
        Assert.False(await store.ScheduleRetryAsync(
            eventId, firstLease.LeaseVersion, workerA, reclaimAt.AddSeconds(1), reclaimAt.AddSeconds(10),
            PersonsOutboxDeliveryFailureCodes.PublishFailed, default));
        Assert.True(await store.MarkDeliveredAsync(
            eventId, secondLease.LeaseVersion, workerB, reclaimAt.AddSeconds(2), default));

        Assert.Equal(1L, await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_deliveries WHERE event_id=@id AND delivery_status='delivered' AND attempt_count=2;",
            eventId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Reused_worker_identity_cannot_acknowledge_or_retry_an_obsolete_lease_generation()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var operation = Guid.CreateVersion7();
        await new PostgresPartyRepository(dataSource)
            .AddNaturalAsync(NewNatural(NewMobile()), Actor(), operation, null, default);
        var eventId = await EventIdForOperationAsync(dataSource, operation);
        var store = new PostgresPersonsOutboxDeliveryStore(dataSource);
        var owner = "reused-worker-identity";
        var firstAt = DateTimeOffset.UtcNow;

        var firstLease = Assert.Single(await store.LeaseDueAsync(
            owner, 1, firstAt, firstAt.AddMinutes(1), default, [eventId]));
        var reclaimedAt = firstAt.AddMinutes(1).AddSeconds(1);
        var secondLease = Assert.Single(await store.LeaseDueAsync(
            owner, 1, reclaimedAt, reclaimedAt.AddMinutes(1), default, [eventId]));

        Assert.Equal(firstLease.Event.EventId, secondLease.Event.EventId);
        Assert.NotEqual(firstLease.LeaseVersion, secondLease.LeaseVersion);
        Assert.False(await store.MarkDeliveredAsync(
            eventId, firstLease.LeaseVersion, owner, reclaimedAt.AddSeconds(1), default));
        Assert.False(await store.ScheduleRetryAsync(
            eventId, firstLease.LeaseVersion, owner, reclaimedAt.AddSeconds(1),
            reclaimedAt.AddSeconds(10), PersonsOutboxDeliveryFailureCodes.PublishFailed, default));
        Assert.True(await store.MarkDeliveredAsync(
            eventId, secondLease.LeaseVersion, owner, reclaimedAt.AddSeconds(2), default));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Concurrent_workers_cannot_lease_the_same_event_twice()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var operation = Guid.CreateVersion7();
        await new PostgresPartyRepository(dataSource)
            .AddNaturalAsync(NewNatural(NewMobile()), Actor(), operation, null, default);
        var eventId = await EventIdForOperationAsync(dataSource, operation);
        var store = new PostgresPersonsOutboxDeliveryStore(dataSource);
        var now = DateTimeOffset.UtcNow;

        var attempts = await Task.WhenAll(
            store.LeaseDueAsync("parallel-a-" + Guid.CreateVersion7().ToString("N"), 1,
                now, now.AddMinutes(1), default, [eventId]),
            store.LeaseDueAsync("parallel-b-" + Guid.CreateVersion7().ToString("N"), 1,
                now, now.AddMinutes(1), default, [eventId]));

        Assert.Equal(1, attempts.Sum(batch => batch.Count));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Retry_keeps_event_identity_and_does_not_make_it_due_before_the_scheduled_time()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var operation = Guid.CreateVersion7();
        var party = await new PostgresPartyRepository(dataSource)
            .AddNaturalAsync(NewNatural(NewMobile()), Actor(), operation, null, default);
        var store = new PostgresPersonsOutboxDeliveryStore(dataSource);
        var start = DateTimeOffset.UtcNow;
        var eventId = await EventIdForOperationAsync(dataSource, operation);
        var leasedEvent = Assert.Single(await store.LeaseDueAsync(
            "worker-a", 1, start, start.AddMinutes(1), default, [eventId]));
        Assert.Equal(Guid.Parse(party.PartyId), leasedEvent.Event.AggregateId);

        var retryAt = start.AddSeconds(30);
        Assert.True(await store.ScheduleRetryAsync(
            leasedEvent.Event.EventId, leasedEvent.LeaseVersion, "worker-a", start.AddSeconds(1), retryAt,
            PersonsOutboxDeliveryFailureCodes.PublishFailed, default));

        Assert.Empty(await store.LeaseDueAsync(
            "worker-b", 1, start.AddSeconds(29), start.AddMinutes(1), default, [eventId]));
        var secondLease = Assert.Single(await store.LeaseDueAsync(
            "worker-b", 1, retryAt.AddSeconds(1), retryAt.AddMinutes(1), default, [eventId]));

        Assert.Equal(leasedEvent.Event.EventId, secondLease.Event.EventId);
        Assert.Equal(2, secondLease.AttemptCount);
        Assert.True(await store.MarkDeliveredAsync(
            secondLease.Event.EventId, secondLease.LeaseVersion, "worker-b", retryAt.AddSeconds(2), default));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Outbox_tables_have_no_explicit_PUBLIC_or_runtime_application_privileges()
    {
        await using var dataSource = await OpenDisposableDataSourceAsync();
        var count = await ScalarAsync(dataSource, """
            SELECT count(*)
            FROM pg_class c
            JOIN pg_namespace n ON n.oid=c.relnamespace
            CROSS JOIN LATERAL aclexplode(coalesce(c.relacl, ARRAY[]::aclitem[])) acl
            WHERE n.nspname='public'
              AND c.relname IN ('persons_outbox_events','persons_outbox_deliveries')
              AND (
                    (acl.grantee=0 AND acl.privilege_type IN ('SELECT','INSERT','UPDATE','DELETE','TRUNCATE'))
                    OR
                    (acl.grantee=(SELECT oid FROM pg_roles WHERE rolname='mdgsm_runtime')
                        AND acl.privilege_type IN ('SELECT','INSERT','UPDATE','DELETE','TRUNCATE'))
              );
            """);
        Assert.Equal(0L, count);
    }

    [Fact]
    public async Task Worker_schedules_a_closed_failure_code_and_never_logs_publisher_exception_message()
    {
        var eventMessage = NewEvent();
        var store = new FakeDeliveryStore(new LeasedPersonsOutboxEvent(eventMessage, 1));
        var publisher = new FakePublisher
        {
            Failure = new InvalidOperationException("SENSITIVE-PII-MUST-NOT-BE-LOGGED")
        };
        var logger = new RecordingLogger<PersonsOutboxDeliveryWorker>();
        var worker = new PersonsOutboxDeliveryWorker(
            store,
            publisher,
            logger,
            options: new PersonsOutboxDeliveryOptions
            {
                InitialRetryDelay = TimeSpan.FromSeconds(2),
                MaximumRetryDelay = TimeSpan.FromSeconds(8)
            },
            leaseOwner: "test-worker");

        Assert.Equal(0, await worker.DispatchOnceAsync(default));
        Assert.Equal(PersonsOutboxDeliveryFailureCodes.PublishFailed, store.LastFailureCode);
        Assert.NotNull(store.LastNextAttemptUtc);
        Assert.Contains(logger.Messages, message => message.Contains("FailureType", StringComparison.Ordinal));
        Assert.DoesNotContain("SENSITIVE-PII-MUST-NOT-BE-LOGGED",
            string.Join(Environment.NewLine, logger.Messages), StringComparison.Ordinal);
        Assert.Empty(store.AcknowledgedEventIds);
    }

    [Fact]
    public async Task Worker_acknowledges_only_after_publisher_success()
    {
        var eventMessage = NewEvent();
        var store = new FakeDeliveryStore(new LeasedPersonsOutboxEvent(eventMessage, 1));
        var publisher = new FakePublisher();
        var worker = new PersonsOutboxDeliveryWorker(
            store, publisher, new RecordingLogger<PersonsOutboxDeliveryWorker>(), leaseOwner: "test-worker");

        Assert.Equal(1, await worker.DispatchOnceAsync(default));
        Assert.Single(publisher.Messages);
        Assert.Equal(eventMessage.EventId, Assert.Single(store.AcknowledgedEventIds));
        Assert.Null(store.LastFailureCode);
    }

    private const string DefaultActor = "019b0000-0000-7000-8000-000000000001";
    private static string Actor() => Guid.CreateVersion7().ToString("D");

    private static RegisterNaturalParty NewNatural(string mobile) =>
        new("آزمایش", "Outbox", PartyBusinessRoles.Customer,
            Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L).ToString(),
            mobile, null, null, null, "تهران", "تهران", "1234567890", "نشانی تست Outbox",
            PartyAcquaintanceSource.Unknown, "", PartyCommunicationPreferences.Default);

    private static string NewMobile() => "0912" + Random.Shared.Next(1_000_000, 9_999_999).ToString();

    private static PersonsOutboxEvent NewEvent() => new(
        Guid.CreateVersion7(),
        PersonsOutboxEventContract.PartyRegistered,
        PersonsOutboxEventContract.PartyRegisteredVersion,
        Guid.CreateVersion7(),
        DefaultActor,
        DateTimeOffset.UtcNow,
        null,
        null,
        Guid.CreateVersion7());

    private static async Task<Guid> EventIdForOperationAsync(NpgsqlDataSource dataSource, Guid operationId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT event_id FROM persons_outbox_events WHERE idempotency_key=@operation AND event_type='PartyRegistered';",
            connection);
        command.Parameters.AddWithValue("operation", operationId);
        return (Guid)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The test PartyRegistered event was not written."));
    }

    private static async Task<long> ScalarAsync(NpgsqlDataSource dataSource, string sql, object? parameter = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        if (parameter is Guid id)
        {
            var name = sql.Contains("@party", StringComparison.Ordinal) ? "party"
                : sql.Contains("@operation", StringComparison.Ordinal) ? "operation" : "id";
            command.Parameters.AddWithValue(name, id);
        }
        else if (parameter is string text)
        {
            var name = sql.Contains("@mobile", StringComparison.Ordinal) ? "mobile"
                : sql.Contains("@operation", StringComparison.Ordinal) ? "operation" : "id";
            command.Parameters.AddWithValue(name, text);
        }
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<NpgsqlDataSource> OpenDisposableDataSourceAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Persons Outbox integration tests require the disposable PostgreSQL database supplied by CI.");
        var dataSource = NpgsqlDataSource.Create(connectionString!);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var marker = new NpgsqlCommand(
            "SELECT coalesce(shobj_description(oid, 'pg_database'), '') FROM pg_database WHERE datname=current_database();",
            connection);
        var comment = (string?)await marker.ExecuteScalarAsync();
        Assert.Contains("MDGSM_DISPOSABLE_TEST_DB", comment ?? string.Empty);
        return dataSource;
    }

    private sealed class FakeDeliveryStore(LeasedPersonsOutboxEvent message) : IPersonsOutboxDeliveryStore
    {
        public string? LastFailureCode { get; private set; }
        public DateTimeOffset? LastNextAttemptUtc { get; private set; }
        public List<Guid> AcknowledgedEventIds { get; } = [];

        public Task<IReadOnlyList<LeasedPersonsOutboxEvent>> LeaseDueAsync(
            string leaseOwner, int batchSize, DateTimeOffset nowUtc, DateTimeOffset leaseExpiresAtUtc,
            CancellationToken cancellationToken, IReadOnlyCollection<Guid>? eventIds = null) =>
            Task.FromResult<IReadOnlyList<LeasedPersonsOutboxEvent>>([message]);

        public Task<bool> MarkDeliveredAsync(Guid eventId, long leaseVersion, string leaseOwner, DateTimeOffset deliveredAtUtc,
            CancellationToken cancellationToken)
        {
            AcknowledgedEventIds.Add(eventId);
            return Task.FromResult(true);
        }

        public Task<bool> ScheduleRetryAsync(Guid eventId, long leaseVersion, string leaseOwner, DateTimeOffset nowUtc,
            DateTimeOffset nextAttemptAtUtc, string failureCode, CancellationToken cancellationToken)
        {
            LastFailureCode = failureCode;
            LastNextAttemptUtc = nextAttemptAtUtc;
            return Task.FromResult(true);
        }
    }

    private sealed class FakePublisher : IPersonsOutboxPublisher
    {
        public Exception? Failure { get; init; }
        public List<PersonsOutboxEvent> Messages { get; } = [];

        public Task PublishAsync(PersonsOutboxEvent message, CancellationToken cancellationToken)
        {
            if (Failure is not null) throw Failure;
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
