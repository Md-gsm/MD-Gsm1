# ADR-0012 — Deployment topology, transport security and authentication boundary

- Status: ACCEPTED — owner-directed architecture constraint (2026-11-04).
- Decision owner: Project Owner.
- Scope: V1 Windows desktop clients connected to one shop/server deployment.
- Related decision: ADR-0006 (opaque access tokens).

## Context

MD-Gsm1 is required to support multiple users working concurrently from multiple Windows systems. A desktop-local API bound only to loopback cannot satisfy this requirement. Persons data contains personal information and must not be exposed directly to desktop clients or unauthenticated network callers.

## Decision

1. **Central client/server topology.** One designated server host runs the MD-Gsm API and connects to the authoritative PostgreSQL database. Multiple Windows Desktop clients connect to that API over the shop LAN. The API is the only application component permitted to access PostgreSQL.
2. **No client-to-database connections.** PostgreSQL must not be exposed to workstation networks. Its listener and firewall rules must restrict access to the API host and approved administrative/backup paths only.
3. **TLS on every non-loopback hop.** LAN traffic between Desktop clients and the API must use TLS with certificate-chain or explicitly pinned trust validation. Clients must reject invalid, expired, mismatched, or untrusted certificates; disabling certificate validation or accepting arbitrary self-signed certificates is prohibited. TLS termination must occur at a controlled endpoint under the operator's administration.
4. **Explicit network binding.** Production API binding must use an explicitly configured server/LAN interface and port. Wildcard binding is not an acceptable default. Firewall policy must allow only the required client subnet and administrative paths. Loopback HTTP is permitted only for isolated local development and tests, never as the production topology.
5. **Authentication is mandatory.** All non-health application endpoints require an authenticated, active user session. Follow ADR-0006: opaque high-entropy bearer tokens, server-side token-hash lookup, authoritative PostgreSQL session state, expiry and immediate revocation. No JWT substitution, process-local validity cache, raw-token persistence, or token logging.
6. **Authorization is server-enforced.** Every request must resolve the current account and session state and enforce the operation's permission on the server. Desktop visibility or disabled controls are not security boundaries.
7. **No cloud/remote deployment in V1.** Internet-facing or cloud deployment is out of scope and requires a separate threat model and owner-approved ADR.
8. **Safe activation boundary.** This decision records the required target topology; it does not authorize opening network listeners, enabling Persons endpoints, running production migrations, or setting `g1Authorized=true`. Those actions remain blocked until IdentityAccess, TLS provisioning/trust, firewall/deployment controls, security tests, and the applicable G1 acceptance evidence are complete.

## Required implementation and verification

- Configuration profiles distinguish Development/Test from Production; production must fail closed if TLS certificate material, explicit bind address, or required security configuration is missing.
- Integration tests prove multiple independent clients can authenticate concurrently against the same API and database.
- Negative tests prove unauthenticated, expired, revoked, disabled-account, unauthorized, and invalid-certificate requests are rejected.
- Deployment tests prove a workstation cannot connect directly to PostgreSQL and that the API is not exposed on unintended interfaces.
- Operational documentation covers server installation, certificate provisioning/rotation, firewall setup, database backup/restore, and session revocation.
- Secrets and bearer tokens are excluded from logs, diagnostics, crash reports, and CI artifacts.

## Consequences

- V1 is a LAN client/server product, not a single-machine-only application.
- Server availability and network security become deployment responsibilities.
- Certificate provisioning and trust establishment are release blockers, not optional setup advice.
- The existing loopback-only API configuration remains a development-only setting until the production host configuration and tests are implemented.
