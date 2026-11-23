# GOV-OWNER-AUTH-001 — Product Owner Final Governance Authority

| Field | Value |
|---|---|
| Decision ID | GOV-OWNER-AUTH-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | OWNER-APPROVED CHANGE REQUEST; GOVERNING MASTER INTEGRATION / TECHNICAL REVIEW OPEN |
| Owner authority | Product Owner decision recorded in PR #92 comment 7094120301 |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Decision packet | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6 |
| PR | #92 Draft / unmerged |

## 1. Decision

The Product Owner is the highest final product decision authority for MD-Gsm and its controlled product changes. This authority applies to:
- product scope, architecture direction and product priorities;
- approval, rejection or return for correction of module, contract and design changes;
- access-governance and delegated-management policy;
- database model and migration decisions, subject to technical and security gates;
- release decisions and feature activation;
- project or capability prioritization, suspension or termination;
- appointment or replacement of accountable technical/specialist responsibilities;
- acceptance of residual product risk only within applicable law, security invariants and mandatory repository/release controls.

No System Manager, developer, DBA, Worker, service identity or technical function may independently acquire or transfer Product Owner authority or publish product changes outside the approved change/release path.

## 2. Specialist accountability and evidence duties

The responsible specialist is accountable for the correctness of domain analysis, design, implementation, tests, audit and evidence in that specialty. A specialist may identify a proposal as technically invalid, unsafe, incomplete or non-compliant and must provide the rationale, consequences, evidence and feasible remediation options.

The Product Owner makes the final product decision after receiving the required specialist evidence: approve, reject, return for correction, prioritize or stop. Specialist evidence is not optional where it is a technical, legal or security prerequisite, and the Product Owner's final product authority does not replace specialist acceptance where the architecture or gate requires it.

Initial responsibility mapping:
1. Inventory lock order and transaction-boundary semantics — Inventory technical owner and cross-module architect prepare design and concurrency evidence.
2. Transaction Participant / Unit of Work — affected module teams implement and prove their participation.
3. UUIDv7 conformance — Platform supplies implementation, cross-runtime and clock/rollback evidence.
4. IdentityAccess public contract — IdentityAccess and Organization technical owners establish and validate the contract.
5. AuditSecurity durable ACK — AuditSecurity and the event consumer establish event and acknowledgement semantics.
6. Isolated Backup/Restore — infrastructure/security owners prove isolation, recovery and restore safety.
7. Legal retention and records classification — qualified Iran legal/records authority classifies operator and record classes and approves applicable obligations.
8. Independent exact-SHA Security review — independent reviewer records findings, severity, disposition and retests.

This responsibility list does not appoint a named owner where governance evidence does not identify one. Accountable individuals/teams must be formally assigned before their gates can be accepted.

## 3. Non-overridable limits

The Product Owner cannot:
- report an unrun or failed test as passed, falsify or suppress required evidence;
- waive applicable law or represent an unverified legal interpretation as clearance;
- delete or shorten retention for immutable WORM evidence outside its lawful controls;
- override immutable accounting/financial-document and ledger invariants;
- bypass mandatory technical, security, independent-review, branch-protection or repository gates;
- certify their own work as an independent Security review;
- transfer, replace or appoint a successor Product Owner.

The Product Owner identity is the installation-created bound AccountId, immutable and non-transferable. Recovery may restore only the same AccountId/principal; no alternate-owner fallback exists. Unverifiable recovery makes owner-only operations unavailable.

## 4. Controlled change and release traceability

Every material product change must have:
- a uniquely identified request and rationale;
- an accountable executor and relevant specialists;
- required technical/security/legal evidence and independent review where applicable;
- an explicit Product Owner decision;
- document/source version and immutable audit history.

Publication or release occurs only after all applicable technical, security, legal, independent-review and repository gates have passed. An owner decision may approve the product direction but does not itself authorize DDL, migrations, production credentials, grants, cloud resources, runtime activation, Go-Live or PR merge.

## 5. Scope and acceptance boundary

This request governs product decision authority and responsibility boundaries only. It does not assign ownership of specific module data, identity stores, Platform standards, global lock order, legal records, AuditSecurity events or technical implementation. Their domain owners retain those responsibilities and must provide evidence for Product Owner acceptance.

The decision is owner-approved in principle by the explicit decision recorded in PR #92. It has not yet been integrated into MDG-ARCH-MASTER-001 v3.0.3. Master integration must follow the project's controlled architecture amendment, checksum, consistency review and independent-review process; do not edit the Master in place outside that process.

**Disposition:** Product Owner policy — APPROVED; architecture Master successor/addendum integration — OPEN; specialist responsibility assignments — OPEN where no accountable owner is formally named; implementation/Go-Live authority — NOT GRANTED.

**END — GOV-OWNER-AUTH-001 v0.1**
