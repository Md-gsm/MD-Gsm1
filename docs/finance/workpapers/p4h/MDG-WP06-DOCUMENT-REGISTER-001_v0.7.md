# MDG-WP06-DOCUMENT-REGISTER-001
## Controlled Document Register — Version 1.3

| Field | Value |
|---|---|
| Document ID | MDG-WP06-DOCUMENT-REGISTER-001 |
| Version | 1.3 |
| Classification | CONTROLLED REGISTER / IMPLEMENTATION STATUS |
| Status | CONTROLLED REGISTER — CURRENT AS OF 2026-11-19; MASTER V3.0 CANDIDATE UNDER REVIEW |
| Supersedes | v1.2; prior versions retained as historical snapshots |
| Architecture baseline | v2.5 remains last accepted baseline; MDG-ARCH-MASTER-001 v3.0 candidate submitted for independent review |
| Change control | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 |
| PR status | #66 MERGED; #70 MERGED; #71 OPEN (GitHub draft=false; title still contains [Draft]) |
| Branch | Historical PR #66 branch; v1.3 updates tracked in the MD-Gsm architecture master v3.0 review PR |
| Current main SHA at register preparation | b8c672cb70dcb2b274f43f118ac08b2eea8438c5 |
| Exact-head CI for this register/validator commit | PENDING — must run after all changes including this register and integrity manifest |

## 1. Authority hierarchy

1. MDG-ARCH-MASTER-001 v2.5 is the accepted architecture baseline; v2.6 remains draft.
2. Accepted owner decisions and ADRs control explicit dispositions and constraints.
3. COA v1.2 is the master document and primary authority for account coding and General Ledger rules.
4. COA v1.4 is the current supplementary register for account-level reconciliation based on v1.2.
5. COA v1.3 is an intermediate stage only.

## 2. Current controlled workpapers

| Workpaper | Current version | Status |
|---|---:|---|
| WP06-P4H-7B-POSTING-MATRIX-001 | v0.2 | 39 event dispositions (38 FE identifiers + FE-10b); 0 accepted positive Posting Rules; unresolved fields BLOCKED |
| MDG-WP06-7B-POSTING-RULE-CLOSURE-001 | v0.2 | 39/39 policy dispositions reconciled; 7B-CL-014 and 7B-CL-019 closed at policy-only level; positive posting rules remain 0 |
| WP06-P4H-7C-COA-RECONCILIATION-001 | v0.4 | 78 identifiers; v1.2 master + v1.4 supplement; canonical attribute gaps OPEN |
| WP06-P4H-7D-TEST-REGISTRY-001 | v0.2 | 67 Test IDs: 39 FE + 16 COA + 12 shared controls; runtime tests NOT RUN |
| MDG-WP06-7B-7C-7D-INTEGRATED-001 | v0.6 | Current integrated crosswalk and implementation boundary |
| MDG-WP06-5-STAGE-EXECUTION-REPORT | v0.2 | Current execution report; test evidence scoped to the SHA recorded in that report |
| MDG-WP06-COA-SOURCE-AUTHORITY-CORRECTION-001 | v0.2 | Current owner-declared COA hierarchy |
| MDG-WP06-7B-7C-7D-CROSS-AUDIT-001 | v0.3 | Historical supporting audit; source-authority statements superseded by correction v0.2 |
| MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001 | v0.2 | Historical audit snapshot; O-01 deliverable acceptance remains open |
| MDG-WP06-COA-SOURCE-RECOVERY-001 | v0.1 | Historical only; not an active source or acceptance dependency |
| MDG-WP06-7C-ACCOUNT-DISPOSITION-001 | v0.1 | Historical only; not an active source or acceptance dependency |
| MDG-WP06-7D-TEST-CLOSURE-001 | v0.1 | End-to-end test closure remains NOT RUN / NOT VERIFIED |

## 3. 7B closure result — carefully bounded

