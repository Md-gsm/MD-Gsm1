# O-21 AuditSecurity Hash Chain — Implementation Readiness and Proof Contract

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O21-HASHCHAIN-IMPLEMENTATION-READINESS-001 |
| Version / Date | 0.4 / 2026-11-21 |
| Baseline | MDG-ARCH-MASTER-001 v3.0.3 N.7; Stage 08 Security Design v0.1; Reconciliation Addendum v0.1 |
| Status | DISPOSABLE POSTGRESQL POC PASSED IN 37/37 INTEGRATION SUITE ON SHA 7ef35cca… — PRODUCTION/INDEPENDENT ACCEPTANCE OPEN |
| Execution | A disposable-only PostgreSQL PoC test source was added to PR #86. It first passed on SHA `8e0b3e10a96e6e605be6513cc22e19673d8c89ac` (workflow `44910227811`), where a separate IdentityAccess test failed. After that unrelated IdentityAccess assertion was corrected, the **same unchanged Hash Chain test source blob** passed again on SHA `7ef35cca3b3c86757ce1893656f9b6f617cdb4d9` in workflow `44912452300`, PostgreSQL job `136871018414`, as part of a complete **37 passed / 0 failed** integration suite. No production schema, migration, key, checkpoint, WORM storage or production configuration was created. |

## 1. Current implementation finding

The current repository tree contains the Stage 08 design documents but no AuditSecurity module, canonical AuditEvent stream implementation, hash-chain writer, checkpoint signer/exporter, independent verifier or operational runbook. The IdentityAccess migration has an append-only `identity.security_audit_events` table and a trigger rejecting UPDATE/DELETE, but that table is not the canonical cross-module AuditSecurity ledger and does not implement N.7 Hash Chain requirements.

The current IdentityAccess security audit table must not be relabeled as the canonical Hash Chain. IdentityAccess and other modules submit immutable audit intents through transactional outboxes; AuditSecurity alone owns canonical event ordering, chain heads, checkpoints, verification and retention.

## 2. Required component boundaries

| Component | Sole responsibility | Must not do |
|---|---|---|
| AuditSecurity domain | Canonical event envelope, deterministic canonicalization version, stream sequence and hash invariants | Own account/session policy or business-module private data |
| AuditSecurity persistence | Append event + sequence + predecessor + chain head atomically; idempotent EventId handling | Update/delete committed canonical events |
| AuditSecurity verifier | Read-only recomputation, continuity/signature/anchor checks and evidence report | Repair, rewrite, reseed or truncate the chain |
| Platform checkpoint adapter | Sign checkpoint using externally managed asymmetric key; export to independent append-only/WORM trust domain | Store private signing key in PostgreSQL, application config or the same backup domain |
| Source module outboxes | Durable immutable audit intents in the source business transaction | Create parallel canonical AuditEvent ledgers |
| Operations | Monitor, alert, retain evidence, run restore verification and incident process | Automatically bless a mismatched or rolled-back chain |

## 3. Canonical event and serialization contract

The canonical envelope must contain:
- EventId (stable idempotency key);
- AuditStreamId and committed StreamSequence;
- SchemaVersion, CanonicalizationVersion and hash algorithm identifier;
- EventTypeCode, OccurredAtUtc and RecordedAtUtc;
- typed ActorRef and optional SubjectRef;
- source module and source event identity;
- CorrelationId and CausationId;
- outcome and allow-listed, non-secret metadata;
- PreviousEventHash and EventHash.

Canonicalization must be deterministic and versioned. The specification must fix field order, UTF-8 encoding, Unicode normalization, null representation, number encoding, UTC timestamp format, string escaping and prohibited/allowed metadata. It must not use locale-sensitive formatting, serializer defaults that can change silently, unordered maps or floating-point representations for identity-critical fields.

The exact canonical byte specification and published test vectors are a hard prerequisite to implementation. At least one golden vector must cover each supported event schema version, optional/null fields, non-ASCII Persian/Arabic text, escaped characters, UTC precision and numeric edge cases. Cross-runtime implementations must produce byte-identical canonical payloads and SHA-256 hashes.

Hash construction must be domain-separated and versioned, with the previous hash and canonical envelope included. Genesis uses a documented fixed marker; it must not silently use an implicit all-zero hash. The algorithm is SHA-256 as specified by Stage 08; no custom cryptographic algorithm is permitted.

Secrets, password hashes, raw bearer tokens, OTPs, recovery codes, HMAC keys, connection strings and unnecessary raw PII are prohibited from canonical payloads.

## 4. Two-independent-writer transaction protocol

N.7 requires proof using two independent writers against the same AuditStreamId. A single process, a single connection or a sequential unit test is insufficient.

