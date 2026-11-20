# MDG Security Stage 08 — Database, Session, Recovery and Audit Hash Chain

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-001 |
| Version | 0.1 |
| Date | 2026-11-20 |
| Status | INTEGRATED SECURITY DESIGN — REVIEW REQUIRED — NO IMPLEMENTATION AUTHORIZATION |
| Sole architecture baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Governing decisions | ADR-0012, ADR-0013, ADR-0006 |
| Related contracts | IdentityAccess schema design; IdentityAccess owner decisions; Inventory PD v0.3 |
| Target | Windows desktop clients, central API, PostgreSQL 18 |

## 1. Authority, scope and precedence

This document consolidates the Stage 08 security design for database access, IdentityAccess sessions, account recovery and tamper-evident audit chaining. It is subordinate to MDG-ARCH-MASTER-001 v3.0.3 and accepted owner decisions. It does not amend the architecture master.

Where historical IdentityAccess notes conflict, the latest owner decision recorded in ADR-0013 and its 2026-11-07 session correction controls. In particular: sessions have no fixed absolute TTL and no approved per-account concurrent-session cap; the approved system-wide limit starts at five distinct active accounts; heartbeat is 30 seconds and lease is 180 seconds. Recovery is email-only for this phase, is not MFA, and is not operationally enabled merely by this design. SMS and offline recovery codes are outside this phase.

This is design-only. It does not authorize code changes, schema/DDL, migration reservation or execution, production credentials, listener activation, feature-flag changes, seed, G1 activation, data retention/purge, or Go-Live.

## 2. Trust boundaries and ownership

1. Desktop is an untrusted presentation/client. It never connects directly to PostgreSQL and never decides identity, authorization, scope, session validity, recovery eligibility or audit sequence.
2. API is the authentication and authorization enforcement boundary. It accepts TLS-protected requests, derives ActorId only from validated server-side principal/session state, and rechecks authoritative state for every protected request.
3. PostgreSQL is authoritative for accounts, credential version, session digest/status/lease, login/recovery throttles, permission assignments and durable local security/audit state.
4. IdentityAccess owns accounts, credentials, login attempts, sessions, recovery challenges and authorization decisions.
5. AuditSecurity is the sole owner of canonical AuditEvent storage, event ordering, hash-chain state, checkpoints, verification and audit retention policy. IdentityAccess and other modules submit immutable audit intents through transactional outboxes; they do not create parallel canonical audit ledgers.
6. Platform owns deployment secrets, TLS certificates, database service identity, backup encryption, external checkpoint storage and alert delivery. No module may read another module's secrets or write its tables directly.

Cross-module calls use versioned contracts and stable event IDs. No module may infer another module's private schema or bypass its API/contract.

## 3. PostgreSQL security model

### 3.1 Role separation

The design requires distinct non-interchangeable database identities:

- `mdgsm_db_owner`: NOLOGIN owner of schemas/objects; used only by controlled provisioning, never by API or worker processes.
- `mdgsm_migrator`: short-lived deployment identity with narrowly scoped DDL rights for an explicitly approved migration window; credentials are not shipped to desktop or runtime.
- `mdgsm_api_runtime`: NOLOGIN or managed runtime identity with only required EXECUTE and narrowly scoped DML on approved functions/tables; no ownership, CREATE, ALTER, DROP, TRUNCATE, role membership or superuser.
- `mdgsm_worker_runtime`: separate identity for approved background work; cannot impersonate API or migration owner.
- `mdgsm_audit_ingest`: restricted identity for AuditSecurity ingestion/outbox acknowledgement only; no general audit update/delete.
- `mdgsm_readonly_support`: optional, time-bound, approved diagnostic role; no access to password hashes, token digests, recovery digests, raw PII or secret material by default.

Exact grants are not specified here. Each owning module must publish a privilege matrix before DDL. `PUBLIC` privileges are revoked for sensitive schemas/functions; default privileges are explicitly controlled. Runtime credentials are rotated through the Platform secret mechanism and never embedded in source, appsettings, installer, migration, logs or support bundles.

### 3.2 Enforcement and transport

