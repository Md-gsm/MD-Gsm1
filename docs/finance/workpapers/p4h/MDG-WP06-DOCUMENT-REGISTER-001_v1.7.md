# MDG-WP06-DOCUMENT-REGISTER-001
## Controlled Document Register — Version 1.7

| Field | Value |
|---|---|
| Document ID | MDG-WP06-DOCUMENT-REGISTER-001 |
| Version | 1.7 |
| Classification | CONTROLLED REGISTER / IMPLEMENTATION STATUS |
| Status | CONTROLLED REGISTER — CURRENT AS OF 2026-11-20; ARCHITECTURE MASTER v3.0.3 OWNER-ACCEPTED |
| Supersedes | v1.6; prior versions retained as historical snapshots |
| Architecture baseline | MDG-ARCH-MASTER-001 v3.0.3 owner-accepted 2026-11-20; independent technical review and main registration pending |
| Change control | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 |
| PR status | #66 MERGED; #70 MERGED; #71 MERGED; #72 CLOSED AS SUPERSEDED; #73 MERGED; #82 OPEN / DRAFT — reviewer mrdeljoe4 requested; latest owner-decision successor commits require exact-head CI rerun/review |
| Branch | Package base: main at `10c8a8ba8f9aad54c4e2ff05fad49ce12511380f`; proposed branch: docs/inventory-cross-module-contracts-20261120 |
| Current main SHA at register preparation | 10c8a8ba8f9aad54c4e2ff05fad49ce12511380f |
| Exact-head CI for this package | PENDING — must run on final PR SHA |

## 1. Authority hierarchy

1. MDG-ARCH-MASTER-001 v3.0.3 is the owner-accepted architecture baseline as of 2026-11-20; v2.5/v2.6 are historical references.
2. Accepted owner decisions and ADRs control explicit dispositions and constraints.
3. COA v1.2 is the master document and primary authority for account coding and General Ledger rules.
4. COA v1.5 is the controlled supplementary reconstruction/crosswalk based on v1.2; v1.4 is historical/superseded for current supplementary authority.
5. COA v1.3 is an intermediate stage only.

## 2. Current controlled workpapers

| Workpaper / artifact | Current version | Status |
|---|---:|---|
| WP06-P4H-7B-POSTING-MATRIX-001 | v0.2 | 39 event dispositions (38 FE identifiers + FE-10b); 0 accepted positive Posting Rules; unresolved fields BLOCKED |
| MDG-WP06-7B-POSTING-RULE-CLOSURE-001 | v0.2 | 39/39 policy dispositions reconciled; 7B-CL-014 and 7B-CL-019 closed at policy-only level; positive posting rules remain 0 |
| WP06-P4H-7C-COA-RECONCILIATION-001 | v0.4 | 78 identifiers; v1.2 master + v1.5 controlled supplement; canonical attribute gaps OPEN |
| WP06-P4H-7D-TEST-REGISTRY-001 | v0.2 | 67 Test IDs: 39 FE + 16 COA + 12 shared controls; finance runtime tests NOT RUN |
| MDG-ACC-PDS-001 | v0.4 | Accounting PostgreSQL physical design; reversal retains original COA snapshot; journal/header/line snapshot consistency enforced by composite FKs; exact-head CI/review pending |
| MDG-WP06-7B-7C-7D-INTEGRATED-001 | v0.6 | Integrated crosswalk, aligned to v3.0.3 and COA v1.5 |
| MDG-WP06-5-STAGE-EXECUTION-REPORT | v0.2 | Current authority statements corrected to v3.0.3/COA v1.5; execution evidence remains SHA-scoped |
| MDG-WP06-COA-SOURCE-AUTHORITY-CORRECTION-001 | v0.2 | Current hierarchy: COA v1.2 master + v1.5 controlled supplement; v1.4 historical/superseded |
| MDG-WP06-7B-7C-7D-CROSS-AUDIT-001 | v0.3 | Historical supporting audit; source-authority statements superseded by correction v0.2 |
| MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001 | v0.2 | Historical audit snapshot; O-01 deliverable acceptance remains open |
| MDG-WP06-COA-SOURCE-RECOVERY-001 | v0.1 | Historical only; not an active source or acceptance dependency |
| MDG-WP06-7C-ACCOUNT-DISPOSITION-001 | v0.1 | Historical only; not an active source or acceptance dependency |
| MDG-WP06-7D-TEST-CLOSURE-001 | v0.1 | End-to-end finance test closure remains NOT RUN / NOT VERIFIED |
| Stage 04 sales/returns/GRNI/credit-exposure tie-out | v0.1 | Design contract completed; source-to-ledger tie-out is specified, not runtime evidence |
| Inventory Stage 05 equations/custody/stocktake | v0.1 | Integrated design contract; INV-T-001..102 are test specifications, not runtime PASS |
| Inventory Logical ERD | v0.3 | Owner-directed successor: 24h Sales TTL snapshot, WarehouseId cursor and scoped negative-stock authority concept; independent review pending |
| Inventory PostgreSQL Physical Design | v0.4 | WarehouseId-wide source cursor, dual cutover budgets, zero-difference/zero-watermark guards, O-64 scope; independent review pending; PD-T-001..048 NOT RUN |
| Inventory Physical Design Re-audit 002 | v0.1 | Internal re-audit of v0.2; historical supporting evidence |
| Inventory Physical Design Re-audit 003 | v0.1 | Internal re-audit of v0.3; all 12 design findings closed internally; not independent attestation |

