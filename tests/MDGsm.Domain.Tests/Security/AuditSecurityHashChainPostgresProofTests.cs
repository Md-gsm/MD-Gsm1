using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Security;

/// <summary>
/// Disposable PostgreSQL proof of the AuditSecurity chain protocol.
/// The schema, LOGINs, and RSA keys exist only for this test and are always removed.
/// This is not a production migration, WORM adapter, or production key-custody implementation.
/// </summary>
[Collection("IdentityAccessPostgres")]
public sealed class AuditSecurityHashChainPostgresProofTests
{
    private const short SchemaVersion = 1;
    private const short CanonicalizationVersion = 1;
    private static readonly byte[] GenesisHash =
        SHA256.HashData(Encoding.UTF8.GetBytes("MDGSM-AUDIT-GENESIS-V1"));

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Two_independent_writers_are_serialized_and_chain_is_verifiable_and_tamper_evident()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("IdentityAccessTestConnectionString");
        Assert.False(string.IsNullOrWhiteSpace(adminConnectionString));
        var baseBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        Assert.Contains(baseBuilder.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Matches("^mdgsm_identityaccess_disposable_[0-9]+_[0-9]+$", baseBuilder.Database);

        await using (var guard = new NpgsqlConnection(adminConnectionString))
        {
            await guard.OpenAsync();
            await using var marker = new NpgsqlCommand(
                "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = current_database();",
                guard);
            var value = (string?)await marker.ExecuteScalarAsync();
            Assert.StartsWith("MDGSM_DISPOSABLE_TEST_DB;", value);
        }

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var schema = "auditsecurity_poc_" + suffix;
        var owner = "auditsecurity_owner_" + suffix;
        var writerGroup = "auditsecurity_writer_" + suffix;
        var writerA = "audit_writer_a_" + suffix;
        var writerB = "audit_writer_b_" + suffix;
        var verifierRole = "audit_verifier_" + suffix;
        var writerAConnection = WithUser(adminConnectionString, writerA);
        var writerBConnection = WithUser(adminConnectionString, writerB);
        var verifierConnection = WithUser(adminConnectionString, verifierRole);
        var streamId = Guid.NewGuid();
        var tempCheckpoint = Path.Combine(Path.GetTempPath(), "mdgsm-audit-checkpoint-" + suffix + ".json");

        try
        {
            await CreateDisposableSchemaAsync(adminConnectionString, schema, owner,
                writerGroup, writerA, writerB, verifierRole);
            await InitializeStreamAsync(adminConnectionString, schema, streamId);

            await AssertDeniedAsync(writerAConnection, $"SELECT * FROM {Q(schema)}.audit_events;");
            await AssertDeniedAsync(writerAConnection, $"SELECT * FROM {Q(schema)}.audit_stream_heads;");
            await AssertDeniedAsync(writerAConnection, $"UPDATE {Q(schema)}.audit_events SET event_type_code = event_type_code;");
            await AssertDeniedAsync(writerBConnection, $"DELETE FROM {Q(schema)}.audit_events;");
            await AssertDeniedAsync(writerAConnection, $"TRUNCATE TABLE {Q(schema)}.audit_events;");
            await AssertDeniedAsync(writerAConnection, $"CREATE TABLE {Q(schema)}.runtime_probe(id integer);");
            await AssertDeniedAsync(verifierConnection, $"UPDATE {Q(schema)}.audit_stream_heads SET last_sequence = last_sequence;");

            // Two distinct LOGIN roles, each on its own connection pool, append concurrently.
            const int writerCount = 2;
            const int eventsPerWriter = 20;
            var work = Enumerable.Range(0, writerCount * eventsPerWriter)
                .Select(index =>
                {
                    var cs = index % 2 == 0 ? writerAConnection : writerBConnection;
                    var intent = NewIntent(streamId, "event-" + index.ToString(CultureInfo.InvariantCulture),
                        new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["description"] = "رویداد ممیزی",
                            ["ordinal"] = index.ToString(CultureInfo.InvariantCulture)
                        });
                    return AppendAsync(cs, schema, intent);
                });
            var committed = await Task.WhenAll(work);
            Assert.Equal(writerCount * eventsPerWriter, committed.Length);
            Assert.Equal(writerCount * eventsPerWriter, committed.Select(x => x.Sequence).Distinct().Count());
            Assert.Equal(Enumerable.Range(1, writerCount * eventsPerWriter).Select(x => (long)x),
                committed.Select(x => x.Sequence).OrderBy(x => x));

            var beforeFailure = await ReadHeadAsync(adminConnectionString, schema, streamId);
            var rollbackIntent = NewIntent(streamId, "rollback-event", new Dictionary<string, string> { ["reason"] = "injected" });
            await Assert.ThrowsAsync<InjectedRollbackException>(() =>
                AppendAsync(writerAConnection, schema, rollbackIntent, injectRollbackAfterInsert: true));
            var afterRollback = await ReadHeadAsync(adminConnectionString, schema, streamId);
            Assert.Equal(beforeFailure.Sequence, afterRollback.Sequence);
            Assert.Equal(beforeFailure.Hash, afterRollback.Hash);
            Assert.False(await EventExistsAsync(adminConnectionString, schema, rollbackIntent.EventId));

            // Kill the PostgreSQL backend after INSERT but before head update/COMMIT.
            // PostgreSQL must roll back the open transaction on backend termination.
            var crashIntent = NewIntent(streamId, "crash-before-commit", new Dictionary<string, string> { ["reason"] = "backend-termination" });
            await Assert.ThrowsAnyAsync<Exception>(() =>
                AppendAsync(writerBConnection, schema, crashIntent, adminConnectionString: adminConnectionString, terminateBackendAfterInsert: true));
            var afterCrash = await ReadHeadAsync(adminConnectionString, schema, streamId);
            Assert.Equal(beforeFailure.Sequence, afterCrash.Sequence);
            Assert.Equal(beforeFailure.Hash, afterCrash.Hash);
            Assert.False(await EventExistsAsync(adminConnectionString, schema, crashIntent.EventId));

            // Simulate an unknown client outcome: retry the same committed EventId.
            var replayIntent = NewIntent(streamId, "unknown-outcome-replay",
                new SortedDictionary<string, string> { ["message"] = "retry-safe" });
            var firstOutcome = await AppendAsync(writerAConnection, schema, replayIntent);
            var replay = await AppendAsync(writerBConnection, schema, replayIntent);
            Assert.False(firstOutcome.Duplicate);
            Assert.True(replay.Duplicate);
            Assert.Equal(firstOutcome.Sequence, replay.Sequence);
            Assert.Equal(firstOutcome.EventHash, replay.EventHash);

            var conflictingReplay = replayIntent with { EventTypeCode = "different-event-type" };
            var replayConflict = await Assert.ThrowsAsync<PostgresException>(() => AppendAsync(writerBConnection, schema, conflictingReplay));
            Assert.Equal("P0001", replayConflict.SqlState);

            // Fixed canonicalization vector exercises Persian text, NFC, sorted metadata and UTF-8.
            var vector = new EventIntent(
                Guid.Parse("00000000-0000-7000-8000-000000000001"),
                Guid.Parse("00000000-0000-7000-8000-000000000002"),
                "security.test",
                DateTimeOffset.Parse("2026-11-21T12:34:56.123456Z", CultureInfo.InvariantCulture),
                "ناظر",
                null,
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["z"] = "ک",
                    ["a"] = "e\u0301"
                });
            var normalizedVector = Normalize(vector);
            var vectorDigest = Convert.ToHexString(SHA256.HashData(SerializeIntent(normalizedVector)));
            Assert.Equal("A29FF7C8936B7926B4BE8D8E5F9E55EA1EF65CF33BAD4BAB354C98D8ABC8A8D0", vectorDigest);