The current 7B closure ledger distinguishes policy/disposition closure from positive posting-rule acceptance:
- 39/39 event rows have explicit disposition, owner, blocker and Test ID in 7B v0.2.
- 7B-CL-014 (R2 scope guards) and 7B-CL-019 (unknown-event fail-closed policy) are CLOSED at policy/disposition level only.
- FE-09, FE-12, FE-14, FE-20 and the R2 boundaries retain their explicit prohibitions/constraints; their positive posting contracts are not accepted.
- All other closure groups retain PARTIAL or OPEN status as documented in closure ledger v0.2.
- Accepted positive Posting Rules: 0.
- Posting Release: NOT GRANTED.

## 4. Implemented Accounting Domain/Application slice

PR #66 was merged into main at merge commit a8b9b6fe2a4b91a1d429a29a53558d7102be293c and contains isolated Accounting.Contracts, Accounting.Domain and Accounting.Application projects.

Implemented:
- Integer-only IRR amount operations and deterministic Largest Remainder allocation.
- Canonical Account model with field-level evidence validation.
- AccountRegistryValidator for duplicate IDs/codes, prohibited 110809, explicit parent references, parent cycles and parent Level ordering.
- Full 7B runtime PostingRule projection; every required contract field must have VERIFIED evidence matching the exact runtime value. RecognitionTime is explicit and is not inferred from another field.
- FiscalPeriod close/reopen guards; balanced Journal/JournalLine; immutable finalization; separate reversal; Audit references.
- Stable PostingIdentity and read-only, fail-closed PostingPreflight.
- Fail-closed tests for all 39 FE identifiers and 16 COA gate cases; exhaustive required 7B evidence-field tests.

These domain tests do not prove PostgreSQL Accounting persistence, transaction atomicity, database idempotency or end-to-end financial posting.

## 5. CI/test evidence and exact-head discipline

Historical evidence recorded by the earlier register:
- Foundation Build & Test: 220/220 passed; 0 failed; 0 skipped on SHA 333856b41df29fbdd2e6082d3f76ff05c8a96a61.
- Windows Preview Installer, CodeQL, Persons gates and other CI results in v0.5 are evidence for the SHA and time stated in that historical report only; they are not automatically evidence for the current head.

Finance Workpaper Structural Validation:
- Run #96 passed on SHA c1d38847a0e7f5c4ed3fc3f588cfe6eae771480d.
- Its verified scope was structural only: 39 7B event rows and fail-closed gate references, 16 7C Open Items mapped to 16 COA gate tests, and 67 unique 7D Test IDs.
- That run predates the new 7B closure ledger and its additional validator assertions. Exact-head validation for the current register/validator changes is PENDING and must be checked before claiming success.

## 6. Remaining blockers

### 7B
- Accepted positive Posting Rules: 0.
- No debit/credit rule may be invented or seeded without complete source/decision/recognition/amount/tax/subledger/dimension/idempotency/reversal/period/audit/test evidence.
- Source module event contracts and 7C canonical attributes remain dependencies.

### 7C
- 78-ID scope is registered.
- AccountId, Parent/Level, Nature, NormalBalance, Postable, DirectPostingAllowed, effective dates, subledger and FE eligibility remain OPEN wherever sources/owner decisions do not establish the value.
- No prefix inference or silent canonicalization.

### 7D
- 67 Test IDs are specified.
- Domain-level fail-closed tests are implemented.
- Positive journal assertions and PostgreSQL end-to-end tests remain NOT RUN because persistent Posting runtime is not implemented.

### Persistence dependency
- No Accounting SQL schema/migration, persistent Journal/Subledger repository, transactional Posting writer or public Posting API exists in this slice.
- PR #70 was merged at b8c672cb70dcb2b274f43f118ac08b2eea8438c5. Migration 0009 is RESERVED in main; no SQL file was created, SQL remains NOT AUTHORIZED, and no database migration was applied.
- No merge is performed by this work.