Current Accounting PDS: `docs/modules/accounting/MDG-ACC-PDS-001_PHYSICAL-DATABASE-DESIGN_v0.4.md`. PDS v0.1, v0.2 and v0.3 are retained as historical/superseded records. No Accounting DDL, Migration 0009 SQL, COA seed, Posting writer, production grant or Posting Release is authorized.

Current Inventory PDS: `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001_v0.4.md`. Versions v0.1-v0.3 are retained as historical predecessors. PDS v0.4 is a design successor only; PD-T-001..046 remain NOT RUN; O-35 remains OPEN. D-15 policy decision is CLOSED, but both numeric budgets remain NOT_SET and operational Cutover is prohibited. No DDL, SQL, EF mapping, migration, grant or runtime implementation is authorized by these design records.

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
- No merge is performed by this register. Controlled integration into `main` is a separate release action and requires exact-head CI, current independent review/approval and the owner's final acceptance.

## 7. Gate and release status

| Gate | Status | Basis |
|---|---|---|
| Gate 1 — owner design decisions | CLOSED for recorded decisions | Accepted owner decisions remain controlling |
| Gate 2 — architecture/design baseline | OWNER-ACCEPTED for v3.0.3 on 2026-11-20; exact-SHA independent review and main registration tracked separately |
| Gate 3 — implementation/behavioral verification | OPEN | Persistence and full finance tests remain |
| Gate 4 — independent evidence acceptance | OPEN | Independent review and end-to-end evidence not accepted |
| O-01 | OPEN | Owner-approved replacement criterion exists; deliverable/evidence acceptance pending |
| Posting Release | NOT GRANTED | No accepted positive rule or persistent posting runtime |

## 8. PR and integrity controls

- PR #66 is MERGED. Its merge is not a Posting Release and does not establish PostgreSQL persistence or runtime posting.
- PR #71 was merged at 8f89653e25e6a599387128e0209f5009d9dd59cd after reviewer approval and successful exact-head CI; those results are specific to PR #71's contents.
- PR #71 exact-head CI passed; this is not PostgreSQL runtime or financial Posting evidence.
- SHA256SUMS.txt is the integrity manifest; each later documentation package must update and verify its own exact content hashes.
- PR #72 was closed as superseded, not merged independently. PR #73 merged at 10c8a8ba8f9aad54c4e2ff05fad49ce12511380f after exact-head reviewer approval and successful CI.
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

**PDS v0.2 audit finding — corrected in v0.3:** the revised PDS removes `accounting.audit_events`, assigns canonical AuditEvent/SecurityEvent exclusively to AuditSecurity and retains only opaque event IDs without cross-module foreign keys. PDS v0.3 still requires independent exact-SHA review; no SQL may be created.

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


