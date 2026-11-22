# O-45 IdentityAccess Session Lifecycle Audit and Closure Contract

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O45-SESSION-LIFECYCLE-AUDIT-001 |
| Version / Date | 0.2 / 2026-11-21 |
| Baseline | ADR-0013 (2026-11-07 correction), Stage 08, N.3 reconciliation proposal |
| Status | HEARTBEAT/LOGOUT AND DATABASE-CLOCK E2E PASS IN 37/37 POSTGRESQL SUITE ON SHA 284ac9db…; REMAINING LIFECYCLE OPEN |
| Execution | Source audit plus limited API/handler/test changes in this PR; no schema, migration, production setting or runtime activation |

## 1. Current source findings

The current code implements several important primitives:
- Opaque 256-bit bearer token generation and SHA-256 digest persistence.
- PostgreSQL-backed session resolution on protected requests.
- 30-second heartbeat target and 180-second lease in the session policy.
- Credential-version comparison, account-enabled check and immediate rejection of revoked/expired sessions.
- Revoke-all on self-service password change and in the existing AdminResetPasswordHandler.
- Transactional session-capacity serialization by locking the singleton IdentityAccess system-settings row.

However, O-45 remains OPEN. The following gaps are present in the reviewed main source:

| Gap | Current evidence | Required closure |
|---|---|---|
| Explicit heartbeat endpoint | This PR adds `POST /api/v1/identity/heartbeat`; the PostgreSQL-backed session middleware authenticates it and renews the 180-second lease. A new `IDatabaseClock` port reads `clock_timestamp()` from PostgreSQL; login, session resolution/heartbeat, logout, password-change/reset revocation and audit timestamps use that database clock. It does not use a client-supplied account/session identifier. | Candidate implementation added; E2E CI result pending. Idle-client timing and boundary tests remain required. |
| Logout | This PR adds `LogoutHandler` and `POST /api/v1/identity/logout`. It hashes the presented opaque token, obtains time from PostgreSQL, revokes only the matching current digest, writes secret-free audit evidence on first successful revoke, and returns the same 204 response for unknown/already-revoked tokens. | Candidate implementation and E2E replay test added; CI result pending. |
| Token rotation | No token-rotation handler, endpoint, persistence operation or test was found. | Owner-approved rotation trigger/grace policy, atomic old/new digest transition, replay and unknown-outcome tests. |
| Stale-session cleanup | Active-session queries exclude expired leases, but no cleanup worker/job or lifecycle transition for abandoned sessions was found. | Safe stale marking/cleanup with retained evidence; no unapproved purge. |
| Graceful close / process crash | Graceful logout route is added in this PR. Process crash still relies on lease expiry; no stale-session cleanup worker/job exists. | Graceful close candidate added; crash/lease-expiry/reconnect and cleanup scenarios remain open. |
| Account disablement | Domain primitive exists, but no handler/endpoint atomically disables the account and revokes every session was found. | Server-authorized transactional disable + revoke-all. |
| Admin reset registration | Handler exists but is not registered in API DI or exposed by a reviewed endpoint. | Register only after permission enforcement and API threat review. |
| API-wide authentication | Middleware protects Persons and password-change paths; other application routes pass through anonymously. | Default-deny authenticated route policy before any gated application activation; health/liveness and explicitly public routes remain allow-listed. |
| Concurrent-session policy | Existing code counts distinct active accounts and allows more than one session for the same account. | Preserve ADR-0013; do not introduce a per-account cap. Resolve N.3 manager-specific ambiguities before manager-specific policy tests. |

No fixed absolute TTL is to be added. The current heartbeat/lease model is liveness, not an absolute session lifetime.

## 2. Accepted session invariants

