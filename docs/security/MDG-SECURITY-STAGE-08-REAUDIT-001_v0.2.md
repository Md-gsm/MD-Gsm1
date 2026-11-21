# MDG Security Stage 08 — Internal Reconciliation Review

| Field | Value |
|---|---|
| Review ID | MDG-SECURITY-STAGE-08-REAUDIT-001 |
| Version | 0.2 |
| Date | 2026-11-21 |
| Audited package | Stage 08 Security Design v0.1 + Reconciliation Addendum v0.1 |
| Baseline | MDG-ARCH-MASTER-001 v3.0.3; ADR-0013 |
| Review type | Internal consistency review; NOT independent external attestation |
| Disposition | **REVISED DESIGN DRAFT; BASELINE RECONCILIATION AND RELEASE GATES OPEN** |

## 1. Executive conclusion

Review of PR #74 found the Stage 08 security design technically substantial, but not ready to merge as a “complete integrated design” without reconciliation. The v0.1 role names were not aligned to the canonical database identity names in v3.0.3 N.6; the session contract followed ADR-0013 but did not record the visible conflict with v3.0.3 N.3; and important requirements from N.7/O-21, O-45 and O-46 were not explicitly tracked as open acceptance dependencies.

The controlled addendum records those discrepancies without altering the accepted architecture master or inventing new owner policy.

## 2. Findings

| Finding | Disposition |
|---|---|
| N.3 historical 5/2/1 wording conflicts with the later explicit ADR-0013 session correction | OPEN — controlled architecture-document correction required |
| v0.1 illustrative `mdgsm_*` database identities differ from canonical N.6 names | Corrected by mapping to `runtime_app`, `migration_runner`, `backup_operator`, `reporting_ro`, `emergency_operator`; actual credentials/grants remain OPEN under O-20 |
| N.7/O-21 requires two independent writers and commit/rollback/crash/replay proof | Added to requirements; NOT RUN |
| N.7 requires checkpoint key custody, backup, recovery and revocation | Added to requirements; NOT RUN |
| O-45 token rotation and stale-session cleanup | OPEN / NOT RUN |
| O-46 bootstrap and recovery capture-then-invalidate/logging | OPEN / NOT RUN |

## 3. Test/evidence boundary

SEC-T-001..036 are design acceptance specifications only: NOT RUN / NOT PASS.

Existing Gate D evidence for eight logical restore PoCs and 36 IdentityAccess integration tests is limited to the disposable Infrastructure/IdentityAccess schema and the tested database-level boundary. It does not establish application-level production identity, account recovery runtime, Hash Chain operation, full-system recovery, WAL/PITR, off-site backup or Gate D acceptance.

## 4. Final decision

| Scope | Decision |
|---|---|
| Stage 08 security design package | Revised controlled draft; suitable for independent exact-SHA review after CI |
| v3.0.3 N.3 / ADR-0013 crosswalk | OPEN |
| Database role/grant matrix O-20 | OPEN |
| Hash Chain two-writer/key recovery O-21 | OPEN / NOT RUN |
| Session lifecycle O-45 | OPEN / NOT RUN |
| Bootstrap/recovery lifecycle O-46 | OPEN / NOT RUN |
| Runtime recovery / Hash Chain | NOT IMPLEMENTED / NOT RUN |
| Gate D | OPEN / NOT VERIFIED |
| G1 / production / Posting Release / Go-Live | NOT AUTHORIZED |
| Independent reviewer attestation | PENDING |

**END — MDG-SECURITY-STAGE-08-REAUDIT-001 v0.2**
