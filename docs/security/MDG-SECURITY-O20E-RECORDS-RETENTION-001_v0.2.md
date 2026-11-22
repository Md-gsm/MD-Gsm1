# O20-E Organization Assignment History — Records Retention and Legal Hold Policy

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-RECORDS-RETENTION-001 |
| Version / Date | 0.2 / 2026-11-22 |
| Supersedes | v0.1 |
| Status | OWNER-ACCEPTED RETENTION PRINCIPLES; RETENTION SCHEDULE AND LEGAL IMPLEMENTATION OPEN |
| Authority | Explicit Product Owner approval recorded in the conversation on 2026-11-22; no digital signature is claimed |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Related policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |
| Related design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.5; MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.5 |

## 1. Accepted policy decision

The Product Owner accepted the following default retention and disposal policy for Organization-owned Branch/Warehouse and AccountBranchAssignment/AccountWarehouseAssignment history:

1. Assignment and relationship lifecycle history is retained by default. Physical deletion is not the normal lifecycle operation.
2. No automatic expiry, purge, hard-delete, anonymization, irreversible masking, or cryptographic-erasure job may run until an applicable retention schedule and lawful disposal conditions have been explicitly approved and implemented.
3. Disposal may be considered only after the applicable legally required retention period has elapsed, there is no active Legal Hold covering the record, and a documented authorization has been recorded by the accountable records/legal authority under the approved disposal procedure.
4. A Legal Hold overrides ordinary retention expiry and blocks disposal for its covered records. A hold must remain effective until an authorized release is recorded.
5. The exact retention duration is intentionally **UNSET**. No number of years, months, or days is inferred by this decision. Until the owner/legal authority approves the schedule, automatic deletion remains disabled and the history remains retained.
6. This decision establishes a conservative policy baseline; it does not assert legal clearance, select a jurisdiction-specific statutory period, or authorize disposal.

## 2. Records in scope

The policy applies to Organization-owned historical evidence for:
- Branch/Warehouse master lifecycle transitions;
- Warehouse-to-Branch relationship changes and their historical intervals;
- AccountBranchAssignment and AccountWarehouseAssignment creation, change, approval, rejection and revocation;
- the actor, subject, approver, reason, effective time, aggregate/version, idempotency and correlation references needed to reconstruct the authorized state transition;
- associated local audit-intent/outbox delivery state to the extent needed for reconciliation and proof of canonical AuditSecurity acknowledgement.

The policy does not transfer ownership of canonical shared AuditEvent or SecurityEvent from AuditSecurity to Organization. Organization may own domain history for its aggregates; AuditSecurity remains the canonical shared audit/security-event owner.

## 3. Data minimization and separation

Retained history must contain only the minimum identifiers and decision metadata required to reconstruct and audit the Organization lifecycle. It must not include passwords, credential hashes, bearer/session tokens, recovery secrets, or copied IdentityAccess private records. AccountId remains an opaque reference. Free-text reasons must be subject to the approved data-minimization and secret/PII controls.

No duplicate canonical security-audit ledger may be introduced under the label of retention history.

## 4. Legal Hold controls

The implementation design must define:
- who may place, change, release and review a Legal Hold;
- authenticated authority, reason, scope, timestamps, version and audit evidence for each hold operation;
- how holds cover current records, historical versions, outbox evidence, replicas, exports and backup media where applicable;
- how conflicting holds and retention expiries are resolved;
- how the system proves that a held record was not disposed of.

Until those controls are approved and implemented, hold-related uncertainty must fail closed for disposal: the record is retained.

## 5. Lawful disposal controls

A future disposal workflow must, at minimum, verify all of the following before any destructive operation:
1. an approved, versioned retention schedule applies to the exact record class and jurisdiction;
2. the required retention period has elapsed using a trusted time source;
3. no active or unresolved Legal Hold covers the record;
4. the accountable records/legal authority has issued a documented, independently attributable authorization;
5. the disposal operation is scoped, idempotent, concurrency-protected and fully audited;
6. the disposal evidence is retained under its own applicable schedule without retaining the disposed protected content;
7. required primary, replica, export and backup dispositions are handled under separately approved controls.

