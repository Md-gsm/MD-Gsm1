# IdentityAccess ↔ Organization Principal / Permission Contract

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 |
| Version / Date | 1.2 / 2026-11-22 |
| Supersedes | v1.1 |
| Status | PUBLIC CONTRACT v1.1 SOURCE-PUBLISHED; REQUEST-TIME PERMISSION IMPLEMENTED; TRANSACTION-SCOPED GUARD REMAINS A PROPOSAL |
| Related evidence | IdentityAccessAuthorizationLinearizationPostgresProofTests |

## 1. Existing public contract

The public v1.1 API remains in `src/Modules/IdentityAccess/Contracts/IdentityAccessAuthorizationContract.cs`. Host supplies only middleware-validated request-principal facts. IdentityAccess Infrastructure owns permission evaluation and persistence. Organization must consume the public Contracts assembly only.

The request-time permission query checks the same AccountId/SessionId, current credential version, enabled account, non-revoked/unexpired session and exact active permission grant. Unknown keys deny; persistence errors return Unavailable. Product Owner authority remains Unavailable until the protected binding lifecycle is implemented.

## 2. Request-time decision is not commit-time authorization

The existing one-statement permission result is a consistent request-time decision. It does not hold database row locks until a later Organization mutation commits. A caller must not cache or reuse Allowed as proof that a concurrent session/permission revocation is serialized against that later write.

## 3. Candidate transaction-scoped guard evidence

`tests/MDGsm.Domain.Tests/Security/IdentityAccessAuthorizationLinearizationPostgresProofTests.cs` now demonstrates a candidate two-writer protocol in a marked disposable database:

1. a mutation transaction resolves the exact account/session/grant tuple;
2. it takes `FOR SHARE` locks on account, session, account-role, role, role-permission and permission rows;
3. it stages a protected write under the same transaction;
4. independent transactions attempt session revocation and permission-grant deletion;
5. the test observes both writers waiting in `pg_stat_activity` with `wait_event_type='Lock'`;
6. the protected transaction commits, then both revocations proceed;
7. a fresh permission query after revocation denies.

This is stronger than a timing-only assertion: the test confirms both competing PostgreSQL sessions have entered lock wait before releasing the protected transaction.

## 4. Remaining integration requirement

The actual public IdentityAccess adapter still executes a request-time query. It does not accept an already-open Organization connection/transaction and does not retain `FOR SHARE` locks through a downstream write.

A future cross-module transaction participant must:
- join the exact same local PostgreSQL connection and transaction as the Organization mutation and its domain history/outbox;
- lock the current account/session and all role/grant rows which establish the exact permission;
- retain those locks through commit/rollback;
- use a Platform-approved global lock acquisition order;
- return typed Denied/Unavailable results on stale or missing state;
- be invalidated by every IdentityAccess operation that changes a locked authorization fact;
- have real mutation/revocation race tests, including crash/timeout/deadlock/unknown-commit/replay.

The exact public C# shape, transaction coordinator ownership and lock ordering remain unaccepted. No Organization module currently consumes this contract.

**Disposition:** request-time IdentityAccess contract SOURCE-IMPLEMENTED; candidate PostgreSQL transaction guard proof strengthened and CI pending; real transaction participant, live module integration and global lock-order acceptance remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — IdentityAccess ↔ Organization contract v1.2**
