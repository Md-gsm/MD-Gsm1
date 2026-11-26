using MDGsm.Persons.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDGsm.Persons.Infrastructure;

/// <summary>Closed vocabulary for retention-alert events. It cannot carry record data or exception messages.</summary>
public enum TemporaryWorkflowRetentionFailureCode
{
    SweepFailed = 1
}

/// <summary>Receives a closed, PII-free failure code; alert delivery and its five-minute SLA are owned by the host.</summary>
public interface ITemporaryWorkflowRetentionAlert
{
    Task NotifyFailureAsync(
        TemporaryWorkflowRetentionFailureCode failureCode,
        CancellationToken cancellationToken);
}

/// <summary>
/// Runs retention cleanup immediately and then every fifteen minutes. Expiry enforcement is performed
/// by the store on every read, so worker delay/failure never restores access to expired payloads.
/// </summary>
public sealed class TemporaryWorkflowRetentionWorker(
    ITemporaryWorkflowRetentionStore store,
    ITemporaryWorkflowRetentionAlert alert,
    ILogger<TemporaryWorkflowRetentionWorker> logger,
    TimeProvider? timeProvider = null,
    TimeSpan? interval = null) : BackgroundService
{
    public const int DefaultSweepIntervalMinutes = 15;

    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan _interval = interval ?? TimeSpan.FromMinutes(DefaultSweepIntervalMinutes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Retention worker interval must be positive.");

        using var timer = new PeriodicTimer(_interval, _clock);
        do
        {
            try
            {
                await RunSweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunSweepOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var purged = 0;
            int batch;
            do
            {
                batch = await store.PurgeExpiredAsync(_clock.GetUtcNow(), 250, cancellationToken);
                purged += batch;
            } while (batch == 250 && !cancellationToken.IsCancellationRequested);

            logger.LogInformation(
                "Temporary workflow retention sweep completed. PurgedRows={PurgedRows}",
                purged);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Never log exception.Message or payload/record values: driver exceptions can contain SQL parameters.
            const TemporaryWorkflowRetentionFailureCode failureCode =
                TemporaryWorkflowRetentionFailureCode.SweepFailed;
            logger.LogError(
                "Temporary workflow retention sweep failed. FailureCode={FailureCode}; FailureType={FailureType}",
                failureCode,
                exception.GetType().Name);
            try
            {
                await alert.NotifyFailureAsync(failureCode, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception alertException)
            {
                logger.LogCritical(
                    "Temporary workflow retention alert delivery failed. FailureCode={FailureCode}; AlertFailureType={AlertFailureType}",
                    failureCode,
                    alertException.GetType().Name);
            }
        }
    }
}
