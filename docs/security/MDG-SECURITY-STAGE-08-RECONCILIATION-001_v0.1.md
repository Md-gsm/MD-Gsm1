# Stage 08 — Controlled Security Design Reconciliation Addendum

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-RECONCILIATION-001 |
| Version | 0.1 |
| Date | 2026-11-21 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner-policy reference | ADR-0013, including the explicit session-policy correction dated 2026-11-07 |
| Applies to | MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001 v0.1 |
| Status | CONTROLLED DESIGN AMENDMENT — RECONCILIATION ITEMS OPEN — NO IMPLEMENTATION AUTHORIZATION |

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

## 3. Database identity alignment: v3.0.3 N.6

The canonical logical identity names in accepted master N.6 are:
- `runtime_app`: only approved API/owner-contract commands and scoped reads/writes; no SUPERUSER, ownership, DDL, TRUNCATE, GRANT, bypass or direct mutation of immutable Journal/Audit/Movement facts.
- `migration_runner`: versioned migration execution only in an approved maintenance window after Gate B-final; never the routine application credential.
- `backup_operator`: controlled backup/WAL read and restore operations; no daily business writes.
- `reporting_ro`: SELECT on approved views/read models with RowScope; no DML or out-of-scope direct Journal/Movement access.
- `emergency_operator`: explicit allow-listed incident-mode technical/recovery operations only; no commercial commands or posting.

The `mdgsm_db_owner`, `mdgsm_migrator`, `mdgsm_api_runtime`, `mdgsm_worker_runtime`, `mdgsm_audit_ingest` and `mdgsm_readonly_support` labels in Stage 08 v0.1 are illustrative placeholders, not owner-approved PostgreSQL role names. They must not be created or treated as accepted production identities.

The actual LOGIN principals, membership model, non-login object owner, SECURITY DEFINER ownership, grants, default privileges and RowScope remain OPEN under O-20. No additional worker or audit-ingest identity is authorized by this addendum. Any separate identity requires an explicit O-20 permission-matrix decision.

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

The v0.1 test SEC-T-020 (“concurrent chain append”) is not by itself sufficient unless it proves two independent writers against the same stream and exercises commit, rollback, crash and replay without a fork. SEC-T-023..026 must also prove checkpoint trust, key custody/recovery/revocation and restore continuity. These requirements remain OPEN / NOT RUN under O-21.

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

O-20, O-21, O-45, O-46 and the N.3/ADR-0013 crosswalk remain OPEN. SEC-T-001..036 are NOT RUN / NOT PASS. No code, SQL, DDL, migration, grant, production credential, runtime feature activation, production listener, audit purge, G1 activation, Posting Release or Go-Live is authorized.

**END — MDG-SECURITY-STAGE-08-RECONCILIATION-001 v0.1**
