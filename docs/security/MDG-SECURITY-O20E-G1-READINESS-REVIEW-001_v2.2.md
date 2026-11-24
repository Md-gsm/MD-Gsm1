# O20-E G1 Readiness Review — Ordered Technical Closure

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 2.2 / 2026-11-22 |
| Supersedes | v2.1 |
| Status | O20-E OPEN — OWNER DECISIONS / EXACT-HEAD CI / INDEPENDENT REVIEW REQUIRED |
| Branch | security/o20-o21-final-closure |
| PR | #92 Draft / unmerged |

## 1. Additional DB identity / O21 worker reconciliation

The role-matrix crosswalk identified an unresolved distinction:
- O20/N.6 has five accepted logical identity categories, plus NOLOGIN schema owner;
- an older Platform role proposal separately names a background-worker class;
- O21/AuditSecurity outbox requires a worker execution boundary which must not share broad runtime business-data privileges.

The five identity baseline is retained. A disposable LOGIN-shim PoC proves only a candidate physical connection mapping for the runtime ACL role. It does not approve production login binding.

A new owner decision gate is recorded in O20 DB privilege audit v1.8 and role/object matrix v1.6. Preferred alternative is a dedicated narrowly privileged outbox-worker identity; alternatively, the existing five-category design may be retained only if the DB/process boundary can prove runtime and worker privileges cannot overlap. No production role or grant is created.

## 2. Current blocker ledger

| Order | Blocker | Current evidence | Remaining closure condition |
|---:|---|---|---|
| 1 | IdentityAccess public principal/permission contract | v1.1 source interface and live-session request-time check implemented | Exact current CI and IdentityAccess owner acceptance |
| 2 | Commit-time authorization linearization | Disposable two-writer PostgreSQL row-lock proof | Wire transaction participant into real Organization UoW; prove all identity/grant changes and global lock order |
| 3 | Product Owner authority | Typed decision fails closed | Protected binding store, trusted bootstrap/enrollment, change/recovery/disable/restore |
| 4 | Organization maker-checker | Disposable policy PoC | Production trusted principal/approver/owner binding and transaction enforcement |
| 5 | Permission matrix | Exact ten-role golden matrix and sensitive gate | Latest CI and independent review |
| 6 | Relationship history | Immutable WarehouseId append-only PoC | Production privileges/schema and Legal Hold/retention implementation |
| 7 | Idempotency/concurrency | Synthetic multi-writer PoCs | Complete races, failures, retry and unknown-commit test evidence |
| 8 | AuditSecurity/outbox | Synthetic local outbox/ACK tests | Canonical AuditSecurity API/durable ACK and production worker/credentials/monitoring |
| 9 | Database identities/runtime binding | O20 support-role and runtime LOGIN-shim PoCs | Platform/DB accepts physical LOGIN model, pool reset, rotation and actual effective grant scan |
| 10 | Outbox worker identity | Separate six-vs-five logical identity ambiguity recorded | Explicit owner/Platform/Security choice of dedicated worker identity or demonstrably isolated existing role/process model |
| 11 | Platform ID and global lock order | Candidate design requests and local PostgreSQL tests | Platform selection and global cross-module lock/race matrix |
| 12 | RowScope | No current Branch/Warehouse query consumer | Every future route/export/worker inventory and positive/negative SQL test |
| 13 | Retention/Legal Hold | OD-05 core policy owner-accepted | Legal schedule, hold authority/lifecycle, backups/replicas/exports, fail-closed proof |
| 14 | O21 production Hash Chain | Disposable crypto proof | Production AuditSecurity, Object Lock, key custody, verifier, restore, alerts/runbook |
| 15 | Independent review/G1 | Not performed | Independent Architecture/Security exact-SHA review and owner G1 authorization |

## 3. Non-authorization

No production Organization runtime/schema/migration/credential/owner binding/outbox worker/LOGIN shim/new identity/grant/provider has been created. PR #92 stays Draft/unmerged and PR #91 stays Draft/unmerged pending coordination.

**Disposition:** technical proofs and cross-owner decision gates expanded; O20-E OPEN; O-20 OPEN; O-21 OPEN; G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v2.2**
