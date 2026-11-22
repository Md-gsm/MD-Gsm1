# O20 Database Role / Object Matrix — Candidate Outbox Worker Extension

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 |
| Version / Date | 1.7 / 2026-11-22 |
| Supersedes | v1.6 |
| Status | WORKER MATRIX IS A TESTED CANDIDATE; ADDITIONAL IDENTITY NOT OWNER-ACCEPTED |
| Related audit | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 v1.9 |

## 1. O20 baseline

The accepted N.6 five logical identities remain unchanged. The runtime LOGIN-shim PoC is a physical binding candidate and does not create a new logical identity. The schema owner remains a distinct NOLOGIN owner.

## 2. Candidate additional O21 worker identity

The new disposable test proves this candidate:
- physical LOGIN principal: `outbox_worker` (synthetic only);
- NOLOGIN ACL role: `mdgsm_outbox_worker` (synthetic only);
- one SET-only membership, with INHERIT FALSE and ADMIN FALSE;
- event payload is immutable and stored separately from delivery state;
- worker SELECTs the event and delivery rows;
- worker UPDATEs only delivery metadata columns;
- no INSERT/DELETE/DDL, no access to Branch/Warehouse/identity credential rows, no role administration and no SET ROLE to other protected identities.

The database object model splits immutable event content from mutable delivery state so the delivery worker cannot alter canonical payload or hash. Organization runtime creates the local event/intent inside the domain transaction; the delivery worker only advances delivery metadata after commit.

## 3. Minimum target object matrix proposal

| Object | Worker privilege | Rationale |
|---|---|---|
| Immutable OutboxEvent envelope | SELECT | Read stable EventId/version/hash/payload |
| OutboxDelivery metadata | SELECT | Inspect pending/claimed/ack/quarantine state |
| OutboxDelivery mutable metadata columns | UPDATE on explicit columns only | Claim lease, attempts, ACK reference, quarantine state |
| Branch/Warehouse/Assignment aggregates | NONE | Worker is not a business command processor |
| IdentityAccess Account/Credential/Session/Role/Permission | NONE | No private identity data access |
| Schema/DDL/sequence/owner objects | NONE | No schema administration |
| AuditSecurity canonical ledger | NONE direct DB access | Publish through authenticated public AuditSecurity contract only |
| KMS/HSM/WORM credentials | NONE in DB | Provider-specific secret custody outside database and separately approved |

No event payload columns may be updateable by the worker. No generic UPDATE grant on the whole delivery table should be used if column-scoped grants suffice.

## 4. Required owner decision

The candidate is an additional logical service identity and therefore requires explicit owner/Platform/DB/Security acceptance. The five-category N.6 baseline is not amended by this proposal. If a separate worker is not approved, O21 delivery must remain disabled until an alternative non-overlapping runtime/worker boundary is independently proven.

**Disposition:** least-privilege worker object matrix has disposable PostgreSQL source evidence; exact-head CI pending; production worker identity/grant not approved or provisioned. O20/O21 remain OPEN.

**END — MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 v1.7**