## 21. Inventory logical ERD — v3.0.3 aligned — 2026-11-20

| Artifact | Version | Scope | Status |
|---|---:|---|---|
| MDG-INVENTORY-LOGICAL-ERD-001 | 0.2 | Inventory logical entities, ownership, relationships, stable references, integrity invariants, equation mapping and traceability to INV-T-001..102 | INTEGRATED LOGICAL DESIGN — OWNER/INDEPENDENT REVIEW PENDING |

Path: `docs/modules/inventory/MDG-INVENTORY-LOGICAL-ERD-001_v0.2.md`.

This v0.2 supersedes INVENTORY-ERD-DATA-DICTIONARY-001 v0.1 as the current ERD proposal; v0.1 remains historical. It aligns with sole accepted baseline MDG-ARCH-MASTER-001 v3.0.3 and Stage 05 v0.1. It explicitly:
- separates Inventory-owned facts from Organization, Catalog, Sales, Purchasing, RepairOperations, Persons, Accounting, Treasury and AuditSecurity ownership;
- defines StockMovement/Line as the sole physical quantity source and StockPosition as a rebuildable projection;
- models Reservation, Allocation, Pick, Custody, Transfer and Stocktake observations as distinct aggregates/facts;
- binds each CountObservation to its own committed ledger watermark and preserves recount lineage;
- maps quantity/valuation equations to authoritative facts and derived projections;
- defines 25 logical integrity invariants and traceability groups for INV-T-001..102;
- corrects the prior ATP proposal: ATP = Sellable − EffectiveReserved, with unreceived purchase and transfer quantities excluded;
- preserves FE-31 R2, negative-stock feature OFF, Sales TTL OPEN/configurable, no Repair TTL and all accounting/Posting ownership boundaries.

The document is logical design only. No physical schema, DDL, EF mapping, migration, API/runtime, PostgreSQL test PASS, O-35 closure, Posting Release or Go-Live is claimed or authorized. All inventory runtime tests remain NOT_RUN unless separately evidenced on an exact SHA.


## 22. Inventory PostgreSQL physical design proposal — 2026-11-20

| Artifact | Version | Scope | Status |
|---|---:|---|---|
| MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 | 0.1 | PostgreSQL table map, keys, exact-type policy, integrity/index strategy, committed-prefix ledger sequencing, transactions, locking, projection/replay, stocktake watermark, outbox/audit, security and test evidence | TECHNICAL DESIGN PROPOSAL — REVIEW REQUIRED — NOT IMPLEMENTATION AUTHORITY |

Path: `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001_v0.1.md`.

The proposal maps ERD v0.2 to PostgreSQL 18-oriented tables and constraints. It deliberately leaves quantity/valuation precision and UUID generator policy OPEN pending their owning contracts; it rejects floating point for quantity/currency; describes transactional scope sequencing because PostgreSQL sequences are non-transactional; defines atomic movement/projection/outbox/audit boundaries and deterministic lock ordering; and sets required PostgreSQL concurrency, replay, stocktake, valuation and recovery tests.

It preserves v3.0.3: no TTL default is invented, ATP remains Sellable − EffectiveReserved, FE-31 remains R2, negative-stock feature remains OFF, integer Rial rounding remains at the approved final boundary, and no COA/Posting Rule is inferred.

This is a design proposal only. No SQL/DDL, migration, EF mapping, API/runtime, database grants, PostgreSQL PASS, O-35 closure, Posting Release, production migration or Go-Live is claimed or authorized.


## 23. Inventory PostgreSQL physical-design adversarial audit — 2026-11-20

| Artifact | Version | Status |
|---|---:|---|
| MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-AUDIT-001 | 0.1 | INTERNAL ADVERSARIAL DESIGN AUDIT — BLOCKING CLARIFICATIONS REQUIRED |

Path: `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-AUDIT-001_v0.1.md`.