Each append must:
1. Validate the immutable event intent and supported schema/canonicalization version.
2. Begin a PostgreSQL transaction.
3. Acquire the stream-head row lock for the target AuditStreamId.
4. Resolve EventId idempotency. Same EventId + same canonical digest returns the original committed event; same EventId + different digest fails closed.
5. Read the committed chain head after lock acquisition.
6. Allocate the next committed StreamSequence; calculate PreviousEventHash and EventHash.
7. Insert the canonical event and update the stream head in the same transaction.
8. Commit both atomically. Any error/rollback leaves neither a committed event nor an advanced head.

Unique constraints must enforce EventId and (AuditStreamId, StreamSequence). Concurrent appends serialize only within the same stream. No gap skipping, fork repair, sequence reseeding or automatic tail deletion is allowed.

### Required failure-injection scenarios

| Scenario | Writer A | Writer B | Required invariant |
|---|---|---|---|
| Parallel commit | Holds stream lock and commits sequence N | Waits, then commits sequence N+1 | One linear chain; B references A |
| Rollback | Locks stream, allocates tentative N, then rolls back | Appends after rollback | N is not committed; next committed event uses the next valid committed sequence with no fork |
| Process crash before commit | Terminates after event/head writes but before COMMIT | Retries/appends after recovery | PostgreSQL rolls back both writes; one valid head |
| Connection loss during COMMIT | Commit outcome deliberately made unknown | Retries same EventId | Read-after-unknown-commit resolves stable outcome; no duplicate or fork |
| Same EventId / same payload | Appends first copy | Replays same event | Returns original event, no second sequence |
| Same EventId / different payload | Appends first copy | Replays changed payload | Fails closed and alerts; no mutation |
| Out-of-order delivery | Receives later source intent first | Receives earlier source intent later | Ordering policy is explicit per stream; stable source event identity prevents silent reorder or duplicate |
| Different streams | Appends to stream A | Appends to stream B | Independent stream heads; no unsupported global order claim |

## 5. Checkpoint trust, key custody and external anchor

A checkpoint must include AuditStreamId, last committed StreamSequence, chain-head hash, schema/canonicalization/hash versions, checkpoint timestamp and previous checkpoint reference.

The signing private key must be Platform-managed and outside PostgreSQL and the application database backup domain. The selected provider/algorithm, key policy, backup/recovery mechanism, custody owners, separation of duties, rotation schedule and revocation process are Platform decisions and are not invented here. Prefer non-exportable key material where supported. No private key or signing secret is placed in source, appsettings, installer, database, CI logs or application telemetry.

The signed checkpoint must be exported to independently controlled append-only/WORM storage whose administrators and credentials are separate from PostgreSQL database administrators. A checkpoint copy in PostgreSQL is not a trust anchor.

Required lifecycle proof:
- initial key provisioning and owner/custodian separation;
- signing authorization and audit trail;
- public-key and key-version retention;
- key rotation without rewriting historical checkpoints;
- backup and recovery of verification material;
- private-key revocation and compromise incident procedure;
- failed signing/export and ambiguous provider outcomes;
- external-anchor availability and integrity;
- restore comparison against the latest independently held checkpoint.

A lost, revoked or compromised signing key must not silently validate old or new history. Any trust epoch transition must be explicit and auditable.

## 6. Independent verifier and runbook

The verifier must be a separately invocable, read-only utility. It must:
- verify deterministic canonical bytes and each EventHash;
- verify sequence continuity and PreviousEventHash;
- detect duplicate EventId and duplicate stream sequence;
- verify checkpoint signatures and retained public-key versions;
- compare restored stream heads with the latest independent external checkpoint;
- report first failing stream/sequence, failure category and evidence digest;
- exit non-zero on any mismatch and never modify source data.

The runbook must define scheduled verification, after-restore verification, incident verification, checkpoint export monitoring, alert ownership, evidence preservation, quarantine, escalation and recovery decision authority. A mismatch freezes trusted export for the affected stream and triggers incident response. There is no automatic repair or re-seeding.

## 6.1 Disposable PostgreSQL PoC candidate added in PR #86

The new integration test is `tests/MDGsm.Domain.Tests/Security/AuditSecurityHashChainPostgresProofTests.cs`. It creates an ephemeral schema and ephemeral role set only inside the marked disposable PostgreSQL test database. It is test code, not an AuditSecurity production module or migration.

