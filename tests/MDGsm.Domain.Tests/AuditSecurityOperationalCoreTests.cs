using System.Security.Cryptography;
using MDGsm.AuditSecurity.Application;
using MDGsm.AuditSecurity.Domain;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class AuditSecurityOperationalCoreTests
{
    [Fact]
    public void Canonical_intent_matches_the_approved_persian_unicode_golden_vector()
    {
        var intent = new AuditEventIntent(
            Guid.Parse("00000000-0000-7000-8000-000000000001"),
            Guid.Parse("00000000-0000-7000-8000-000000000002"),
            1, 1, "SHA-256", "security.test",
            DateTimeOffset.Parse("2026-11-21T12:34:56.123456Z"),
            "operator", Guid.Parse("00000000-0000-7000-8000-000000000003"),
            null, null, "IdentityAccess",
            Guid.Parse("00000000-0000-7000-8000-000000000004"),
            Guid.Parse("00000000-0000-7000-8000-000000000005"),
            null, "succeeded",
            new Dictionary<string, string> { ["z"] = "ک", ["a"] = "e\u0301" });

        var digest = Convert.ToHexString(AuditEventCanonicalizer.ComputeIntentDigest(intent));

        Assert.Equal("E186D866975D3549541AFF549D00433BC18299E2D229A934DCD1667A65B06056", digest);
    }

    [Fact]
    public void Hash_chain_verifier_accepts_a_valid_stream_and_rejects_tampered_payload()
    {
        var streamId = Guid.NewGuid();
        var intent = CreateIntent(streamId, Guid.NewGuid(), "audit.created");
        var first = AuditEventCanonicalizer.CreateRecord(
            intent, 1, DateTimeOffset.Parse("2026-11-24T10:00:00Z"), AuditEventCanonicalizer.GenesisHash);
        var head = new AuditStreamHead(streamId, 1, first.EventHash.Span);

        Assert.True(AuditHashChainVerifier.Verify(streamId, [first], head).IsValid);

        var altered = intent with { OutcomeCode = "tampered" };
        var tampered = AuditEventRecord.Rehydrate(
            altered, first.Sequence, first.RecordedAtUtc, first.IntentDigest.Span,
            first.PreviousEventHash.Span, first.CanonicalPayload.Span, first.EventHash.Span);

        var invalid = AuditHashChainVerifier.Verify(streamId, [tampered], head);
        Assert.False(invalid.IsValid);
        Assert.Equal((long?)1, invalid.FirstInvalidSequence);
        Assert.Equal("event_canonicalization_or_hash_mismatch", invalid.ReasonCode);
    }

    [Fact]
    public void Hash_chain_verifier_rejects_sequence_and_head_mismatches()
    {
        var streamId = Guid.NewGuid();
        var first = AuditEventCanonicalizer.CreateRecord(
            CreateIntent(streamId, Guid.NewGuid(), "audit.created"),
            1, DateTimeOffset.Parse("2026-11-24T10:00:00Z"), AuditEventCanonicalizer.GenesisHash);

        var wrongHead = new AuditStreamHead(streamId, 2, first.EventHash.Span);
        var result = AuditHashChainVerifier.Verify(streamId, [first], wrongHead);

        Assert.False(result.IsValid);
        Assert.Equal("stream_head_sequence_mismatch", result.ReasonCode);
    }

    [Fact]
    public void Checkpoint_signature_requires_the_exact_pinned_key_id_and_rsa3072_key()
    {
        using var key = RSA.Create(3072);
        var payload = new AuditCheckpointPayload(
            Guid.NewGuid(), 12, new string('A', 64), "platform-checkpoint-key-v1",
            DateTimeOffset.Parse("2026-11-24T10:00:00Z"), 1, 1, "SHA-256", null);
        var signature = key.SignData(
            AuditCheckpointCanonicalizer.Serialize(payload),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);
        var envelope = new AuditCheckpointEnvelope(payload, signature);
        var publicKey = key.ExportSubjectPublicKeyInfo();

        Assert.True(AuditCheckpointVerifier.Verify(envelope, "platform-checkpoint-key-v1", publicKey));
        Assert.False(AuditCheckpointVerifier.Verify(envelope, "another-key-id", publicKey));
        Assert.False(AuditCheckpointVerifier.Verify(
            new AuditCheckpointEnvelope(payload with { HeadHashHex = new string('B', 64) }, signature),
            "platform-checkpoint-key-v1",
            publicKey));
    }

    [Fact]
    public async Task Appender_is_idempotent_and_rejects_reuse_of_event_id_with_changed_intent()
    {
        var store = new InMemoryAuditEventStore();
        var appender = new AuditEventAppender(store, new EmptyOnlyAuditEventMetadataPolicy());
        var intent = CreateIntent(Guid.NewGuid(), Guid.NewGuid(), "identity.login.succeeded");

        var first = await appender.AppendAsync(intent, CancellationToken.None);
        var duplicate = await appender.AppendAsync(intent, CancellationToken.None);

        Assert.False(first.Duplicate);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(first.Sequence, duplicate.Sequence);
        Assert.Equal(first.EventHashHex, duplicate.EventHashHex);

        var changed = intent with { OutcomeCode = "denied" };
        await Assert.ThrowsAsync<AuditEventConflictException>(
            () => appender.AppendAsync(changed, CancellationToken.None));

        var snapshot = await store.ReadSnapshotAsync(intent.AuditStreamId, CancellationToken.None);
        Assert.Single(snapshot.Events);
        Assert.True(AuditHashChainVerifier.Verify(intent.AuditStreamId, snapshot.Events, snapshot.Head).IsValid);
    }

    [Fact]
    public void Intent_rejects_normalized_metadata_key_collisions_and_unsupported_versions()
    {
        var streamId = Guid.NewGuid();
        var collision = CreateIntent(streamId, Guid.NewGuid(), "audit.created") with
        {
            Metadata = new Dictionary<string, string> { ["é"] = "one", ["e\u0301"] = "two" }
        };
        Assert.Throws<ArgumentException>(() => collision.NormalizeAndValidate());

        var unsupported = CreateIntent(streamId, Guid.NewGuid(), "audit.created") with
        {
            CanonicalizationVersion = 2
        };
        Assert.Throws<NotSupportedException>(() => unsupported.NormalizeAndValidate());
    }

    private static AuditEventIntent CreateIntent(Guid streamId, Guid eventId, string eventType) =>
        new(
            eventId, streamId, 1, 1, "SHA-256", eventType,
            DateTimeOffset.Parse("2026-11-24T10:00:00Z"),
            "service", Guid.NewGuid(), null, null, "IdentityAccess",
            Guid.NewGuid(), Guid.NewGuid(), null, "succeeded",
            new Dictionary<string, string>());

    private sealed class InMemoryAuditEventStore : IAuditEventStore
    {
        private static readonly DateTimeOffset DatabaseTime = DateTimeOffset.Parse("2026-11-24T10:00:00Z");
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly Dictionary<Guid, AuditStreamHead> _heads = new();
        private readonly Dictionary<Guid, List<AuditEventRecord>> _events = new();

        public async Task<IAuditAppendTransaction> BeginAppendTransactionAsync(
            Guid auditStreamId, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            EnsureStream(auditStreamId);
            return new MemoryAppendTransaction(this, auditStreamId);
        }

        public async Task<AuditStreamSnapshot> ReadSnapshotAsync(
            Guid auditStreamId, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                EnsureStream(auditStreamId);
                return new AuditStreamSnapshot(_heads[auditStreamId], _events[auditStreamId].ToArray());
            }
            finally
            {
                _gate.Release();
            }
        }

        private void EnsureStream(Guid streamId)
        {
            if (streamId == Guid.Empty)
                throw new ArgumentException("Stream ID is required.", nameof(streamId));
            if (!_heads.ContainsKey(streamId))
            {
                _heads.Add(streamId, new AuditStreamHead(
                    streamId, 0, AuditEventCanonicalizer.GenesisHash));
                _events.Add(streamId, new List<AuditEventRecord>());
            }
        }

        private AuditEventRecord? FindEvent(Guid eventId) =>
            _events.Values.SelectMany(items => items).SingleOrDefault(item => item.Intent.EventId == eventId);

        private void Commit(Guid streamId, AuditEventRecord record, AuditStreamHead newHead)
        {
            if (FindEvent(record.Intent.EventId) is not null)
                throw new AuditEventConflictException("Duplicate EventId.");
            if (newHead.AuditStreamId != streamId || newHead.LastSequence != record.Sequence)
                throw new InvalidOperationException("Invalid staged stream head.");
            _events[streamId].Add(record);
            _heads[streamId] = newHead;
        }

        private sealed class MemoryAppendTransaction(
            InMemoryAuditEventStore owner,
            Guid streamId) : IAuditAppendTransaction
        {
            private AuditEventRecord? _record;
            private AuditStreamHead? _newHead;
            private bool _disposed;

            public Task<AuditAppendState> LockStreamHeadAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new AuditAppendState(owner._heads[streamId], DatabaseTime));
            }

            public Task<AuditEventRecord?> FindByEventIdAsync(Guid eventId, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(owner.FindEvent(eventId));
            }

            public Task InsertEventAsync(AuditEventRecord record, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _record = record;
                return Task.CompletedTask;
            }

            public Task AdvanceStreamHeadAsync(
                Guid requestedStreamId,
                long newSequence,
                ReadOnlyMemory<byte> newHeadHash,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (requestedStreamId != streamId)
                    throw new InvalidOperationException("Wrong stream.");
                _newHead = new AuditStreamHead(streamId, newSequence, newHeadHash.Span);
                return Task.CompletedTask;
            }

            public Task CommitAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_record is null || _newHead is null)
                    throw new InvalidOperationException("Event and stream head must be staged before commit.");
                owner.Commit(streamId, _record, _newHead);
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    owner._gate.Release();
                }
                return ValueTask.CompletedTask;
            }
        }
    }
}