No purge worker, hard-delete path, anonymization rule, backup pruning rule, or key-destruction workflow is authorized by this policy.

## 6. Unresolved legal and technical details

The following remain OPEN and must not be guessed:
- statutory/contractual retention duration by record class and applicable jurisdiction;
- the records/legal authority and required separation of duties for approving disposal;
- Legal Hold scope taxonomy, triggers, release controls and review interval;
- treatment of backups, immutable external audit evidence, replicas and exported artifacts;
- privacy/data-subject obligations and the lawful treatment of opaque AccountId references;
- technical enforcement, evidence format, operational alerts, recovery and independent test cases.

A duration or disposal rule becomes effective only through a controlled successor approved by the accountable authority. A later schedule must not weaken an active Legal Hold or retroactively authorize an earlier unapproved purge.

## 7. Acceptance tests required before implementation

- no physical-delete or automatic-purge path is available by default;
- expired/unknown retention duration does not trigger deletion;
- active, overlapping or unresolved Legal Hold blocks disposal;
- untrusted or self-authorized disposal requests are denied;
- missing legal schedule, missing authorization, unavailable hold resolver or audit failure blocks disposal;
- concurrent hold placement and disposal obey a documented serialization/linearization rule;
- disposal is idempotent, scoped, version-checked and produces durable audit evidence;
- backup/replica/export handling is proved against the separately approved media policy.

## 8. Authority and disposition

This is an owner-accepted policy baseline, not a completed technical implementation. It does not approve G1, Organization runtime, DDL, migrations, grants, production access, deletion, or Go-Live.

**PD-ORG-11 core retention policy: OWNER-ACCEPTED.** Retention schedule, Legal Hold implementation, lawful disposal workflow, technical enforcement and proofs remain OPEN. **O20-E remains OPEN; overall O-20 remains OPEN.**

**END — MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.1**


## 9. Fail-closed Legal Hold eligibility PoC — 2026-11-22 (v0.2)

Added disposable integration test:
tests/MDGsm.Domain.Tests/Security/O20OrganizationLegalHoldRetentionPostgresProofTests.cs

The test defines only an isolated eligibility predicate; it performs no deletion, masking, anonymization, cryptographic erasure or purge. It verifies that eligibility is denied when:
- the retention schedule is UNSET, missing, not approved or lacks approver evidence;
- the retention period has not elapsed;
- the Legal Hold resolver is unavailable;
- a hold is ACTIVE, PENDING or UNKNOWN;
- disposal authorization is absent, expired, consumed, schedule-version-mismatched or not independently approved;
- the actor and approver are the same;
- records/legal authority or a reason is missing.

The positive control is a synthetic fixture with an approved test schedule, elapsed synthetic date, available hold resolver, no unresolved holds and a distinct synthetic records/legal authorizer. The function only returns eligibility; it does not destroy a record and does not choose a real statutory schedule.

A second PostgreSQL test establishes a competing hold-vs-disposal-decision serialization rule in the fixture: hold placement obtains the protected record row lock; the eligibility decision waits behind that lock and, after the hold commits, returns DENY. This is a proposed proof of the fail-closed ordering requirement, not a production Hold service.

A dedicated explicit CI step has been added. Exact current-head compilation and PostgreSQL output are PENDING until the workflow on the resulting commit completes.

## 10. Unchanged legal and production boundary

The retention duration remains UNSET; no jurisdiction or legal interpretation has been selected. The accepted baseline remains retain-by-default, no automatic deletion and no physical deletion absent approved applicable schedule, no active/unresolved Legal Hold, and documented authorization from the accountable records/legal authority.

This test does not implement a production retention schedule, Legal Hold authority/resolver, hold review/release, backup/replica/export coverage, disposal function, records authority identity, legal audit, operational alerts or independent review. It does not authorize a purge job or deletion endpoint.

**Disposition:** owner policy remains accepted; fail-closed retention/Hold PoC source added and exact-head CI pending; legal schedule, production Hold, lawful disposal and G1 remain OPEN.

**END — MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.2**
