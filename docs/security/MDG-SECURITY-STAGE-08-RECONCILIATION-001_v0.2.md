# Stage 08 — Controlled Security Design Reconciliation Addendum

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-RECONCILIATION-001 |
| Version | 0.2 |
| Date | 2026-11-21 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner-policy reference | ADR-0013, including the explicit session-policy correction dated 2026-11-07 |
| Applies to | MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001 v0.1 |
| Status | CONTROLLED SUCCESSOR ADDENDUM — N.3 OWNER DECISION D RECORDED; O-20/O-21 CANDIDATE SOURCE ADDED; EVIDENCE/REVIEW GATES OPEN |

## 1. Purpose and precedence

This addendum is read together with Stage 08 Security Design v0.1. It does not alter or rewrite that historical version. Where this addendum identifies a discrepancy or replaces an interpretation, this addendum controls the Stage 08 design package until a later controlled successor is accepted.

The v0.1 statement “COMPLETE AS AN INTEGRATED DESIGN SPECIFICATION” is superseded. Stage 08 is an integrated design draft with unresolved architecture crosswalk and implementation/evidence dependencies. The design is not independently approved or release-ready.

## 2. Session-policy conflict: v3.0.3 N.3 versus ADR-0013

The accepted master v3.0.3 Appendix N.3 still displays historical 5/2/1 limits, including one active session per account and limits for System Manager accounts/sessions.

ADR-0013 contains a later explicit owner correction dated 2026-11-07:
- no fixed absolute session TTL;
- no approved per-account concurrent-session cap;
- heartbeat 30 seconds and lease 180 seconds;
- initial system cap of five distinct active accounts;
- an authorized administrator may change that system-wide cap with a dedicated permission and audit.

The Stage 08 design follows the later explicit ADR-0013 owner correction for the IdentityAccess session contract. However, the contradictory N.3 wording remains an OPEN architecture-document reconciliation item. This addendum does not amend the accepted master or independently change policy. A controlled architecture successor/correction must reconcile N.3 with ADR-0013 before the cross-document baseline can be marked consistent.

Until that correction is registered, do not implement the obsolete one-session-per-account or 5/2/1 restrictions merely because they remain in N.3; equally, do not claim that N.3 itself has already been corrected.

## 2.1 Owner resolution of the two System Manager limits — 2026-11-21

The owner explicitly selected option **D** in the N.3 decision discussion. The controlled N.3/ADR-0013 crosswalk is recorded in `docs/architecture/MDG-ARCH-N3-ADR-0013-RECONCILIATION-001_v0.2.md`.

- No N.3-specific numeric cap applies to the number of provisioned System Manager accounts.
- At most one active System Manager session is permitted globally across all System Manager accounts.
- This is a role-wide global session slot, not a general per-account session cap. The general no-approved-per-account-concurrent-session-cap rule remains unchanged for other accounts.
- The implementation must bind this limit to the canonical System Manager role identity, claim/release the global slot atomically in PostgreSQL across API instances, and test concurrent claims. Do not infer that a generic `manager` role is the System Manager role.
- The accepted architecture master v3.0.3 is not silently rewritten by this addendum; the successor crosswalk records the later owner decision. No session policy activation is authorized by this document.

## 2.2 Current O-20 / O-21 / O-46 execution evidence

- **O-20:** PR #86 adds a disposable `runtime_app` LOGIN/API end-to-end test candidate and wider role/DDL/escalation denial assertions. The candidate applies only the existing IdentityAccess `mdgsm_runtime` grant script. It is not a complete N.6 object-by-object Role/Object/Privilege/RowScope matrix; no exact-head CI PASS is claimed. Production identities/grants are unchanged.
- **O-21:** PR #86 adds a disposable PostgreSQL Hash Chain PoC test candidate with independent writer LOGINs, owner-controlled append/lock functions, canonical event envelope, concurrent writes, rollback, backend termination, unknown-commit replay, pinned RSA-PSS test checkpoints and a read-only verifier. Exact-head CI is pending. Test keys and local checkpoint files are not production key custody or external WORM anchors.
- **O-46:** PR #86 adds no runtime bootstrap/recovery code. Email-only OTP policy remains owner-approved, while trusted bootstrap delivery, Recovery entrypoint and verified-email proof, SMTP, dedicated key custody, schema reservation, atomicity proofs and independent review remain open. Migration 0009 remains reserved.
## 3. Database identity alignment: v3.0.3 N.6