- PostgreSQL listens only on approved server interfaces; host firewall allows only the API/administration paths required by ADR-0012. Desktop-to-DB access is prohibited.
- TLS is mandatory for API and database connections across a network. Certificate validation is strict; invalid, expired, hostname-mismatched or untrusted certificates fail closed.
- Use SCRAM-SHA-256 or a stronger supported PostgreSQL authentication method for database service identities; no cleartext password authentication over the network.
- Secrets are injected at runtime from the Platform secret store. Missing, malformed or expired required secrets stop startup; no default/fallback production secret exists.
- Backups and exported audit checkpoints are encrypted before leaving the protected host boundary. Restore procedures verify integrity and access controls before service resumes.
- Production diagnostics must not expose connection strings, SQL parameters containing secrets, password hashes, bearer tokens, recovery codes, HMAC keys or raw recovery challenges.
- Security configuration changes are versioned, authorized, audited and fail closed when unknown or malformed.

### 3.3 Database enforcement

Sensitive state transitions must be enforced by database constraints and narrowly scoped transaction functions where required; application conventions alone are insufficient. Runtime roles cannot own committed audit records, mutate immutable ledger/audit rows, bypass tenant/company/scope predicates, disable triggers or change security functions. SECURITY DEFINER functions, if accepted by the owning design, require a non-login owner, fixed trusted `search_path` with `pg_temp` last, fully schema-qualified object references, revoked PUBLIC EXECUTE and explicit argument validation.

No broad table grant is accepted as a shortcut. Each module must document every runtime operation, object, principal and transaction boundary. Migration owner and runtime owner are separate identities. No production superuser credential is used by application processes.

## 4. IdentityAccess session contract

### 4.1 Token and persistence

- Login remains username/password only. Email and phone are contact/recovery data, not login identifiers.
- On successful authentication, generate an opaque bearer token using a CSPRNG with at least 256 bits of entropy.
- Return the raw token once over validated TLS. Persist only SHA-256(token) as a fixed 32-byte digest with a unique constraint. Never log, cache, persist, include in telemetry or place in crash artifacts the raw token.
- A token digest is not a password hash and is not interchangeable with the HMAC privacy digest. Random high-entropy bearer tokens use SHA-256; identifiers/source keys requiring privacy-preserving correlation use the separately managed HMAC-SHA-256 key.
- Store SessionId, AccountId, token digest, issued time, last-seen time, lease expiry, revocation time/reason, client-instance opaque ID and credential_version_at_issue. No hardware fingerprint is required.
- Database time in UTC is authoritative. Client time is informational only.

### 4.2 Liveness, concurrency and revocation

- Heartbeat target: every 30 seconds. Session lease: 180 seconds from the last accepted heartbeat.
- A session is valid only when account is enabled, session is not revoked, lease has not expired, and credential_version_at_issue equals the account's current credential_version.
- The program-open + valid-heartbeat model controls session life. No absolute TTL is imposed. No maximum concurrent sessions per account is approved.
- The system-wide initial cap is five distinct active accounts, not five sessions. Multiple sessions for an already-active account do not consume another distinct-account slot.
- Capacity enforcement is transactionally serialized in PostgreSQL across API processes. When full, reject the new distinct account; do not evict an existing user silently.
- Heartbeat extends only the lease. It cannot reverse revocation, account disablement or credential-version mismatch.
- Logout revokes the current session. Password change/reset and account disablement atomically increment credential version where applicable and revoke every session for that account in the same transaction. The next protected request must fail after re-reading PostgreSQL.
- Session validation must not rely on a local cache that can outlive a revocation. Any cache used for performance must never authorize after revocation and requires separate proof; default is no session-validity cache.
- Session, login, lockout and capacity operations are idempotent and race-safe. Unknown transaction outcomes are resolved by stable operation identity and a read-after-unknown-commit procedure, not by issuing a second session blindly.

### 4.3 Lockout and authorization

The accepted ordinary-login policy remains five failed attempts in a 15-minute window followed by a 15-minute account lock; source-based rate limiting is an additional independent control. Login responses must not disclose whether a username exists. All authorization is deny-by-default, granular, server-side and scope-aware; wildcard grants are prohibited. Technician scope comes from authoritative assignment relations, never client-supplied identifiers.

## 5. Account recovery contract

### 5.1 Accepted policy

- Email-only recovery is the owner-approved channel for this phase. It is not MFA and does not provide two independent factors.
- Email OTP lifetime is 600 seconds. Only a cryptographic digest is stored; the raw OTP is delivered once and never logged or persisted.
- A challenge is bound to one account, one purpose and one issuance. It is single-use; expiration, consumption, revocation or replacement makes it unusable.
- Maximum five failed attempts per challenge; then recovery is locked for 24 hours.
- Resend cooldown is at least one hour; at most one send in a rolling hour and five sends in a rolling 24 hours. Exceeding the send limit locks recovery/sending for 24 hours.
- Issuing a replacement challenge revokes the prior open challenge for that account. SMS, SMS provider/SDK/credentials/jobs/tests and offline recovery-code issuance are outside this phase.
- Recovery is not active until all required runtime dependencies and gates below are satisfied.

