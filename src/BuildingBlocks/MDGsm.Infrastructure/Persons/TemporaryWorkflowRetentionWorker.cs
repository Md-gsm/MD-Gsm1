using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDGsm.Infrastructure.Persons;

/// <summary>Receives a PII-free failure code; alert delivery and its five-minute SLA are owned by the host.</summary>
public interface ITemporaryWorkflowRetentionAlert
{
    Task NotifyFailureAsync(string failureCode, CancellationToken cancellationToken);
}

/// <summary>
/// Runs retention cleanup immediately and then every fifteen minutes. Expiry enforcement is performed
/// by the store on every read, so worker delay/failure never restores access to expired payloads.
/// </summary>
public sealed class TemporaryWorkflowRetentionWorker(
    PostgresTemporaryWorkflowStore store,
    ITemporaryWorkflowRetentionAlert alert,
    ILogger<TemporaryWorkflowRetentionWorker> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

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
                    batch = await store.PurgeExpiredAsync(_clock.GetUtcNow(), 250, stoppingToken);
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
                // Never log exception.Message or payload/record values: driver exceptions can contain SQL parameters.
                var failureCode = "temporary_workflow_retention_sweep_failed:" + exception.GetType().Name;
                logger.LogError("Temporary workflow retention sweep failed. FailureCode={FailureCode}", failureCode);
                try
                {
                    await alert.NotifyFailureAsync(failureCode, stoppingToken);
                }
                catch (Exception alertException) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogCritical("Temporary workflow retention alert delivery failed. FailureCode={FailureCode}; AlertFailureType={AlertFailureType}",
                        failureCode, alertException.GetType().Name);
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
