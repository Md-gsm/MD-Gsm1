# MDG-ARCH-BASELINE-REGISTRATION-001
## Architecture Master Acceptance and Repository Registration Record — Version 1.3

| Field | Value |
|---|---|
| Document ID | MDG-ARCH-BASELINE-REGISTRATION-001 |
| Version | 1.3 |
| Decision / record date | 2026-11-24 |
| Owner | Product Owner — @Md-gsm |
| Status | **OWNER-ACCEPTED OFFICIAL MASTER — v3.0.4** |
| Governing artifact | `docs/architecture/MDG-ARCH-MASTER-001_v3.0.4.txt` |
| Owner-acceptance effective date | 2026-11-24 |
| Repository registration vehicle | PR #94 (documentation authority reconciliation); merge pending |
| Prior repository registration | v3.0.3 registered in `main` through PR #71 merge commit `8f89653e25e6a599387128e0209f5009d9dd59cd` |
| Supersedes | v1.2 as current authority; v1.0–v1.2 remain historical records |

## 1. Current Product Owner directive

On 2026-11-24, the Product Owner explicitly designated **MDG-ARCH-MASTER-001 v3.0.4 as the sole current, accepted, official and governing Architecture/Accounting Master** for all MD-Gsm project work.

1. v3.0.4 is the current normative baseline from this decision onward.
2. v3.0.3, v3.0.1, v3.0, v2.6, v2.5 and earlier versions are historical snapshots/reference records only. They must not be used as current governing baselines.
3. All active architecture documents, module baselines, contracts, workpapers, registers, implementation plans, review checklists and project-level references must be reconciled to v3.0.4. Their own independent status and acceptance remain unchanged unless expressly updated.
4. The prior v1.2 statement that v3.0.3 remains the sole governing Master is superseded by this v1.3 record. The earlier record is preserved as historical evidence and is not silently rewritten.
5. The prior v1.1 and v1.2 records are retained unchanged as a record of earlier decisions. Their former current-status statements do not override this v1.3 decision.

## 2. v3.0.4 scope and incorporated owner decisions

v3.0.4 retains the complete architecture/accounting body derived from v3.0.3 and incorporates the bounded IdentityAccess / Bootstrap / Recovery changes recorded under CHG-101, including the 2026-11-24 clarification of initial Product Owner recovery.

The owner-recovery policy is:
- recovery restores access only to the original immutable Product Owner AccountId and binding;
- no second owner, replacement, transfer or successor can be created;
- the owner can recover by either an owner-defined recovery code chosen in the initial Setup Wizard or an Email OTP sent to the verified owner email;
- customers do not configure SMTP or sender credentials;
- the product uses a centrally managed Google/Gmail sender account owned by MD-Gsm; its account and authorization are provisioned during final release preparation, not customer installation;
- sender credentials/tokens must not be embedded in distributed binaries, source control or store databases; the final central sending component, secure transport, OAuth/token custody, abuse controls and operational behavior remain design and release gates;
- the owner-defined recovery code remains independent of email-service availability.

This is an owner-approved product/architecture decision. It does not assert implementation, test PASS, independent review or release readiness.

## 3. Repository registration status

- The v3.0.4 governing artifact and this v1.3 registration record are being added through PR #94, branch `docs/official-master-v3.0.3-20261124`.
- Until PR #94 is merged, GitHub `main` may still physically contain v3.0.3 and the v1.2 record. This repository-state lag does not revoke or postpone the Product Owner's effective decision; PR #94 is the controlled registration vehicle.
- PR #92 remains a separate O20/O21 technical workstream. Its branch copy of v3.0.4 and historical registration v1.1 are not the repository's sole registration mechanism.
- The prior PR #94 description and branch purpose that reaffirmed v3.0.3 are superseded and must be updated to reflect this v1.3 authority.

## 4. Repository-wide reconciliation requirements

The following active project surfaces must be reconciled against v3.0.4:
- root README, project charter and architecture index;
- current architecture baseline and registration records;
- current IdentityAccess module baseline, Bootstrap/Recovery decisions, CHG-101 and O-46 registers;
- current security, Platform, release and open-issue registers;
- current module/finance workpaper document registers and active PR descriptions;
- SHA-256 manifests and CI checks that validate controlled artifacts.

Historical versioned snapshots remain unchanged for traceability unless a specific correction is required to prevent a stale document from being mistaken for current authority. In that case, add a dated supersession notice or a new successor record; do not silently rewrite the historical decision.

## 5. Independent status boundaries

Master acceptance and repository registration do not imply independent technical review, design completion, implementation, test PASS, evidence acceptance, legal clearance, Gate C or Gate D PASS, Posting Release, production migration, IdentityAccess G1, runtime activation or Go-Live. Each retains its own criteria and status. No NOT_RUN or NOT_VERIFIED item is promoted by this record.

## 6. Explicit non-authorization

This is a document-authority and product-policy decision only. It does not authorize production Gmail credentials, OAuth secrets, a live central email service, SQL/DDL, schema or role creation, GRANT/REVOKE, credentials for PostgreSQL, migrations, production database changes, module activation, Posting Release, production migration or Go-Live.

## 7. Revision history

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-11-21 | Original registration of v3.0.3 through PR #71. |
| 1.1 | 2026-11-23 | Historical record of the bounded v3.0.4 successor direction under CHG-101. |
| 1.2 | 2026-11-24 | Historical reaffirmation of v3.0.3; superseded by the Product Owner's later explicit direction in v1.3. |
| 1.3 | 2026-11-24 | Records v3.0.4 as the sole official governing Master and incorporates the owner-recovery decision: owner-defined recovery code or centrally sent Gmail OTP. |

**END — MDG-ARCH-BASELINE-REGISTRATION-001 v1.3**
