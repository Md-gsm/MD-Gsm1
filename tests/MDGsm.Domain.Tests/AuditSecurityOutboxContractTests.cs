using System.Security.Cryptography;
using MDGsm.AuditSecurity.Application;
using MDGsm.AuditSecurity.Contracts;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class AuditSecurityOutboxContractTests
{
    [Fact]
    public void Contract_canonicalizer_matches_the_audit_domain_persian_golden_vector()
    {
        var submission = new AuditEventSubmission(
            Guid.Parse("00000000-0000-7000-8000-000000000001"),
            Guid.Parse("00000000-0000-7000-8000-000000000002"),
            1, 1, "SHA-256", "security.test",
            DateTimeOffset.Parse("2026-11-21T12:34:56.123456Z"),
            "operator", Guid.Parse("00000000-0000-7000-8000-000000000003"),
            null, null, "IdentityAccess",
            Guid.Parse("00000000-0000-7000-8000-000000000004"),
            Guid.Parse("00000000-0000-7000-8000-000000000005"),
            null, "succeeded",
            new Dictionary<string, string> { ["z"] = "ک", ["a"] = "e\u0301" },
            ModuleContractVersion.Current);

        Assert.Equal(
            "E186D866975D3549541AFF549D00433BC18299E2D229A934DCD1667A65B06056",
            AuditEventSubmissionCanonicalizer.ComputeIntentDigestHex(submission));
    }

    [Fact]
    public async Task Dispatcher_requires_matching_durable_receipt_and_retries_or_quarantines_by_failure_class()
    {
        var submission = CreateSubmission();
        var digest = AuditEventSubmissionCanonicalizer.ComputeIntentDigestHex(submission);
        var lease = new AuditEventOutboxLease(
            submission.EventId, Guid.NewGuid(), submission, digest, 1,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));

        var store = new FakeDeliveryStore([lease]);
        var validReceipt = new AuditAppendReceipt(
            submission.EventId, submission.AuditStreamId, 1, new string('A', 64),
            false, ModuleContractVersion.Current, digest);
        var dispatcher = new AuditEventOutboxDispatcher(
            store, new FixedIngestionContract(validReceipt),
            TimeProvider.System,
            new AuditOutboxWorkerOptions(10, TimeSpan.FromMinutes(1), 3,
                TimeSpan.FromDays(7), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5)));

        var result = await dispatcher.DispatchBatchAsync(CancellationToken.None);
        Assert.Equal(new AuditOutboxDispatchResult(1, 1, 0, 0), result);
        Assert.Equal(1, store.Acknowledged);
        Assert.Equal(0, store.Retried);
        Assert.Equal(0, store.Quarantined);

        var transientStore = new FakeDeliveryStore([lease]);
        var transient = new AuditEventOutboxDispatcher(
            transientStore, new ThrowingIngestionContract(new AuditEventTransientDeliveryException("service_unavailable")),
            TimeProvider.System,
            new AuditOutboxWorkerOptions(10, TimeSpan.FromMinutes(1), 3,
                TimeSpan.FromDays(7), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5)));
        var transientResult = await transient.DispatchBatchAsync(CancellationToken.None);
        Assert.Equal(1, transientResult.Retried);
        Assert.Equal(0, transientResult.Quarantined);

        var rejectedStore = new FakeDeliveryStore([lease]);
        var rejected = new AuditEventOutboxDispatcher(
            rejectedStore, new ThrowingIngestionContract(new AuditEventDeliveryRejectedException("schema_rejected")),
            TimeProvider.System,
            new AuditOutboxWorkerOptions(10, TimeSpan.FromMinutes(1), 3,
                TimeSpan.FromDays(7), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5)));
        var rejectedResult = await rejected.DispatchBatchAsync(CancellationToken.None);
        Assert.Equal(1, rejectedResult.Quarantined);
        Assert.Equal(0, rejectedResult.Retried);

        var mismatchStore = new FakeDeliveryStore([lease]);
        var mismatch = new AuditEventOutboxDispatcher(
            mismatchStore,
            new FixedIngestionContract(validReceipt with { IntentDigestHex = new string('B', 64) }),
            TimeProvider.System,
            new AuditOutboxWorkerOptions(10, TimeSpan.FromMinutes(1), 3,
                TimeSpan.FromDays(7), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5)));
        var mismatchResult = await mismatch.DispatchBatchAsync(CancellationToken.None);
        Assert.Equal(1, mismatchResult.Quarantined);
    }

    private static AuditEventSubmission CreateSubmission() => new(
        Guid.NewGuid(), Guid.NewGuid(), 1, 1, "SHA-256", "security.test",
        DateTimeOffset.UtcNow, "service", Guid.NewGuid(), null, null,
        "TestModule", Guid.NewGuid(), Guid.NewGuid(), null, "succeeded",
        new Dictionary<string, string>(), ModuleContractVersion.Current);

    private sealed class FixedIngestionContract(AuditAppendReceipt receipt) : IAuditEventIngestionContract
    {
        public Task<AuditAppendReceipt> AppendAsync(AuditEventSubmission submission, CancellationToken cancellationToken) =>
            Task.FromResult(receipt);
    }

    private sealed class ThrowingIngestionContract(Exception exception) : IAuditEventIngestionContract
    {
        public Task<AuditAppendReceipt> AppendAsync(AuditEventSubmission submission, CancellationToken cancellationToken) =>
            Task.FromException<AuditAppendReceipt>(exception);
    }

    private sealed class FakeDeliveryStore(IReadOnlyList<AuditEventOutboxLease> leases) : IAuditEventOutboxDeliveryStore
    {
        public int Acknowledged { get; private set; }
        public int Retried { get; private set; }
        public int Quarantined { get; private set; }

        public Task<IReadOnlyList<AuditEventOutboxLease>> ClaimBatchAsync(
            string workerIdentity, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            Task.FromResult(leases);

        public Task MarkAcknowledgedAsync(AuditEventOutboxLease lease, AuditAppendReceipt receipt, CancellationToken cancellationToken)
        {
            Acknowledged++;
            return Task.CompletedTask;
        }

        public Task ScheduleRetryAsync(AuditEventOutboxLease lease, string failureCode, TimeSpan delay, CancellationToken cancellationToken)
        {
            Retried++;
            return Task.CompletedTask;
        }

        public Task QuarantineAsync(AuditEventOutboxLease lease, string reasonCode, CancellationToken cancellationToken)
        {
            Quarantined++;
            return Task.CompletedTask;
        }
    }
}