The implementation and tests must preserve:
1. Initial system capacity is five distinct active accounts; an authorized administrator may change the setting only through the dedicated permission and audit boundary.
2. Multiple sessions for one already-active account do not consume additional distinct-account capacity. No per-account concurrent-session cap is approved by the 2026-11-07 owner correction.
3. Heartbeat target is 30 seconds; lease is 180 seconds from the last accepted heartbeat.
4. PostgreSQL time and state are authoritative; no local cache may authorize after revocation.
5. A session is valid only if account is enabled, session is not revoked, lease has not expired and credential_version_at_issue equals the current account credential_version.
6. Password change/reset and account disablement revoke all sessions for the account atomically. Logout revokes the current session.
7. A stale/expired session never becomes active merely because the client reconnects. After lease expiry, the client must authenticate again.
8. Graceful close revokes immediately; process crash relies on lease expiry and must not create a duplicate active-account slot.
9. No raw bearer token is logged, persisted, cached server-side or placed in telemetry/crash artifacts.
10. Expired/revoked rows are not physically purged until retention and legal-hold policy is approved.

## 3. Token rotation — policy not yet decided

No owner-approved trigger, cadence, overlap/grace interval or ambiguous-response policy for token rotation was found. The implementation must not invent a periodic TTL or silently rotate on every heartbeat.

Before implementation, owner/security must decide:
- rotation triggers (explicit client request, risk event, credential/security event, or another named condition);
- whether old-token acceptance overlap is zero or a bounded grace period and how replay is prevented;
- how the client handles response loss/unknown commit without persisting raw token server-side;
- whether rotation replaces the current session token or creates a new session identity;
- idempotency key, audit event and failure recovery semantics.

Minimum technical invariants once policy is accepted:
- generate a new opaque token with at least 256 bits CSPRNG entropy;
- persist only its 32-byte SHA-256 digest;
- atomically transition the session token digest and invalidate the old digest according to the approved overlap policy;
- old token cannot be replayed after its allowed window;
- same operation replay cannot create multiple valid replacements;
- no raw token or recoverable token material is written to logs, database, telemetry or artifacts.

## 4. Stale-session lifecycle

The safe target behavior is:
- A valid heartbeat extends lease to PostgreSQL-now + 180 seconds.
- A network interruption shorter than the remaining lease may reconnect using the still-valid token.
- At or after lease expiry, the old token is rejected; the client must log in again.
- Graceful logout records revocation and the next request is rejected immediately.
- Process crash has no reliable close callback; expiry is the authoritative fallback.
- Stale cleanup may mark/annotate expired sessions for operational reconciliation, but must not delete security evidence until an approved retention/legal-hold schedule exists.
- Cleanup must be idempotent, concurrency-safe and must not revoke a newer replacement session or a live session for the same account.

## 5. API contract required

The following endpoints are now present as a gated implementation candidate in this PR, except token rotation which remains a proposal. HTTP login remains controlled by its existing disabled-by-default feature flag; no production activation is included:

| Endpoint | Behavior | Authentication |
|---|---|---|
| `POST /api/v1/identity/heartbeat` | Middleware validates the current bearer token against PostgreSQL and extends the lease; returns 204 | Current valid session |
| `POST /api/v1/identity/logout` | Revokes the session matching the presented token digest; no account/session ID accepted from client; returns stable 204 for repeat/unknown token | Bearer token presented; invalid/expired/revoked input is non-disclosing |
| `POST /api/v1/identity/token/rotate` | Rotates current session token under the owner-approved rotation policy | Current valid session + approved rotation conditions |

All identity/account/session state is derived from the validated server-side principal and PostgreSQL. Client-supplied AccountId, SessionId, credential version or role is never trusted.

## 5.1 Exact-head execution evidence,,The IdentityAccess heartbeat/current-session logout and database-clock end-to-end coverage passed as part of the latest PostgreSQL 18 integration suite:,,| Evidence | Result |,|---|---|,| SHA | `284ac9dbb94fbbd05d19de18146ccc27d35d8cbf` |,| Workflow / job | `44914448417` / `136876940898` |,| Integration suite | 37 passed / 0 failed / 0 skipped |,| Checksum verifier | `persons-spec-lint` job `136876940885` — PASS, 459 tracked files |,,The passing suite includes authenticated heartbeat, logout of only the presented session, preservation of another session for the same account, repeated logout, and a two-day host-clock skew while PostgreSQL `clock_timestamp()` is authoritative. This proves the current disposable PostgreSQL/API test path only; it does not authorize production session-policy activation.,## 6. Required test catalogue — NOT RUN

