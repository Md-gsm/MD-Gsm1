# O-46 — Implementation Authorization and Accepted Runtime Boundaries

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-IMPLEMENTATION-AUTHORIZATION-001 |
| Version / date | 0.1 / 2026-11-28 |
| Status | OWNER AUTHORIZATION RECORDED — IMPLEMENTATION IN PROGRESS; NOT RELEASE ACCEPTANCE |
| Supersedes | No historical proposal; supplements O-46 proposal v0.4 |
| Tracking | Issue #118; implementation branch `implementation/o46-exact-replay-bootstrap-20261128` |

## 1. Authorization recorded

On 2026-11-28, the Product Owner explicitly directed implementation of O-46 after confirming that the supervisor approval and Product Owner approval had been recorded. This instruction authorizes implementation work for the exact-replay architecture, the two fixed-signature routines, the protected local Journal, the Windows local Named Pipe boundary, and the test harnesses required by v0.4.

This authorization does not constitute CI success, production deployment approval, Go-Live, G1 activation, or independent acceptance of the eventual implementation SHA. Those remain separate gates.

## 2. Explicitly accepted service-restart behavior

The Product Owner accepts the conservative v0.4 rule:

- ServiceInstanceId is generated afresh for every Windows service process start.
- A Journal operation bound to a previous ServiceInstanceId is suspended after service restart.
- Matching SID, AuthenticationId/LUID and TokenSessionId do not override the service-instance mismatch.
- No automatic continuation, rebinding, challenge replacement, new Owner or credential regeneration occurs.
- Recovery remains unavailable unless a separate runbook is reviewed and authorized.

## 3. Unchanged hard constraints

- Exactly two PostgreSQL principals remain in scope: existing `mdgsm_identity_bootstrap_fn_owner` (NOLOGIN) and `mdgsm_bootstrap_executor` (NOLOGIN).
- No third principal, LOGIN, membership, View, grant or privilege expansion.
- No Executor SELECT on tables or Views; no expansion of `mdgsm_runtime`; no runtime use of `migration_runner`.
- Exactly two fixed-signature SECURITY DEFINER routines are allowed:
  - `identity_bootstrap.issue_initial_owner_challenge_v1(...)`
  - `identity_bootstrap.claim_initial_product_owner_v1(...)`
- No generic dispatcher, dynamic SQL, extra routine, public/network setup endpoint, fallback identity or second Owner.
- Recovery after irrecoverable Journal loss remains unavailable and fail-closed.
- No production credentials, passwords, raw challenge, private keys or TPM authorization values may enter source control, logs, traces, crash artifacts or CI artifacts.

## 4. Implementation sequencing and evidence

Implementation is performed on the dedicated branch and must be split into reviewable vertical slices:

1. Windows logon-session capture, local-only Named Pipe security and worker quarantine on impersonation/revert failure.
2. Protected Journal record format, durable PREPARED publication, authenticated replay payload and anti-rollback adapter.
3. PostgreSQL exact Issue/Claim functions and immutable binding/receipt state, only after catalog/ownership/ACL preflight proves the existing principals can safely own and execute the exact routines without privilege expansion.
4. Integration tests for exact Issue replay, Claim single-owner invariant, lost commit acknowledgement, and hostile transaction outcomes on disposable PostgreSQL 18.6.
5. Windows tests for session binding, Named Pipe provenance, impersonation/revert failure, TPM counter fault injection, backup rollback and file durability.
6. Independent Security/Architecture review against the exact final implementation SHA.

Hardware power-loss durability and TPM NV semantics must not be reported as proven by mocks. Where real hardware/power-cut testing is required, the result remains an explicit release gate until evidence from supported physical platforms is attached.

## 5. No shortcut acceptance

A test of a fake implementation is not production-path proof. A successful CI workflow that does not exercise the exact implementation SHA is not acceptance. PR #124 remains the design record; implementation is tracked on the dedicated branch and must not be merged to main until all required gates pass.
