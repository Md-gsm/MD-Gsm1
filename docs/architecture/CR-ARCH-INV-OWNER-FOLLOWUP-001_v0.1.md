# CR-ARCH-INV-OWNER-FOLLOWUP-001
## Version 0.1 — Owner-Approved Inventory Decision Crosswalk

| Field | Value |
|---|---|
| Change Request ID | CR-ARCH-INV-OWNER-FOLLOWUP-001 |
| Version / date | 0.1 / 2026-11-20 |
| Status | OWNER DECISIONS RECORDED — ARCHITECTURE INTEGRATION PROPOSAL — BASELINE PROVENANCE RECONCILIATION REQUIRED |
| Owner decision source | OWN-INV-019 v1.0 |
| Related issues | O-35, O-36, O-64, D-15 |
| Successor design sources | Inventory PDS v0.4; Logical ERD v0.3; Cross-Module Contract v1.1; Source Event Proposal v0.3; Test Spec v1.1 |
| Implementation / runtime | NOT AUTHORIZED / NOT RUN |
| Master edit / acceptance | NOT PERFORMED |

## 1. Purpose and change-control boundary

This change request maps the owner's follow-up decisions of 2026-11-20 into the architecture baseline and records the exact deltas required for a controlled successor. It does not silently overwrite the Master, assign an unapproved successor version number, claim independent acceptance, or authorize implementation.

The decisions are recorded in OWN-INV-019 v1.0. This crosswalk states how the Master and its Inventory supplements must be reconciled. The current PR #82 branch contains the successor design proposals; it is not merged into `main`.

## 2. Baseline provenance reconciliation — BLOCKING

The canonical Open-Issue Register and owner supplement identify `MDG-ARCH-MASTER-001 v3.0.3` as the owner-accepted architecture baseline on 2026-11-20. However, the Master file currently retrievable from `main` is:

- Path: `docs/architecture/MDG-ARCH-MASTER-001_v3.0.md`
- Retrieved content SHA (blob): `c7bb9e8e93802dc33f1f3e515a2865ce19663938`
- Its internal header identifies it as `Version 3.0 — Controlled Integrated Master Candidate`.
- Its status text says `v2.5 REMAINS LAST ACCEPTED BASELINE UNTIL FORMAL ACCEPTANCE`.

No separately named `MDG-ARCH-MASTER-001_v3.0.3.md` was located in the current `main` tree.

This is a material document-provenance/status mismatch. It is not resolved by merely repeating v3.0.3 in a register. Until the exact accepted v3.0.3 artifact or its formal acceptance record is reconciled to the repository, this CR is a proposed delta against the owner decisions and the located Master Candidate—not proof that the accepted baseline has already been amended.

**Required resolution:** publish or identify the exact canonical owner-accepted v3.0.3 Master artifact and its SHA/acceptance record, or complete formal review/acceptance of a controlled successor Master. Do not promote the located v3.0 Candidate silently.

## 3. Controlled decision crosswalk

| Delta | Existing Master / design statement | Owner-approved target rule | Controlled successor mapping | Status |
|---|---|---|---|---|
| CR-INV-01 / D-15 | Master §8.1 makes Movement authoritative and Projection rebuildable but does not specify two independent cutover budgets or the three mandatory completeness/zero-difference guards. PDS v0.3 leaves the numeric cutover budget unmeasured. | Two independent limits: `MAX_CUTOVER_TAIL_COUNT` and `MAX_CUTOVER_LOCK_DURATION`; both values remain NOT_SET until target PostgreSQL/hardware load and rollback measurement. Cutover requires every pre-cutover movement applied, exact zero quantity difference between Projection and Ledger, and exact zero watermark difference for every affected Warehouse. Any failure prohibits Pointer Swap, rolls back, preserves the previous Pointer, releases locks and resumes Catch-up. | OWN-INV-019 §2; PDS v0.4 §§2.3, 13; Cross-Module Contract v1.1 §§4.4, 10.1; Test Spec v1.1 CUT-01..12; OIR v1.2 §9. | POLICY DECISION CLOSED; numeric budgets NOT_SET; operational Cutover Gate NOT SATISFIED. |
| CR-INV-02 / O-35 | Master §L.3 requires Snapshot/Cutoff and delta/replay but leaves the technical concurrency method to PoC. PDS v0.3 uses a per-Warehouse×Variant allocator. | Committed Warehouse sequence is per `WarehouseId`, not Warehouse×Variant. Each physical movement gets one movement-level sequence entry per affected Warehouse. Narrower projections consume unrelated Variant events as verified no-op and advance their Warehouse cursor. Multi-Warehouse operations use WarehouseId vectors and acquire counter locks in ascending WarehouseId order. | OWN-INV-019 §3; PDS v0.4 §§2.1–2.3; ERD v0.3 §12.1; Cross-Module Contract v1.1 §§4.3–4.4; PD-T-040..048; INV-11..18. | OWNER DECISION APPROVED; design successor prepared; independent review and PostgreSQL evidence NOT RUN. |
| CR-INV-03 / O-36 | Master §8.5 and §L.4 describe Sales TTL as configurable but exact value OPEN; §S.10 INV-EVT-005 and CHG-020 retain the open value. | Sales confirmed-reservation default TTL is 24 hours from server-confirmed creation, configurable/versioned and snapshotted per Reservation; changes affect new reservations only. Repair has no automatic TTL. Expiry equality is expired; expired reservations are excluded from ATP even if cleanup Worker is offline. | OWN-INV-019 §4; ERD v0.3 §12.2; PDS v0.4 §§5, 13; Cross-Module Contract v1.1 §5; Source Event Proposal v0.3; Test Spec RSV-01..12. | OWNER DECISION RESOLVED; controlled baseline/source reconciliation and runtime lifecycle tests remain open. |
| CR-INV-04 / O-64 | Master §S.10 INV-EVT-012 says System Manager only may change the limit; §L.5 refers generically to a manager authorized to change it. | `GENERAL_MANAGER` may change a negative-stock limit only within explicitly assigned `WarehouseId×VariantId` pairs. Every assignment creation/change/reassignment/expansion requires explicit owner approval and Audit. `SYSTEM_MANAGER` and Emergency Session alone are denied. Serial/IMEI negative stock is absolutely prohibited. Existing D-258 eligibility/defaults and Feature Flag OFF remain unchanged. | OWN-INV-019 §5; ERD v0.3 §12.3; PDS v0.4 §12; Cross-Module Contract v1.1 §§4.5, 10.2; Source Event Proposal v0.3; Test Spec SCOPE-01..07 / PD-T-046. | OWNER DECISION APPROVED-CONSTRAINT; server enforcement, owner-approved scope evidence and permission tests remain open. |

