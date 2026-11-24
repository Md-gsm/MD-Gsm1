using System.Security.Cryptography;
using MDGsm.AuditSecurity.Contracts;

namespace MDGsm.AuditSecurity.Application;

public sealed record AuditOutboxWorkerOptions(
    int BatchSize,
    TimeSpan LeaseDuration,
    int MaximumAttempts,
    TimeSpan MaximumEventAge,
    TimeSpan InitialRetryDelay,
    TimeSpan MaximumRetryDelay)
{
    public void Validate()
    {
        if (BatchSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(BatchSize));
        if (LeaseDuration <= TimeSpan.Zero || LeaseDuration > TimeSpan.FromMinutes(15))
            throw new ArgumentOutOfRangeException(nameof(LeaseDuration));
        if (MaximumAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(MaximumAttempts));
        if (MaximumEventAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MaximumEventAge));
        if (InitialRetryDelay <= TimeSpan.Zero || MaximumRetryDelay < InitialRetryDelay)
            throw new ArgumentOutOfRangeException(nameof(InitialRetryDelay));
    }
}

public sealed record AuditOutboxDispatchResult(int Claimed, int Acknowledged, int Retried, int Quarantined);

/// <summary>
/// At-least-once dispatcher. A transport failure after remote commit is resolved by replaying
/// the same EventId and canonical intent. The local row is ACKNOWLEDGED only after a matching
/// durable receipt has returned over the authenticated ingestion channel.
/// </summary>
public sealed class AuditEventOutboxDispatcher(
    IAuditEventOutboxDeliveryStore outbox,
    IAuditEventIngestionContract ingestion,
    TimeProvider timeProvider,
    AuditOutboxWorkerOptions options)
{
    public async Task<AuditOutboxDispatchResult> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(ingestion);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var leases = await outbox.ClaimBatchAsync(
            AuditEventOutboxWorkerIdentity.LogicalIdentity,
            options.BatchSize,
            options.LeaseDuration,
            cancellationToken);

        var acknowledged = 0;
        var retried = 0;
        var quarantined = 0;
        foreach (var lease in leases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsLeaseDigestValid(lease))
            {
                await outbox.QuarantineAsync(lease, "outbox_integrity_mismatch", cancellationToken);
                quarantined++;
                continue;
            }

            var age = timeProvider.GetUtcNow() - lease.CreatedAtUtc;
            if (lease.AttemptCount > options.MaximumAttempts || age > options.MaximumEventAge)
            {
                await outbox.QuarantineAsync(lease, "delivery_policy_exhausted", cancellationToken);
                quarantined++;
                continue;
            }

            try
            {
                var receipt = await ingestion.AppendAsync(lease.Submission, cancellationToken);
                if (!IsMatchingDurableReceipt(lease, receipt))
                    throw new AuditEventDeliveryRejectedException("durable_acknowledgement_mismatch");

                await outbox.MarkAcknowledgedAsync(lease, receipt, cancellationToken);
                acknowledged++;
            }
            catch (AuditEventDeliveryRejectedException exception)
            {
                await outbox.QuarantineAsync(lease, exception.ReasonCode, cancellationToken);
                quarantined++;
            }
            catch (AuditEventTransientDeliveryException exception)
            {
                if (lease.AttemptCount >= options.MaximumAttempts
                    || age >= options.MaximumEventAge)
                {
                    await outbox.QuarantineAsync(lease, "delivery_policy_exhausted", cancellationToken);
                    quarantined++;
                }
                else
                {
                    await outbox.ScheduleRetryAsync(
                        lease, exception.ReasonCode, GetRetryDelay(lease.AttemptCount), cancellationToken);
                    retried++;
                }
            }
        }

        return new AuditOutboxDispatchResult(leases.Count, acknowledged, retried, quarantined);
    }

    private TimeSpan GetRetryDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 30);
        var multiplier = Math.Pow(2, exponent);
        var milliseconds = Math.Min(
            options.MaximumRetryDelay.TotalMilliseconds,
            options.InitialRetryDelay.TotalMilliseconds * multiplier);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static bool IsLeaseDigestValid(AuditEventOutboxLease lease)
    {
        try
        {
            var stored = Convert.FromHexString(lease.IntentDigestHex);
            var actual = AuditEventSubmissionCanonicalizer.ComputeIntentDigest(lease.Submission);
            return stored.Length == 32 && CryptographicOperations.FixedTimeEquals(stored, actual)
                && lease.EventId == lease.Submission.EventId
                && lease.LeaseToken != Guid.Empty
                && lease.AttemptCount > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsMatchingDurableReceipt(
        AuditEventOutboxLease lease,
        AuditAppendReceipt receipt)
    {
        if (receipt.EventId != lease.EventId
            || receipt.AuditStreamId != lease.Submission.AuditStreamId
            || receipt.Sequence < 1
            || !string.Equals(receipt.ContractVersion, ModuleContractVersion.Current, StringComparison.Ordinal))
            return false;

        try
        {
            var expectedDigest = Convert.FromHexString(lease.IntentDigestHex);
            var actualDigest = Convert.FromHexString(receipt.IntentDigestHex);
            var eventHash = Convert.FromHexString(receipt.EventHashHex);
            return expectedDigest.Length == 32 && actualDigest.Length == 32 && eventHash.Length == 32
                && CryptographicOperations.FixedTimeEquals(expectedDigest, actualDigest);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