## 7. Gate and release status

| Gate | Status | Basis |
|---|---|---|
| Gate 1 — owner design decisions | CLOSED for recorded decisions | Accepted owner decisions remain controlling |
| Gate 2 — architecture/design baseline | CLOSED for v2.5 | v2.6 remains draft |
| Gate 3 — implementation/behavioral verification | OPEN | Persistence and full finance tests remain |
| Gate 4 — independent evidence acceptance | OPEN | Independent review and end-to-end evidence not accepted |
| O-01 | OPEN | Owner-approved replacement criterion exists; deliverable/evidence acceptance pending |
| Posting Release | NOT GRANTED | No accepted positive rule or persistent posting runtime |

## 8. PR and integrity controls

- PR #66 is MERGED. Its merge is not a Posting Release and does not establish PostgreSQL persistence or runtime posting.
- PR #71 has reviewer approval by mrdeljoe4 on exact head SHA b165e957ff7bf886616de21933018395d76bbff9; this review is limited to PDS v0.2.
- PR #71 has six successful workflow runs on exact head SHA b165e957ff7bf886616de21933018395d76bbff9; these runs do not prove SQL, PostgreSQL Posting, production readiness or Master acceptance.
- SHA256SUMS.txt is the integrity manifest for tracked source and workpaper files.
- Owner approval of design is not implementation acceptance, test acceptance or Posting Release.

## 9. Version history

- v0.5 through v0.9 are retained as historical snapshots.
- v1.0 registered the Inventory stock ownership/source-event proposal v0.1 as a historical snapshot.
- v1.1 registered OWN-INV-018 v1.0, Inventory contract v0.2 and the three design deliverables.
- v1.2 registers the internal technical owner review and its blocking actions; no technical owner acceptance or runtime authorization is implied.
- v1.3 corrects PR #66/#70 merge states, PR #71 draft metadata/review/CI, current main SHA and Migration 0009 authorization boundary; adds the v3.0 master candidate crosswalk.


## 10. FE-01 / FE-02 source contract proposals — owner review pending

The following v0.2 proposals translate approved owner policy into technical dependencies without claiming runtime implementation:

| Proposal | Scope | Status |
|---|---|---|
| `docs/modules/sales/SALES-SOURCE-EVENT-CONTRACT-PROPOSAL-001_v0.2.md` | Sales document/line facts for FE-01, explicitly dependent on Inventory-owned committed fulfillment evidence | PROPOSAL — OWNER REVIEW REQUIRED |
| `docs/modules/repairoperations/REPAIROPERATIONS-SOURCE-EVENT-CONTRACT-PROPOSAL-001_v0.2.md` | Separate technical completion, optional quality acceptance and handover facts for FE-02 | PROPOSAL — OWNER REVIEW REQUIRED |
| `docs/finance/workpapers/p4h/integration/MDG-WP06-FE01-FE02-SOURCE-CROSSWALK-PROPOSAL-001_v0.2.md` | Cross-module source evidence and RecognitionTime questions for FE-01/FE-02 | PROPOSAL — SOURCE OWNER REVIEW REQUIRED |

Boundaries:
- These are documentation proposals only. Sales, RepairOperations, Inventory and Accounting runtime event publishers/adapters are not added. The proposals now reflect OWN-FE-01-02-001 v1.0; module-owner technical acceptance remains open.
- The existing RepairOperations Contracts README links the proposal and continues to state that no runtime repair event contract is published.
- Sales document finalization is not treated as proof of Inventory fulfillment.
- Repair technical completion, required QC, readiness, handover, invoice finalization and payment are distinct facts. Standard physical repair requires completion plus confirmed handover. R1 repair intake is disabled; R1 FE-02 posting is blocked absent a real, authorized, immutable and independently addressable source.
- Inventory's committed movement/fulfillment contract remains a required external dependency and is not fabricated here.
- Business policy OWN-FE-01-02-001 is APPROVED; technical acceptance IDs remain OPEN until their respective data owners review and accept them.

