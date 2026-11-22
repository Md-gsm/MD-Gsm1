# O-21 AuditSecurity Hash Chain — Implementation Readiness and Proof Contract

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O21-HASHCHAIN-IMPLEMENTATION-READINESS-001 |
| Version / Date | 0.1 / 2026-11-21 |
| Baseline | MDG-ARCH-MASTER-001 v3.0.3 N.7; Stage 08 Security Design v0.1; Reconciliation Addendum v0.1 |
| Status | IMPLEMENTATION CONTRACT — PLATFORM KEY/ANCHOR DECISIONS AND CODE/TEST EVIDENCE OPEN |
| Execution | No schema, code, key, checkpoint, WORM storage or production configuration created |

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

O-21 remains **OPEN / NOT IMPLEMENTED / NOT RUN**. This document is an implementation/proof contract, not a PoC or acceptance. No canonical AuditSecurity schema, DDL, migration, key, checkpoint, WORM bucket, production secret, grant, runtime activation, audit purge, G1 activation, Posting Release or Go-Live is authorized by this document.

**END — MDG-SECURITY-O21-HASHCHAIN-IMPLEMENTATION-READINESS-001 v0.1**
