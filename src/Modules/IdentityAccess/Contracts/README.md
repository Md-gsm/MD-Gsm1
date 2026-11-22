# IdentityAccess Contracts

This assembly is the public, versioned cross-module boundary. It contains request/response DTOs and typed authorization decisions only. It does not expose IdentityAccess persistence entities, private Application ports, credential hashes, session bearer tokens or recovery secrets.

## Published request-scoped authorization boundary — v1.1

`IIdentityAccessAuthorizationContract` is the module-facing authorization boundary:

- `ResolveCurrentPrincipalAsync` resolves the current principal only from the validated server-side request context. It accepts no AccountId or actor claim from the caller.
- `EvaluateCurrentPermissionAsync` evaluates one exact permission key for that current principal. The result distinguishes Allowed, Denied and Unavailable; an unavailable dependency is not an Allow.
- `ResolveCurrentProductOwnerAuthorityAsync` returns a typed, versioned Product Owner binding decision. Until the protected binding store and its enrollment/recovery lifecycle are implemented and accepted, implementations must return Unavailable. A role or caller-supplied owner flag is never a fallback.

The decision values are immutable, include the contract version and use opaque AccountId references only. They intentionally contain no access token, credentials, secret or internal IdentityAccess persistence type.

## Acceptance boundary

The interface is now published at source level. This does not by itself prove an IdentityAccess module-owner review, Organization consumer integration, owner-binding enrollment/recovery, revocation concurrency or G1 authorization. Organization must reference only this Contracts assembly; it must not reference IdentityAccess Domain/Application/Infrastructure or query IdentityAccess private tables.

**Module contract version:** 1.1.