7B disposition remains unchanged:
- FE-01: BLOCKED.
- FE-02: BLOCKED.
- Accepted positive Posting Rules: 0.
- Runtime accounting tests: NOT RUN / NOT VERIFIED.
- Independent review: PENDING.
- Posting Release: NOT GRANTED.

The finance structural validator checks the v0.2 proposal version markers, R1 repair-intake prohibition, ownership boundaries and non-runtime/no-release status. A passing structural check does not constitute owner acceptance, source contract publication, Accounting RecognitionTime approval or Posting Release.

## 11. Version history

- v0.5 and v0.6 are retained as historical snapshots.
- v0.7 registered the FE-01/FE-02 source-contract proposals and their explicit owner-review boundary; the underlying 7B/7C/7D workpaper versions remain unchanged.\n- v0.8 records owner approval of FE-01/FE-02 and shared contract policies in OWN-FE-01-02-001 v1.0; source contracts and runtime gates remain pending.


## 12. Approved FE-01 / FE-02 owner decision

| Decision | Scope | Status |
|---|---|---|
| OWN-FE-01-02-001 v1.0 | FE-01 (28), FE-02 (37), shared controls C-01..C-15 and accounting principles A-01..A-10 | APPROVED — OWNER POLICY DECISIONS |

Authoritative record: `docs/finance/workpapers/p4h/integration/OWN-FE-01-02-001-v1.0.md`.

The decision fixes the approved business policy and technical boundaries, including Inventory ownership of committed fulfillment/movements, separation of revenue recognition from receipts/settlements, R1 repair-intake prohibition, restricted R1 repair financial facts, physical handover for standard repair revenue recognition, distinct recipient/actor references, scoped idempotency and transactional outbox. It does not publish runtime contracts, authorize any Posting Rule, or grant Posting Release.

- Sales source contract: revise proposal and obtain Sales/Pricing/Tax owner acceptance — PENDING.
- RepairOperations source contract: revise proposal and obtain owner acceptance — PENDING.
- Inventory committed movement/fulfillment contract: v0.2 — BUSINESS OWNER POLICY APPROVED; technical owner acceptance remains OPEN.
- RecognitionTime and exact Posting Matrix: PENDING.
- Runtime implementation/tests and independent review: NOT RUN / NOT VERIFIED.
- Accepted positive Posting Rules: 0; Posting Release: NOT GRANTED.


## 13. v0.2 proposal alignment status

| Artifact | Version | Current status |
|---|---:|---|
| Sales source event proposal | 0.2 | PROPOSAL — OWNER REVIEW REQUIRED |
| RepairOperations source event proposal | 0.2 | PROPOSAL — OWNER REVIEW REQUIRED |
| FE-01/FE-02 Accounting source crosswalk | 0.2 | PROPOSAL — SOURCE OWNER REVIEW REQUIRED |

The v0.2 revisions implement the approved policy boundaries in documentation: Inventory exclusively owns committed movement/fulfillment and valuation; Sales owns commercial documents and references Inventory evidence; revenue recognition is separate from payment/settlement; return entitlement/revenue reversal is separate from Inventory inspection/restock; standard physical repair requires technical completion plus confirmed handover; R1 repair intake is disabled and no synthetic RepairCase is permitted; any R1 FE-02 posting requires a real, approved external source; actor and recipient references are distinct; request IdempotencyKey is distinct from EventId; transactional outbox semantics are specified without claiming exactly-once delivery.

No module-owner technical acceptance has been received by this register. Inventory contract v0.2 is registered; business policy is approved and cross-module technical owner acceptance remains OPEN. FE-01 and FE-02 remain BLOCKED. Accepted positive Posting Rules: 0. Runtime tests: NOT RUN / NOT VERIFIED. Posting Release: NOT GRANTED.


## 14. Inventory source-event and stock ownership decision