Candidate test coverage in source:
- Two independent PostgreSQL LOGIN writers append concurrently into the same stream; their shared NOLOGIN group can execute only the owner-controlled `SECURITY DEFINER` append/lock functions. Writers have no direct table SELECT/INSERT/UPDATE/DELETE/TRUNCATE rights. The functions use a fixed `search_path`, schema-qualified objects and a NOLOGIN owner.
- The append transaction locks the stream head, checks the prior sequence/hash, inserts the event, and advances the head with a compare-and-swap predicate. `EventId` is serialized with an advisory transaction lock; identical intent replay returns the original sequence/hash, while changed intent with the same id fails.
- Canonical intent serialization is versioned, UTF-8/NFC, length-prefixed and ordinally sorted. The envelope includes typed actor/subject identity, source module/event, correlation/causation IDs, outcome, hash algorithm, occurred time, DB-read recorded time, metadata, sequence and predecessor hash.
- The source includes a fixed golden digest vector, two concurrent writers, injected transaction rollback, PostgreSQL backend termination before commit, simulated lost acknowledgement after commit followed by same-EventId replay, and conflicting replay rejection.
- A read-only verifier recomputes canonical intent/event bytes, verifies sequence continuity, predecessor hashes, current chain head and an externally pinned RSA-PSS checkpoint public key. The candidate exercises key-id pinning, old/new test keys and a corrupted signature.
- Tamper and deletion probes modify/remove the first event only as the disposable DB owner; the read-only verifier must detect the modified payload and a sequence gap. The test attempts to restore the original event after the tamper check, before testing the deletion gap.
- Test roles/schema and temporary checkpoint file are cleaned up in `finally`.

Important evidence boundary:
- The checkpoint is written to a local temporary JSON file for test round-trip. This is **not** an independent WORM or external trust-domain anchor. The signing keys are ephemeral test-only RSA keys; they prove format/signature verification, not production key custody, protected key storage, backup, recovery, rotation governance or revocation.
- The candidate obtains a DB time value from a test-only SECURITY DEFINER lock function and binds it into the event envelope. The current candidate still passes the recorded timestamp from the test writer into the append function; production must make timestamp assignment and hash construction non-forgeable at the trusted persistence boundary before claiming authoritative database time.
- No production AuditSecurity module, canonical schema, application/outbox adapter, durable outbox retry, checkpoint scheduler/provider, WORM integration, key registry, independent deployed verifier, alerts or runbook has been implemented.
- The Hash Chain PoC integration test is **PASS on the exact unchanged PoC test-file blob** `4c90263f019c27f5063f0cbe1c1793be44bfc50c` within the successful 37/37 PostgreSQL 18 suite on SHA `7ef35cca3b3c86757ce1893656f9b6f617cdb4d9` (workflow `44912452300`, job `136871018414`). The current PR head has later documentation/checksum-only commits; a fresh final-head package run is pending. This is a test-source PASS, not production AuditSecurity acceptance. Independent security review and all production acceptance requirements remain open.

### PoC-to-acceptance mapping

| Requirement | Candidate coverage in source | Status |
|---|---|---|
| Canonical serialization / golden vector | Versioned binary UTF-8/NFC envelope, full typed source/correlation fields and fixed vector | SOURCE ADDED — CI PENDING |
| Concurrent independent writers / no fork | Two separate LOGINs, 40 events on one stream, locked head and compare-and-swap | SOURCE ADDED — CI PENDING |
| Rollback / crash / replay / unknown outcome | Injected rollback, backend termination, lost acknowledgement + same EventId replay, changed-intent conflict | SOURCE ADDED — CI PENDING |
| Read-only verification / tamper / gap | Separate SELECT-only verifier and mutation/deletion detection | SOURCE ADDED — CI PENDING |
| Signed checkpoint / trust pinning | Test RSA-PSS checkpoint and out-of-band public-key pinning | SOURCE ADDED — CI PENDING; TEST ONLY |
| External WORM / independent anchor | None | OPEN — NOT IMPLEMENTED |
| Production key custody / backup / restore / revocation | None; only ephemeral test keys | OPEN — NOT IMPLEMENTED |
| Outbox durability / outage replay / source transaction integration | None | OPEN — NOT IMPLEMENTED |
| Alerts, operations runbook, restore continuity | None | OPEN — NOT IMPLEMENTED |
## 6.2 Recorded PoC execution evidence — workflow run 44910227811

| Field | Evidence |
|---|---|
| Repository / PR | `Md-gsm/MD-Gsm1`, PR #86 (Draft) |
| Tested source SHA | `8e0b3e10a96e6e605be6513cc22e19673d8c89ac` |
| Workflow run | `44910227811` — MD-Gsm Persons Module Gates |
| PostgreSQL job | `persons-postgres-migrations`, job `136864370902` |
| Integration result | 37 total; 36 passed; one failed test was `IdentityAccessHttpEndToEndTests.Http_login_issues_opaque_session_and_forced_password_change_unlocks_only_after_relogin` at its runtime privilege assertion. The Hash Chain PoC test was among the passing tests. |
| PoC test source identity | The PoC test file blob at the tested SHA is `4c90263f019c27f5063f0cbe1c1793be44bfc50c`; the current PR head contains the identical blob. |

