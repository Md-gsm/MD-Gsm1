# OWN-INV-019 — Inventory Owner Decision Supplement
## Version 1.0 — Cutover Guard and Negative-Stock Authority Scope

| Field | Value |
|---|---|
| Decision ID | OWN-INV-019 |
| Version / decision date | 1.0 / 2026-11-20 |
| Status | OWNER APPROVED — CONTROLLED POLICY SUPPLEMENT |
| Owner authority | Explicit Product/Business Owner decisions recorded 2026-11-20 |
| Candidate semantic source | MDG-ARCH-MASTER-001 v3.0.3 (owner-supplied Controlled Master Candidate; not formally accepted) |
| Last accepted architecture baseline | MDG-ARCH-MASTER-001 v2.5; this supplement does not silently rewrite or promote the candidate |
| Related records | OWN-INV-018 v1.0; Inventory Source Event Contract Proposal v0.2; Inventory PDS v0.3; O-35, O-36, O-64, D-15 |
| Implementation / tests | NOT AUTHORIZED / NOT RUN |
| Release authority | NOT GRANTED |

## 1. Purpose and effect

This record captures the owner's follow-up decisions for Inventory cutover safety, Warehouse watermark, Sales reservation expiry and negative-stock limit authorization. It is a controlled owner-decision supplement, not implementation evidence.

It supersedes only the conflicting portions identified below. The other decisions in OWN-INV-018 v1.0 remain unchanged. Where a source document has not yet received its controlled successor, this record is the accepted owner decision and the affected implementation/design gate remains blocked until reconciliation.

No SQL, DDL, EF mapping, migration, Migration 0009, GRANT/REVOKE, database role creation, API/runtime/worker code, Inventory activation, negative-stock activation, Posting Rule/Release, production migration or Go-Live is authorized. Migration 0009 remains RESERVED. Negative-stock Feature Flag remains OFF.

## 2. D-15-CUTOVER-GUARD-01 — Cutover policy decision

**Decision status: APPROVED — POLICY DECISION CLOSED.**  
**Operational budget status: NOT_SET.**  
**Cutover gate: NOT SATISFIED until both numeric budgets are measured, approved and configured.**

A single cutover threshold is insufficient. Two independent limits are mandatory:

| Parameter | Meaning | Numeric value |
|---|---|---|
| `MAX_CUTOVER_TAIL_COUNT` | Maximum number of unprocessed movement-level Warehouse sequence entries remaining at the final cutover stage | NOT_SET — derive from representative load tests |
| `MAX_CUTOVER_LOCK_DURATION` | Maximum elapsed duration from acquisition of the first required Warehouse counter lock until all required locks are finally released after commit or rollback; it includes final Tail processing, reconciliation, Pointer transaction, rollback when required and final lock release | NOT_SET — derive from representative load tests |

Cutover is permitted only when **both** independent limits are satisfied and all three consistency conditions below pass:

1. Every committed movement before the selected cutover point has been processed by the candidate Projection.
2. The Inventory quantity difference between the candidate Projection and the authoritative Ledger is exactly zero at that cutover point.
3. The candidate Projection's applied Warehouse watermark equals the authoritative Ledger watermark exactly for every affected Warehouse; watermark difference is zero.

If either numeric limit is exceeded, or any consistency condition fails, Pointer Swap is prohibited. The attempt must roll back, the prior active Pointer must remain unchanged, the required locks must be released, and background Catch-up must continue. The abort must begin early enough that rollback and final lock release finish within MAX_CUTOVER_LOCK_DURATION; the required abort reserve is measured on target hardware and is not guessed. A rollback/release duration breach preserves the prior Pointer and fails the Cutover Gate. A failed attempt must not partially publish the candidate Projection.

The two numeric values must be derived from representative data volume, real movement distribution, target PostgreSQL version/configuration, target server hardware, Warehouse count, concurrent writer rate, measured catch-up throughput, lock wait, Tail processing, reconciliation and rollback. No guessed or copied number is accepted. The approved values and benchmark envelope must be versioned and audited.

This decision closes the policy question in D-15; it does not close the numeric-budget, PostgreSQL-test or operational cutover gates.

## 3. WATERMARK-WH-01 — WarehouseId committed watermark

The committed sequence is allocated per `WarehouseId`, not per `WarehouseId × VariantId`.