| Artifact | Version | Status |
|---|---:|---|
| OWN-INV-018 | 1.0 | APPROVED — BUSINESS OWNER POLICY |
| Inventory source event and stock ownership contract | 0.2 | BUSINESS POLICY APPROVED — TECHNICAL OWNER ACCEPTANCE REQUIRED |

OWN-INV-018 v1.0 approves INV-EVT-001..018 on 2026-11-18 and resolves D-213 and D-215. The approved policy fixes exclusive Inventory ownership; Organization-owned warehouse/location masters; Variant/SKU stock identity; tracking and dual-IMEI rules; Decimal/UOM governance; type-specific reservations; movement boundaries; Sales/RepairOperations linkage; in-transit transfer custody; return inspection; stocktake; default -2 non-serialized shortage and serialized prohibition; Moving Weighted Average/Specific Identification; True-up; event identity; atomic outbox; PostgreSQL testing; and exact-SHA independent acceptance.

Sales reservation has configurable TTL; Repair reservation has no automatic TTL. Transfers require in-transit custody; Accounting must separately approve the exact COA mapping and Posting Rule. No account code is inferred. All technical acceptance items remain open for Inventory and relevant module owners. No runtime, SQL, migration, API, publisher or grants are added. Migration 0009 is reserved in main after PR #70 merged; SQL remains NOT AUTHORIZED and no migration file exists.

## 15. Inventory design deliverables

| Artifact | Version | Status |
|---|---:|---|
| INVENTORY-ERD-DATA-DICTIONARY-001 | 0.1 | DESIGN PROPOSAL — OWNER REVIEW REQUIRED |
| INVENTORY-STATE-MOVEMENT-SPEC-001 | 0.1 | DESIGN PROPOSAL — OWNER REVIEW REQUIRED |
| INVENTORY-TEST-SPEC-001 | 0.1 | TEST DESIGN — QA/DB/OWNER REVIEW REQUIRED |

These specifications define a conceptual ERD/data dictionary, separate state and movement machines, and 78 test definitions (34 owner-approved baseline + 31 additional + 12 review additions INV-T-066..077 + 1 targeted per-observation stocktake case INV-T-078). The earlier 65-test baseline is retained as historical scope (65 test definitions); the expanded current specification adds INV-T-066..078. They are design/test proposals, not physical schema, executable migrations or evidence of execution.

Before implementation, resolve Decimal precision/scale and UOM fractional rules, default Sales TTL, serial uniqueness namespace, landed-cost/True-up allocation and rounding, stocktake cut-off strategy, and exact in-transit COA/Posting Rule mapping.

## 16. Status and release boundary

- Business owner policy INV-EVT-001..018: APPROVED.
- D-213 and D-215: RESOLVED.
- Technical owner acceptance: OPEN.
- Inventory runtime/persistence/SQL/migrations: NOT IMPLEMENTED / NOT VERIFIED.
- Inventory tests: NOT RUN.
- FE-01 and FE-02: BLOCKED under separate source/Accounting gates; FE-09 remains controlling.
- Accepted positive Posting Rules: 0.
- Posting Release remains NOT GRANTED; operational release remains NOT GRANTED.


## 17. Inventory technical owner review

| Artifact | Version | Status |
|---|---:|---|
| INVENTORY-TECHNICAL-OWNER-REVIEW-001 | 0.1 | INTERNAL DESIGN REVIEW COMPLETE WITH BLOCKERS — CROSS-MODULE OWNER ACCEPTANCE OPEN |

The internal review found seven blocking design parameters/decisions (Decimal/UOM precision; Sales reservation TTL; serial namespace; landed-cost allocation and rounding; stocktake concurrency/cut-off; in-transit accounting classification and COA/Posting Rule; and provisional valuation for permitted negative stock) plus five ERD/state-model refinements. It also proposes 12 additional test cases, expanding the test design from 65 to 77 cases if accepted.

