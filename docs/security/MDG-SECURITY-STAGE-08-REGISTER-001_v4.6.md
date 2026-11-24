# Stage 08 Security Register — O20 / O21 Closure Status

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 4.6 / 2026-11-22 |
| Supersedes | v4.5 |
| Status | O20 / O21 OPEN — VALIDATION AND OWNER ACCEPTANCE INCOMPLETE |

## 1. Latest O20 runtime login mapping evidence

Added `tests/MDGsm.Domain.Tests/Security/O20RuntimeLoginMappingPostgresProofTests.cs` and an explicit CI step.

The marked-disposable PostgreSQL test exercises a possible physical binding where:
- a synthetic LOGIN `runtime_app` has no inherited privilege;
- it may SET ROLE only to a synthetic NOLOGIN `mdgsm_runtime` ACL role;
- `session_user` remains the connection login and `current_user` becomes the ACL role;
- the ACL role has only test SELECT/INSERT grants;
- owner, migration, backup, reporting and emergency role assumption and DDL/mutation escalation are denied.

This is a disposable PoC only. It does not silently alter the accepted N.6 five logical identity baseline, does not create production credentials or role membership, and does not prove the connection pool will initialize/reset role state on every physical connection.

## 2. O20 / O21 status

| Area | Current status |
|---|---|
| OD-05 retention baseline | OWNER-ACCEPTED; schedule/Legal Hold implementation OPEN |
| IdentityAccess v1.1 public contract | SOURCE-IMPLEMENTED; exact-head CI and IdentityAccess owner acceptance pending |
| Protected Product Owner binding | NOT IMPLEMENTED; typed decision returns Unavailable |
| Organization assignment policy | Disposable maker-checker and owner path PoC added |
| Immutable Warehouse relationship history | Disposable PostgreSQL PoC added |
| Outbox/idempotency | Synthetic local PostgreSQL PoCs added; no production AuditSecurity |
| Deterministic local lock order | Candidate Organization-local proof added; global Platform order OPEN |
| DB support roles/runtime mapping | Support-role PoC plus candidate LOGIN-shim PoC; production binding/credentials/grants OPEN |
| RowScope | No current Branch/Warehouse consumer; future path re-inventory required |
| O-21 hash chain | Disposable PostgreSQL PoC only; production writer/WORM/key custody/verifier/restore/operations OPEN |
| Independent review | NOT COMPLETED |
| Organization G1 | NOT READY / NOT GRANTED |

## 3. Coordination and safety

PR #91 remains Draft/unmerged (full revert of PR #86). PR #92 remains Draft/unmerged and is still based on the current main snapshot. If #91 is accepted and merged, PR #92 must be reconciled/rebased against the new main and every test rerun. No direct main update or production database action is authorized by this register.

**Current disposition:** runtime LOGIN shim test added; no production login mapping approved. O20-E OPEN; O-20 OPEN; O-21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v4.6**