### 5.2 Safe orchestration

1. The request endpoint returns a generic response independent of account existence or eligibility. It does not reveal whether an email is registered.
2. Eligibility and email proof are checked against server-owned account data. Email ownership verification state must be authoritative; merely having an email string on the account is not proof.
3. Challenge creation, replacement/revocation, attempt counters, cooldown and rolling send quotas are enforced atomically using PostgreSQL time and concurrency-safe account/challenge locks. A source-only throttle must not let an attacker permanently deny recovery to a victim.
4. The delivery adapter uses a replaceable transactional-email/SMTP relay configured by Platform. No vendor or secret is invented here. Delivery failure or ambiguous provider outcome is never treated as proof of delivery or successful recovery.
5. OTP comparison is constant-time over fixed-length digests. Expiry uses `now_utc >= expires_at_utc`; equality is expired. A challenge can be consumed once only using a conditional atomic transition.
6. On successful proof, one transaction changes the password hash, increments credential_version, revokes every account session, sets `must_change_password=true`, consumes/revokes the challenge and records the audit intent. Any failure rolls back the entire state transition.
7. A retry after an unknown commit outcome returns the stable outcome for the same operation and cannot reset the password twice or leave a challenge reusable.
8. Notifications after recovery do not contain the new password, OTP or token; they report that a security change occurred and provide a safe support path.

### 5.3 Recovery dependencies and risk

Recovery remains DISABLED until: trusted endpoint/start-point and abuse controls are approved; email ownership proof exists; SMTP relay and secrets are configured; atomic quotas and delivery orchestration are implemented; concurrency/replay/expiry/provider-failure/unknown-commit tests pass; mailbox-compromise/phishing/forwarding threat analysis is reviewed; independent security reviewer accepts the exact SHA; and the owner grants the relevant release gate. No master password, backdoor, support bypass, SMS fallback or unapproved offline code is allowed.

## 6. AuditSecurity Hash Chain

### 6.1 Purpose and limits

The hash chain makes changes to canonical audit history detectable. It does not itself prevent a privileged database attacker from deleting or rewriting unanchored tail records, does not replace database access controls, does not prove that every business event was emitted, and does not guarantee legal non-repudiation. Those properties require restricted database roles, transactional audit intents, independent signed checkpoints, monitoring and restore tests.

AuditSecurity alone owns canonical AuditEvent rows, chain sequence, checkpoints and verification. Other modules publish immutable audit intents in their own business transaction through a transactional outbox. Delivery is at-least-once; stable EventId deduplication prevents duplicate canonical events.

### 6.2 Canonical event envelope

Each accepted event has: EventId; AuditStreamId; StreamSequence; SchemaVersion; EventTypeCode; OccurredAtUtc; RecordedAtUtc; ActorRef (including typed system principal); SubjectRef when applicable; source module and source event identity; correlation/causation IDs; outcome; allow-listed non-secret metadata; PreviousEventHash; EventHash; and hash algorithm identifier.

Canonical serialization is deterministic and versioned: fixed field order, UTF-8, normalized string rules, explicit null representation, canonical number/time representation and no locale-dependent formatting. The exact canonicalization specification and test vectors are a release prerequisite. Secrets, password hashes, raw tokens, OTPs, recovery codes, HMAC keys, connection strings and unnecessary raw PII are prohibited from event payloads. Sensitive values are represented only by approved opaque references or keyed digests where justified.

### 6.3 Chain construction and concurrency

- Each AuditStreamId has a monotonically increasing committed StreamSequence and one current chain head.
- Appending an event locks the stream-head row, allocates the next sequence transactionally, obtains the prior hash and writes event + new head atomically.
- Unique constraints enforce `(AuditStreamId, StreamSequence)` and EventId. Same EventId/same canonical digest returns the original event; same EventId/different digest fails closed.
- EventHash is SHA-256 over a domain-separated, versioned byte sequence containing the previous hash and canonical event envelope excluding EventHash itself. Genesis uses a documented fixed genesis marker and no implicit zero hash.
- Hash chain order is stream-local. No global ordering across independent streams is claimed. If a future requirement needs global order, it requires an explicit owner decision and concurrency design; timestamps are not substitutes for sequence.
- Concurrent appends serialize only within the same stream. A failed transaction does not advance the committed sequence or chain head. Gaps, duplicate sequence, invalid previous hash or unsupported canonicalization version quarantine the append/stream and alert; no skip-ahead repair is permitted.
- Corrections are new events referencing the prior event; canonical events are never updated or deleted by runtime roles.

