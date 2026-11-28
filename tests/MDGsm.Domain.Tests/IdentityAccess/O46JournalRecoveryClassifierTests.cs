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
}