The review does not constitute approval by Organization, Catalog, Purchasing, Sales, RepairOperations, Accounting, Security, Platform/DB or QA. Those owners must explicitly accept their assigned items against exact document versions and SHA. The three design specifications remain proposals. The targeted stocktake cut-off ambiguity is clarified in the technical resolution proposal and test specification; this is not evidence of implementation or PostgreSQL execution.

No architecture master change, runtime, SQL, migration, API, publisher, grant, Posting Rule or release authorization is included. Migration 0009 is reserved in main after PR #70 merged; SQL remains NOT AUTHORIZED and no migration file exists. OWN-INV-018 v1.0 remains the business-policy authority.


## 18. Inventory technical resolution proposal

| Artifact | Version | Status |
|---|---:|---|
| INVENTORY-TECHNICAL-RESOLUTION-PROPOSAL-001 | 0.1 | TECHNICAL DISPOSITION PROPOSED — CROSS-MODULE OWNER ACCEPTANCE REQUIRED |

The proposal records explicit technical dispositions for INV-REV-01..07: PostgreSQL numeric/UOM precision; configurable Sales reservation TTL; serial/IMEI normalization and active uniqueness; evidence-based landed-cost allocation and True-up; immutable per-observation stocktake cut-off with ordered delta replay; Inventory in-transit custody with GL posting blocked until canonical COA evidence and a separately approved Posting Rule; and provisional valuation for permitted non-serialized shortage. It also records the related ERD/state invariants and test additions INV-T-066..077. A focused correction now requires each physical count observation to capture its own committed per-scope ledger watermark under a consistent database snapshot; reconciliation replays only movements committed after that observation. Test INV-T-078 covers per-observation watermark capture, concurrent movement/count ordering, exclusion of pre-observation movements, and rollback/sequence visibility.

User-confirmed acceptance: the user and Accounting have approved the technical disposition proposal. Separate per-module sign-offs are not required for this design gate. Independent reviewer acceptance remains PENDING and will be recorded by the reviewer after CI/tests complete. In particular, the exact in-transit GL account and Posting Rule are intentionally not selected; absence of canonical evidence is fail-closed. The proposal does not authorize physical schema, SQL, migration, runtime, publisher, grants, Posting Release or operational release. Migration 0009 is reserved in main after PR #70 merged; SQL remains NOT AUTHORIZED and no migration file exists.


## 19. Verified repository and release status — 2026-11-19

| Item | Verified state | Scope limitation |
|---|---|---|
| main | `b8c672cb70dcb2b274f43f118ac08b2eea8438c5` | Current source baseline at register preparation. |
| PR #66 | MERGED; merge commit `a8b9b6fe2a4b91a1d429a29a53558d7102be293c` | Accounting Foundation projects and controlled workpapers are in main; no Posting Release. |
| PR #70 | MERGED; merge commit `b8c672cb70dcb2b274f43f118ac08b2eea8438c5` | Migration 0009 reservation is in main; SQL file not created; SQL NOT AUTHORIZED. |
| PR #71 | OPEN; GitHub metadata `draft=false`; title contains `[Draft]`; head `b165e957ff7bf886616de21933018395d76bbff9` | PDS v0.2 only; not merged; not SQL or Posting authorization. |
| PR #71 review | APPROVED by `mrdeljoe4` on exact head SHA | Limited to PR #71 changes/PDS v0.2, not this Master document. |
| PR #71 CI | Six successful runs: Project Charter Owner Gate #44361437434/#44357509586/#44357487928; Windows Preview Installer #44357487917; CodeQL #44357487885; Persons Module Gates #44357487900 | Exact SHA only; no PostgreSQL Posting or production proof. |
| Accounting Foundation | Implemented within ADR-0018 scope and merged in PR #66 | Persistence, Posting runtime and positive Posting Rules remain unaccepted. |
| 7B/7C/7D | 39/39 policy dispositions; 78 COA identifiers; 67 Test IDs | Accepted positive Posting Rules = 0; runtime tests NOT RUN / NOT VERIFIED. |
| Inventory | OWN-INV-018 v1.0 owner-approved business policy | Runtime NOT VERIFIED; tests NOT RUN; operational release NOT GRANTED. |
| Master v3.0 | Controlled candidate in architecture review PR | v2.5 remains last accepted Master until independent review and document acceptance. |

