using MDGsm.Infrastructure.Persons;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsTemporaryWorkflowWorkerTests
{
    [Fact]
    public async Task AC35_failed_sweep_is_retried_on_next_cycle_and_does_not_log_exception_text()
    {
        const string piiCanary = "private-person-name@example.invalid";
        var store = new SequenceRetentionStore(
            new InvalidOperationException(piiCanary),
            250,
            4);
        var alert = new RecordingRetentionAlert();
        var logger = new CapturingLogger<TemporaryWorkflowRetentionSweep>();
        var sweep = new TemporaryWorkflowRetentionSweep(store, alert, logger);

        Assert.False(await sweep.RunOnceAsync());
        Assert.True(await sweep.RunOnceAsync());

        Assert.Equal(3, store.Calls);
        Assert.Single(alert.FailureCodes);
        Assert.Contains("invalidoperationexception", alert.FailureCodes[0], StringComparison.Ordinal);
        Assert.DoesNotContain(piiCanary, string.Join(Environment.NewLine, logger.Messages), StringComparison.Ordinal);
        Assert.DoesNotContain(piiCanary, string.Join(Environment.NewLine, alert.FailureCodes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC38_structured_alert_rejects_untrusted_failure_code_content()
    {
        const string piiCanary = "private-person-name@example.invalid";
        var logger = new CapturingLogger<StructuredLogTemporaryWorkflowRetentionAlert>();
        var alert = new StructuredLogTemporaryWorkflowRetentionAlert(logger);

        await alert.NotifyFailureAsync(piiCanary, CancellationToken.None);

        var output = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("invalid_failure_code", output, StringComparison.Ordinal);
        Assert.DoesNotContain(piiCanary, output, StringComparison.Ordinal);
    }

    private sealed class SequenceRetentionStore(params object[] outcomes) : ITemporaryWorkflowRetentionStore
    {
        private readonly Queue<object> _outcomes = new(outcomes);
        public int Calls { get; private set; }

        public Task<int> PurgeExpiredAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.InRange(batchSize, 1, 5000);
            Calls++;
            var outcome = _outcomes.Dequeue();
            if (outcome is Exception exception)
                throw exception;
            return Task.FromResult((int)outcome);
        }
    }

    private sealed class RecordingRetentionAlert : ITemporaryWorkflowRetentionAlert
    {
        public List<string> FailureCodes { get; } = [];

        public Task NotifyFailureAsync(string failureCode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FailureCodes.Add(failureCode);
            return Task.CompletedTask;
        }
    }
}

internal sealed class CapturingLogger<T> : ILogger<T>
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
        Messages.Add(formatter(state, exception));
    }
}
