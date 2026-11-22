# IdentityAccess Contracts

This assembly is the public, versioned cross-module boundary. It contains request/response DTOs and typed authorization decisions only. It does not expose IdentityAccess persistence entities, private Application ports, credential hashes, session bearer tokens or recovery secrets.

## Published request-scoped authorization boundary — v1.1

`IIdentityAccessAuthorizationContract` is the module-facing authorization boundary:

- `ResolveCurrentPrincipalAsync` resolves the current principal only from the validated server-side request context. It accepts no AccountId or actor claim from the caller.
- The principal snapshot contains opaque AccountId and SessionId, the current credential/principal version, account/session state and the server authentication timestamp.
- `EvaluateCurrentPermissionAsync` evaluates one exact permission key for the current principal. The host adapter delegates to an IdentityAccess persistence query that checks the exact active permission grant together with the same session's non-revoked state, lease, enabled account and current credential version in one PostgreSQL statement.
- The result distinguishes Allowed, Denied and Unavailable. Unknown permission keys deny; storage/dependency errors are Unavailable, never Allow.
- `ResolveCurrentProductOwnerAuthorityAsync` returns a typed, version-capable Product Owner binding decision. Until the protected binding store and its enrollment/recovery lifecycle are implemented and accepted, implementations must return Unavailable. A role or caller-supplied owner flag is never a fallback.

The decision values are immutable and include the contract version. They contain no access token, credential, secret or internal IdentityAccess persistence type.

## Consistency and transaction boundary

The permission query is a request-time point-in-time decision. It revalidates session/account/permission state in one database statement, reducing the gap between a middleware-authenticated principal and the permission check. It does **not** by itself prove that a later mutation in another module is serialized against concurrent session revocation or permission change. A future mutating consumer must have an accepted commit-time authorization linearization protocol and PostgreSQL race tests before it can be enabled.

The current session middleware applies to the existing protected Persons/password/heartbeat routes. No Organization route or runtime module exists yet; any future protected endpoint must be explicitly included in the authenticated route policy before consuming this contract.

## Acceptance boundary

The interface and bounded Host adapter are source-published. This does not by itself prove the full 41-case IdentityAccess authorization runtime, IdentityAccess module-owner approval, Organization consumer integration, trusted Product Owner enrollment/recovery, commit-time revocation concurrency or G1 authorization. Organization must consume only this Contracts assembly; it must not reference IdentityAccess Domain/Application/Infrastructure or query IdentityAccess private tables.

**Module contract version:** 1.1.
