# O20-F Emergency Access — Scoped Allow-list Proof Successor

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20F-EMERGENCY-ACCESS-LIFECYCLE-001 |
| Version / Date | 0.3 / 2026-11-22 |
| Supersedes | v0.2 |
| Status | OWNER-ACCEPTED POLICY; EXPANDED DISPOSABLE PROOF ADDED; EXACT-HEAD CI AND PRODUCTION CONTROLS OPEN |
| Scope | Emergency support identity lifecycle within O-20; no production access |

## 1. Accepted policy remains unchanged

Every activation requires two distinct authorized approvers. Neither may be the requester or emergency operator. One activation is one-use and lasts no more than 30 minutes, measured using trusted database/server UTC. At now greater than or equal to expiry, authorization is denied synchronously; cleanup is housekeeping only. The Product Owner self-assignment exception does not apply.

## 2. Disposable PostgreSQL proof expansion

The O20EmergencyAccessPostgresProofTests fixture now stores database_name and schema_name on each emergency grant, alongside the exact operator principal, requester, operation, resource and requested duration.

The test allow-list records:
- exact principal;
- exact disposable database and schema;
- exact operation and resource;
- active status;
- valid_from_utc and valid_until_utc.

Activation requires both independent approvers to match the grant's full database/schema/operation/resource scope and to be currently eligible. The authorization predicate independently rechecks current database, requested database, schema, exact principal, operation, resource, ACTIVE state and expiry. The schema must still exist.

Negative cases include wrong database, wrong schema, unlisted resource, expired approver eligibility, duplicate approver, requester/operator-as-approver, altered operation/resource, replay, expiry and revocation.

## 3. Proof boundary

This is a disposable policy PoC. The allow-list principal rows are seeded synthetic test identities; they are not resolved through a trusted production IdentityAccess contract. The ephemeral emergency LOGIN is intentionally unprivileged and does not receive real production object capabilities. The proof therefore does not demonstrate that a real emergency operator can safely invoke narrowly scoped database operations.

The test's local audit outbox proves intent retention and rollback on local audit-intent failure only. It does not prove canonical AuditSecurity acknowledgement, delivery worker, retry, quarantine, alerts or operational recovery.

## 4. Remaining production gates

Still required:
1. trusted approver identity mapping to live IdentityAccess principals;
2. protected allow-list administration, lifecycle, revocation and recovery;
3. narrowly scoped emergency capability with an accepted database/object/function grant model;
4. synchronous authorization on every protected operation and revocation/connection invalidation;
5. canonical AuditSecurity durable ACK, retry, quarantine, alerts and reconciliation;
6. cleanup, evidence and incident runbook;
7. exact-head Linux/Windows PostgreSQL proof and independent review.

No LOGIN, credential, role, membership, production grant, migration or production access is authorized by this test or document.

**Disposition:** exact database/schema and time-bounded approver allow-list semantics are SOURCE-IMPLEMENTED IN DISPOSABLE POC; exact-head CI is pending. Trusted approver binding, actual scoped DB capability, production audit ACK/worker and independent review remain OPEN. O20-F and overall O-20 remain OPEN.

**END — O20-F emergency access successor v0.3**
