# O-46 — InstallationId Lock and Reconciliation Contract Proposal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-LOCK-AND-RECONCILIATION-CONTRACT-PROPOSAL-001 |
| Version / date | 0.1 / 2026-11-26 |
| Status | **TECHNICAL PROPOSAL — OWNER / INDEPENDENT REVIEW REQUIRED; NOT IMPLEMENTATION AUTHORIZATION** |
| Baseline | `main` `598fd829c02556659ca855250ece9dd05b821f87` |
| Tracking | Issue #118; JRN-REC-01/02/03 decisions in PR #92 history |

## 1. Scope and accepted boundaries

This proposal defines one InstallationId-scoped serialization barrier for O-46 challenge issuance, initial Owner claim and read-only reconciliation. It does not authorize code, SQL/DDL, Migration 0010, a View, role/grant changes, routines, credentials, keys, Owner data or runtime activation.

The existing limits remain unchanged: exactly two bootstrap PostgreSQL principals (`mdgsm_identity_bootstrap_fn_owner` NOLOGIN and `mdgsm_bootstrap_executor` initially NOLOGIN); exactly two fixed-signature SECURITY DEFINER routines (`identity_bootstrap.issue_initial_owner_challenge_v1(...)` and `identity_bootstrap.claim_initial_product_owner_v1(...)`); no third principal, Executor table/sequence access, Executor View SELECT, generic dispatcher or silent expansion of `mdgsm_runtime`. The logical `JOURNAL_RECONCILIATION_READER` is not a PostgreSQL principal. Migration 0010 remains `RESERVED — SQL NOT AUTHORIZED`.

## 2. Proposed common lock key

Use PostgreSQL transaction-scoped advisory locks. Derive the 64-bit key inside PostgreSQL; never accept a caller-supplied key or derive it independently in .NET and SQL.

Canonical candidate expression:

```sql
pg_catalog.hashtextextended(
  'MDGSM:IDENTITYACCESS:O46:OWNER-BOOTSTRAP:V1:' || lower(p_installation_id::text),
  0::bigint
)
```

The same normalized InstallationId, namespace and seed yield the same key on the same PostgreSQL server implementation. A 64-bit collision can only serialize unrelated installations unnecessarily; it must not split callers for one InstallationId. The key is ephemeral, not persisted. PostgreSQL 18 proof must verify the expression and effective EXECUTE privileges. If an already-permitted caller cannot execute the required pg_catalog functions, stop; do not add a grant or principal by inference.

## 3. Lock mode

| Path | Required call |
|---|---|
| Challenge issue routine | `pg_catalog.pg_advisory_xact_lock(key)` — exclusive |
| Owner claim routine | `pg_catalog.pg_advisory_xact_lock(key)` — exclusive |
| Reconciliation client | `pg_catalog.pg_advisory_xact_lock_shared(key)` — shared |

Use transaction-level locks only; COMMIT/ROLLBACK releases them. No session-level locks and no durable row mutation merely to simulate locking. Every path acquires the lock before reading or mutating binding, challenge or receipt state. No alternate writer path may bypass it. Keep transactions short; no Wizard/UI, network, TPM or secret-store wait while a lock is held.

## 4. Isolation and snapshot sequence

All paths use a short `READ COMMITTED` transaction. For reconciliation, acquire the shared lock as the first operational command, then read the approved View in a separate later command:

```sql
BEGIN ISOLATION LEVEL READ COMMITTED;
SELECT pg_catalog.pg_advisory_xact_lock_shared(<same-derived-key>);
SELECT ... FROM <approved-read-only-reconciliation-view> WHERE installation_id = ... AND setup_operation_id = ...;
COMMIT;
```

Do not combine lock acquisition and View read in one SELECT/CTE. At READ COMMITTED each command receives a new snapshot, so the later View read can see a writer that committed while the reader waited. REPEATABLE READ/SERIALIZABLE retain an older transaction snapshot and must be rejected for this protocol. The two routine bodies must acquire the exclusive lock before their first bootstrap-state read; exact PL/pgSQL statement-snapshot behavior must be verified by PostgreSQL 18 tests.

## 5. Cross-operation challenge invariant

The exclusive lock serializes the entire InstallationId, not just one SetupOperationId. Under it:

- An existing Owner binding blocks all new challenges.
- Same challenge_request_id + identical immutable inputs is idempotent; conflicting reuse fails closed.
- A different request ID may revoke only the prior unconsumed challenge for the same SetupOperationId, using the already-approved revoked_at transition.
- A non-expired, unrevoked, unconsumed challenge for a different SetupOperationId on the same InstallationId blocks new issuance (`setup_operation_in_progress`). Do not silently revoke or replace another operation.
- The current partial unique index is scoped by InstallationId + SetupOperationId + purpose; it does not by itself prevent two SetupOperationIds from each having an active challenge. The issuer's post-lock check and an adversarial PostgreSQL test are mandatory.
- A new SetupOperationId is prohibited while the prior challenge remains claimable or the commit outcome is unknown. An active challenge with no receipt permits only an exact retry of the same operation/challenge/hash/fingerprint. A new operation is possible only after the prior challenge is proven revoked or expired under DB time and no binding/receipt exists.

## 6. Reconciliation states