The internal review compared PostgreSQL physical-design proposal v0.1 with the sole accepted architecture baseline v3.0.3, Stage 05 inventory semantics and logical ERD v0.2. It found four blockers: (1) no complete durable command-idempotency model for both movement and non-movement commands; (2) unresolved atomic durability boundary between Inventory and AuditSecurity while preserving the single canonical AuditEvent owner; (3) committed-ledger immutability is not tied to enforceable PostgreSQL privilege/trigger/procedure controls; and (4) lock ordering is not globally consistent across reservation, stock scope and other mutable aggregates. Three high-priority clarifications concern canonical sequence/watermark scope, executable stocktake snapshot protocol and projection rebuild/cutover concurrency. Additional medium/low findings cover stock-position identity, draft/commit boundary, reservation expiry persistence, audit/outbox states and naming/nullability normalization.

The review is an internal adversarial self-review, not an independent external reviewer attestation. It is a design audit only: no SQL, DDL, migration, PostgreSQL runtime test, implementation, grant, Posting Rule, Posting Release or Go-Live was performed or authorized. Physical-design v0.1 is not ready for DDL/migration freeze until blockers and high findings are closed and an external review is obtained.


## 24. Inventory PostgreSQL physical-design correction and re-audit — 2026-11-20

| Artifact | Version | Status |
|---|---:|---|
| MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 | 0.2 | Corrected design; PD-AUD-01..06 CLOSED IN DESIGN only |
| MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-002 | 0.1 | Internal re-audit; PD-AUD-01..06 CLOSED IN DESIGN; residual findings remained open |

Paths:
- `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001_v0.2.md`
- `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-002_v0.1.md`

The v0.2 correction specified durable command receipts for movement and non-movement commands, local transactional audit-intent delivery while AuditSecurity remains sole canonical AuditEvent owner, database-enforced committed-ledger immutability, one global lock hierarchy, Warehouse x Variant committed-prefix sequence and per-observation stocktake watermark. Re-audit 002 closed PD-AUD-01..06 at design level only. Residual PD-AUD-07..12 were still open in that historical snapshot. This did not authorize DDL, migration, grants, runtime, PostgreSQL implementation or O-35 closure.

## 25. Inventory PostgreSQL physical-design residual blocker closure — 2026-11-20

| Artifact | Version | Status |
|---|---:|---|
| MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 | 0.3 | PD-AUD-01..12 CLOSED IN DESIGN; independent review / implementation evidence pending |
| MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-003 | 0.1 | Internal re-audit; all 12 design findings closed; NOT DDL FREEZE |

Paths:
- `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001_v0.3.md`
- `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-003_v0.1.md`

The v0.3 supplement closes PD-AUD-07..12 in design: projection generation/rebuild/catch-up/cutover and rollback; complete StockPosition grain and NULL applicability; optimistic Draft version and atomic Header/Line commit completeness; persisted reservation expiry and UTC equality boundary; outbox delivery/lease/retry/quarantine/monitoring/reconciliation; and naming/nullability dictionary. Together with v0.2, PD-AUD-01..12 are closed in design.

The re-audit is internal and by the same workflow that authored the supplement. It is not independent reviewer attestation, code review, PostgreSQL execution or proof of runtime behavior. PD-T-001..039 remain NOT RUN. AuditSecurity, Catalog and Platform contracts remain implementation dependencies. O-35 remains OPEN pending exact-SHA PostgreSQL watermark/race/replay/tie-out evidence. No SQL, DDL, EF mapping, migration, grants, API/runtime, PostgreSQL test PASS, Posting Release, production migration or Go-Live is claimed or authorized.

## 26. Package 5 source/review alignment — 2026-11-20

The exact current source heads and CI run IDs are recorded in the controlled final package evidence register `EVD-PACKAGE-05-FINAL-RELEASE-001`. The earlier review/CI entries in section 19 are explicitly historical and must not be reused as evidence for revised heads.

Owner acceptance of this register/document package is separate from technical owner acceptance by Inventory/AuditSecurity/Catalog/Platform, independent exact-SHA review, runtime test acceptance and any release authorization. None of those separate permissions is inferred from this register.

## 20. Canonical O-01..O-65 status register — 2026-11-20

The single authoritative live status ledger for all architecture and finance open issues O-01 through O-65 is:

`docs/governance/MDG-OPEN-ISSUE-REGISTER-001_v1.1.md`