            var head = await ReadHeadAsync(adminConnectionString, schema, streamId);
            using var keyV1 = RSA.Create(2048);
            var keyV1Public = Convert.ToBase64String(keyV1.ExportSubjectPublicKeyInfo());
            var checkpointV1 = CreateCheckpoint(streamId, head.Sequence, head.Hash, "test-key-v1", keyV1);
            await File.WriteAllTextAsync(tempCheckpoint, JsonSerializer.Serialize(checkpointV1));
            var reloadedCheckpointV1 = JsonSerializer.Deserialize<CheckpointEnvelope>(
                await File.ReadAllTextAsync(tempCheckpoint))!;
            Assert.True(VerifyCheckpoint(reloadedCheckpointV1, "test-key-v1", keyV1Public));

            // Rotation proof pins trusted public keys out-of-band from the checkpoint.
            using var keyV2 = RSA.Create(2048);
            var keyV2Public = Convert.ToBase64String(keyV2.ExportSubjectPublicKeyInfo());
            var checkpointV2 = CreateCheckpoint(streamId, head.Sequence, head.Hash, "test-key-v2", keyV2);
            Assert.True(VerifyCheckpoint(checkpointV1, "test-key-v1", keyV1Public));
            Assert.True(VerifyCheckpoint(checkpointV2, "test-key-v2", keyV2Public));
            Assert.False(VerifyCheckpoint(checkpointV2, "test-key-v1", keyV1Public));
            Assert.NotEqual(checkpointV1.KeyId, checkpointV2.KeyId);