The PoC's successful execution proves the disposable concurrency/rollback/backend-termination/replay/checkpoint/verifier test code completed on PostgreSQL 18 for that file version. It does **not** prove production AuditSecurity readiness, external WORM anchoring, key custody, outbox delivery, or independent attestation. The failed IdentityAccess test from that run was corrected separately; the latest full package SHA must still pass the current end-to-end job.
## 6.3 Full integration-suite confirmation on SHA 7ef35cca…

| Field | Result |
|---|---|
| Source SHA | `7ef35cca3b3c86757ce1893656f9b6f617cdb4d9` |
| Workflow | `44912452300` — MD-Gsm Persons Module Gates |
| PostgreSQL job | `136871018414` — `persons-postgres-migrations` |
| Integration suite | **37 passed / 0 failed / 0 skipped** |
| Hash Chain PoC test source blob | `4c90263f019c27f5063f0cbe1c1793be44bfc50c` — identical to tested source blob in previous run |

This later 37/37 run is the stronger execution evidence. It shows the PoC test was successful together with the runtime_app IdentityAccess hardening tests. The run does not satisfy the external WORM/key custody/outbox/runbook/independent-review acceptance criteria. The latest PR head after documentation/checksum successors is a different SHA and its package CI must finish before the Stage 08 package is considered fully verified.
## 7. Required test catalogue — NOT RUN

| ID | Test | Pass condition |
|---|---|---|
| O21-T01 | Canonical golden vectors | All implementations produce byte-identical bytes and SHA-256 |
| O21-T02 | Two independent concurrent writers | One contiguous stream with correct predecessor links |
| O21-T03 | Rollback atomicity | Failed transaction commits neither event nor head |
| O21-T04 | Crash before commit | Restart leaves one valid prior head; retry is safe |
| O21-T05 | Unknown commit outcome | Stable EventId resolves outcome without duplicate append |
| O21-T06 | EventId replay | Same digest deduplicates; changed digest fails closed |
| O21-T07 | Tamper/reorder/delete/gap | Verifier identifies first invalid stream/sequence |
| O21-T08 | Checkpoint signature | Valid signatures verify; invalid/unknown key version fails closed |
| O21-T09 | External anchor separation | DB-only administrator cannot replace external WORM checkpoint |
| O21-T10 | Key custody and backup | Recovery of verification trust is proven without exposing private key |
| O21-T11 | Rotation and revocation | Historical checkpoints remain verifiable; revoked key cannot sign new checkpoints |
| O21-T12 | Restore continuity | Restored head matches latest independent checkpoint or stream is quarantined |
| O21-T13 | Read-only verifier | Verification does not mutate events, heads, checkpoints or source DB |
| O21-T14 | Outbox outage/replay | Durable retry, dedupe and no silent event loss |
| O21-T15 | Secret/PII exclusion | No prohibited secret or unnecessary raw PII enters canonical events |
| O21-T16 | Alerts/runbook | Gap, corruption, checkpoint lag and provider failures alert the assigned operator |

All tests above are specifications only and NOT RUN / NOT PASS.

## 8. External decisions required before executable implementation

1. Platform selects the supported asymmetric signing provider and algorithm; key custody, backup, recovery, rotation and revocation owners.
2. Platform/owner selects the independent append-only/WORM storage trust domain and access separation.
3. Platform/owner sets checkpoint cadence, retention and legal-hold behavior; no duration is invented.
4. Architecture confirms stream partitioning and whether any future global ordering is required. Current design claims stream-local ordering only.
5. Owner confirms the canonical event metadata allow-list and data-minimization policy.
6. O-20 canonical database roles and grants must be resolved before production AuditSecurity database privileges are provisioned.

## 9. Status and non-authorization

O-21 remains **OPEN / disposable PoC integration test PASS in the 37/37 integration suite; production and independent acceptance OPEN**. This v0.2 documents candidate test source only; it does not certify a successful PostgreSQL execution or satisfy the external anchor, platform key-operations, outbox, operational or independent-review requirements. No canonical AuditSecurity schema, DDL, migration, key, checkpoint, WORM bucket, production secret, grant, runtime activation, audit purge, G1 activation, Posting Release or Go-Live is authorized by this document.

**END — MDG-SECURITY-O21-HASHCHAIN-IMPLEMENTATION-READINESS-001 v0.4**