**Migration 0009 boundary:** PR #70 merge records only a reserved migration slot. It does not create SQL, authorize DDL, permit a disposable or production migration, seed COA, persist journals or enable Posting. Any later SQL requires the separately controlled review/authorization path and applicable architecture gates.

**PDS v0.2 audit note:** the proposed `accounting.audit_events` table conflicts with the single shared AuditEvent owner (AuditSecurity). PDS v0.2 must be revised before design acceptance; no SQL may be created from the current table definition.

## Current authority/status correction — 2026-11-20

- Sole accepted architecture baseline: MDG-ARCH-MASTER-001 v3.0.3. References in prior register text to v2.5 as the last accepted baseline are historical and superseded.
- COA hierarchy: v1.2 master; v1.5 controlled supplementary reconstruction/crosswalk under v3.0.3; v1.4 historical/superseded for current supplementary authority; v1.3 intermediate only. Original canonical nine-sheet source and Part 4H-6 remain unrecovered.
- 7B: 39/39 event dispositions; 0 accepted positive Posting Rules; B-final remains OPEN.
- 7C: 78 IDs in scope; canonical attributes remain partial/open; 110809 prohibited; 2114 and 5106 DESIGN ONLY / NO POSTING until qualified.
- 7D: 67 Test IDs specified; this is not runtime test PASS or evidence acceptance.
- Integer IRR base = 1 Rial; one-time document-total AwayFromZero; max 1 IRR per document; no per-row monetary threshold.
- O-65/CHG-100 mixed-sign allocation is owner-approved policy, not Posting/Release approval.
- Gate C/D and Posting Release remain open/not granted.


## 19. Controlling v3.0.3 and Stage 04 register overlay — 2026-11-20

This overlay supersedes any earlier statement in this register that v2.5 is the current accepted architecture or that COA v1.4 is the current supplementary authority. The sole accepted architecture baseline is **MDG-ARCH-MASTER-001 v3.0.3**. COA v1.2 is the master coding/GL authority; COA v1.5 is the controlled supplementary reconstruction/crosswalk. COA v1.4 is historical/superseded for current supplementary authority; v1.3 is intermediate only. The original nine-sheet workbook and Part 4H-6 are not active authorities or prerequisites.

### Stage 04 integrated contract

| Artifact | Version | Scope | Status |
|---|---:|---|---|
| MDG-WP06-STAGE-04-SALES-RETURNS-GRNI-CREDIT-EXPOSURE-TIEOUT-001 | 0.1 | Sales, customer returns/refunds, goods GRNI, supplier returns, CreditExposure and financial tie-out | CONTROLLED DESIGN WORKPAPER — integrated; technical owner acceptance and runtime evidence pending |

Path: `docs/finance/workpapers/p4h/integration/MDG-WP06-STAGE-04-SALES-RETURNS-GRNI-CREDIT-EXPOSURE-TIEOUT-001_v0.1.md`.

The Stage 04 workpaper consolidates the v3.0.3 semantics and contracts:
- Sales commercial snapshots, Inventory-owned committed fulfillment, transfer-of-control/RecognitionTime and FE-01/FE-08 boundaries.
- Separate customer return entitlement, physical return/inspection/disposition, revenue/tax reversal, refund and COGS/inventory effects; cumulative return allocation follows v3.0.3.
- Goods-only 2103 GRNI, separate invoiced AP 2101, and separate service-accrual design-only 2114; Purchase Order alone is not a liability or inventory receipt.
- CreditExposure keyed PartyId × Product/Item, cheque replacement counted once, only explicit allocations reduce exposure, R2 installment scope excluded from R1 unless enabled, aging and overdue review, and O-09 kept independent.
- Source-to-subledger-to-GL and Treasury external-statement tie-out identities, cutoff, exception register and close blockers.
- 25 design-level test cases, not represented as executed PASS.