It records independent Decision, Design, Implementation, Test, Evidence and Gate statuses for every O-ID. This finance document register is a supporting document inventory and does not create a competing O-status source. Its earlier status statements and snapshots are historical where they conflict with the v3.0.3 authority overlay or the canonical O register.

Current finance boundaries recorded by the canonical register:
- 39 event dispositions exist; positive accepted Posting Rules remain 0.
- 78 COA identifiers are in scope; COA v1.2 master + v1.5 supplement control. Canonical account attributes/crosswalk remain partial/open.
- 67 Test IDs are specified; specification is not runtime PASS. Full finance tests remain NOT RUN / NOT VERIFIED.
- O-01 remains OPEN; independent review, complete FE↔COA↔Test crosswalk, executed tests and accepted evidence are outstanding.
- O-65 owner decision is closed; implementation, O-65-specific tests and evidence remain open.
- Posting Release is NOT GRANTED; Gate C/Gate D remain OPEN / NOT VERIFIED.

This controlled register follows PR #71 merge at 8f89653e25e6a599387128e0209f5009d9dd59cd, PR #72 superseded closure and PR #73 merge at 10c8a8ba8f9aad54c4e2ff05fad49ce12511380f. Historical v0.7/v0.8 register snapshots remain superseded; current register history is maintained in v1.4/v1.5.


## 27. Accounting Journal snapshot consistency — PDS v0.4 — 2026-11-20

Accounting PDS v0.4 supersedes v0.3 for the explicit Journal/JournalLine/Reversal snapshot invariants. Each JournalLine is constrained to the same COA snapshot as its Journal header by a composite FK. A reversal is an independent Journal in its own posting identity and fiscal period/date, but retains the original finalized Journal's immutable COA snapshot; it cannot rebind copied account identifiers/codes to a different snapshot. The Domain reversal factory enforces this by copying the original aggregate's `CoaSnapshotId`, and the physical design requires a composite self-FK for database enforcement.

This is a design/domain consistency correction only. It creates no SQL, DDL, EF mapping, Migration 0009, COA seed, Posting writer, Posting Release, production migration or Go-Live authorization.

## 28. PR #73 post-#71 Inventory integration record — 2026-11-20

PR #73 is the controlled Inventory physical-design extension following the PR #71 merge and PR #72 superseded closure.

- Base: `main` at PR #71 merge commit `8f89653e25e6a599387128e0209f5009d9dd59cd`.
- The rebuilt PR #73 delta contains only four Inventory physical-design/re-audit documents, this current v1.4 register update, the canonical O-register O-35 traceability update, and SHA256 manifest changes. Superseded finance-register v0.7/v0.8 revisions are not reintroduced.
- The Inventory design additions are v0.2, v0.3, re-audit 002 and re-audit 003. The internal re-audits close PD-AUD-01..12 in design only; they are not independent reviewer attestations or implementation evidence.
- OperationContractVersion is included consistently in the command-receipt uniqueness and idempotency lock scope. Projection cutover is constrained to a bounded locked tail after background catch-up.
- O-35 remains OPEN. PD-T-001..039 remain NOT RUN. AuditSecurity, Catalog and Platform contracts, independent exact-SHA review and PostgreSQL watermark/race/replay/tie-out evidence remain outstanding.
- PR #73 merged at 10c8a8ba8f9aad54c4e2ff05fad49ce12511380f after reviewer mrdeljoe4 approved exact head ef48e23b32ee623efe3fdfb8f506c391d139eddb and all required exact-head workflows succeeded. The merge remains design-only; no SQL, DDL, EF mapping, migration, grants, API/runtime, Posting Release, production migration or Go-Live is authorized.

This dated integration record is historical evidence of the PR's scope and boundaries; GitHub PR metadata is authoritative for the final head SHA, current review state and workflow run results.


## 16. Owner-directed Inventory cross-module successor package — 2026-11-20

