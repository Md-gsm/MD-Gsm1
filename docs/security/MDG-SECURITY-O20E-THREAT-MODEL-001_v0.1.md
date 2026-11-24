# O20-E Organization Assignment and RowScope — Threat Model Proposal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-THREAT-MODEL-001 |
| Version / Date | 0.1 / 2026-11-22 |
| Status | INTERNAL THREAT MODEL DRAFT — INDEPENDENT SECURITY REVIEW NOT PERFORMED |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |

## 1. Assets and security objectives

Assets: Branch/Warehouse masters and relationship history; AccountBranchAssignment/AccountWarehouseAssignment facts; maker/approver/Product Owner decisions; live IdentityAccess principal/permission/session state; source-side SQL RowScope; immutable domain history and local outbox; canonical AuditSecurity events/ACK; records subject to OD-05 and Legal Hold.

Objectives:
- no identity, owner authority, role or scope is trusted from client input;
- Branch assignment never grants Warehouse access;
- Warehouse visibility requires exact active Warehouse assignment + active Warehouse/current Branch + required IdentityAccess permission;
- Warehouse reassignment cannot broaden or silently transfer grants;
- non-owner assignment actions require distinct approval; Product Owner exception only through trusted server-side binding;
- missing/stale/ambiguous/unavailable authorization denies;
- history is not physically deleted via ordinary paths and audit delivery gaps remain observable/recoverable.

## 2. Trust boundaries

| Boundary | Trusted evidence | Untrusted input / prohibited inference |
|---|---|---|
| Client → API | Opaque bearer token after server validation | Client AccountId, actor, subject, BranchId/WarehouseId scope, owner flag, role, approver |
| API → IdentityAccess | Future versioned public principal/permission contract | Private-table queries, cached Allow, stale claims |
| API → Organization | Validated server principal and typed permission/owner decision | Client assignment facts, owner booleans, guessed permissions |
| Organization → source module | Published authoritative BranchId/WarehouseId and query contract | Inferred IDs, client filters, other module private schema |
| Organization → AuditSecurity | Immutable local intent, stable EventId and canonical hash | HTTP 2xx as durable ACK, altered payload under reused ID |
| Runtime → PostgreSQL | Least-privilege service identity | Runtime DDL, ownership, hard-delete, broad base table reads |

## 3. Threats and required controls

| ID | Threat | Required control | Evidence required |
|---|---|---|---|
| TM-01 | Forged AccountId/ActorId/SubjectId | Actor from validated server session; ignore caller identity fields | API forged-field tests |
| TM-02 | Forged owner/super_admin claim | Protected server owner binding; no role/username/claim inference | Enrollment/rotation/recovery review and negative tests |
| TM-03 | Self-approval or maker = approver/subject | Distinct authenticated approver; actor/subject exclusion | Full maker-checker matrix |
| TM-04 | Branch assignment treated as Warehouse access | Exact active AccountWarehouseAssignment and WarehouseId | SQL RowScope PostgreSQL tests |
| TM-05 | Warehouse reassignment broadens or moves assignment | Exact WarehouseId binding; inactive-only reassignment; no implicit copy/revoke | Reassignment and concurrency proof |
| TM-06 | Stale cache after revoke | Live decisions, explicit linearization, no fallback | Concurrent revoke/read/write tests |
| TM-07 | Counts/aggregates/pagination leak despite filtered rows | SQL predicate before materialization for every route | Query-level report/aggregate tests |
| TM-08 | Export worker uses stale permissions | Revalidate session, permission and assignment before delivery | Export worker tests when consumer exists |
| TM-09 | History update/delete or premature purge | Append-only enforcement and OD-05; Legal Hold fail-closed | UPDATE/DELETE and retention/hold tests |
| TM-10 | Outbox payload mutated on retry | Stable EventId/hash; mismatch quarantine | Replay/tamper tests |
| TM-11 | Audit outage loses intent | Atomic local outbox, at-least-once delivery, durable ACK and reconciliation | Crash/unknown-commit/recovery tests |
| TM-12 | Concurrent mutation deadlock/lost revoke | Platform lock registry, bounded retry and PG race evidence | Two-writer lock/failure matrix |
| TM-13 | Reporting role bypasses RowScope | No broad reporting grants; only approved query boundary | Effective privilege inventory |
| TM-14 | Unknown Legal Hold state during purge | Disposal fails closed; no purge before accepted legal schedule | Hold-vs-disposal concurrency |
| TM-15 | Organization reads IdentityAccess private tables | Versioned public contract only; dependency-direction tests | Architecture tests and review |

## 4. High-risk invariants

- Assignment binds to exact AccountId and exact BranchId/WarehouseId.
- Product Owner exception remains limited to Organization assignment administration, and still requires authenticated actor, reason, idempotency, version check and audit.
- Non-owner approver cannot be actor or subject. Product Owner direct execution is tagged `PRODUCT_OWNER_DIRECT`, not recorded as self-approval.
- IdentityAccess permission AND Organization assignment are conjunctive. No IdentityAccess scope can widen Organization assignments.
- No RowScope consumer is enabled until source owner provides authoritative resource ID and all read/query/export paths are inventoried.
- Organization domain history and local outbox do not replace canonical AuditSecurity.
- OD-05 duration is unset; retain/no-auto-delete baseline applies.
- Missing or failed dependencies never produce Allow.

## 5. Open attack-surface questions

Owner binding enrollment/change/recovery/disable; Organization permission namespace/role mapping; published IdentityAccess principal/permission interface and session-revocation linearization; AuditSecurity EventId/ACK/worker/retry/quarantine; Platform lock order and ID generation; Legal Hold and jurisdictional retention; future report/query/export sources; exact production privilege matrix.

## 6. Validation and review

Every threat must map to owner, code location, positive/negative test, PostgreSQL/API evidence, checksum, residual risk and acceptance authority. A disposable PoC or policy document alone does not close a threat. Independent Security review must use one exact final SHA; Critical/High findings must be resolved or formally risk-accepted.

**Disposition:** initial threat inventory recorded; independent review and executable controls not claimed. O20-E OPEN; G1 NOT READY / NOT GRANTED.

**END — MDG-SECURITY-O20E-THREAT-MODEL-001 v0.1**