            var badSignature = checkpointV2 with { Signature = Convert.ToBase64String(new byte[256]) };
            Assert.False(VerifyCheckpoint(badSignature, "test-key-v2", keyV2Public));
            Assert.True(await VerifyChainAsync(verifierConnection, schema, streamId,
                checkpointV2, "test-key-v2", keyV2Public));
            await AssertDeniedAsync(verifierConnection,
                $"UPDATE {Q(schema)}.audit_stream_heads SET last_sequence = last_sequence;");

            // Tamper and a sequence gap are injected only by the disposable DB owner.
            // The read-only verifier must return a failed result, never repair the ledger.
            var originalMetadata = await ReadMetadataCanonicalAsync(adminConnectionString, schema, streamId, 1);
            await MutateFirstEventAsync(adminConnectionString, schema, streamId, originalMetadata, delete: false);
            Assert.False(await VerifyChainAsync(verifierConnection, schema, streamId,
                checkpointV2, "test-key-v2", keyV2Public));
            await MutateFirstEventAsync(adminConnectionString, schema, streamId, originalMetadata, delete: false);
            Assert.True(await VerifyChainAsync(verifierConnection, schema, streamId,
                checkpointV2, "test-key-v2", keyV2Public));
            await MutateFirstEventAsync(adminConnectionString, schema, streamId, originalMetadata, delete: true);
            Assert.False(await VerifyChainAsync(verifierConnection, schema, streamId,
                checkpointV2, "test-key-v2", keyV2Public));
        }
        finally
        {
            try
            {
                await CleanupDisposableSchemaAsync(adminConnectionString, schema, owner,
                    writerGroup, writerA, writerB, verifierRole);
            }
            finally
            {
                if (File.Exists(tempCheckpoint))
                    File.Delete(tempCheckpoint);
            }
        }
    }

    private static async Task CreateDisposableSchemaAsync(
        string adminConnectionString, string schema, string owner, string writerGroup,
        string writerA, string writerB, string verifier)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        var sql = $"""
            CREATE ROLE {Q(owner)} NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
            CREATE ROLE {Q(writerGroup)} NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
            CREATE ROLE {Q(writerA)} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE INHERIT;
            CREATE ROLE {Q(writerB)} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE INHERIT;
            CREATE ROLE {Q(verifier)} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
            GRANT {Q(writerGroup)} TO {Q(writerA)};
            GRANT {Q(writerGroup)} TO {Q(writerB)};
            CREATE SCHEMA {Q(schema)} AUTHORIZATION {Q(owner)};
            CREATE TABLE {Q(schema)}.audit_stream_heads (
                stream_id uuid PRIMARY KEY,
                last_sequence bigint NOT NULL CHECK (last_sequence >= 0),
                head_hash bytea NOT NULL CHECK (octet_length(head_hash) = 32)
            );
            CREATE TABLE {Q(schema)}.audit_events (
                event_id uuid PRIMARY KEY,
                stream_id uuid NOT NULL,
                stream_sequence bigint NOT NULL CHECK (stream_sequence > 0),
                schema_version smallint NOT NULL,
                canonicalization_version smallint NOT NULL,
                event_type_code text NOT NULL,
                occurred_at_utc timestamptz NOT NULL,
                actor_ref text NOT NULL,
                subject_ref text,
                metadata_canonical text NOT NULL,
                intent_digest bytea NOT NULL CHECK (octet_length(intent_digest) = 32),
                previous_event_hash bytea NOT NULL CHECK (octet_length(previous_event_hash) = 32),
                canonical_payload bytea NOT NULL,
                event_hash bytea NOT NULL CHECK (octet_length(event_hash) = 32),
                UNIQUE (stream_id, stream_sequence)
            );
            CREATE FUNCTION {Q(schema)}.reject_audit_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'AuditSecurity PoC ledger is append-only';
            END;
            $$;
            CREATE TRIGGER audit_events_no_update_delete
                BEFORE UPDATE OR DELETE ON {Q(schema)}.audit_events
                FOR EACH ROW EXECUTE FUNCTION {Q(schema)}.reject_audit_mutation();
            CREATE TRIGGER audit_events_no_truncate
                BEFORE TRUNCATE ON {Q(schema)}.audit_events
                FOR EACH STATEMENT EXECUTE FUNCTION {Q(schema)}.reject_audit_mutation();
            ALTER TABLE {Q(schema)}.audit_stream_heads OWNER TO {Q(owner)};
            ALTER TABLE {Q(schema)}.audit_events OWNER TO {Q(owner)};
            ALTER FUNCTION {Q(schema)}.reject_audit_mutation() OWNER TO {Q(owner)};
            REVOKE ALL ON {Q(schema)}.audit_events, {Q(schema)}.audit_stream_heads FROM PUBLIC;
            GRANT USAGE ON SCHEMA {Q(schema)} TO {Q(writerGroup)}, {Q(verifier)};
            CREATE FUNCTION {Q(schema)}.lock_stream_head(p_stream_id uuid)
            RETURNS TABLE(last_sequence bigint, head_hash bytea)
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $fn$
            BEGIN
                RETURN QUERY SELECT h.last_sequence,h.head_hash
                FROM {Q(schema)}.audit_stream_heads AS h
                WHERE h.stream_id=p_stream_id FOR UPDATE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Unknown audit stream'; END IF;
            END;
            $fn$;
            CREATE FUNCTION {Q(schema)}.append_event(
                p_event_id uuid,p_stream_id uuid,p_next_sequence bigint,
                p_schema_version smallint,p_canonicalization_version smallint,
                p_event_type text,p_occurred_at timestamptz,p_actor text,p_subject text,
                p_metadata_canonical text,p_intent_digest bytea,p_previous_hash bytea,
                p_canonical_payload bytea,p_event_hash bytea)
            RETURNS TABLE(sequence bigint,event_hash bytea,is_duplicate boolean)
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $fn$
            DECLARE existing_digest bytea; existing_sequence bigint; existing_hash bytea;
                    current_sequence bigint; current_hash bytea; affected integer;
            BEGIN
                PERFORM pg_advisory_xact_lock(hashtextextended(p_event_id::text,0));
                SELECT e.intent_digest,e.stream_sequence,e.event_hash
                INTO existing_digest,existing_sequence,existing_hash
                FROM {Q(schema)}.audit_events AS e WHERE e.event_id=p_event_id;
                IF FOUND THEN
                    IF existing_digest <> p_intent_digest THEN
                        RAISE EXCEPTION 'EventId replay has different canonical intent';
                    END IF;
                    RETURN QUERY SELECT existing_sequence,existing_hash,TRUE;
                    RETURN;
                END IF;
                SELECT h.last_sequence,h.head_hash
                INTO current_sequence,current_hash
                FROM {Q(schema)}.audit_stream_heads AS h
                WHERE h.stream_id=p_stream_id FOR UPDATE;
                IF NOT FOUND THEN RAISE EXCEPTION 'Unknown audit stream'; END IF;
                IF p_next_sequence <> current_sequence+1 OR p_previous_hash <> current_hash THEN
                    RAISE EXCEPTION 'Stale or forked audit chain head';
                END IF;
                INSERT INTO {Q(schema)}.audit_events(
                    event_id,stream_id,stream_sequence,schema_version,canonicalization_version,
                    event_type_code,occurred_at_utc,actor_ref,subject_ref,metadata_canonical,
                    intent_digest,previous_event_hash,canonical_payload,event_hash)
                VALUES(p_event_id,p_stream_id,p_next_sequence,p_schema_version,p_canonicalization_version,
                    p_event_type,p_occurred_at,p_actor,p_subject,p_metadata_canonical,
                    p_intent_digest,p_previous_hash,p_canonical_payload,p_event_hash);
                UPDATE {Q(schema)}.audit_stream_heads AS h
                SET last_sequence=p_next_sequence,head_hash=p_event_hash
                WHERE h.stream_id=p_stream_id AND h.last_sequence=current_sequence
                    AND h.head_hash=current_hash;
                GET DIAGNOSTICS affected = ROW_COUNT;
                IF affected <> 1 THEN RAISE EXCEPTION 'Audit head compare-and-swap failed'; END IF;
                RETURN QUERY SELECT p_next_sequence,p_event_hash,FALSE;
            END;
            $fn$;
            ALTER FUNCTION {Q(schema)}.lock_stream_head(uuid) OWNER TO {Q(owner)};
            ALTER FUNCTION {Q(schema)}.append_event(uuid,uuid,bigint,smallint,smallint,text,timestamptz,text,text,text,bytea,bytea,bytea,bytea) OWNER TO {Q(owner)};
            REVOKE ALL ON FUNCTION {Q(schema)}.lock_stream_head(uuid) FROM PUBLIC;
            REVOKE ALL ON FUNCTION {Q(schema)}.append_event(uuid,uuid,bigint,smallint,smallint,text,timestamptz,text,text,text,bytea,bytea,bytea,bytea) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION {Q(schema)}.lock_stream_head(uuid) TO {Q(writerGroup)};
            GRANT EXECUTE ON FUNCTION {Q(schema)}.append_event(uuid,uuid,bigint,smallint,smallint,text,timestamptz,text,text,text,bytea,bytea,bytea,bytea) TO {Q(writerGroup)};
            GRANT SELECT ON {Q(schema)}.audit_events, {Q(schema)}.audit_stream_heads TO {Q(verifier)};
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupDisposableSchemaAsync(
        string adminConnectionString, string schema, string owner, string writerGroup,
        string writerA, string writerB, string verifier)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        var sql = $"""
            DROP SCHEMA IF EXISTS {Q(schema)} CASCADE;
            REVOKE {Q(writerGroup)} FROM {Q(writerA)};
            REVOKE {Q(writerGroup)} FROM {Q(writerB)};
            DROP ROLE IF EXISTS {Q(writerA)}, {Q(writerB)}, {Q(verifier)}, {Q(writerGroup)}, {Q(owner)};
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InitializeStreamAsync(string adminConnectionString, string schema, Guid streamId)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"INSERT INTO {Q(schema)}.audit_stream_heads(stream_id,last_sequence,head_hash) VALUES(@id,0,@genesis);",
            connection);
        command.Parameters.AddWithValue("id", streamId);
        command.Parameters.AddWithValue("genesis", GenesisHash);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<AppendResult> AppendAsync(
        string writerConnectionString, string schema, EventIntent incoming,
        bool injectRollbackAfterInsert = false, string? adminConnectionString = null,
        bool terminateBackendAfterInsert = false)
    {
        var intent = Normalize(incoming);
        var intentDigest = SHA256.HashData(SerializeIntent(intent));
        await using var connection = new NpgsqlConnection(writerConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        long priorSequence;
        byte[] priorHash;
        await using (var lockHead = new NpgsqlCommand(
            $"SELECT last_sequence,head_hash FROM {Q(schema)}.lock_stream_head(@stream);",
            connection, transaction))
        {
            lockHead.Parameters.AddWithValue("stream", intent.StreamId);
            await using var reader = await lockHead.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("Audit stream head was not provisioned.");
            priorSequence = reader.GetInt64(0);
            priorHash = reader.GetFieldValue<byte[]>(1);
        }

        var sequenceNumber = checked(priorSequence + 1);
        var payload = SerializeEvent(intent, sequenceNumber, priorHash);
        var eventHashNew = SHA256.HashData(payload);
        long returnedSequence;
        byte[] returnedHash;
        bool duplicate;
        await using (var append = new NpgsqlCommand($"SELECT sequence,event_hash,is_duplicate FROM {Q(schema)}.append_event(@event,@stream,@sequence,@schema_version,@canonical_version,@event_type,@occurred,@actor,@subject,@metadata,@intent_digest,@previous,@payload,@hash);", connection, transaction))
        {
            append.Parameters.AddWithValue("event", intent.EventId);
            append.Parameters.AddWithValue("stream", intent.StreamId);
            append.Parameters.AddWithValue("sequence", sequenceNumber);
            append.Parameters.AddWithValue("schema_version", intent.SchemaVersion);
            append.Parameters.AddWithValue("canonical_version", intent.CanonicalizationVersion);
            append.Parameters.AddWithValue("event_type", intent.EventTypeCode);
            append.Parameters.AddWithValue("occurred", intent.OccurredAtUtc);
            append.Parameters.AddWithValue("actor", intent.ActorRef);
            append.Parameters.AddWithValue("subject", (object?)intent.SubjectRef ?? DBNull.Value);
            append.Parameters.AddWithValue("metadata", intent.MetadataJson);
            append.Parameters.AddWithValue("intent_digest", intentDigest);
            append.Parameters.AddWithValue("previous", priorHash);
            append.Parameters.AddWithValue("payload", payload);
            append.Parameters.AddWithValue("hash", eventHashNew);
            await using var reader = await append.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            returnedSequence = reader.GetInt64(0);
            returnedHash = reader.GetFieldValue<byte[]>(1);
            duplicate = reader.GetBoolean(2);
        }

        if (injectRollbackAfterInsert)
            throw new InjectedRollbackException();

        if (terminateBackendAfterInsert)
        {
            if (string.IsNullOrWhiteSpace(adminConnectionString))
                throw new ArgumentException("Admin connection is required for backend-termination simulation.");
            int backendPid;
            await using (var pidCommand = new NpgsqlCommand("SELECT pg_backend_pid();", connection, transaction))
                backendPid = Convert.ToInt32(await pidCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            await using var admin = new NpgsqlConnection(adminConnectionString);
            await admin.OpenAsync();
            await using var kill = new NpgsqlCommand("SELECT pg_terminate_backend(@pid);", admin);
            kill.Parameters.AddWithValue("pid", backendPid);
            Assert.True((bool)(await kill.ExecuteScalarAsync())!);
            await transaction.CommitAsync();
            throw new InvalidOperationException("PostgreSQL backend termination unexpectedly committed.");
        }

        await transaction.CommitAsync();
        return new AppendResult(returnedSequence, Convert.ToHexString(returnedHash), duplicate);
    }

    private static async Task<bool> VerifyChainAsync(
        string verifierConnectionString, string schema, Guid streamId, CheckpointEnvelope checkpoint,
        string trustedKeyId, string trustedPublicKeyBase64)
    {
        await using var connection = new NpgsqlConnection(verifierConnectionString);
        await connection.OpenAsync();
        var expectedSequence = 1L;
        var expectedPrevious = GenesisHash;
        await using (var read = new NpgsqlCommand($"""
            SELECT event_id,stream_sequence,schema_version,canonicalization_version,
                   event_type_code,occurred_at_utc,actor_ref,subject_ref,metadata_canonical,
                   intent_digest,previous_event_hash,canonical_payload,event_hash
            FROM {Q(schema)}.audit_events
            WHERE stream_id=@stream ORDER BY stream_sequence;
            """, connection))
        {
            read.Parameters.AddWithValue("stream", streamId);
            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var eventId = reader.GetGuid(0);
                var sequence = reader.GetInt64(1);
                var schemaVersion = reader.GetInt16(2);
                var canonicalVersion = reader.GetInt16(3);
                var eventType = reader.GetString(4);
                var occurred = reader.GetFieldValue<DateTimeOffset>(5);
                var actor = reader.GetString(6);
                var subject = reader.IsDBNull(7) ? null : reader.GetString(7);
                var metadataCanonical = reader.GetString(8);
                var intentDigest = reader.GetFieldValue<byte[]>(9);
                var previous = reader.GetFieldValue<byte[]>(10);
                var canonicalPayload = reader.GetFieldValue<byte[]>(11);
                var storedHash = reader.GetFieldValue<byte[]>(12);

                if (sequence != expectedSequence ||
                    !CryptographicOperations.FixedTimeEquals(previous, expectedPrevious))
                    return false;

                SortedDictionary<string, string> metadata;
                try
                {
                    metadata = ParseCanonicalMetadata(metadataCanonical);
                }
                catch (Exception exception) when (exception is FormatException or InvalidDataException or ArgumentException)
                {
                    return false;
                }
                var intent = Normalize(new EventIntent(eventId, streamId, eventType, occurred,
                    actor, subject, metadata, schemaVersion, canonicalVersion));
                var recomputedIntent = SHA256.HashData(SerializeIntent(intent));
                var recomputedPayload = SerializeEvent(intent, sequence, expectedPrevious);
                var recomputedHash = SHA256.HashData(recomputedPayload);

                if (!CryptographicOperations.FixedTimeEquals(intentDigest, recomputedIntent) ||
                    !canonicalPayload.AsSpan().SequenceEqual(recomputedPayload) ||
                    !CryptographicOperations.FixedTimeEquals(storedHash, recomputedHash))
                    return false;

                expectedPrevious = storedHash;
                expectedSequence++;
            }
        }

        var head = await ReadHeadAsync(verifierConnectionString, schema, streamId);
        var verifiedCount = expectedSequence - 1;
        return checkpoint.StreamId == streamId.ToString("N") &&
            checkpoint.LastSequence == verifiedCount &&
            Convert.FromHexString(checkpoint.HeadHash).AsSpan().SequenceEqual(head.Hash) &&
            head.Sequence == verifiedCount &&
            CryptographicOperations.FixedTimeEquals(expectedPrevious, head.Hash) &&
            VerifyCheckpoint(checkpoint, trustedKeyId, trustedPublicKeyBase64);
    }

    private static CheckpointEnvelope CreateCheckpoint(
        Guid streamId, long sequence, byte[] headHash, string keyId, RSA signingKey)
    {
        var createdAt = NormalizeTimestamp(DateTimeOffset.UtcNow);
        var bytes = SerializeCheckpoint(streamId, sequence, headHash, keyId, createdAt);
        var signature = signingKey.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return new CheckpointEnvelope(streamId.ToString("N"), sequence, Convert.ToHexString(headHash),
            keyId, createdAt.ToString("O", CultureInfo.InvariantCulture),
            Convert.ToBase64String(signature));
    }

    private static bool VerifyCheckpoint(
        CheckpointEnvelope envelope, string trustedKeyId, string trustedPublicKeyBase64)
    {
        if (!string.Equals(envelope.KeyId, trustedKeyId, StringComparison.Ordinal))
            return false;
        using var publicKey = RSA.Create();
        publicKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(trustedPublicKeyBase64), out _);
        var bytes = SerializeCheckpoint(Guid.ParseExact(envelope.StreamId, "N"), envelope.LastSequence,
            Convert.FromHexString(envelope.HeadHash), envelope.KeyId,
            DateTimeOffset.Parse(envelope.CreatedAtUtc, CultureInfo.InvariantCulture));
        return publicKey.VerifyData(bytes, Convert.FromBase64String(envelope.Signature),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
    }

    private static byte[] SerializeCheckpoint(Guid streamId, long sequence, byte[] headHash,
        string keyId, DateTimeOffset createdAt)
    {
        using var stream = new MemoryStream();
        WriteString(stream, "MDGSM-AUDIT-CHECKPOINT-V1");
        WriteString(stream, streamId.ToString("N"));
        WriteInt64(stream, sequence);
        WriteBytes(stream, headHash);
        WriteString(stream, keyId);
        WriteString(stream, FormatTimestamp(createdAt));
        return stream.ToArray();
    }

    private static EventIntent NewIntent(Guid streamId, string suffix, IDictionary<string, string> metadata) =>
        new(Guid.NewGuid(), streamId, "security.test", DateTimeOffset.UtcNow,
            "test:independent-writer", "subject:test", new SortedDictionary<string, string>(metadata, StringComparer.Ordinal));

    private static EventIntent Normalize(EventIntent value) =>
        value with
        {
            EventTypeCode = NormalizeText(value.EventTypeCode),
            OccurredAtUtc = NormalizeTimestamp(value.OccurredAtUtc),
            ActorRef = NormalizeText(value.ActorRef),
            SubjectRef = value.SubjectRef is null ? null : NormalizeText(value.SubjectRef),
            MetadataJson = value.MetadataJson ?? CanonicalMetadata(value.Metadata)
        };

    private static string CanonicalMetadata(IDictionary<string, string> metadata)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in metadata)
        {
            var key = NormalizeText(item.Key);
            var value = NormalizeText(item.Value);
            if (!sorted.TryAdd(key, value))
                throw new InvalidOperationException("Metadata keys collide after NFC normalization.");
        }
        return string.Join(";", sorted.Select(pair =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Key)) + ":" +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value))));
    }

    private static SortedDictionary<string, string> ParseCanonicalMetadata(string canonical)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (canonical.Length == 0) return result;
        foreach (var pair in canonical.Split(';'))
        {
            var separator = pair.IndexOf(':');
            if (separator <= 0) throw new InvalidDataException("Malformed canonical metadata.");
            var key = Encoding.UTF8.GetString(Convert.FromBase64String(pair[..separator])).Normalize(NormalizationForm.FormC);
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(pair[(separator + 1)..])).Normalize(NormalizationForm.FormC);
            if (!result.TryAdd(key, value)) throw new InvalidDataException("Duplicate canonical metadata key.");
        }
        if (!string.Equals(CanonicalMetadata(result), canonical, StringComparison.Ordinal))
            throw new InvalidDataException("Metadata is not in canonical order/encoding.");
        return result;
    }
    private static string NormalizeText(string value) => value.Normalize(NormalizationForm.FormC);

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utcTicks = value.ToUniversalTime().Ticks;
        return new DateTimeOffset(utcTicks - (utcTicks % 10), TimeSpan.Zero);
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        NormalizeTimestamp(value).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);

    private static byte[] SerializeIntent(EventIntent value)
    {
        using var stream = new MemoryStream();
        WriteString(stream, "MDGSM-AUDIT-INTENT-V1");
        WriteString(stream, value.EventId.ToString("N"));
        WriteString(stream, value.StreamId.ToString("N"));
        WriteInt32(stream, value.SchemaVersion);
        WriteInt32(stream, value.CanonicalizationVersion);
        WriteString(stream, value.EventTypeCode);
        WriteString(stream, FormatTimestamp(value.OccurredAtUtc));
        WriteString(stream, value.ActorRef);
        WriteNullableString(stream, value.SubjectRef);
        WriteString(stream, value.MetadataJson);
        return stream.ToArray();
    }

    private static byte[] SerializeEvent(EventIntent value, long sequence, byte[] previousHash)
    {
        using var stream = new MemoryStream();
        WriteString(stream, "MDGSM-AUDIT-EVENT-V1");
        WriteString(stream, value.EventId.ToString("N"));
        WriteString(stream, value.StreamId.ToString("N"));
        WriteInt64(stream, sequence);
        WriteInt32(stream, value.SchemaVersion);
        WriteInt32(stream, value.CanonicalizationVersion);
        WriteString(stream, value.EventTypeCode);
        WriteString(stream, FormatTimestamp(value.OccurredAtUtc));
        WriteString(stream, value.ActorRef);
        WriteNullableString(stream, value.SubjectRef);
        WriteString(stream, value.MetadataJson);
        WriteBytes(stream, previousHash);
        return stream.ToArray();
    }

    private static void WriteString(Stream stream, string value) =>
        WriteBytes(stream, Encoding.UTF8.GetBytes(NormalizeText(value)));

    private static void WriteNullableString(Stream stream, string? value)
    {
        stream.WriteByte(value is null ? (byte)0 : (byte)1);
        if (value is not null)
            WriteString(stream, value);
    }

    private static void WriteBytes(Stream stream, byte[] value)
    {
        WriteInt32(stream, value.Length);
        stream.Write(value);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static async Task<(long Sequence, byte[] Hash)> ReadHeadAsync(
        string connectionString, string schema, Guid streamId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT last_sequence,head_hash FROM {Q(schema)}.audit_stream_heads WHERE stream_id=@stream;",
            connection);
        command.Parameters.AddWithValue("stream", streamId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetFieldValue<byte[]>(1));
    }

    private static async Task<string> ReadMetadataCanonicalAsync(
        string connectionString, string schema, Guid streamId, long sequence)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT metadata_canonical FROM {Q(schema)}.audit_events WHERE stream_id=@stream AND stream_sequence=@sequence;",
            connection);
        command.Parameters.AddWithValue("stream", streamId);
        command.Parameters.AddWithValue("sequence", sequence);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task MutateFirstEventAsync(
        string adminConnectionString, string schema, Guid streamId,
        string originalMetadata, bool delete)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using (var disable = new NpgsqlCommand(
            $"ALTER TABLE {Q(schema)}.audit_events DISABLE TRIGGER USER;", connection))
            await disable.ExecuteNonQueryAsync();
        try
        {
            var sql = delete
                ? $"DELETE FROM {Q(schema)}.audit_events WHERE stream_id=@stream AND stream_sequence=1;"
                : $"UPDATE {Q(schema)}.audit_events SET metadata_canonical=@metadata WHERE stream_id=@stream AND stream_sequence=1;";
            await using var mutate = new NpgsqlCommand(sql, connection);
            mutate.Parameters.AddWithValue("stream", streamId);
            if (!delete) mutate.Parameters.AddWithValue("metadata", originalMetadata == "tampered" ? "changed" : "tampered");
            await mutate.ExecuteNonQueryAsync();
        }
        finally
        {
            await using var enable = new NpgsqlCommand(
                $"ALTER TABLE {Q(schema)}.audit_events ENABLE TRIGGER USER;", connection);
            await enable.ExecuteNonQueryAsync();
        }
    }

    private static async Task<bool> EventExistsAsync(string connectionString, string schema, Guid eventId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT EXISTS(SELECT 1 FROM {Q(schema)}.audit_events WHERE event_id=@event);", connection);
        command.Parameters.AddWithValue("event", eventId);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task AssertDeniedAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("42501", exception.SqlState);
    }

    private static string WithUser(string adminConnectionString, string username) =>
        new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = username,
            Password = null,
            IncludeErrorDetail = false
        }.ConnectionString;

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private sealed record EventIntent(
        Guid EventId, Guid StreamId, string EventTypeCode, DateTimeOffset OccurredAtUtc,
        string ActorRef, string? SubjectRef, IDictionary<string, string> Metadata,
        short SchemaVersion = 1, short CanonicalizationVersion = 1,
        string? MetadataJson = null);

    private sealed record AppendResult(long Sequence, string EventHash, bool Duplicate);
    private sealed record CheckpointEnvelope(
        string StreamId, long LastSequence, string HeadHash, string KeyId,
        string CreatedAtUtc, string Signature);

    private sealed class InjectedRollbackException : Exception { }
}
