# MDG-WP06-DOCUMENT-REGISTER-001
## Controlled Document Register — Version 0.9

| Field | Value |
|---|---|
| Document ID | MDG-WP06-DOCUMENT-REGISTER-001 |
| Version | 0.9 |
| Classification | CONTROLLED REGISTER / IMPLEMENTATION STATUS |
| Status | DRAFT — CURRENT REGISTER FOR PR #66 |
| Supersedes | v0.8; v0.8 and earlier versions retained as historical snapshots |
| Architecture baseline | MDG-ARCH-MASTER-001 v2.5 |
| Change control | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 |
| PR | #66 — OPEN / DRAFT |
| Branch | docs/finance-7b-7c-7d-integrated-baseline-20261117 |
| Current source head at register preparation | e7b354f8ae6868f8e58ffb6f7284450944ae3081 |
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

PR #66 contains isolated Accounting.Contracts, Accounting.Domain and Accounting.Application projects.

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
- Migration 0009 is reserved by PR #70 only. PR #70 remains OPEN/DRAFT and has no independent review. No SQL migration may be added or consumed until PR #70 is reviewed and merged into main.
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

- PR #66 remains OPEN and DRAFT; no merge has occurred.
- No valid independent approval on the final SHA is recorded.
- CI must be checked against the exact current head SHA before readiness claims.
- SHA256SUMS.txt is the integrity manifest for tracked source and workpaper files.
- Owner approval of design is not implementation acceptance, test acceptance or Posting Release.

## 9. Version history

- v0.5 through v0.8 are retained as historical snapshots.
- v0.9 updates FE-01/FE-02 proposals to v0.2, aligns them with OWN-FE-01-02-001, and records the R1 repair-intake boundary and common event/outbox requirements.


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
- Inventory committed movement/fulfillment contract: NOT STARTED.
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

No module-owner technical acceptance has been received by this register. Inventory contract is NOT STARTED. FE-01 and FE-02 remain BLOCKED. Accepted positive Posting Rules: 0. Runtime tests: NOT RUN / NOT VERIFIED. Posting Release: NOT GRANTED.
