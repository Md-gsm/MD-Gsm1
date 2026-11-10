using MDGsm.Infrastructure.Persons;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class TemporaryWorkflowRetentionWorkerTests
{
    [Fact]
    public async Task AC35_transient_database_failure_is_retried_and_recovers_without_alert()
    {
        var store = new FakeStore(new IOException("transient network failure"), new IOException("transient network failure"), 0);
        var alert = new FakeAlert();
        using var worker = new TemporaryWorkflowRetentionWorker(
            store, alert, NullLogger<TemporaryWorkflowRetentionWorker>.Instance,
            retryBaseDelay: TimeSpan.Zero);

        await worker.StartAsync(CancellationToken.None);
        await store.FirstSweepCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, store.Attempts);
        Assert.Equal(0, alert.Calls);
    }

    [Fact]
    public async Task AC35_exhausted_transient_retries_emit_one_pii_free_alert()
    {
        var store = new FakeStore(
            new IOException("transient network failure"),
            new IOException("transient network failure"),
            new IOException("transient network failure"),
            new IOException("transient network failure"),
            new IOException("transient network failure"));
        var alert = new FakeAlert();
        using var worker = new TemporaryWorkflowRetentionWorker(
            store, alert, NullLogger<TemporaryWorkflowRetentionWorker>.Instance,
            retryBaseDelay: TimeSpan.Zero);

        await worker.StartAsync(CancellationToken.None);
        await alert.AlertDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(TemporaryWorkflowRetentionWorker.MaxAttempts, store.Attempts);
        Assert.Equal(1, alert.Calls);
        Assert.Equal("temporary_workflow_retention_sweep_failed:IOException", alert.LastFailureCode);
        Assert.DoesNotContain("payload", alert.LastFailureCode!, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeStore(params object[] outcomes) : ITemporaryWorkflowRetentionStore
    {
        private int _index;
        public int Attempts => _index;
        public TaskCompletionSource FirstSweepCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, int batchSize = 250, CancellationToken cancellationToken = default)
        {
            var index = Interlocked.Increment(ref _index) - 1;
            var outcome = outcomes[Math.Min(index, outcomes.Length - 1)];
            if (outcome is Exception exception) throw exception;
            FirstSweepCompleted.TrySetResult();
            return Task.FromResult((int)outcome);
        }
    }

    private sealed class FakeAlert : ITemporaryWorkflowRetentionAlert
    {
        public int Calls { get; private set; }
        public string? LastFailureCode { get; private set; }
        public TaskCompletionSource AlertDelivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task NotifyFailureAsync(string failureCode, CancellationToken cancellationToken)
        {
            Calls++;
            LastFailureCode = failureCode;
            AlertDelivered.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
