# MDG-ARCH-BASELINE-REGISTRATION-001
## Architecture Master Acceptance and Repository Registration Record — Version 1.1

| Field | Value |
|---|---|
| Document ID | MDG-ARCH-BASELINE-REGISTRATION-001 |
| Version | 1.1 |
| Decision / record date | 2026-11-23 |
| Owner | Product Owner — @Md-gsm |
| Status | **OWNER-ACCEPTED SUCCESSOR MASTER v3.0.4 IN PR #92 — MAIN REGISTRATION PENDING** |
| Governing artifact in this PR | docs/architecture/MDG-ARCH-MASTER-001_v3.0.4.txt |
| Branch / PR | security/o20-o21-final-closure / PR #92 |
| Existing main baseline | MDG-ARCH-MASTER-001 v3.0.3, registered through PR #71 |
| Supersedes | Registration record v1.0 for the owner-directed successor decision only; v1.0 remains the historical registration fact for v3.0.3 |

## 1. Owner direction and acceptance

On 2026-11-23, the Product Owner explicitly accepted the bounded IdentityAccess/O-46 changes recorded under CHG-101 and accepted Master successor v3.0.4 as the target governing document for the reconciled bootstrap and Recovery policy.

The v3.0.4 successor updates Appendix N.1, N.2, IAM-BOOTSTRAP, IAM-RECOVERY, O-46 status and the related cross-references. It leaves all unrelated v3.0.3 architecture/accounting decisions unchanged.

The controlling owner decision is MDG-SECURITY-O46-BOOTSTRAP-OWNER-DECISION-001_v0.1.md. The change request is CR-ARCH-IDENTITYACCESS-BOOTSTRAP-OWNER-001_v0.1.md (CHG-101).

## 2. Main-branch and PR registration facts

- v3.0.3 remains the registered Master in main until PR #92 is independently reviewed and merged.
- v3.0.4 is currently present only on the controlled PR #92 branch. It must not be represented as already registered in main.
- PR #92 must remain Draft, open and unmerged until exact-head CI, independent Security/Architecture review and all applicable acceptance gates are complete.
- Owner acceptance of the bounded Master successor does not replace independent review, implementation, test evidence, legal clearance, release or Go-Live gates.

## 3. Current architecture authority hierarchy

1. Product Owner decisions and accepted accounting policies within their explicit scope.
2. MDG-ARCH-MASTER-001 v3.0.4 — owner-accepted successor target in PR #92; effective repository-wide after independent review and merge.
3. Until v3.0.4 merges, v3.0.3 remains the registered Master in main.
4. Accepted ADRs, IdentityAccess Module Baseline v1.1, O-46 owner decision and controlled module contracts consistent with the active Master version.
5. Test specifications, implementation evidence, audits and release records retain their independent statuses.

## 4. Independent status boundaries — unchanged

- Independent review of the exact v3.0.4 / PR #92 final SHA: PENDING.
- O46-G01 owner-decision component: RECORDED; technical threat review and implementation proof OPEN.
- O46-G02 through O46-G07: retain their actual open/not-started states.
- Bootstrap/Recovery runtime, production listener, migrations, grants, roles, permission seeds and G1 activation: NOT AUTHORIZED.
- No test marked NOT RUN or NOT VERIFIED is promoted by this registration record.

## 5. Required repository reconciliation

| Artifact | Current controlled successor |
|---|---|
| Architecture Master target | MDG-ARCH-MASTER-001 v3.0.4 (PR #92) |
| IdentityAccess module baseline | MDG-IDENTITYACCESS-MODULE-BASELINE-001_v1.1.md |
| O-46 owner decision | MDG-SECURITY-O46-BOOTSTRAP-OWNER-DECISION-001_v0.1.md |
| Bootstrap trust proposal | MDG-SECURITY-O46-BOOTSTRAP-TRUST-BOUNDARY-PROPOSAL-001_v0.2.md |
| O-46 audit | MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001_v0.5.md |
| First-run/data preservation ADR | ADR-0019 with 2026-11-23 owner-decision addendum |
| Master change record | CR-ARCH-IDENTITYACCESS-BOOTSTRAP-OWNER-001_v0.1.md / CHG-101 |

## 6. Revision history

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-11-21 | Records v3.0.3 as the sole owner-accepted Master and its registration through PR #71. |
| 1.1 | 2026-11-23 | Records owner-accepted Master successor v3.0.4 for CHG-101, preserves v3.0.3 as the currently registered main baseline until PR #92 is reviewed and merged, and keeps all independent implementation/release gates separate. |

**END — MDG-ARCH-BASELINE-REGISTRATION-001 v1.1**
