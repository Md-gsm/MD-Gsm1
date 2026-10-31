# ADR-0006 — Opaque access tokens

- Status: accepted security constraint.
- Owner module: IdentityAccess.

## Decision

- Access tokens are opaque random bearer values, not self-describing JWTs.
- The raw token is held only by the client that receives it. The server stores only a cryptographic hash of the token; raw tokens must never be persisted, logged, included in telemetry, or placed in CI artifacts.
- Every authenticated request validates token/session state and account state against authoritative server-side data.
- Revocation must take effect immediately on the next request.
- In-memory token caches are prohibited; PostgreSQL is the authoritative state store.
- IdentityAccess owns issuance, validation, expiry, revocation, and session lifecycle.

## Required proof before activation

Tests must prove that raw token material is never stored or logged, hashes are used for lookup, revoked tokens fail on the next request, disabled accounts are rejected, and no process-local cache can extend token validity.

This ADR does not authorize starting IdentityAccess implementation before the specified architecture/security review and explicit G1 authorization.
