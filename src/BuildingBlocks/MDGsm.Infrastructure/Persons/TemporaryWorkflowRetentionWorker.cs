using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MDGsm.Infrastructure.Persons;

/// <summary>Persistence port used by the hosted retention worker.</summary>
public interface ITemporaryWorkflowRetentionStore
{
    Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, int batchSize = 250, CancellationToken cancellationToken = default);
}

/// <summary>Receives a PII-free failure code; the host must route this event to its operational alert destination.</summary>
public interface ITemporaryWorkflowRetentionAlert
{
    Task NotifyFailureAsync(string failureCode, CancellationToken cancellationToken);
}

/// <summary>
/// Runs retention cleanup immediately and then every fifteen minutes. Each batch is independently
/// retryable: a committed batch is absent on the next query, while an uncommitted batch is rolled back.
/// Expiry enforcement remains in the store's read predicate and does not depend on this worker.
/// </summary>
public sealed class TemporaryWorkflowRetentionWorker(
    ITemporaryWorkflowRetentionStore store,
    ITemporaryWorkflowRetentionAlert alert,
    ILogger<TemporaryWorkflowRetentionWorker> logger,
    TimeProvider? timeProvider = null,
    TimeSpan? retryBaseDelay = null) : BackgroundService
{
    public const int MaxAttempts = 5;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan _retryBaseDelay = retryBaseDelay ?? TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _clock);
        do
        {
            try
            {
                var purged = 0;
                int batch;
                do
                {
                    batch = await PurgeBatchWithRetryAsync(stoppingToken);
                    purged += batch;
                } while (batch == 250 && !stoppingToken.IsCancellationRequested);

                logger.LogInformation("Temporary workflow retention sweep completed. PurgedRows={PurgedRows}", purged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Do not log exception.Message: database exceptions may contain SQL parameters or data.
                var failureCode = "temporary_workflow_retention_sweep_failed:" + exception.GetType().Name;
                logger.LogError("Temporary workflow retention sweep exhausted retries. FailureCode={FailureCode}", failureCode);
                try
                {
                    await alert.NotifyFailureAsync(failureCode, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception alertException)
                {
                    logger.LogCritical("Temporary workflow retention alert delivery failed. FailureCode={FailureCode}; AlertFailureType={AlertFailureType}",
                        failureCode, alertException.GetType().Name);
                }
            }
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<int> PurgeBatchWithRetryAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await store.PurgeExpiredAsync(_clock.GetUtcNow(), 250, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsTransient(exception) && attempt < MaxAttempts)
            {
                var delay = TimeSpan.FromTicks(_retryBaseDelay.Ticks * (1L << (attempt - 1)));
                logger.LogWarning("Temporary workflow retention batch failed transiently. Attempt={Attempt}; NextDelayMs={NextDelayMs}; FailureType={FailureType}",
                    attempt, delay.TotalMilliseconds, exception.GetType().Name);
                await Task.Delay(delay, _clock, cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception exception) =>
        exception is NpgsqlException { IsTransient: true } or TimeoutException or IOException;
}