The workpaper uses only v3.0.3, COA v1.2 and v1.5. It does not create missing account mappings, debit/credit pairs, tax rates, tolerances or runtime behavior.

### Stage 04 status

- Owner-policy alignment: COMPLETED in the workpaper.
- Integrated design contract and tie-out specification: SPECIFIED.
- Module technical owner acceptance: PENDING.
- Canonical row-level COA attributes and positive 7B Posting Rules: OPEN/BLOCKED where evidence is incomplete.
- Runtime schema/API/Posting writer and PostgreSQL tests: NOT IMPLEMENTED / NOT VERIFIED / NOT RUN.
- B-final, Gate C and Gate D: OPEN.
- Posting Release, production migration and Go-Live: NOT GRANTED.

This overlay corrects the register's architecture and COA authority references but does not itself accept the Stage 04 workpaper on behalf of Sales, Purchasing, Inventory, Treasury, Accounting, QA or Platform owners.


## 20. Controlling v3.0.3 and Stage 05 Inventory overlay — 2026-11-20

All older architecture-baseline references in historical Inventory records are superseded for current work. The sole accepted architecture baseline is **MDG-ARCH-MASTER-001 v3.0.3**. This overlay does not rewrite historical owner decisions or historical proposal files.

### Stage 05 integrated Inventory contract

| Artifact | Version | Scope | Status |
|---|---:|---|---|
| MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001 | 0.1 | Quantity conservation, OnHand/Sellable/ATP/Reserved/InTransit/Custody/Deficit/BookBalance, Custody lifecycle, Section/warehouse transfer, Stocktake watermark/delta replay and tie-out | INTEGRATED DESIGN CONTRACT — runtime evidence pending |

Path: `docs/modules/inventory/MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001_v0.1.md`.

The workpaper:
- Defines inventory ownership and canonical stock grain; Movement ledger remains source of truth and StockBalance is rebuildable projection.
- Defines quantity equations and prevents double counting of aggregate/location, reservation/allocation/picking, custody and in-transit quantities.
- Defines Custody as a separate committed handover/return/consumption/resolution lifecycle, excluded from ATP until a valid physical return and disposition.
- Applies v3.0.3 Section transfer Option B as an atomic paired movement; inter-Warehouse FE-31 remains R2.
- Defines stocktake using immutable cut-off plus a per-observation ledger watermark and ordered delta replay; count alone never changes stock.
- Preserves v3.0.3 negative-stock classes, five ReasonCodes, feature gate OFF, WAC/Specific Identification, and the distinction between inventory subledger and Accounting GL.
- Extends the inventory test registry through INV-T-102. These are test specifications, not runtime PASS evidence.

### Stage 05 status

- Semantic equations, Custody and Stocktake design: COMPLETED in the Stage 05 workpaper.
- O-35: design specified; remains OPEN pending exact-SHA PostgreSQL race, watermark, replay and tie-out evidence.
- O-36 Sales TTL final value: OPEN/configurable; Repair reservation has no automatic TTL. No proposal-only 30-minute value is adopted.
- Physical schema/API/runtime and PostgreSQL tests: NOT IMPLEMENTED / NOT VERIFIED / NOT RUN.
- Negative-stock feature: OFF pending separate Gate.
- Warehouse transfer FE-31: R2 / NOT ENABLED.
- B-final, Gate C and Gate D: OPEN.
- Migration, Posting Release, production migration and Go-Live: NOT GRANTED.

This overlay registers the workpaper but does not infer technical owner acceptance, QA PASS or release authorization.
