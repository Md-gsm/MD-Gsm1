using MDGsm.Application.Persons;
using MDGsm.Domain.Persons;
using MDGsm.Infrastructure.Persons;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsTemporaryDataRetentionTests
{
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AC31_expiry_blocks_access_at_the_exact_utc_expiry_instant()
    {
        var record = TemporaryWorkflowRetention.Create(Guid.NewGuid(), Created);
        Assert.True(record.IsAccessibleAt(record.ExpiresAtUtc.AddTicks(-1)));
        Assert.False(record.IsAccessibleAt(record.ExpiresAtUtc));
        Assert.True(record.IsPurgeDueAt(record.ExpiresAtUtc));
    }

    [Fact]
    public void AC31_expiry_comparison_normalizes_non_utc_offsets()
    {
        var record = TemporaryWorkflowRetention.Create(Guid.NewGuid(), Created);
        Assert.False(record.IsAccessibleAt(record.ExpiresAtUtc.ToOffset(TimeSpan.FromHours(3))));
    }

    [Fact]
    public async Task AC35_worker_retries_on_next_cycle_and_logs_only_redacted_failure_codes()
    {
        var store = new TransientFailureThenSuccessRetentionStore();
        var alert = new FailingRetentionAlert();
        var logger = new CapturingLogger<TemporaryWorkflowRetentionWorker>();
        var worker = new TemporaryWorkflowRetentionWorker(
            store,
            alert,
            logger,
            interval: TimeSpan.FromMilliseconds(10));

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await store.SecondSweepCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.True(store.CallCount >= 2);
        Assert.Equal(
            "temporary_workflow_retention_sweep_failed:InvalidOperationException",
            Assert.Single(alert.FailureCodes));
        var logText = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("FailureCode=temporary_workflow_retention_sweep_failed:InvalidOperationException", logText);
        Assert.Contains("AlertFailureType=InvalidOperationException", logText);
        Assert.DoesNotContain(TransientFailureThenSuccessRetentionStore.PrivatePayloadSentinel, logText);
        Assert.DoesNotContain(FailingRetentionAlert.PrivateAlertSentinel, logText);
    }

    [Fact]
    public void AC33_renewal_never_extends_beyond_absolute_thirty_day_age()
    {
        var record = TemporaryWorkflowRetention.Create(Guid.NewGuid(), Created);
        var renewed = record.Renew(Created.AddDays(6), TimeSpan.FromDays(40),
            "Customer workflow remains active", authorized: true);
        Assert.Equal(Created.AddDays(30), renewed.ExpiresAtUtc);
        Assert.Equal(Created.AddDays(30), renewed.AbsoluteExpiresAtUtc);
    }

    [Fact]
    public void AC34_only_one_authorized_renewal_with_a_reason_is_accepted()
    {
        var record = TemporaryWorkflowRetention.Create(Guid.NewGuid(), Created);
        var renewed = record.Renew(Created.AddDays(2), TimeSpan.FromDays(3),
            "Awaiting customer response", authorized: true);
        Assert.Equal(1, renewed.RenewalCount);
        Assert.Throws<InvalidOperationException>(() => renewed.Renew(Created.AddDays(3),
            TimeSpan.FromDays(1), "Second extension is not allowed", authorized: true));
    }

    [Fact]
    public void AC34_renewal_rejects_missing_reason_or_missing_authorization()
    {
        var record = TemporaryWorkflowRetention.Create(Guid.NewGuid(), Created);
        Assert.Throws<ArgumentException>(() => record.Renew(Created.AddDays(1),
            TimeSpan.FromDays(1), "short", authorized: true));
        Assert.Throws<UnauthorizedAccessException>(() => record.Renew(Created.AddDays(1),
            TimeSpan.FromDays(1), "Approved workflow extension", authorized: false));
    }

    [Fact]
    public void Closed_or_expired_workflow_cannot_be_renewed()
    {
        var record = TemporaryWorkflowRetention.Create(Guid.NewGuid(), Created).Close();
        Assert.False(record.IsAccessibleAt(Created.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() => record.Renew(Created.AddDays(1),
            TimeSpan.FromDays(1), "Workflow was already closed", authorized: true));
    }

    private sealed class TransientFailureThenSuccessRetentionStore : ITemporaryWorkflowRetentionStore
    {
        public const string PrivatePayloadSentinel = "PRIVATE-CONTACT-PII-SENTINEL-STORE";
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);
        public TaskCompletionSource<bool> SecondSweepCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> PurgeExpiredAsync(
            DateTimeOffset nowUtc,
            int batchSize = 250,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
                return Task.FromException<int>(new InvalidOperationException(PrivatePayloadSentinel));

            SecondSweepCompleted.TrySetResult(true);
            return Task.FromResult(0);
        }
    }

    private sealed class FailingRetentionAlert : ITemporaryWorkflowRetentionAlert
    {
        public const string PrivateAlertSentinel = "PRIVATE-CONTACT-PII-SENTINEL-ALERT";
        public List<string> FailureCodes { get; } = [];

        public Task NotifyFailureAsync(string failureCode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FailureCodes.Add(failureCode);
            return Task.FromException(new InvalidOperationException(PrivateAlertSentinel));
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Messages)
                Messages.Add(formatter(state, exception));
        }
    }

}
