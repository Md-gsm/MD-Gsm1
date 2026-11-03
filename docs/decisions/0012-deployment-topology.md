# ADR-0012 — Deployment topology, transport security and authentication boundary

- Status: PROPOSED — draft by Claude for Project Owner decision. Not a decision.

## Current facts (main)
- The API exposes only `/health/live`, `/health/ready` and `/api/v1/system/info`; no authentication.
- The Desktop (WPF) client talks to the API over loopback HTTP.
- ADR-0006 selects opaque access tokens, but no token issuance or validation exists.
- Persons endpoints, once added, will serve personal data (mobile numbers, names).

## Open questions
1. Is the API always co-located with the desktop client on one machine (loopback only), or will a shop LAN / multiple workstations / a server be supported?
2. Who may reach the API port (bind address, firewall)?
3. Is TLS required on a LAN hop, and how are certificates provisioned in a small shop?
4. Where does PostgreSQL run relative to the API, and what is the network trust between them?

## Options
- **A. Single-machine only (V1).** Kestrel binds 127.0.0.1 explicitly; no TLS needed; token auth still required before any Persons endpoint so other local processes/users cannot read data.
- **B. Shop LAN server.** API on a server, TLS mandatory (internal CA or self-signed with pinning), bind to LAN interface, firewall rules, token auth, DB not exposed beyond the API host.
- **C. Cloud/remote.** Out of scope for V1; requires a full threat model.

## Recommendation (advisory)
Declare A as the V1 scope in writing, enforce 127.0.0.1 binding in configuration plus a test, and make "no Persons endpoint without authentication" a gate (the existing security gate is PASS-N/A only because no endpoints exist). Decide B before any multi-workstation requirement appears.

## Decision
Pending owner.
