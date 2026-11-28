namespace MDGsm.Infrastructure.IdentityAccess.Bootstrap;

public enum O46JournalRecordState
{
    Prepared,
    Committed
}

public enum O46JournalRecoveryDisposition
{
    Healthy,
    RetryExactPendingTransition,
    FinalizeExactPreparedRecord,
    Blocked
}

public sealed record O46JournalRecord(
    long Sequence,
    O46JournalRecordState State,
    string RecordHash,
    string PreviousRecordHash,
    Guid SetupOperationId,
    string RequestFingerprint,
    bool AuthenticationVerified);

public sealed record O46JournalRecoverySnapshot(
    ulong AuthoritativeTpmCounter,
    IReadOnlyList<O46JournalRecord> CommittedRecords,
    IReadOnlyList<O46JournalRecord> PreparedRecords);

public sealed record O46JournalRecoveryDecision(
    O46JournalRecoveryDisposition Disposition,
    long? PendingSequence,
    string Reason);

/// <summary>
/// Pure fail-closed classifier for the O-46 TPM-counter / PREPARED-COMMITTED
/// protocol. It does not issue TPM commands or mutate Journal files.
/// </summary>
public static class O46JournalRecoveryClassifier
{
    public static O46JournalRecoveryDecision Classify(O46JournalRecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.CommittedRecords);
        ArgumentNullException.ThrowIfNull(snapshot.PreparedRecords);

        if (snapshot.CommittedRecords.Count == 0 ||
            snapshot.CommittedRecords.Any(x => !IsValidRecord(x, O46JournalRecordState.Committed)) ||
            snapshot.PreparedRecords.Any(x => !IsValidRecord(x, O46JournalRecordState.Prepared)))
        {
            return Blocked("Missing, malformed, wrongly typed or unauthenticated Journal evidence.");
        }

        if (snapshot.PreparedRecords.Count > 1)
        {
            return Blocked("Multiple pending PREPARED records are not recoverable automatically.");
        }

        var committed = snapshot.CommittedRecords.OrderBy(x => x.Sequence).ToArray();
        for (var index = 1; index < committed.Length; index++)
        {
            var previous = committed[index - 1];
            var current = committed[index];

            // Guard before addition: unchecked long overflow must never make
            // long.MaxValue appear contiguous with long.MinValue.
            if (previous.Sequence == long.MaxValue ||
                current.Sequence != previous.Sequence + 1 ||
                !string.Equals(current.PreviousRecordHash, previous.RecordHash, StringComparison.Ordinal))
            {
                return Blocked("Committed Journal sequence or hash chain is discontinuous.");
            }
        }

        if (committed.Select(x => x.Sequence).Distinct().Count() != committed.Length)
        {
            return Blocked("Committed Journal sequence contains duplicates.");
        }

        var latest = committed[^1];
        var latestSequence = (ulong)latest.Sequence;
        if (snapshot.AuthoritativeTpmCounter < latestSequence)
        {
            return Blocked("TPM counter is behind the latest committed Journal sequence.");
        }

        if (snapshot.AuthoritativeTpmCounter == latestSequence)
        {
            if (snapshot.PreparedRecords.Count == 0)
            {
                return new(O46JournalRecoveryDisposition.Healthy, null,
                    "TPM counter matches the latest committed Journal record.");
            }

            if (!TryGetNextSequence(latest.Sequence, out var nextSequence))
            {
                return Blocked("Journal sequence space is exhausted; no pending transition can be represented.");
            }

            var pending = snapshot.PreparedRecords[0];
            if (pending.Sequence != nextSequence || !ReferencesPrevious(pending, latest))
            {
                return Blocked("PREPARED record does not exactly extend the latest COMMITTED record.");
            }

            return new(O46JournalRecoveryDisposition.RetryExactPendingTransition,
                pending.Sequence, "Only the exact authenticated pending transition may be retried.");
        }

        if (TryGetNextSequence(latest.Sequence, out var expectedNext) &&
            snapshot.AuthoritativeTpmCounter == (ulong)expectedNext &&
            snapshot.PreparedRecords.Count == 1)
        {
            var pending = snapshot.PreparedRecords[0];
            if (pending.Sequence == expectedNext && ReferencesPrevious(pending, latest))
            {
                return new(O46JournalRecoveryDisposition.FinalizeExactPreparedRecord,
                    pending.Sequence, "TPM increment is already reflected; finalize only the exact PREPARED bytes.");
            }
        }

        return Blocked("TPM counter and authenticated Journal records disagree; no automatic reconstruction is permitted.");
    }

    private static bool IsValidRecord(O46JournalRecord record, O46JournalRecordState expectedState) =>
        record is not null &&
        record.State == expectedState &&
        record.Sequence >= 0 &&
        !string.IsNullOrWhiteSpace(record.RecordHash) &&
        !string.IsNullOrWhiteSpace(record.PreviousRecordHash) &&
        record.SetupOperationId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(record.RequestFingerprint) &&
        record.AuthenticationVerified;

    private static bool TryGetNextSequence(long current, out long next)
    {
        if (current == long.MaxValue)
        {
            next = default;
            return false;
        }

        next = current + 1;
        return true;
    }

    private static bool ReferencesPrevious(O46JournalRecord candidate, O46JournalRecord previous) =>
        string.Equals(candidate.PreviousRecordHash, previous.RecordHash, StringComparison.Ordinal) &&
        candidate.SetupOperationId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(candidate.RequestFingerprint);

    private static O46JournalRecoveryDecision Blocked(string reason) =>
        new(O46JournalRecoveryDisposition.Blocked, null, reason);
}
