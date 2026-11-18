# MDG-WP06-DOCUMENT-REGISTER-001
## Controlled Document Register — Version 0.4

| مشخصه | مقدار |
|---|---|
| Document ID | MDG-WP06-DOCUMENT-REGISTER-001 |
| Version | 0.4 |
| Classification | CONTROLLED REGISTER / IMPLEMENTATION STATUS |
| Status | DRAFT — CURRENT REGISTER FOR PR #66 |
| Supersedes | MDG-WP06-DOCUMENT-REGISTER-001 v0.3 (retained as historical snapshot) |
| Architecture baseline | MDG-ARCH-MASTER-001 v2.5 |
| Change control | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 |
| Owner decision basis | OWN-7C-043 and previously accepted owner decisions |
| PR | #66 — open, Draft |
| Current code SHA tested | 7eff7fde8a10452dfdbf4b34da40929ee10aecc6 |
| Current PR head at register creation | 7bef76f4ff5a85be9484a3912b71941283a38f1e (documentation-only commits after tested code SHA) |

## 1. Authority hierarchy

1. MDG-ARCH-MASTER-001 v2.5 is the accepted architecture baseline. v2.6 remains draft.
2. COA v1.2 is the master document and primary authority for account coding and General Ledger rules.
3. COA v1.4 is the current supplementary register for account-level reconciliation based on v1.2.
4. COA v1.3 is an intermediate stage only.
5. OWN-7C-043 and other accepted owner decisions control explicit account dispositions and constraints.
6. No separate workbook or extracted spreadsheet is a prerequisite, active authority, or acceptance blocker for 7C.

## 2. Current controlled workpapers

| Workpaper | Version / SHA256 | Current scope and status |
|---|---|---|
| WP06-P4H-7B-POSTING-MATRIX-001 | v0.2 / afd34e3764474e17bde774280b39335615ac5e1b1850f2b3e277a4e300f9a67e | 39 FE dispositions; zero accepted positive Posting Rules; full acceptance evidence required; positive journal assertions blocked |
| WP06-P4H-7C-COA-RECONCILIATION-001 | v0.3 / 7fcdb743eb137630c9b882a514da4f8a1fe6a6b8bd7d9c444a2aa6d182988724 | 78 account identifiers in scope; v1.2 master + v1.4 supplement; canonical attribute gaps remain OPEN/NOT VERIFIED |
| WP06-P4H-7D-TEST-REGISTRY-001 | v0.2 / 596ed924816876d9a64ed08b39c96735ba8c17b2e31202f354d0136508a470a2 | 67 Test IDs: 39 FE + 16 COA + 12 shared controls; domain-level subset implemented; PostgreSQL/end-to-end posting evidence not run |
| MDG-WP06-7B-7C-7D-INTEGRATED-001 | v0.5 / 74a2285653aa80f5b8e507c708bdb754cebd37f9dc53d59d7ad219b8b5585117 | Integrated FE/COA/Test crosswalk; open issues retained |
| MDG-WP06-7B-7C-7D-CROSS-AUDIT-001 | v0.3 / 12eeb495c3996efed5279076ae91cdcff1d04c115bd40792cd1a2e940e0c003a | Cross-audit register; no independent acceptance implied |
| MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001 | v0.2 / ff9ce8d87fb27233be69ce9e3be46474e09e44f68abfbd96bae6f8b621c7b4c1 | Audit closure state; O-01 remains OPEN |
| MDG-WP06-5-STAGE-EXECUTION-REPORT | v0.1 / 0c0298a100c87498022de2793f6b60783a4932f5a0dcf92d4fe29737abe92774 | Updated for the first Accounting Domain/Application implementation slice and 218 passing Domain tests |
| MDG-WP06-7D-TEST-CLOSURE-001 | v0.1 / fe670fad99204e025ff59c68c2feddcd9bb401a70a83f77dabe44fb217792e7b | End-to-end test closure remains NOT RUN / NOT VERIFIED |

## 3. Implemented Accounting Domain/Application slice

The following code is present in PR #66:

