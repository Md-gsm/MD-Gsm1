# IdentityAccess — preparation boundary

- Status: owner-approved preparation only (2026-11-04).
- Related decisions: ADR-0006 and ADR-0012.
- This document does not authorize G1, enable authentication endpoints, expose the API to a network, activate Persons endpoints, or permit production migrations.

## Scope of this preparation

The project owner approved preparation while retaining `g1Authorized=false`. This stage records the implementation contract and security acceptance plan. Runtime login/logout, account provisioning, session issuance/validation, protected API routes, and operational activation remain blocked until the ADR-0006 security review and formal G1 gate are satisfied.

## Ownership and trust boundaries

IdentityAccess owns accounts, credential lifecycle, failed-login throttling, opaque sessions, expiry, revocation, roles, permissions, and authentication/access-control audit events. Desktop is an untrusted client. API is the enforcement boundary. PostgreSQL is authoritative. Desktop clients never connect directly to PostgreSQL.

## Proposed schema (not implemented or applied)

- `identity_accounts`: UUID, normalized unique login, password hash, enabled state, failed-attempt state, lockout expiry, credential version, UTC audit timestamps.
- `identity_roles` and `identity_permissions`: stable keys and descriptions.
- `identity_account_roles` and `identity_role_permissions`: unique assignments; deny by default.
- `identity_sessions`: UUID, account ID, unique SHA-256 token digest, created/expiry/last-seen/revoked UTC timestamps, credential version at issuance.
- `identity_login_attempts`: minimized throttling/audit metadata; never passwords or bearer tokens.

Every protected request must resolve account/session/permission state from PostgreSQL. No process-local validity cache. Revocation must affect the next request.

## Security requirements

- Bearer tokens use the platform CSPRNG with at least 256 bits of entropy; raw token returned once over validated TLS and never persisted, logged, traced, or exported.
- Store only SHA-256 token digests. Passwords use a maintained framework password hasher with versioned parameters and rehash-on-success; custom cryptography is prohibited.
- Generic login failure for unknown, invalid, disabled, or locked accounts; deny by default and throttle brute-force attempts.
- Logout revokes the current session atomically. Account disable, password reset/change, and administrative revocation invalidate affected sessions immediately.
- Every protected operation is authenticated and authorized server-side; UI visibility is not authorization.
- No public self-registration. First-admin bootstrap must be a one-time, auditable offline/deployment operation and requires separate review.

## Decisions requiring owner review before runtime activation

1. Login identifier and normalization rules.
2. Absolute/idle session lifetime and concurrent-session policy.
3. Password and recovery/reset policy.
4. Lockout threshold/window and rate-limit dimensions.
5. First-admin bootstrap and emergency recovery procedure.
6. Initial roles and permission catalog.
7. Audit retention and access policy.

No undocumented defaults may be inferred for these settings.

## Required tests before activation

Unit tests for token entropy/hash, password verification/rehash, expiry boundaries, permission resolution, lockout and throttling. Disposable-PostgreSQL tests for unique accounts, disabled accounts, session issue/revoke/expiry, concurrent updates, transaction rollback and migration idempotence. API/security tests for missing/malformed/expired/revoked tokens, unauthorized operations, account enumeration, brute force, TLS trust failures, direct database denial, and absence of secret leakage in responses/logs. CI uses disposable databases only.

## Explicit non-goals

No runtime endpoint, live account/session table, production migration, network listener, Persons integration, or production credential is enabled by this preparation. `g1Authorized=false` remains mandatory.