| Artifact | Version | Scope | Status |
|---|---:|---|---|
| OWN-INV-019 Inventory Owner Decision Supplement | 1.0 | D-15 dual cutover budgets/mandatory guards; O-64 scoped GM authority; WarehouseId watermark; Sales 24h reservation TTL | OWNER APPROVED — POLICY ONLY; no implementation/test evidence |\n| CR-ARCH-INV-OWNER-FOLLOWUP-001 | 0.1 | Maps D-15/O-35/O-36/O-64 owner decisions to Master successor and identifies Master v3.0.3 provenance/status mismatch | CHANGE PROPOSAL — BASELINE RECONCILIATION REQUIRED; Master not edited |
| MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 | 0.4 | WarehouseId watermark, Projection cursor/cutover protocol, dual independent budgets, O-64 security scope and reservation expiry | CONTROLLED DESIGN SUCCESSOR — INDEPENDENT REVIEW PENDING — NO IMPLEMENTATION AUTHORITY |
| MDG-INVENTORY-LOGICAL-ERD-001 | 0.3 | Reservation TTL snapshot, WarehouseId CountObservation cursor, NegativeStockLimitScopeAssignment logical concept | LOGICAL DESIGN SUCCESSOR — INDEPENDENT REVIEW PENDING |
| MDG-INVENTORY-CROSS-MODULE-TECHNICAL-CONTRACT-001 | 1.1 | Owner cross-module contracts, cutover fail-closed policy, O-64 assignment scope and Finance boundary | OWNER APPROVED DESIGN DIRECTION — CROSS-OWNER ACCEPTANCE / BASELINE RECONCILIATION PENDING |
| MDG-INVENTORY-CROSS-MODULE-TEST-SPEC-001 | 1.1 | Added SCOPE-01..07 and CUT-01..12; all test cases are NOT_RUN | TEST SPECIFICATION ONLY |
| INVENTORY-SOURCE-EVENT-CONTRACT-PROPOSAL-001 | 0.3 | Supersedes stale System Manager threshold authority and Sales TTL terms; current ATP formula restored | OWNER-DECISION SUCCESSOR — TECHNICAL ACCEPTANCE PENDING |
| MDG-PLATFORM-DB-ROLE-GRANT-MATRIX-001 | 0.1 | Six PostgreSQL roles and object-class privilege intent | DESIGN ONLY; no role/grant created |

Owner decisions recorded: D-15-CUTOVER-GUARD-01 (policy decision CLOSED/APPROVED; MAX_CUTOVER_TAIL_COUNT and MAX_CUTOVER_LOCK_DURATION remain NOT_SET); O-64-SCOPE (GENERAL_MANAGER only within explicitly owner-approved assigned WarehouseId×VariantId pairs; every scope change requires owner approval + Audit; SYSTEM_MANAGER/Emergency denied; Serial/IMEI zero-negative); WATERMARK-WH-01 (contiguous counter per WarehouseId); RES-TTL-SALES-01 (24h Sales default, configurable/versioned/snapshotted; Repair has no automatic TTL); AUD-ATTENTION-01 (attention flag non-terminal; retry continues). Existing owner decisions VAL-POOL-01, SERIAL-NS-01 and NEG-STOCK-SCOPE-01 remain in force. FE-08 remains BLOCKED pending Finance/Accounting acceptance of the WAC pool scope.

The successor package is design and test specification only. It does not amend v3.0.3 by implication; controlled baseline crosswalk/reconciliation and all relevant consumer-owner acceptance remain required. The privilege matrix does not execute GRANT/REVOKE. All new tests are NOT_RUN.

Open controls:
- O-35 OPEN pending independent review and exact-SHA PostgreSQL watermark/race/replay/tie-out evidence. PD-T-047 covers writer active-Pointer resolution after counter-lock acquisition; PD-T-048 covers abort/rollback and final lock-release duration.
- O-36 OPEN pending baseline/source reconciliation, implementation and reservation lifecycle tests.
- O-64 OPEN pending server-side authorization enforcement, scope assignment/approval/Audit proof and deny/allow tests.
- D-15 policy decision CLOSED/APPROVED; numeric budgets NOT_SET and Cutover Gate unsatisfied.
- Migration 0009 RESERVED; negative-stock Feature Flag OFF; zero accepted positive Posting Rules; no Posting Release.
- No SQL/DDL/EF/migration/grants/runtime/Inventory activation/Posting/Go-Live authorization.