## 4. Exact Master successor edits required

After the baseline provenance issue in §2 is resolved, a controlled Master successor must make only these scoped edits:

1. **Inventory/stocktake watermark:** preserve the rule that Movement is authoritative and Projection rebuildable; add the WarehouseId-wide contiguous committed cursor, per-Warehouse vector for multi-Warehouse observations, and no-op cursor advancement for unrelated Variants. Keep Warehouse×Variant as the negative-stock aggregation/lock scope; it is not the commit-sequence scope.
2. **D-15 Cutover:** add the two independent budget parameters and all three mandatory guards. A failed budget/guard means no Pointer Swap, previous Pointer preserved, rollback/release and continued Catch-up. Numeric values remain NOT_SET pending target measurement.
3. **Sales reservation:** replace the exact-value OPEN wording in §8.5, §L.4, §S.10 INV-EVT-005 and CHG-020 crosswalk with the 24-hour configurable/versioned default and per-reservation snapshot semantics.
4. **Negative-stock authority:** replace the System Manager-only/unspecified manager wording in §S.10 INV-EVT-012 and §L.5 with O-64-SCOPE. Retain D-258 class eligibility, the allowed non-serialized integer-UOM default, absolute Serial/IMEI zero and Feature Flag OFF.
5. **Traceability:** update the Master Change Record, CHG crosswalk, O-35/O-36/O-64 entries and source references without renumbering historical IDs or rewriting accepted immutable history.

## 5. Deliberate non-changes

This CR does not decide or silently amend unrelated policy:

- WAC Pool key: owner decision VAL-POOL-01 (`BusinessEntityId×VariantId`) still conflicts with Finance FE-08's `ProductVariant×Warehouse`. FE-08 remains BLOCKED until Finance/Accounting explicitly accepts and updates its source. This CR does not resolve the WAC conflict.
- Negative-stock D-258 eligibility and ReasonCodes are unchanged; no monetary threshold is introduced.
- No negative-stock Feature Flag activation is authorized.
- No SQL/DDL/EF mapping/migration/GRANT/REVOKE/runtime/worker implementation is authorized; Migration 0009 remains RESERVED.
- No Journal, Posting Rule, Posting Release, production migration or Go-Live is authorized.
- No finalized financial or Inventory document is rewritten; correction/reversal remains append-only.

## 6. Acceptance sequence

1. Reconcile the accepted Master v3.0.3 artifact and SHA against the file currently located on `main` (§2).
2. Obtain exact-SHA independent review of this CR and successor design documents.
3. Accept required source-owner contracts: Catalog, Platform, AuditSecurity, IdentityAccess, Sales, RepairOperations and Inventory.
4. Obtain Finance/Accounting acceptance for FE-08 separately; it is not a prerequisite for recording D-15/O-36/O-64 but remains a cross-module WAC blocker.
5. Prepare the controlled Master successor using §4; assign a version only under the repository's formal change rule.
6. Keep all execution test statuses NOT_RUN until PostgreSQL/runtime evidence exists.

## 7. Current decision status

- D-15 policy decision: CLOSED / APPROVED; numeric budgets NOT_SET; operational Cutover Gate NOT SATISFIED.
- O-35: OPEN pending independent review and exact-SHA PostgreSQL watermark/race/replay/tie-out evidence.
- O-36: OPEN pending baseline/source reconciliation, implementation and reservation lifecycle tests.
- O-64: OWNER DECISION APPROVED-CONSTRAINT; overall issue OPEN pending server-side enforcement, owner-approved scope evidence and authorization tests.

**No Master baseline is edited by this CR. No implementation or release authorization is granted.**

**END — CR-ARCH-INV-OWNER-FOLLOWUP-001 v0.1**
