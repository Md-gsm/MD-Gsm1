# O20-E / O20-F — Owner Decision Acceptance Record

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 |
| Version / Date | 0.2 / 2026-11-22 |
| Supersedes | v0.1 |
| Status | OWNER-ACCEPTED POLICY DECISIONS — IMPLEMENTATION / G1 / PRODUCTION ACCESS NOT AUTHORIZED |
| Governing package | PR #86 — Stage 08 security closure |
| Branch | `security/stage08-n3-adr0013-reconciliation` |
| Authority | Explicit Product Owner approval recorded in the conversation on 2026-11-22 |

## 1. Purpose and limits

This record converts exactly four bounded proposals into owner-accepted policy decisions. It does not approve Organization G1, runtime implementation, DDL, migrations, grants, production access, or PR merge. It does not silently accept adjacent implementation details that were not part of the four proposals.

## 2. Accepted decisions

### OD-01 — BranchCode / WarehouseCode canonicalization

**ACCEPTED:** v1 canonicalization uses Unicode NFC, trims leading and trailing whitespace, rejects empty values, control/format characters, and any whitespace remaining after the trim. Punctuation and non-ASCII characters are preserved. The canonical UTF-8 byte sequence is compared deterministically under PostgreSQL C collation. Codes are case-sensitive. No Persian/Arabic digit folding, transliteration, diacritic stripping, punctuation removal, or locale-sensitive case conversion is performed.

The exact equivalence rule must be implemented identically in .NET and PostgreSQL and verified with positive, negative, Unicode-normalization, collision, and cross-runtime equivalence tests before any DDL or runtime activation. Historical reservation, rename, and reassignment-collision details not stated above remain governed by the reviewed physical design and are not independently approved by OD-01.

### OD-02 — Warehouse reassignment identity

**ACCEPTED:** AccountWarehouseAssignment remains attached to the immutable WarehouseId across Warehouse-to-Branch reassignment. Reassignment does not copy, broaden, create, or automatically revoke an assignment. The Warehouse must be INACTIVE during reassignment; Warehouse RowScope is denied while inactive. After explicit reactivation, only still-active assignments to that exact WarehouseId may become effective again, and only when the current Branch is ACTIVE and IdentityAccess permission also passes. A separate Branch assignment is not implied by this decision.

### OD-03 — ARCHIVED terminal lifecycle

**ACCEPTED:** ARCHIVED is a terminal state. An ARCHIVED Branch or Warehouse cannot be reactivated. No physical deletion is authorized; historical identity and audit evidence remain retained under the applicable records policy. Archive prerequisites, dependent-resource handling, and exact transition transaction rules remain subject to physical-design and records-owner review; this decision does not waive those controls.

### OD-04 — Emergency activation

**ACCEPTED:** Emergency access requires two distinct authorized approvers, and each activation is one-use with a maximum lifetime of 30 minutes measured using trusted server/database UTC. A second activation requires a new request and two new approvals. At the exact expiry instant, access is denied synchronously; cleanup is not the authorization boundary. The emergency operator cannot approve its own activation. This emergency policy is separate from the Product Owner exception for Organization account assignments.

## 3. Controls that remain mandatory

- Product Owner self-assignment exception for Organization assignments remains as separately recorded in O20-E Data Ownership v0.4; trusted server-side owner binding is still a technical prerequisite.
- All assignment and emergency operations remain authenticated, reasoned, idempotent, concurrency-protected and audited.
- No request-supplied identity, client claim, role name, username convention, or GitHub identity may establish Product Owner or emergency authority.
- Audit-intent durability and canonical AuditSecurity acknowledgement remain separate controls; an unavailable or unacknowledged audit path must fail closed according to the accepted technical contract.
- The accepted policies do not create Organization runtime, RowScope consumers, reporting grants, emergency identities, database roles, credentials, or production access.

## 4. Required verification before implementation authorization

1. .NET/PostgreSQL canonicalization equivalence and collision tests for OD-01.
2. PostgreSQL transaction/concurrency proof for Warehouse reassignment, exact WarehouseId assignment retention, inactive denial, and explicit reactivation for OD-02.
3. Positive/negative lifecycle transition proof showing ARCHIVED terminal and no deletion/reactivation for OD-03.
4. Disposable PostgreSQL 18 emergency lifecycle proof for all allow/deny, two-approver, one-use, 30-minute expiry, synchronous revocation, audit-outbox failure/outage, cleanup and no-production-connectivity cases for OD-04.
5. Exact-head Linux and Windows PostgreSQL 18 evidence, threat-model review, technical-owner acceptance, and independent security review.

## 5. Gate disposition

| Gate | Status |
|---|---|
| OD-01 code canonicalization policy | OWNER-ACCEPTED |
| OD-02 Warehouse reassignment policy | OWNER-ACCEPTED |
| OD-03 terminal archive policy | OWNER-ACCEPTED |
| OD-04 emergency two-approver / 30-minute one-use policy | OWNER-ACCEPTED |
| Implementation / DDL / migrations | NOT AUTHORIZED |
| Organization G1 | NOT GRANTED |
| O20-E | OPEN pending contracts, proofs, retention and independent review |
| O20-F | OPEN pending disposable PostgreSQL proof and remaining technical controls |
| Overall O-20 | OPEN |

**END — MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.1**


## 6. OD-05 — Organization assignment-history retention and Legal Hold

**ACCEPTED:** lifecycle history for Organization Branch/Warehouse relationships and account assignments is retained by default; physical deletion is not the ordinary lifecycle operation. No automatic expiry, purge, hard-delete, irreversible masking/anonymization or cryptographic erasure is permitted until a controlled retention schedule and lawful disposal process are approved and implemented. Disposal may be considered only after the applicable legal retention period has elapsed, no active Legal Hold covers the record, and a documented authorization from the accountable records/legal authority exists. Legal Hold overrides ordinary expiry and blocks disposal until an authorized release is recorded.

**The retention duration remains intentionally unset.** This approval does not select a statutory period, jurisdiction or legal interpretation. Until the accountable owner/legal authority approves the schedule, history remains retained and automatic deletion is disabled.

The detailed policy is recorded in `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.1`. Legal Hold authority/scope/release, backup/replica/export treatment, privacy obligations, lawful disposal workflow, technical enforcement and proofs remain OPEN. This decision does not transfer canonical AuditEvent/SecurityEvent ownership from AuditSecurity and does not authorize G1, runtime, DDL, migration, grant, production access or disposal.

## 7. Updated gate disposition

| Gate | Status |
|---|---|
| OD-01 code canonicalization | OWNER-ACCEPTED |
| OD-02 Warehouse reassignment identity | OWNER-ACCEPTED |
| OD-03 ARCHIVED terminal lifecycle | OWNER-ACCEPTED |
| OD-04 Emergency two-approver / 30-minute one-use policy | OWNER-ACCEPTED |
| OD-05 assignment-history retention baseline / no automatic disposal | OWNER-ACCEPTED |
| Retention duration / jurisdictional schedule | OPEN — owner/legal authority |
| Legal Hold implementation and lawful disposal proof | OPEN |
| Implementation / DDL / migrations | NOT AUTHORIZED |
| Organization G1 | NOT GRANTED |
| O20-E | OPEN pending contracts, technical proofs and independent review |
| O20-F | OPEN pending disposable PostgreSQL proof and remaining technical controls |
| Overall O-20 | OPEN |

**END — MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.2**