- Integer-only IRR amount handling and deterministic Largest Remainder allocation.
- Canonical account attributes with field-level evidence validation; critical fields must be VERIFIED. Unrelated fields may be explicitly represented as N/A only where the applicable policy permits it.
- AccountRegistryValidator for duplicate account IDs/codes, prohibited 110809, missing/explicit parent references, parent cycles and parent Level ordering.
- PostingRuleDefinition aligned to the full 7B acceptance field contract. Every required field must carry verified evidence; a non-applicable field is represented by the verified value NOT_APPLICABLE, not an unverified disposition.
- FiscalPeriod, balanced Journal/JournalLine, immutable finalization, creation/finalization Audit references and linked reversal.
- Stable PostingIdentity and read-only, fail-closed PostingPreflightService.

### Domain test evidence

On code SHA 7eff7fde8a10452dfdbf4b34da40929ee10aecc6:

- CI run 44272283562 — Foundation Build & Test: **218/218 passed, 0 failed, 0 skipped**.
- 39 FE fail-closed cases are mapped to MDG-FIN-GATE-001..039.
- 16 COA fail-closed tests are mapped to MDG-COA-GATE-001..016.
- Seven shared controls have Domain-level test coverage: COA-001, COA-002, REV-001, PERIOD-001, IDEM-001, IDEM-002 and TAX-001.
- Eight additional AccountRegistryValidator tests cover registry structure and hierarchy integrity.

These results prove Domain/preflight behavior only. They do not prove database persistence, transaction atomicity, PostgreSQL idempotency, or end-to-end financial posting.

## 4. Remaining blockers

### 7B — Posting Rules
- Accepted positive Posting Rules: **0**.
- No positive debit/credit rules may be invented or seeded.
- Full source/decision/trigger/recognition/amount/tax/subledger/dimension/idempotency/reversal/period/audit/test evidence is required for each accepted rule.

### 7C — Canonical COA
- COA v1.2 remains the master; v1.4 remains the current supplementary register.
- The 78-ID scope is recorded, but canonical account attributes remain incomplete where source/owner evidence is absent.
- Account IDs, parent IDs, levels, nature, normal balance, postability, subledger, tax treatment and FE eligibility must not be inferred from code prefixes or account titles.

### 7D — Behavioral and runtime tests
- Domain-level FE/COA fail-closed coverage described above is implemented and passed.
- Five shared controls remain without runtime evidence: ATOMIC-001..003, SUBLEDGER-001 and IMEI-001.
- Positive journal assertions and PostgreSQL end-to-end tests remain blocked until 7B/7C rules and persistent Posting runtime exist.

### Persistence dependency
- No Accounting SQL schema, finance migration, transactional Posting writer, persistent Journal/Subledger repository or Posting API is implemented in this slice.
- Migration 0009 is reserved by PR #70, which remains OPEN/DRAFT and has no independent review. No SQL migration may be added or consumed until PR #70 is reviewed and merged into main.
- No merge is performed by this work.

## 5. Gate and release status

| Gate | Status | Basis |
|---|---|---|
| Gate 1 — owner design decisions | CLOSED for recorded decisions | Accepted owner decisions remain controlling |
| Gate 2 — architecture/design baseline | CLOSED for v2.5 | v2.6 is still draft |
| Gate 3 — implementation and behavioral verification | OPEN | Domain slice exists; persistence and full finance tests remain |
| Gate 4 — independent evidence acceptance | OPEN | Independent review and end-to-end evidence not accepted |
| O-01 | OPEN | 7B/7C/7D acceptance criteria not all met |
| Posting Release | NOT GRANTED | No positive Posting Rules or persistent posting runtime |

## 6. PR and integrity controls

- PR #66 remains OPEN and DRAFT; no merge has occurred.
- No valid independent approval on the final SHA is recorded.
- CI evidence must be checked against the exact current head SHA before any readiness statement.
- SHA256SUMS.txt is the integrity manifest for tracked source and workpaper files.
- Owner approval of design is not implementation acceptance, test acceptance, or Posting Release.

## 7. Version history

- v0.3 is retained as the previous register snapshot.
- v0.4 is the current status register for PR #66 and supersedes v0.3 for current implementation, test and gate reporting.
