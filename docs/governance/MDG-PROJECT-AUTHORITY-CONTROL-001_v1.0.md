# MDG-PROJECT-AUTHORITY-CONTROL-001
## Project-wide Architecture Master Authority and Document Hierarchy — v1.0

| Field | Value |
|---|---|
| Document ID | MDG-PROJECT-AUTHORITY-CONTROL-001 |
| Version | 1.0 |
| Decision date | 2026-11-24 |
| Product Owner | @Md-gsm |
| Current governing Master | MDG-ARCH-MASTER-001 v3.0.4 |
| Current baseline registration | MDG-ARCH-BASELINE-REGISTRATION-001 v1.3 |
| Status | OWNER-ACCEPTED AUTHORITY CONTROL; repository registration through PR #94 pending |

## 1. Owner's authority decision

Effective 2026-11-24, **MDG-ARCH-MASTER-001 v3.0.4 is the sole accepted, official and governing Architecture/Accounting Master for the MD-Gsm project**.

All earlier Architecture Master versions (including v3.0.3) and prior baseline-registration records (v1.0–v1.2) are historical work records. Their prior status statements remain evidence of the decisions and sequence at the time, but they do not define current authority.

## 2. How other project documents are treated

The statement that earlier documents are work history does not mean that technical evidence, module contracts, accepted owner decisions, tests or implementation records are deleted or ignored.

- Earlier Master versions and old baseline-registration records: historical snapshots only.
- Current subordinate architecture, module, security, accounting, operations and test artifacts: supporting work products. They remain usable only within their own stated scope and status, and must not contradict or supersede v3.0.4.
- Historical reports, workpapers, audit snapshots and PR descriptions: provenance/evidence only. Their historical conclusions are not automatically current.
- Where a current subordinate artifact conflicts with v3.0.4 or a later explicit Product Owner decision, it must be reconciled through a controlled successor or dated supersession notice. Do not silently rewrite historical snapshots.
- An owner-approved decision does not imply implementation, test PASS, evidence acceptance, independent review, gate passage or release authorization.

## 3. Current owner-recovery decision in v3.0.4

The initial Product Owner's identity remains immutable. Recovery must restore only the original AccountId and Product Owner binding.

Two alternative recovery methods are accepted:
1. A recovery code chosen by the Product Owner in the initial Setup Wizard. Only a salted, versioned verifier/hash is persisted; raw code is never logged or stored in plaintext.
2. A single-use Email OTP sent to the verified Product Owner email.

Email delivery uses one centrally managed Google/Gmail sender account owned by MD-Gsm. Store customers do not configure SMTP or sender credentials. The account and authorization are created/configured during final release preparation. No account, OAuth secret, token or production email service is created at this stage. The recovery code remains available if the central email service is unavailable.

Other user accounts remain recoverable/resettable only under Product Owner authority. Product Owner recovery does not require approval by the Product Owner account itself.

## 4. Repository reconciliation scope

The controlled registration vehicle is PR #94, branch docs/official-master-v3.0.3-20261124. The current baseline registration is v1.3. The following current successor registers are included in this reconciliation:
- MDG-OPEN-ISSUE-REGISTER-001 v1.6
- MDG-WP06-DOCUMENT-REGISTER-001 v2.1
- MDG-SECURITY-STAGE-08-REGISTER-001 v2.8

The root README points to this authority control and the governing Master. Further active-document references must be audited and reconciled before the project can be described as fully synchronized. Historical snapshots remain unchanged.

## 5. Independent gates and non-authorization

This authority decision does not authorize implementation, source/runtime changes, SQL/DDL, schema, roles, grants, migrations, production credentials, Gmail/OAuth secrets, listener activation, IdentityAccess G1, Posting Release, production migration or Go-Live. Independent technical review, exact-head CI, implementation and tests, evidence acceptance, legal/security review and release gates remain separate.

**END — MDG-PROJECT-AUTHORITY-CONTROL-001 v1.0**