The approved View, if separately accepted/created, exposes only minimum identifiers and non-secret state: InstallationId, SetupOperationId, AccountId, ChallengeId, fingerprint version/value, completion time and minimum challenge status/expiry. It must not expose password hashes, verifier digests, raw challenges, contact data or unrelated account fields.

After the shared lock and a later fresh READ COMMITTED View SELECT:

- `COMMITTED_MATCH`: exactly one immutable binding and one append-only receipt exist and all IDs/fingerprint values match the protected Journal. Never claim again.
- `COMMITTED_CONFLICT`: mismatch, duplicate, partial state, consumed challenge without expected receipt, or invalid Journal proof. Fail closed; never repair/replace/create another Account.
- `NOT_COMMITTED_RETRY_SAME_OPERATION`: no binding/receipt, but the same operation's challenge is live and the exact protected Journal payload/hash/fingerprint is intact. Retry only the same operation/challenge/fingerprint.
- `PROVEN_ABANDONED`: no binding/receipt and the old challenge is confirmed revoked or expired under database time. Only this terminal state can support a separately authorized new SetupOperationId.
- `UNKNOWN`: DB/lock/View/Reader/key registry/Journal/evidence unavailable, stale or inconsistent. Preserve evidence and do not retry with a new operation.

A negative read while a challenge remains live is not abandonment: after the shared lock is released, a later claimant may acquire the exclusive lock. This is why a live challenge permits only same-operation idempotent retry.

## 7. Reconciliation Reader remains a hard blocker

JRN-REC-02 View ownership and JRN-REC-03 View SELECT are independent. `mdgsm_schema_owner` is only a conditional View-owner candidate, subject to target catalog, ownership graph, SET ROLE and PG18 proof. JRN-REC-03 prohibits a new principal and prohibits silently broadening runtime grants or granting Executor SELECT.

The available disposable catalog has no `reporting_ro`, no `runtime_app` LOGIN, no O-46 View and no effective SELECT path. The repository does not identify the actual production API LOGIN; Issue #117 says no production PostgreSQL connection is currently configured/tested. Therefore there is no proven caller that can execute the shared lock and SELECT the approved View. **JRN-REC-03 remains OPEN / BLOCKED.** Required next evidence: sanitized read-only catalog export from the actual target cluster plus the actual API/Setup Service DB connection identity and effective function/View privileges. No identity or grant is to be guessed or created.

## 8. PostgreSQL 18.6 evidence — bounded partial proof

PR #120 is merged into `main` at `598fd829c02556659ca855250ece9dd05b821f87`. Its dedicated disposable PostgreSQL 18.6 harness completed successfully with **24 explicit PASS assertions** (workflow run [45879818050](https://github.com/Md-gsm/MD-Gsm1/actions/runs/45879818050)). The harness verifies:

- InstallationId-scoped advisory-lock key derivation and deterministic behavior.
- Exclusive/shared lock interaction, serialization and isolation between installations.
- Transaction lock release on COMMIT, ROLLBACK and backend termination.
- Test-only SECURITY DEFINER owner and Executor EXECUTE-only boundaries; Reader view-only privilege assertions.
- Idempotency and concurrent SetupOperation race behavior.
- READ COMMITTED versus REPEATABLE READ snapshot behavior.

This is a **bounded primitive/protocol proof on a disposable CI database**, not an end-to-end O-46 implementation or production deployment test. It does not prove the production routine bodies, target-cluster effective ACLs, or that an actual deployed Reader can execute the shared lock and read an approved View. It also does not establish the central PostgreSQL target or API/Setup Service connection identity.

The following O-46 acceptance evidence remains **NOT RUN / NOT VERIFIED** and must not be inferred from PR #120:

- Full O-46 challenge-issue and Owner-claim routine behavior, including exact persisted state transitions and replay/restart handling.
- Actual target-cluster function and View ACLs under the exact deployed API/Setup Service identity.
- A real approved Reader path, including the shared lock plus approved View SELECT and negative base-table/write privilege checks.
- End-to-end crash/unknown-commit reconciliation against the protected Journal and the actual Windows/CNG/TPM environment.
- Full adversarial acceptance suite against the approved deployment topology.

Until those proofs exist, JRN-REC-03 remains OPEN / BLOCKED and no Bootstrap, Migration 0010, production connection, role/grant, feature activation, G1 or Go-Live is authorized.

## 9. Review gates and non-authorization

Independent review must accept the lock namespace/key, shared/exclusive protocol, cross-SetupOperationId challenge invariant, negative-result semantics and actual pg_catalog EXECUTE ACL. JRN-REC-02/03 must be reconciled against a real target catalog and live connection identity. If no conforming Reader exists, stop and return for an explicit Product Owner architecture decision; do not add a bypass.

This proposal authorizes documentation/review only. It does not authorize SQL/DDL, Migration 0010 authoring/apply, View creation, role/principal/login/membership/grant changes, routine installation, Executor LOGIN, CNG/TPM keys, Journal provisioning, Wizard/Setup Service/Named Pipe implementation, Owner binding, Recovery, listener/feature activation, G1 or Go-Live.

## References

- PostgreSQL 18 Advisory Lock Functions: https://www.postgresql.org/docs/18/functions-admin.html
- PostgreSQL 18 Explicit Locking / Advisory Locks: https://www.postgresql.org/docs/18/explicit-locking.html
- PostgreSQL 18 Transaction Isolation: https://www.postgresql.org/docs/18/transaction-iso.html