The canonical logical identity names in accepted master N.6 are:
- `runtime_app`: only approved API/owner-contract commands and scoped reads/writes; no SUPERUSER, ownership, DDL, TRUNCATE, GRANT, bypass or direct mutation of immutable Journal/Audit/Movement facts.
- `migration_runner`: versioned migration execution only in an approved maintenance window after Gate B-final; never the routine application credential.
- `backup_operator`: controlled backup/WAL read and restore operations; no daily business writes.
- `reporting_ro`: SELECT on approved views/read models with RowScope; no DML or out-of-scope direct Journal/Movement access.
- `emergency_operator`: explicit allow-listed incident-mode technical/recovery operations only; no commercial commands or posting.

The `mdgsm_db_owner`, `mdgsm_migrator`, `mdgsm_api_runtime`, `mdgsm_worker_runtime`, `mdgsm_audit_ingest` and `mdgsm_readonly_support` labels in Stage 08 v0.1 are illustrative placeholders, not owner-approved PostgreSQL role names. They must not be created or treated as accepted production identities.

PR #86 contains a test-only mapping candidate for `runtime_app` -> disposable LOGIN plus the `mdgsm_runtime` NOLOGIN support group. The production LOGIN principals, membership model, non-login object owner, SECURITY DEFINER ownership, full grants, default privileges, backup/emergency/reporting separation and RowScope remain OPEN under O-20. No additional worker or audit-ingest identity is authorized by this addendum. Any separate identity requires an explicit O-20 permission-matrix decision.

## 4. Hash Chain requirements: v3.0.3 N.7 / O-21

The Stage 08 design must be read with all N.7 requirements:
1. deterministic versioned canonical serialization;
2. ordered events and predecessor hash;
3. atomic chain-head locking/transaction to prevent forks;
4. two independent concurrent writers;
5. rollback, crash and replay proof;
6. periodic signed or external checkpoint and independent comparison;
7. key custody, rotation, backup, recovery and revocation;
8. independent verification utility and runbook;
9. failure recording and alerts for gaps/corruption.

The v0.1 SEC-T-020 catalogue remains the acceptance standard. The new PR #86 candidate covers two independent LOGIN writers on one stream, commit/rollback/backend-termination/unknown-outcome replay, a read-only verifier, test checkpoint signatures and tamper/gap detection. It remains SOURCE ADDED / CI PENDING only; SEC-T-023..026 production external anchor, key custody/recovery/revocation and restore continuity remain OPEN.

## 5. IdentityAccess lifecycle gaps: O-45 and O-46

- **O-45 OPEN:** heartbeat/lease/revocation does not close token rotation and stale-session cleanup. The lifecycle must cover client crash, graceful close, temporary network loss, reconnect, token rotation, abandoned-session cleanup and immediate revocation with threat-model-based tests.
- **O-46 OPEN:** the Stage 08 v0.1 document does not complete one-time bootstrap, recovery capture-then-invalidate, administrator recovery/runbook or secret/logging proofs. Email-only recovery remains the latest owner-approved channel for this phase; SMS and offline recovery-code issuance remain outside scope. This does not enable recovery at runtime.

## 6. Required test and review additions

The following acceptance scenarios are added to the Stage 08 catalogue. They are specifications only and NOT RUN:
- **SEC-T-031:** two independent Hash Chain writers; commit/rollback/crash/replay produce one valid stream sequence without a fork.
- **SEC-T-032:** checkpoint signing-key custody, backup, recovery, rotation and revocation preserve historical verification and fail closed on trust gaps.
- **SEC-T-033:** session token rotation and stale-session cleanup across crash, reconnect and revocation.
- **SEC-T-034:** bootstrap and recovery capture-then-invalidate; one-time state cannot be replayed and raw secrets are not logged or retained.
- **SEC-T-035:** canonical N.6 role/grant matrix with real LOGIN credentials, membership and RowScope evidence.
- **SEC-T-036:** controlled reconciliation of v3.0.3 N.3 with the later ADR-0013 owner correction.

## 7. Current status and non-authorization

O-20, O-21, O-45 and O-46 remain OPEN. The two System Manager N.3 policy questions are resolved by owner option D and recorded in the N.3 v0.2 crosswalk; implementation and cross-document master reconciliation remain controlled follow-up work. SEC-T-001..036 are NOT RUN / NOT PASS. No code, SQL, DDL, migration, grant, production credential, runtime feature activation, production listener, audit purge, G1 activation, Posting Release or Go-Live is authorized.

**END — MDG-SECURITY-STAGE-08-RECONCILIATION-001 v0.2**