### 6.4 Independent checkpoints and key management

- At a configured operational interval, AuditSecurity creates a checkpoint containing stream ID, last committed sequence, chain-head hash, canonicalization/hash versions, checkpoint time and prior checkpoint reference.
- The checkpoint is signed by a Platform-managed asymmetric signing key held outside PostgreSQL and outside the application database backup domain. Private key material is non-exportable where supported; otherwise access is tightly controlled and audited. The algorithm/provider is selected and reviewed by Platform; no custom cryptography is permitted.
- The signed checkpoint is exported to independently controlled append-only/WORM storage with access separation from the database administrators. A local database copy is only a convenience, not the trust anchor.
- Key rotation is versioned. Old public verification keys and key metadata are retained for the full checkpoint-verification period. Rotation does not rewrite prior checkpoints. Compromise triggers incident response, new key generation, a new signed checkpoint epoch and explicit trust-boundary disclosure; it does not silently bless prior history.
- Restore must compare restored stream heads with the latest independently stored signed checkpoints. Missing events, changed hashes, rollback to an older checkpoint, duplicate sequence or mismatch are incidents. Recovery never silently truncates or regenerates a chain to match the restored database.
- Checkpoint frequency, retention and legal hold are Platform/owner decisions. No retention period or legal claim is invented in this document.

### 6.5 Verification and operational response

A read-only verifier recomputes canonical event hashes from genesis or a trusted checkpoint, validates sequence continuity, previous-hash links, EventId uniqueness, checkpoint signatures and external checkpoint continuity. It reports the first failing stream/sequence and evidence digest without modifying source data. Verification runs after restore, after security incidents, on a scheduled cadence and before audit export. Any mismatch freezes further trusted export for the affected stream, preserves evidence, raises a critical alert and requires incident review. No automatic chain rewrite, tail deletion or silent re-seeding is allowed.

## 7. Data lifecycle, privacy and backup

Security audit payloads are allow-listed and minimized. Raw IP addresses are not retained by default; source correlation uses a keyed digest with separately managed key. Retention durations, legal holds, archival and purge are not approved by this document; no purge/archival job may be activated until owner/legal approval and a verifiable retention schedule exist. Audit chain checkpoints must remain verifiable for their approved retention horizon.

Backups include encrypted database data, schema/version metadata, required public verification keys and recovery procedures. Private signing keys and runtime secrets are backed up/escrowed only under Platform-controlled key-management policy, never bundled into database backups. Restore acceptance requires database integrity, role/grant review, session invalidation policy, audit chain verification and reconciliation of pending outboxes against canonical AuditSecurity acknowledgements.

## 8. Failure handling and security invariants

- Fail closed if database identity, TLS validation, required secret, permission scope, session state, challenge state, canonicalization version or chain predecessor cannot be verified.
- Do not expose account existence, token validity detail, OTP validity detail or sensitive database errors to unauthenticated callers.
- Never log secrets or raw credentials; telemetry is allow-listed and redacted before export.
- A database outage must not cause local desktop authorization or offline session acceptance.
- Audit outbox delivery failure does not roll back an already committed business transaction, but it remains durable, observable, retryable and escalated under the outbox contract. No event may be silently dropped.
- No successful password recovery may leave a pre-recovery session valid.
- No audit chain append may commit without the canonical event, sequence and chain head being committed atomically.
- No checkpoint is trusted solely because it is stored in the same PostgreSQL database it is intended to protect.

## 9. Acceptance test catalogue — specifications only

