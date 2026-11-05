using Microsoft.Extensions.Logging;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Executes one complete idempotent retention sweep. A failed sweep is retried by the hosted
/// worker on its next five-minute tick; only exception type codes are emitted, never exception text.
/// </summary>
public sealed class TemporaryWorkflowRetentionSweep(
    ITemporaryWorkflowRetentionStore store,
    ITemporaryWorkflowRetentionAlert alert,
    ILogger<TemporaryWorkflowRetentionSweep> logger)
{
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var purged = 0;
            int batch;
            do
            {
                batch = await store.PurgeExpiredAsync(250, cancellationToken);
                purged += batch;
            } while (batch == 250 && !cancellationToken.IsCancellationRequested);

            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Temporary workflow retention sweep completed. PurgedRows={PurgedRows}", purged);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failureCode = "temporary_workflow_retention_sweep_failed:" + exception.GetType().Name.ToLowerInvariant();
            logger.LogError("Temporary workflow retention sweep failed. FailureCode={FailureCode}", failureCode);
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
                    failureCode, alertException.GetType().Name);
            }
            return false;
        }
    }
}
