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
            snapshot.CommittedRecords.Any(x => !x.AuthenticationVerified) ||
            snapshot.PreparedRecords.Any(x => !x.AuthenticationVerified))
        {
            return Blocked("Missing or unauthenticated Journal evidence.");
        }

        if (snapshot.PreparedRecords.Count > 1)
        {
            return Blocked("Multiple pending PREPARED records are not recoverable automatically.");
        }

        var committed = snapshot.CommittedRecords.OrderBy(x => x.Sequence).ToArray();
        if (committed.Select(x => x.Sequence).Distinct().Count() != committed.Length ||
            committed.Zip(committed.Skip(1), (left, right) => right.Sequence == left.Sequence + 1).Any(x => !x))
        {
            return Blocked("Committed Journal sequence is duplicated or discontinuous.");
        }

        var latest = committed[^1];
        if (latest.State != O46JournalRecordState.Committed ||
            latest.Sequence < 0 ||
            snapshot.AuthoritativeTpmCounter < (ulong)latest.Sequence)
        {
            return Blocked("TPM counter is behind the latest committed Journal sequence.");
        }

        if (snapshot.AuthoritativeTpmCounter == (ulong)latest.Sequence)
        {
            if (snapshot.PreparedRecords.Count == 0)
            {
                return new(O46JournalRecoveryDisposition.Healthy, null,
                    "TPM counter matches the latest committed Journal record.");
            }

            var pending = snapshot.PreparedRecords[0];
            if (pending.State != O46JournalRecordState.Prepared ||
                pending.Sequence != latest.Sequence + 1 ||
                !ReferencesPrevious(pending, latest))
            {
                return Blocked("PREPARED record does not exactly extend the latest COMMITTED record.");
            }

            return new(O46JournalRecoveryDisposition.RetryExactPendingTransition,
                pending.Sequence, "Only the exact authenticated pending transition may be retried.");
        }

        if (snapshot.AuthoritativeTpmCounter == (ulong)latest.Sequence + 1 &&
            snapshot.PreparedRecords.Count == 1)
        {
            var pending = snapshot.PreparedRecords[0];
            if (pending.State == O46JournalRecordState.Prepared &&
                pending.Sequence == latest.Sequence + 1 &&
                ReferencesPrevious(pending, latest))
            {
                return new(O46JournalRecoveryDisposition.FinalizeExactPreparedRecord,
                    pending.Sequence, "TPM increment is already reflected; finalize only the exact PREPARED bytes.");
            }
        }

        return Blocked("TPM counter and authenticated Journal records disagree; no automatic reconstruction is permitted.");
    }

    private static bool ReferencesPrevious(O46JournalRecord candidate, O46JournalRecord previous) =>
        candidate.PreviousRecordHash.Equals(previous.RecordHash, StringComparison.Ordinal) &&
        candidate.SetupOperationId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(candidate.RequestFingerprint);

    private static O46JournalRecoveryDecision Blocked(string reason) =>
        new(O46JournalRecoveryDisposition.Blocked, null, reason);
}