| ID | Test | Pass condition |
|---|---|---|
| O45-T01 | Heartbeat endpoint registration | Correct DI/route; only valid current session extends lease |
| O45-T02 | 30s/180s boundaries | Before/equal/after expiry behaves correctly using DB time |
| O45-T03 | Idle client | Independent heartbeat keeps the open application session alive |
| O45-T04 | Temporary network loss | Reconnect before expiry succeeds; after expiry requires login |
| O45-T05 | Graceful logout | Current session is revoked immediately; repeated logout is safe |
| O45-T06 | Process crash | Lease expires; stale cleanup is idempotent and does not delete evidence |
| O45-T07 | Token rotation | Approved trigger/overlap policy; old token replay denied after transition |
| O45-T08 | Rotation race/replay | Concurrent and repeated requests produce one valid outcome |
| O45-T09 | Unknown commit outcome | Stable operation identity resolves outcome without duplicate valid token/session |
| O45-T10 | Password change/reset | All sessions revoke and credential version changes atomically |
| O45-T11 | Account disablement | Disabled account and all sessions become invalid in one transaction |
| O45-T12 | Capacity concurrency | Multiple API instances never exceed five distinct active accounts |
| O45-T13 | Multiple sessions per account | No unapproved per-account cap; one account consumes one distinct-account slot |
| O45-T14 | Revocation freshness | Next protected request rejects after revoke; no cache-based authorization |
| O45-T15 | API route coverage | Application routes default-deny except explicit public/health allow-list |
| O45-T16 | Secret hygiene | No raw token in logs, telemetry, crash dumps or CI artifacts |
| O45-T17 | Manager-specific N.3 policy | Blocked until open N.3 manager-account/session questions are explicitly resolved |
| O45-T18 | Database clock authority | Session resolve/heartbeat/login/revoke use PostgreSQL `clock_timestamp()`; client/host clock skew cannot extend or shorten a lease |

O45-T01, the O45-T05 current-session-only/replay flow, and O45-T18 have been added to `IdentityAccessHttpEndToEndTests.cs` in this PR. O45-T18 deliberately skews the host `TimeProvider` by two days and verifies that login/session heartbeat still work using PostgreSQL time. These tests remain NOT RUN / NOT PASS until the exact head SHA CI run reaches a terminal successful result. All other tests above remain specifications only and NOT RUN / NOT PASS.

## 7. Dependencies

- O-20: canonical runtime identity and exact grants must support the required session reads/writes and deny unauthorized direct operations.
- O-46: bootstrap/recovery completion must use the same credential-version and revoke-all transaction semantics.
- N.3 reconciliation: the general five-distinct-account and no-per-account-cap decisions are clear; the historical System Manager account/session sub-limits remain unresolved.
- Platform: operational ownership and scheduling for stale-session reconciliation; no background worker DB identity may be invented before O-20 role mapping.
- Owner/security: token rotation trigger, overlap and unknown-outcome policy.

## 8. Status and non-authorization

O-45 remains **OPEN / PARTIALLY IMPLEMENTED / NOT VERIFIED**. This PR adds a heartbeat route and idempotent logout candidate with an end-to-end test extension, and switches IdentityAccess session/lockout/audit timestamps to a PostgreSQL-authoritative clock. Token rotation, stale-session reconciliation, account disablement/revoke, full API default-deny, crash/reconnect proof and exact-SHA CI acceptance remain open. No production endpoint activation, token rotation policy, stale-row purge, new DB identity/grant, migration, G1 activation, Posting Release or Go-Live is authorized by this audit.

**END — MDG-SECURITY-O45-SESSION-LIFECYCLE-AUDIT-001 v0.2**