## 17. PR #82 owner-decision and cross-module successor package — 2026-11-20

- PR: https://github.com/Md-gsm/MD-Gsm1/pull/82
- State: OPEN / DRAFT; not merged.
- Base: main at `10c8a8ba8f9aad54c4e2ff05fad49ce12511380f`.
- Branch: `docs/inventory-cross-module-contracts-20261120`.
- Scope: owner decision supplement, PDS v0.4, Logical ERD v0.3, cross-module contract v1.1, test specification v1.1, source-event proposal v0.3, O-register v1.2, Document Register v1.6, existing role-grant matrix and supporting checksum manifest.
- Reviewer requested: `mrdeljoe4`. Review is still required on the final head.
- Previous branch head `4570d8e1bd362a30410b2173aee7e247240885c2` had all five required workflows SUCCESS. That head is superseded by this successor package; all workflows must be rerun against the final head.
- SHA256 manifest must include every new controlled document, remain sorted, contain no duplicate paths and be validated against the exact committed contents.
- All test specifications remain NOT_RUN. PR merge is intentionally not performed pending exact-head CI, independent review and cross-owner acceptance.

## 18. Remaining boundaries and acceptance sequence

1. Complete exact-head CI, checksum validation and independent review for PR #82.
2. Reconcile the successor contract/ERD/PDS/source proposal with MDG-ARCH-MASTER-001 v3.0.3 through the proper controlled crosswalk; obtain Catalog, Platform, IdentityAccess, AuditSecurity and relevant source-owner acceptance.
3. O-35 remains open until exact-SHA PostgreSQL concurrency/watermark/replay/tie-out evidence is accepted.
4. O-36 remains open until reservation state-machine and expiry-boundary tests pass on the implementation.
5. O-64 remains open until server enforcement proves owner-approved scope assignment, audit trail, out-of-scope denial and the absolute Serial/IMEI prohibition.
6. D-15 policy decision is closed; numeric budget values may be set only after representative PostgreSQL 18/target-hardware load and rollback measurements. Pointer Swap remains prohibited until both limits and all three consistency conditions pass.

### Version history
- v1.6 — registers OWN-INV-019 v1.0 and successor Inventory contracts/design/test specifications; updates D-15/O-35/O-36/O-64 statuses. v1.5 is retained as historical snapshot.

## 19. Architecture baseline owner-decision crosswalk — 2026-11-20

| Artifact | Version | Purpose | Status |
|---|---:|---|---|
| CR-ARCH-INV-OWNER-FOLLOWUP-001 | 0.1 | Controlled mapping of D-15, O-35, O-36 and O-64 into the next Master successor; records exact source deltas, required changes and non-changes | OWNER DECISIONS RECORDED; independent review and Master baseline provenance reconciliation pending |

**Important provenance discrepancy:** OIR v1.2 and OWN-INV-019 refer to an owner-accepted MDG-ARCH-MASTER-001 v3.0.3 baseline. The file found in `main`, `docs/architecture/MDG-ARCH-MASTER-001_v3.0.md`, blob SHA `c7bb9e8e93802dc33f1f3e515a2865ce19663938`, identifies itself as a v3.0 Controlled Master Candidate and says v2.5 remains the last accepted baseline. No separate v3.0.3 file was located. CR-ARCH-INV-OWNER-FOLLOWUP-001 v0.1 records this as a blocking provenance mismatch; it does not edit or promote the Master.

The CR maps D-15 dual cutover budgets and zero-difference guards, O-35 per-WarehouseId watermark/cursors, O-36 Sales 24h/Repair no TTL, and O-64 owner-assigned GENERAL_MANAGER scope. O-35/O-36/O-64 remain open overall pending their stated independent review, implementation and evidence. Finance FE-08 WAC scope remains BLOCKED and is outside this CR.

### Version history
- v1.7 — registers CR-ARCH-INV-OWNER-FOLLOWUP-001 v0.1 and records the Master baseline provenance mismatch; v1.6 retained as historical snapshot.
