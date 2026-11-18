# MDG-WP06-DOCUMENT-REGISTER-001
## Controlled Document Register — Version 0.5

| Field | Value |
|---|---|
| Document ID | MDG-WP06-DOCUMENT-REGISTER-001 |
| Version | 0.5 |
| Classification | CONTROLLED REGISTER / IMPLEMENTATION STATUS |
| Status | DRAFT — CURRENT REGISTER FOR PR #66 |
| Supersedes | v0.4; v0.4 retained as historical snapshot |
| Architecture baseline | MDG-ARCH-MASTER-001 v2.5 |
| Change control | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 |
| Owner decision basis | OWN-7C-043 and previously accepted owner decisions |
| PR | #66 — OPEN / DRAFT |
| Current code/test SHA | 333856b41df29fbdd2e6082d3f76ff05c8a96a61 |

## 1. Authority hierarchy

1. MDG-ARCH-MASTER-001 v2.5 is the accepted architecture baseline; v2.6 remains draft.
2. Owner decisions and accepted ADRs control explicit account dispositions and constraints.
3. COA v1.2 is the master document and primary authority for account coding and General Ledger rules.
4. COA v1.4 is the current supplementary register for account-level reconciliation based on v1.2.
5. COA v1.3 is an intermediate stage only.

## 2. Current controlled workpapers

| Workpaper | Current version | Status |
|---|---:|---|
| WP06-P4H-7B-POSTING-MATRIX-001 | v0.2 | 39 event dispositions (38 FE identifiers + FE-10b); 0 accepted positive Posting Rules; unresolved fields BLOCKED |
| WP06-P4H-7C-COA-RECONCILIATION-001 | v0.4 | 78 identifiers; v1.2 master + v1.4 supplement; canonical attribute gaps OPEN |
| WP06-P4H-7D-TEST-REGISTRY-001 | v0.2 | 67 Test IDs: 39 FE + 16 COA + 12 shared controls; runtime tests NOT RUN |
| MDG-WP06-7B-7C-7D-INTEGRATED-001 | v0.6 | Current integrated crosswalk and implementation status |
| MDG-WP06-5-STAGE-EXECUTION-REPORT | v0.2 | Current execution report and implementation boundary |
| MDG-WP06-COA-SOURCE-AUTHORITY-CORRECTION-001 | v0.2 | Current owner-declared COA hierarchy |
| MDG-WP06-7B-7C-7D-CROSS-AUDIT-001 | v0.3 | Historical supporting audit; source-authority statements superseded by correction v0.2 |
| MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001 | v0.2 | Historical audit snapshot; O-01 remains open |
| MDG-WP06-COA-SOURCE-RECOVERY-001 | v0.1 | Historical only; not an active source or acceptance dependency |
| MDG-WP06-7C-ACCOUNT-DISPOSITION-001 | v0.1 | Historical only; not an active source or acceptance dependency |
| MDG-WP06-7D-TEST-CLOSURE-001 | v0.1 | End-to-end test closure remains NOT RUN / NOT VERIFIED |

## 3. Implemented Accounting Domain/Application slice

PR #66 contains isolated Accounting.Contracts, Accounting.Domain and Accounting.Application projects.

Implemented:
- Integer-only IRR amount operations and deterministic Largest Remainder allocation.
- Canonical Account model with field-level evidence validation.
- AccountRegistryValidator for duplicate IDs/codes, prohibited 110809, explicit parent references, parent cycles and parent Level ordering.
- Full 7B runtime PostingRule projection; every required contract field must have VERIFIED evidence matching the exact runtime value. RecognitionTime is explicit and is not inferred from another field.
- FiscalPeriod close/reopen guards; balanced Journal/JournalLine; immutable finalization; separate reversal; Audit references.
- Stable PostingIdentity and read-only, fail-closed PostingPreflight.
- Fail-closed tests for all 39 FE identifiers and 16 COA gate cases; exhaustive required 7B evidence-field tests.

## 4. Exact-SHA test and CI evidence

On SHA 333856b41df29fbdd2e6082d3f76ff05c8a96a61:
- Foundation Build & Test: 220/220 passed; 0 failed; 0 skipped.
- Windows Preview Installer: SUCCESS.
- CodeQL: SUCCESS.
- Finance Workpaper Structural Validation: SUCCESS.
- Project Charter Owner Gate: SUCCESS.
- Persons Linux PostgreSQL migrations, disposable E2E, security, API contract, architecture boundary, merge contract, consumer registry, CI enforcement and temporary-data-retention: SUCCESS.
- Persons Windows PostgreSQL 18.6 migration verification: IN PROGRESS at the time of this register update.

Domain tests do not prove PostgreSQL Accounting persistence, transaction atomicity, database idempotency or end-to-end financial posting.

## 5. Remaining blockers

### 7B
- Accepted positive Posting Rules: 0.
- No debit/credit rule may be invented or seeded without complete source/decision/recognition/amount/tax/subledger/dimension/idempotency/reversal/period/audit/test evidence.

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

## 6. Gate and release status

| Gate | Status | Basis |
|---|---|---|
| Gate 1 — owner design decisions | CLOSED for recorded decisions | Accepted owner decisions remain controlling |
| Gate 2 — architecture/design baseline | CLOSED for v2.5 | v2.6 remains draft |
| Gate 3 — implementation/behavioral verification | OPEN | Domain slice exists; persistence and full finance tests remain |
| Gate 4 — independent evidence acceptance | OPEN | Independent review and end-to-end evidence not accepted |
| O-01 | OPEN | 7B/7C/7D acceptance conditions not all met |
| Posting Release | NOT GRANTED | No positive rules or persistent posting runtime |

## 7. PR and integrity controls

- PR #66 remains OPEN and DRAFT; no merge has occurred.
- No valid independent approval on the final SHA is recorded.
- CI must be checked against the exact current head SHA before readiness claims.
- SHA256SUMS.txt is the integrity manifest for tracked source and workpaper files.
- Owner approval of design is not implementation acceptance, test acceptance or Posting Release.

## 8. Version history

- v0.4 is retained as a historical snapshot.
- v0.5 is the current register for source authority, implementation, test and gate reporting.
