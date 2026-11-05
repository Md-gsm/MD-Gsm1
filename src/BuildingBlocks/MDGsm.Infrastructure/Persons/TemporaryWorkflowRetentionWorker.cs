using Microsoft.Extensions.Hosting;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Runs an immediate sweep and retries on a five-minute cadence. Expiry checks on every payload
/// read remain authoritative, so a delayed sweep never restores access to expired data.
/// </summary>
public sealed class TemporaryWorkflowRetentionWorker(
    TemporaryWorkflowRetentionSweep sweep,
    TimeProvider? timeProvider = null) : BackgroundService
{
    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(15);
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
