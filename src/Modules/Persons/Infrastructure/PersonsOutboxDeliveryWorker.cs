using MDGsm.Persons.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDGsm.Persons.Infrastructure;

/// <summary>
/// Transport-neutral, at-least-once Outbox dispatcher.
/// Deliberately not registered by PersonsInfrastructureRegistration or ApiHost:
/// activation requires a separately approved runtime database identity and concrete publisher.
/// </summary>
public sealed class PersonsOutboxDeliveryWorker : BackgroundService
{
    private readonly IPersonsOutboxDeliveryStore _store;
    private readonly IPersonsOutboxPublisher _publisher;
    private readonly ILogger<PersonsOutboxDeliveryWorker> _logger;
    private readonly TimeProvider _clock;
    private readonly PersonsOutboxDeliveryOptions _options;
    private readonly string _leaseOwner;

    public PersonsOutboxDeliveryWorker(
        IPersonsOutboxDeliveryStore store,
        IPersonsOutboxPublisher publisher,
        ILogger<PersonsOutboxDeliveryWorker> logger,
        TimeProvider? timeProvider = null,
        PersonsOutboxDeliveryOptions? options = null,
        string? leaseOwner = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = timeProvider ?? TimeProvider.System;
        _options = options ?? new PersonsOutboxDeliveryOptions();
        _leaseOwner = string.IsNullOrWhiteSpace(leaseOwner)
            ? Guid.CreateVersion7().ToString("N")
            : leaseOwner;

        _options.Validate();
        if (_leaseOwner.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(leaseOwner));
    }

    /// <summary>Executes one deterministic poll cycle; exposed for disposable integration tests.</summary>
    public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var leased = await _store.LeaseDueAsync(
            _leaseOwner,
            _options.BatchSize,
            now,
            now.Add(_options.LeaseDuration),
            cancellationToken);

        var completed = 0;
        foreach (var item in leased)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _publisher.PublishAsync(item.Event, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failedAt = _clock.GetUtcNow();
                var nextAttempt = failedAt.Add(ComputeRetryDelay(item.AttemptCount));
                try
                {
                    var scheduled = await _store.ScheduleRetryAsync(
                        item.Event.EventId,
                        item.LeaseVersion,
                        _leaseOwner,
                        failedAt,
                        nextAttempt,
                        PersonsOutboxDeliveryFailureCodes.PublishFailed,
                        cancellationToken);
                    if (!scheduled)
                    {
                        _logger.LogWarning(
                            "Persons Outbox retry was not recorded because the delivery lease was lost. FailureCode={FailureCode}; FailureType={FailureType}",
                            PersonsOutboxDeliveryFailureCodes.LeaseExpired,
                            exception.GetType().Name);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Persons Outbox publish failed and was scheduled for retry. FailureCode={FailureCode}; FailureType={FailureType}",
                            PersonsOutboxDeliveryFailureCodes.PublishFailed,
                            exception.GetType().Name);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception persistenceException)
                {
                    // Exception messages can contain SQL parameters. Log only closed codes and type names.
                    _logger.LogError(
                        "Persons Outbox retry persistence failed. FailureCode={FailureCode}; FailureType={FailureType}; PersistenceFailureType={PersistenceFailureType}",
                        PersonsOutboxDeliveryFailureCodes.PublishFailed,
                        exception.GetType().Name,
                        persistenceException.GetType().Name);
                }

                continue;
            }

            var deliveredAt = _clock.GetUtcNow();
            var acknowledged = await _store.MarkDeliveredAsync(
                item.Event.EventId, item.LeaseVersion, _leaseOwner, deliveredAt, cancellationToken);
            if (!acknowledged)
            {
                // A stale worker must never acknowledge a message after its lease was reclaimed.
                _logger.LogWarning(
                    "Persons Outbox acknowledgement was rejected because the delivery lease was lost. FailureCode={FailureCode}",
                    PersonsOutboxDeliveryFailureCodes.LeaseExpired);
                continue;
            }

            completed++;
        }

        return completed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Never log event contents, provider exception messages, or connection details.
                _logger.LogError(
                    "Persons Outbox polling cycle failed. FailureCode=claim_failed; FailureType={FailureType}",
                    exception.GetType().Name);
            }

            await Task.Delay(_options.PollInterval, _clock, stoppingToken);
        }
    }

    private TimeSpan ComputeRetryDelay(int attemptCount)
    {
        var delay = _options.InitialRetryDelay;
        var doublings = Math.Clamp(attemptCount - 1, 0, 30);
        for (var index = 0; index < doublings && delay < _options.MaximumRetryDelay; index++)
            delay = delay > TimeSpan.FromTicks(_options.MaximumRetryDelay.Ticks / 2)
                ? _options.MaximumRetryDelay
                : TimeSpan.FromTicks(delay.Ticks * 2);

        return delay > _options.MaximumRetryDelay ? _options.MaximumRetryDelay : delay;
    }
}

public sealed record PersonsOutboxDeliveryOptions
{
    public int BatchSize { get; init; } = 25;
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromMinutes(15);

    public void Validate()
    {
        if (BatchSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(BatchSize));
        if (PollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(PollInterval));
        if (LeaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(LeaseDuration));
        if (InitialRetryDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(InitialRetryDelay));
        if (MaximumRetryDelay < InitialRetryDelay) throw new ArgumentOutOfRangeException(nameof(MaximumRetryDelay));
    }
}
