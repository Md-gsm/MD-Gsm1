# IdentityAccess Contracts

This assembly is the public, versioned cross-module boundary. It contains request/response DTOs and typed authorization decisions only. It does not expose IdentityAccess persistence entities, private Application ports, credential hashes, session bearer tokens or recovery secrets.

## Published authorization boundary — v1.2

`IIdentityAccessAuthorizationContract` is the module-facing authorization boundary:

- `ResolveCurrentPrincipalAsync` resolves the current principal only from the validated server-side request context. It accepts no AccountId or actor claim from the caller.
- The principal snapshot contains opaque AccountId and SessionId, the credential/principal version, account/session state and the server authentication timestamp.
- `EvaluateCurrentPermissionAsync` evaluates one exact permission key for the current principal. The IdentityAccess-owned persistence query checks the exact active permission grant together with the same session's non-revoked state, lease, enabled account and current credential version in one PostgreSQL statement.
- `EvaluateCurrentPermissionInTransactionAsync` accepts the caller's already-open `DbConnection` and the `DbTransaction` belonging to that connection. It obtains the principal from the validated server request context, rechecks the exact current account/session/credential/permission facts and obtains PostgreSQL `FOR SHARE` locks on every IdentityAccess row contributing to an Allow decision: account, session, account-role membership, role, role-permission grant and permission. Those locks remain until the supplied transaction commits or rolls back. No caller-supplied AccountId, session, role, owner flag or token is accepted.
- Permission results distinguish Allowed, Denied and Unavailable. Unknown keys, stale or absent sessions, transaction/connection mismatch and dependency failures never produce Allow.
- `ResolveCurrentProductOwnerAuthorityAsync` returns a typed, version-capable Product Owner binding decision. Until the protected owner-binding store and its enrollment/recovery lifecycle are implemented and accepted, implementations return Unavailable. A role or caller-supplied owner flag is never a fallback.

Decision values are immutable and include the contract version. They contain no access token, credential, secret or internal IdentityAccess persistence type.

## Consistency and transaction boundary

The request-time API is a point-in-time decision. The transaction-scoped API can retain the IdentityAccess authorization rows stable through a caller-owned local PostgreSQL transaction, provided the caller uses the same open connection and transaction for both the guard and protected write.

This is a reusable IdentityAccess mechanism, **not** approval of a Platform-wide lock order and **not** proof of Organization integration. A mutating cross-module consumer must not call this API until Platform publishes and affected module owners accept the complete lock order across IdentityAccess, the consumer's domain aggregates, idempotency receipts, approvals, history and outbox. The consumer must use the same local transaction for its protected mutation, required domain history and local outbox. It must also pass concurrency, timeout, deadlock, rollback, crash and unknown-commit/replay tests.

The current Host session middleware applies to the existing protected Persons/password/heartbeat routes. No Organization route or runtime module exists yet. Any future protected endpoint must be explicitly included in the authenticated route policy before consuming this contract.

## Product Owner authority

Product Owner authority is a distinct protected server-side binding, not a role, permission, username, caller-selected AccountId or client boolean. Its binding lifecycle, initial enrollment, cardinality, change, recovery, emergency disable and restoration are not implemented or owner-accepted. The contract therefore fails closed with Unavailable. The narrow Organization assignment exception does not authorize credential/session access, IdentityAccess role/permission mutation, impersonation or source RowScope bypass.

## Acceptance boundary

The public source contract and transaction-scoped IdentityAccess adapter are implemented on the closure branch. The marked-disposable PostgreSQL proof invokes the actual adapter against the real `identity.*` schema and confirms competing session/account/membership/role/grant/permission mutations are blocked by `pg_blocking_pids()` until the protected transaction ends. Exact-head CI, IdentityAccess module-owner acceptance, Platform lock-order acceptance, a real Organization UnitOfWork consumer, and independent exact-SHA review remain separate gates.

Organization must consume only this Contracts assembly; it must not reference IdentityAccess Domain/Application/Infrastructure or query IdentityAccess private tables.

**Module contract version:** 1.2.
