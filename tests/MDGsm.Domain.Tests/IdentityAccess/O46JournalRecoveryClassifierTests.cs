using MDGsm.Infrastructure.IdentityAccess.Bootstrap;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class O46JournalRecoveryClassifierTests
{
    private static readonly Guid OperationId = Guid.Parse("d78c81f6-06f4-4f27-ae3d-4cbd7d1b3c18");

    private static O46JournalRecord Committed(long sequence, string hash, string previous = "genesis") =>
        new(sequence, O46JournalRecordState.Committed, hash, previous, OperationId, "fingerprint", true);

    private static O46JournalRecord Prepared(long sequence, string previousHash) =>
        new(sequence, O46JournalRecordState.Prepared, $"record-{sequence}", previousHash,
            OperationId, "fingerprint-next", true);

    [Fact]
    public void Matching_counter_and_committed_record_is_healthy()
    {
        var decision = O46JournalRecoveryClassifier.Classify(new(
            7, [Committed(7, "record-7")], []));

        Assert.Equal(O46JournalRecoveryDisposition.Healthy, decision.Disposition);
    }

    [Fact]
    public void Complete_committed_chain_with_matching_hash_links_is_healthy()
    {
        var first = Committed(6, "record-6", "anchor");
        var second = Committed(7, "record-7", first.RecordHash);

        var decision = O46JournalRecoveryClassifier.Classify(new(7, [second, first], []));

        Assert.Equal(O46JournalRecoveryDisposition.Healthy, decision.Disposition);
    }

    [Fact]
    public void Committed_chain_with_broken_previous_hash_is_blocked()
    {
        var first = Committed(6, "record-6", "anchor");
        var second = Committed(7, "record-7", "not-record-6");

        var decision = O46JournalRecoveryClassifier.Classify(new(7, [first, second], []));

        Assert.Equal(O46JournalRecoveryDisposition.Blocked, decision.Disposition);
    }

    [Fact]
    public void A_prepared_state_cannot_be_smuggled_into_the_committed_record_set()
    {
        var wrongState = Committed(7, "record-7") with { State = O46JournalRecordState.Prepared };

        var decision = O46JournalRecoveryClassifier.Classify(new(7, [wrongState], []));

        Assert.Equal(O46JournalRecoveryDisposition.Blocked, decision.Disposition);
    }

    [Fact]
    public void Counter_not_incremented_allows_only_exact_pending_transition_retry()
    {
        var committed = Committed(7, "record-7");
        var decision = O46JournalRecoveryClassifier.Classify(new(
            7, [committed], [Prepared(8, committed.RecordHash)]));

        Assert.Equal(O46JournalRecoveryDisposition.RetryExactPendingTransition, decision.Disposition);
        Assert.Equal(8, decision.PendingSequence);
    }

    [Fact]
    public void Counter_ahead_by_one_finalizes_only_matching_prepared_record()
    {
        var committed = Committed(7, "record-7");
        var decision = O46JournalRecoveryClassifier.Classify(new(
            8, [committed], [Prepared(8, committed.RecordHash)]));

        Assert.Equal(O46JournalRecoveryDisposition.FinalizeExactPreparedRecord, decision.Disposition);
    }

    [Fact]
    public void Counter_ahead_without_prepared_record_rejects_older_backup()
    {
        var decision = O46JournalRecoveryClassifier.Classify(new(
            8, [Committed(7, "record-7")], []));

        Assert.Equal(O46JournalRecoveryDisposition.Blocked, decision.Disposition);
    }

    [Fact]
    public void Counter_behind_committed_record_is_blocked()
    {
        var decision = O46JournalRecoveryClassifier.Classify(new(
            6, [Committed(7, "record-7")], []));

        Assert.Equal(O46JournalRecoveryDisposition.Blocked, decision.Disposition);
    }

    [Fact]
    public void Multiple_pending_records_are_blocked()
    {
        var committed = Committed(7, "record-7");
        var decision = O46JournalRecoveryClassifier.Classify(new(
            7, [committed],
            [Prepared(8, committed.RecordHash), Prepared(8, committed.RecordHash)]));

        Assert.Equal(O46JournalRecoveryDisposition.Blocked, decision.Disposition);
    }

    [Fact]
    public void Invalid_prepared_authentication_or_previous_hash_is_blocked()
    {
        var committed = Committed(7, "record-7");
        var unauthenticated = Prepared(8, committed.RecordHash) with { AuthenticationVerified = false };
        var wrongParent = Prepared(8, "wrong-parent");

        Assert.Equal(O46JournalRecoveryDisposition.Blocked,
            O46JournalRecoveryClassifier.Classify(new(7, [committed], [unauthenticated])).Disposition);
        Assert.Equal(O46JournalRecoveryDisposition.Blocked,
            O46JournalRecoveryClassifier.Classify(new(7, [committed], [wrongParent])).Disposition);
    }

    [Fact]
    public void Empty_operation_identifier_or_fingerprint_is_blocked()
    {
        var committed = Committed(7, "record-7");
        var missingOperation = Prepared(8, committed.RecordHash) with { SetupOperationId = Guid.Empty };
        var missingFingerprint = Prepared(8, committed.RecordHash) with { RequestFingerprint = " " };

        Assert.Equal(O46JournalRecoveryDisposition.Blocked,
            O46JournalRecoveryClassifier.Classify(new(7, [committed], [missingOperation])).Disposition);
        Assert.Equal(O46JournalRecoveryDisposition.Blocked,
            O46JournalRecoveryClassifier.Classify(new(7, [committed], [missingFingerprint])).Disposition);
    }

    [Fact]
    public void Sequence_overflow_cannot_wrap_to_a_negative_pending_sequence()
    {
        var committed = Committed(long.MaxValue, "record-max");
        var wrappedPending = Prepared(long.MinValue, committed.RecordHash);

        var decision = O46JournalRecoveryClassifier.Classify(new(
            (ulong)long.MaxValue + 1UL, [committed], [wrappedPending]));

        Assert.Equal(O46JournalRecoveryDisposition.Blocked, decision.Disposition);
    }

    [Fact]
    public void Negative_committed_sequence_is_blocked()
    {
        var decision = O46JournalRecoveryClassifier.Classify(new(
            0, [Committed(-1, "negative")], []));

        Assert.Equal(O46JournalRecoveryDisposition.Blocked, decision.Disposition);
    }
}
