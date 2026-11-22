# IdentityAccess ↔ Organization Principal / Permission Contract — Transaction-Guard Proof Successor

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 |
| Version / Date | 1.3 / 2026-11-22 |
| Supersedes | v1.2 |
| Status | PUBLIC REQUEST-TIME CONTRACT SOURCE-PUBLISHED; TRANSACTION-GUARD PROOF STRENGTHENED; REAL MODULE PARTICIPANT OPEN |
| Branch | security/o20-o21-final-closure |

## 1. Existing public request-time contract

The public v1.1 API remains in src/Modules/IdentityAccess/Contracts/IdentityAccessAuthorizationContract.cs. Host supplies only middleware-validated request-principal facts. IdentityAccess Infrastructure owns permission evaluation and persistence. Organization must consume the public Contracts assembly only.

The request-time permission query checks the same AccountId/SessionId, current credential version, enabled account, non-revoked/unexpired session and exact active permission grant. Unknown keys deny; persistence errors return Unavailable. Product Owner authority remains Unavailable until the protected binding lifecycle is implemented.

## 2. Stronger disposable transaction-guard proof

IdentityAccessAuthorizationLinearizationPostgresProofTests now uses a marked disposable database and a single protected-write transaction. It takes FOR SHARE locks over all rows that participate in the synthetic authorization decision: account, session, account_role, role, role_permission and permission.

While the protected transaction holds these locks, three independent transactions attempt:
- session revocation;
- removal of the exact role_permission grant;
- deactivation of the role itself.

The test captures each competing backend PID and verifies with PostgreSQL pg_blocking_pids() that all three backends are actually blocked before committing the protected transaction. After commit, all three revocations complete. The proof confirms the protected write committed, the session is revoked, the grant is removed, the role is inactive, and a fresh authorization query denies.

This replaces the timing-only observation used in the earlier proof. The test does not query pg_stat_activity directly; it uses pg_blocking_pids() to verify actual blocking relationships. The tested row set is a candidate guard set, not an accepted global resource lock rank.

## 3. Critical implementation gap remains

The real public IdentityAccess adapter still performs a request-time query. It does not accept an already-open Organization connection/transaction and does not retain these FOR SHARE locks through a downstream Organization write.

A future cross-module transaction participant must:
1. join the same local PostgreSQL connection and transaction as the Organization mutation, history and outbox;
2. lock all current account/session/role/grant facts that establish the exact permission;
3. retain those locks until commit or rollback;
4. include account disable, credential-version change, session revocation, role deactivation, role membership and permission grant changes;
5. use a Platform-approved global lock acquisition order;
6. return typed Denied/Unavailable when principal or policy state is stale, missing or unavailable;
7. have real module-level race, rollback, timeout, deadlock, crash, unknown-commit and idempotent replay tests.

The exact contract shape, transaction coordinator owner and cross-module lock-order contract remain unaccepted. No Organization runtime consumer currently exists.

## 4. Disposition

**Source:** request-time IdentityAccess public contract implemented; candidate transaction lock set strengthened in a disposable PostgreSQL proof.
**Not closed:** exact-head CI, IdentityAccess module-owner acceptance, transaction participant implementation, Organization integration, Platform-wide lock-order acceptance and independent review.
**Authorization:** O20-E OPEN; Organization G1 NOT READY / NOT GRANTED. No production transaction API, Organization migration, schema, role or grant is authorized.

**END — IdentityAccess ↔ Organization contract v1.3**