- Each committed physical movement receives one movement-level sequence entry per affected Warehouse.
- A multi-line or multi-Variant movement in one Warehouse receives one sequence entry for that Warehouse.
- Every projection consumes the contiguous Warehouse sequence. A sequence entry for an unrelated Variant is processed as a verified no-op for a narrower projection; it must still advance that projection's Warehouse cursor.
- A multi-Warehouse transaction acquires counter rows in ascending `WarehouseId` order and commits all affected Warehouse sequence entries atomically.
- Rollback leaves no committed sequence gap.

The former per-Warehouse×Variant allocator in PDS v0.2/v0.3 is superseded by this decision. PDS successor design and PostgreSQL proof remain required.

## 4. RES-TTL-SALES-01 — Reservation expiry

- Sales reservation default TTL is 24 hours from server-confirmed reservation creation.
- The policy is configurable and versioned; each confirmed Reservation snapshots the policy version, TTL and calculated expiry.
- Policy changes affect new reservations only; existing Reservation snapshots remain unchanged.
- Repair reservations have no automatic TTL (`ExpiryMode=NONE`).
- Expiry uses trusted UTC after Reservation lock acquisition; equality with `ExpiresAtUtc` is expired.
- An expired reservation is excluded from EffectiveReserved/ATP even if cleanup Worker is unavailable.

This records the owner decision already provided for O-36. Controlled source reconciliation and execution tests remain required.

## 5. O-64-SCOPE — GENERAL_MANAGER authority scope

**Decision status: APPROVED-CONSTRAINT.**

The `GENERAL_MANAGER` role may change a negative-stock limit only for the specific `WarehouseId × VariantId` pairs explicitly assigned to that principal. The role alone does not create global authority and does not authorize self-assignment or scope expansion.

Creation, expansion, reassignment or change of an authorized scope requires explicit owner approval and an Audit record. The scope assignment and each change must preserve the approving owner reference, target WarehouseId/VariantId pair, assigned principal, effective version/time, Actor and Audit correlation. A missing owner approval or Audit record fails closed.

The following restrictions are absolute:

- `SYSTEM_MANAGER` alone has no authority to change the limit.
- Emergency Session alone has no such authority.
- Serial/IMEI-tracked stock may never become negative; this prohibition cannot be overridden by role, assignment, UI or emergency path.
- The existing D-258 eligibility matrix, default `-2` policy for its permitted non-serialized integer-UOM class, and zero eligibility for all other classes remain unchanged.
- Negative-stock Feature Flag remains OFF until its separate gate passes.

This supplement supersedes only the older "System Manager may change the threshold" authorization wording in INV-EVT-012 / OWN-INV-018 v1.0. The remaining OWN-INV-018 decisions are not changed.

## 6. Required controlled reconciliation

| Artifact | Required successor/update |
|---|---|
| Inventory PostgreSQL Physical Design | v0.4: WarehouseId cursor, both independent cutover budgets, zero-difference/zero-watermark cutover guards, Sales 24h, retry-attention behavior and O-64 scope |
| Inventory Logical ERD | v0.3: WarehouseId observation/cursor semantics, Sales 24h snapshot and logical O-64 scope-assignment record |
| Inventory Cross-Module Technical Contract | v1.1: exact owner scope and D-15 cutover guard |
| Inventory Source Event Contract Proposal | v0.3: supersede stale System Manager wording |
| Architecture Master Change Crosswalk | CR-ARCH-INV-OWNER-FOLLOWUP-001 v0.2: maps D-15/O-35/O-36/O-64 and records verified candidate provenance/formal acceptance boundary |
| Cross-Module Test Specification | v1.1: explicit authorization-scope, watermark and cutover-budget tests |
| Canonical Open-Issue Register | v1.3: D-15 policy decision closed; O-35/O-36/O-64 remain open for their remaining evidence |
| Document Register | v1.8: register controlled successors and test status |

## 7. Evidence and acceptance boundary

All PostgreSQL execution tests remain NOT_RUN until independently executed on the target PostgreSQL 18 configuration and recorded against an exact repository SHA. Design documents, structural CI, owner approval and PR review are not substitutes for concurrency, replay, tie-out, permission or performance evidence.

The D-15 benchmark evidence must include dataset profile, target hardware, PostgreSQL configuration, concurrency profile, test versions, Tail count, measured lock duration, quantity reconciliation, watermark reconciliation, Pointer state before/after and rollback result. The two budget values must be approved and recorded with their evidence hash before Cutover Gate can pass.

**END — OWN-INV-019 v1.0**
