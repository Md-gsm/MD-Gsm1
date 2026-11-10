using MDGsm.Infrastructure.Persons;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http;
using System.Text;
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

    [Fact]
    public async Task AC35_webhook_alert_sends_only_pii_free_failure_metadata_over_https()
    {
        string? capturedBody = null;
        Uri? capturedUri = null;
        var handler = new CaptureHandler(async request =>
        {
            capturedUri = request.RequestUri;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        var factory = new FakeHttpClientFactory(new HttpClient(handler));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [WebhookTemporaryWorkflowRetentionAlert.WebhookConfigurationKey] = "https://alerts.example.invalid/retention" }).Build();
        var alert = new WebhookTemporaryWorkflowRetentionAlert(factory, configuration,
            NullLogger<WebhookTemporaryWorkflowRetentionAlert>.Instance);

        await alert.NotifyFailureAsync("temporary_workflow_retention_sweep_failed:IOException", CancellationToken.None);

        Assert.Equal("https://alerts.example.invalid/retention", capturedUri!.ToString());
        Assert.Contains("temporary_workflow_retention_sweep_failed:IOException", capturedBody);
        Assert.DoesNotContain("customer", capturedBody!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload", capturedBody!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AC35_alert_refuses_missing_or_non_https_destination()
    {
        var configuration = new ConfigurationBuilder().Build();
        var alert = new WebhookTemporaryWorkflowRetentionAlert(
            new FakeHttpClientFactory(new HttpClient()), configuration,
            NullLogger<WebhookTemporaryWorkflowRetentionAlert>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            alert.NotifyFailureAsync("temporary_workflow_retention_sweep_failed:IOException", CancellationToken.None));
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request);
    }

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
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
