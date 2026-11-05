using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDGsm.Infrastructure.Persons;

/// <summary>Receives a PII-free failure code; external alert routing remains a host responsibility.</summary>
public interface ITemporaryWorkflowRetentionAlert
{
    Task NotifyFailureAsync(string failureCode, CancellationToken cancellationToken);
}

/// <summary>
/// Runs an immediate sweep and retries on a five-minute cadence. Expiry checks on every payload
/// read remain authoritative, so a delayed sweep never restores access to expired data.
/// </summary>
public sealed class TemporaryWorkflowRetentionWorker(
    TemporaryWorkflowRetentionSweep sweep,
    ILogger<TemporaryWorkflowRetentionWorker> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval, _clock);
        do
        {
            await sweep.RunOnceAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
