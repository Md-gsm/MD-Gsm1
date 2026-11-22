# IdentityAccess ↔ Organization Principal / Permission Contract — Transaction-Guard Proof Successor

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 |
| Version / Date | 1.5 / 2026-11-22 |
| Supersedes | v1.4 |
| Supersedes | v1.3 |
| Supersedes | v1.2 |
| Status | PUBLIC REQUEST-TIME AND TRANSACTION-GUARD API SOURCE-PUBLISHED; EXACT-HEAD TEST / MODULE OWNER / ORGANIZATION UOW ACCEPTANCE OPEN |
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


## 5. Transaction-scoped public guard API — source implemented (2026-11-22; v1.4)

The public Contracts interface now exposes `EvaluateCurrentPermissionInTransactionAsync(permissionKey, DbConnection, DbTransaction, cancellationToken)`. The API accepts no caller AccountId, session ID, role, owner flag or token. It obtains the principal only from the validated request-principal accessor.

The IdentityAccess Infrastructure adapter:
- requires an already-open connection and a transaction belonging to that exact connection;
- re-resolves the current validated request principal;
- rejects unknown permission keys;
- executes one IdentityAccess-owned PostgreSQL query against accounts, sessions, account_roles, roles, role_permissions and permissions;
- checks exact AccountId, SessionId, current credential version, enabled account, non-revoked/unexpired session and active exact permission;
- obtains `FOR SHARE` locks on all rows contributing to an Allow decision and keeps them until the caller commits/rolls back the supplied transaction;
- returns typed Denied/Unavailable on absent/stale principal, missing grant, transaction mismatch, or persistence error.

The corresponding real-IdentityAccess-schema PostgreSQL proof is `IdentityAccessTransactionScopedAuthorizationGuardPostgresProofTests`. It uses the marked disposable IdentityAccess database, invokes the actual adapter, holds locks in the same transaction as a synthetic protected-write record, and starts five competing mutations against live IdentityAccess tables (session revoke, account disable/credential version change, membership removal, grant removal, permission deactivation). It checks actual blocking through `pg_blocking_pids()` before committing the protected transaction.

This test is intentionally not reported as PASS until the exact current head's build and PostgreSQL job complete and the logs are reviewed.

## 6. Acceptance boundary — not a global lock-order approval

This transaction participant is a reusable IdentityAccess mechanism, not authorization to wire any Organization write path yet. The actual Organization module/UnitOfWork is not present. No real Organization transaction, Branch/Warehouse/Assignment mutation, domain history or production outbox currently consumes the new API.

The Platform-wide lock-order registry and all participating module owners still need to approve the order between IdentityAccess rows and the Organization aggregate, idempotency, approval and outbox resources. The IdentityAccess method must not be called by an Organization command until that order and its timeout/deadlock/retry/failure behavior are accepted. A disposable lock test does not itself approve a cross-module resource order.

The public method is source-published but exact-head CI, IdentityAccess module-owner acceptance, Platform lock-order acceptance, Organization transaction integration and independent review remain OPEN.

**Disposition:** request-time contract remains implemented; transaction-scoped authorization guard source implemented and explicit PostgreSQL test added; exact-head validation pending. Product Owner binding remains unavailable/fail-closed. O20-E OPEN; G1 NOT GRANTED.

**END — IdentityAccess ↔ Organization contract v1.4**


## 7. Public contract version 1.2 and evidence status — 2026-11-22 (v1.5)

The IdentityAccess Contracts assembly's `ModuleContractVersion.Current` is now advanced to `1.2` to version the additive transaction-scoped authorization method. Its README now documents both request-time authorization and the transaction-scoped row-lock API, the exact rows locked, the fail-closed outcomes, and the explicit precondition that a cross-module consumer must first obtain the Platform/module-owner global lock-order agreement.

The ArchitectureBoundary test now expects IdentityAccess contract version 1.2. This version change is a public API compatibility marker; it does not make the underlying method permission broader and does not authorize Organization to call it yet.

### Exact bounded evidence

At source/test head `c67d892197d02409827bbc127a296fd64374576b`, Linux PostgreSQL CI job `persons-postgres-migrations` completed the marked disposable IdentityAccess test suite with 52/52 passed. The explicitly invoked `IdentityAccessTransactionScopedAuthorizationGuardPostgresProofTests` reported PASS. The proof called the actual IdentityAccess Infrastructure adapter against real `identity.*` tables, and PostgreSQL confirmed five competing IdentityAccess mutations were blocked by the guard transaction until its commit.

The Windows migration job and CodeQL had not yet completed at the time this section was written. The README/version-marker/architecture-test update in this successor creates a newer source head; the whole workflow must pass again on that exact resulting head before the new marker is marked verified.

### Remaining closure boundary

- IdentityAccess module-owner acceptance remains unrecorded.
- Platform/global lock-order approval remains open; the local guard's lock acquisition is not a cross-module lock-rank approval.
- No real Organization module/UnitOfWork exists to consume the API. No production Organization mutation/history/outbox is protected by this guard.
- Product Owner authority remains unavailable because protected binding enrollment/change/recovery/disable/restore is not implemented.
- No independent exact-SHA Architecture/Security review or G1 authorization exists.

**Disposition:** public IdentityAccess contract v1.2 source-published; bounded Linux disposable PostgreSQL guard proof PASS at the immediately preceding source/test SHA; exact latest-head CI, owner acceptance, Platform order, Organization integration and independent review OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — IdentityAccess ↔ Organization contract v1.5**