| ID | Required scenario | Pass condition |
|---|---|---|
| SEC-T-001 | Runtime role privilege inventory | No ownership, superuser, DDL or broad PUBLIC access |
| SEC-T-002 | Desktop direct DB attempt | Network and credentials deny access |
| SEC-T-003 | TLS certificate failures | Expired, invalid and hostname mismatch fail closed |
| SEC-T-004 | Secret absence/rotation | Startup fails closed; no secret in artifacts/logs |
| SEC-T-005 | Runtime/migrator separation | Runtime cannot perform migration DDL |
| SEC-T-006 | Token entropy/storage | >=256-bit CSPRNG token; only 32-byte SHA-256 digest stored |
| SEC-T-007 | Token replay/revoke | Revoke/password change/disable invalidates next request |
| SEC-T-008 | Heartbeat/lease boundary | 30s heartbeat, 180s lease, expiry/reconnect race correct |
| SEC-T-009 | Five-account concurrency | Parallel distinct logins never exceed configured cap |
| SEC-T-010 | Existing account second session | Does not consume another distinct-account slot |
| SEC-T-011 | Login lockout/rate limit | Five failures/15m then 15m lock; source limit independent |
| SEC-T-012 | Recovery enumeration | Responses do not disclose account existence |
| SEC-T-013 | Recovery OTP lifecycle | 600s, digest-only, one-time, equality-expired |
| SEC-T-014 | Recovery quotas | Five attempts; 24h lock; resend quotas enforced atomically |
| SEC-T-015 | Recovery replacement/race | Old challenge revoked; only one concurrent consume succeeds |
| SEC-T-016 | Recovery transaction | Password, credential version, session revoke, must-change and audit intent atomic |
| SEC-T-017 | SMTP/provider failure | Unknown delivery never proves recovery; retry is idempotent |
| SEC-T-018 | Recovery threat review | Mailbox compromise/phishing/forwarding residual risk accepted or blocked |
| SEC-T-019 | Canonicalization vectors | Identical event produces identical bytes/hash across implementations |
| SEC-T-020 | Concurrent chain append | Unique contiguous stream sequence and correct predecessor |
| SEC-T-021 | Hash tampering | Altered/deleted/reordered event detected |
| SEC-T-022 | Duplicate EventId | Same digest deduplicated; changed digest rejected |
| SEC-T-023 | Checkpoint signing | Signature verifies using retained public key |
| SEC-T-024 | External anchor attack | DB-only administrator cannot replace external WORM checkpoint |
| SEC-T-025 | Key rotation/compromise | Historical checkpoints remain verifiable; incident epoch explicit |
| SEC-T-026 | Restore verification | Restored stream matches latest external checkpoint or is quarantined |
| SEC-T-027 | Audit payload privacy | No raw secrets, tokens, OTPs or prohibited PII |
| SEC-T-028 | Outbox outage/recovery | Durable retry, reconciliation and no silent loss |
| SEC-T-029 | Verifier read-only behavior | Reports mismatch without mutating chain |
| SEC-T-030 | Retention/legal hold | No purge before approved schedule; legal hold blocks purge |

All SEC-T-001..030 are specifications, NOT RUN and NOT PASS.

## 10. Open dependencies and release gates

1. Platform: approved PostgreSQL role/grant matrix, TLS topology, secret manager, backup encryption, asymmetric signing provider, WORM storage, alert routing and operational ownership.
2. IdentityAccess: runtime recovery orchestration, verified-email state, SMTP adapter, atomic rate limits, bootstrap provisioning and exact session concurrency proof.
3. AuditSecurity: canonical event schema, canonical serialization specification/test vectors, stream partitioning, hash/checkpoint verifier, signing and external-anchor operations.
4. Legal/owner: retention, archival, purge and legal-hold schedule.
5. Independent security reviewer: threat model and exact-SHA review covering DB privileges, TLS, secrets, token lifecycle, session races, recovery/mailbox compromise, audit integrity, restore and operational alerts.
6. PostgreSQL 18 disposable integration/concurrency/failure/restore tests and exact-SHA CI.
7. Explicit owner authorization for the relevant security/G1 gate.

Until all applicable gates close, recovery remains disabled, HTTP login feature flag remains in its approved state, production listener/bootstrap remain gated, and `g1Authorized=false`.

## 11. Integrated design review

Internal adversarial review of this document found the design coherent with ADR-0013's current decisions and the v3.0.3 release boundary. No design choice overrides the owner-approved five-distinct-account cap, 30s/180s liveness, no absolute session TTL, no per-account cap, email-only 600s recovery OTP, 5-attempt/24h recovery lock and resend quotas, or the exclusion of SMS/offline codes.

The following are intentionally not declared closed: database grant values; deployment vendor/secret configuration; verified-email provisioning; recovery endpoint/trust boundary; canonical serialization vectors; signing algorithm/provider and checkpoint cadence; retention/legal hold; independent reviewer attestation; runtime implementation; PostgreSQL test execution; G1 authorization. These are explicit owner/platform/reviewer/evidence dependencies, not values to invent.

## 12. Final status and non-authorization

Stage 08 is **COMPLETE AS AN INTEGRATED DESIGN SPECIFICATION**. It is not implementation-complete, independently approved, or release-ready. SEC-T-001..030 are NOT RUN. No code, SQL, DDL, migration, grant, secret, runtime activation, production listener, audit purge, G1 activation or Go-Live is authorized.

**END — MDG-SECURITY-STAGE-08-001 v0.1**
