# MDG-WP06-DOCUMENT-REGISTER-001
## رجیستر نسخه‌ها و وضعیت پذیرش بسته O-01

**نسخه 0.3 — Controlled Execution Addendum — 2026-11-18**

| مشخصه | مقدار |
|---|---|
| Version | 0.3 |
| Status | CONTROLLED DOCUMENT REGISTER — DRAFT PACKAGE |
| Owner criterion | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 — OWNER APPROVED |
| Architecture | MDG-ARCH-MASTER-001 v2.5 last accepted; v2.6 draft pending independent review |
| PR | #66 |
| Branch | docs/finance-7b-7c-7d-integrated-baseline-20261117 |
| PR head SHA | Live value in PR #66; the register does not hard-code a moving branch head |
| O-01 | OPEN — replacement criterion approved; deliverable acceptance pending |
| Gate 3 / Gate 4 | OPEN / OPEN |
| Posting Release | NOT GRANTED |

## 1. اسناد مرجع جاری این بسته

| شناسه | نسخه | مسیر/محل | SHA-256 | وضعیت |
|---|---|---|---|---|
| CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 | 0.1 | docs/architecture/change-requests/CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001_v0.1.md | 2de6c16e724fe0b06faf5978f95020c3fe602433f38655906024a4073aed3d81 | OWNER APPROVED; independent review pending |
| WP06-P4H-7B-POSTING-MATRIX-001 | 0.2 | docs/finance/workpapers/p4h/7b/WP06-P4H-7B-POSTING-MATRIX-001_v0.2.md | 4db46c270000c5565a7cc2408c8225d4d1c52e8310225416d0b7e13f6c58f814 | DRAFT; 39 event rows; field-level disposition complete; positive posting rule detail partial |
| WP06-P4H-7C-COA-RECONCILIATION-001 | 0.3 | docs/finance/workpapers/p4h/7c/WP06-P4H-7C-COA-RECONCILIATION-001_v0.3.md | a09b83f47a69d26ce976031091f579752b108b7f46de59d4e6ff1271dc37f6f5 | DRAFT; owner overlay 44 dispositions; canonical row reconciliation open |
| WP06-P4H-7D-TEST-REGISTRY-001 | 0.2 | docs/finance/workpapers/p4h/7d/WP06-P4H-7D-TEST-REGISTRY-001_v0.2.md | 364b33a49d3846506a940169b1d1d275c14c94809911fee1594d9e702f7d6603 | DRAFT; 51 fail-closed cases (39 FE + 12 COA); positive journal assertions pending; NOT RUN |
| MDG-WP06-7B-7C-7D-INTEGRATED-001 | 0.4 | docs/finance/workpapers/p4h/integration/MDG-WP06-7B-7C-7D-INTEGRATED-001_v0.4.md | d23e476c51af16e2510a4d854967f686d3ff477fbfb89c9084ddbe0238fc2e48 | DRAFT; integrated crosswalk and governance baseline |
| MDG-WP06-7B-7C-7D-CROSS-AUDIT-001 | 0.2 | docs/finance/workpapers/p4h/integration/MDG-WP06-7B-7C-7D-CROSS-AUDIT-001_v0.2.md | 8eaeed9e7a910f66a83f19e6e997ab6b6367d83a04b7339a5799cb6bb087e2cc | STRUCTURAL CHECKS RECORDED; independent review pending |
| MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001 | 0.1 | docs/finance/workpapers/p4h/integration/MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001_v0.1.md | fd1e3af32354b8503b04c023cf1b28bbb6993b995a849fba77d18f4a05d5add0 | PROGRESS REGISTER; O-01 OPEN |

| MDG-WP06-7D-TEST-CLOSURE-001 | 0.1 | docs/finance/workpapers/p4h/integration/MDG-WP06-7D-TEST-CLOSURE-001_v0.1.md | 267e9842469d3c784ae9a4aa8d5e03480de581667fc27b6bbe53a82cd18befa5 | SUPPORTING WORKPAPER; 51 test IDs tracked; NOT RUN |

## 2. اسناد معماری و منبع ثبت‌شده در Library

| شناسه/Artifact | نسخه | Library path | SHA-256 | وضعیت |
|---|---|---|---|---|
| MDG-ARCH-MASTER-001 | 2.6 Draft — Markdown | /MDGsm_Unified_Architecture_Blueprint_v2.6_Draft.md | 01463112db7e7b4c3f6aed171aa16f63eead93235b00acacb01bbd882b7d386d | DRAFT; pending independent review |
| MDG-ARCH-MASTER-001 | 2.6 Draft — Word | /MDGsm_Unified_Architecture_Blueprint_v2.6_Draft.docx | 084a10ecccb3a163b9b3d66481b969ee14b3923614cf6f3b20b1563bfa03ad0a | DRAFT; pending independent review |
| MDG-WP06-COA-76-ROW-SOURCE-REGISTER | 0.2 CSV | /MDG-WP06-COA-76-ROW-SOURCE-REGISTER_v0.2.csv | 78c3793c075ff4aaebef4b86230411d3dca52233f0cca3c81278b2f4607acbb1 | 76 rows × 39 columns; source snapshot + canonical status |
| COA group source ZIP | v1.0 source bundle | /فهرست جامع و مرجع کدینگ حساب‌ها.zip | c17e3c16d05abb9d2ae57b8cfa0c8860e69b613ececb0db7758668cd1db04449 | Source evidence |
| COA source Excel extracted | v1.0 | inside source ZIP | a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866 | 9 sheets; account sheet 76 rows × 24 columns |
| COA source TXT | v1.0 | inside source ZIP | 777328fdc9f42b1884ba8cf5884308f840893534ba86e51212328d4a7e174d79 | Source evidence |

## 3. سلسله‌مراتب COA

- Architecture v2.5 remains the last accepted architecture baseline until v2.6 passes independent review and document acceptance.
- COA v1.2 is the previous controlled design baseline.
- COA v1.4 is the current owner-declared design baseline built from v1.2.
- COA v1.3 is an intermediate stage only; it is not substituted for v1.4.
- Excel/TXT v1.0 are source evidence, not the ultimate decision authority.
- The 76-row CSV v0.2 preserves all source fields and explicitly marks canonical attributes UNVERIFIED/NOT ASSIGNED/NOT SET pending row-level reconciliation to v1.4.

## 4. Supersession and lineage

- 7B v0.1 is superseded by 7B v0.2.
- 7C v0.1 is superseded by v0.2; v0.2 is superseded by v0.3.
- 7D v0.1 is superseded by 7D v0.2.
- Integrated v0.2–v0.4 are historical interim baselines; integrated v0.5 is the current integrated draft.
- Source row register v0.1 is superseded by v0.2. The 7C v0.3 owner overlay is recorded in the repository workpaper; a separate CSV upload was blocked by Library upload throttling and is not claimed as registered.
- Architecture v2.5 is not modified; v2.6 is a controlled amendment draft and does not become accepted until independent review and formal document acceptance.

## 5. پذیرش و بستن O-01

این رجیستر به‌تنهایی هیچ سندی را ACCEPTED یا O-01 را CLOSED نمی‌کند. بسته‌شدن O-01 منوط است به:
1. 7B complete field-level matrix or explicit blocked fields, owner action, clear condition, Test ID and no-posting guard for every FE.
2. 7C canonical attribute reconciliation to COA v1.4; no prefix inference; unresolved fields explicit per account.
3. 7D complete test crosswalk, positive journal assertions for posting-ready rules, and fail-closed tests for blocked/scope-excluded rules.
4. Independent audit on exact final SHA; no unresolved critical document-integrity/provenance/crosswalk/control defects.
5. Execution of all tests that are currently executable, with reproducible evidence; tests blocked by incomplete 7B/7C must remain NOT RUN/BLOCKED.
6. Professional review for all rules/attributes declared FINAL/ACCEPTED.
7. Formal acceptance and supersession record for final versions.

Until then:
- O-01: OPEN.
- Implementation: NOT VERIFIED.
- Test execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.

## 6. Integrity

All repository document hashes are listed in SHA256SUMS.txt at the exact PR head. Library artifacts carry the hashes listed above. Any change to a registered artifact requires a new version or controlled revision and a corresponding hash update; historical versions must remain traceable.


## 7. نسخه جاری بسته پس از اعمال Owner Overlay

| شناسه | نسخه | SHA-256 |
|---|---|---|
| CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 | 0.1 | 2de6c16e724fe0b06faf5978f95020c3fe602433f38655906024a4073aed3d81 |
| WP06-P4H-7B-POSTING-MATRIX-001 | 0.2 | afd34e3764474e17bde774280b39335615ac5e1b1850f2b3e277a4e300f9a67e |
| WP06-P4H-7C-COA-RECONCILIATION-001 | 0.3 | 175bddbd48284c0c29c17d0306893a3e6e8c484f3c61fd8712b13e2111fbd232 |
| WP06-P4H-7D-TEST-REGISTRY-001 | 0.2 | d9e8fce2014fcdbabfa275827ded211f4255ca1e055489085ed13e5ed7b6c722 |
| MDG-WP06-7B-7C-7D-INTEGRATED-001 | 0.5 | 4bfe815dfa644ab31b96373559ebf9a5c6a06aa4c362fff7ec45fa1c3c3ca340 |
| MDG-WP06-7B-7C-7D-CROSS-AUDIT-001 | 0.3 | 12eeb495c3996efed5279076ae91cdcff1d04c115bd40792cd1a2e940e0c003a |
| MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001 | 0.2 | ff9ce8d87fb27233be69ce9e3be46474e09e44f68abfbd96bae6f8b621c7b4c1 |

The current branch head is read live from PR #66; no moving SHA is hard-coded here.


## 8. Execution addendum — latest controlled status (2026-11-18)

این بخش آخرین وضعیت این رجیستر است و بر snapshotهای قدیمی‌تر بخش‌های قبلی مقدم است؛ نسخه‌های قبلی برای lineage حفظ می‌شوند.

| Artifact | Version | Repository path | SHA-256 | Current status |
|---|---:|---|---|---|
| 7B Master Posting Matrix | 0.2 | docs/finance/workpapers/p4h/7b/WP06-P4H-7B-POSTING-MATRIX-001_v0.2.md | afd34e3764474e17bde774280b39335615ac5e1b1850f2b3e277a4e300f9a67e | 39/39 event dispositions; all BLOCKED/SCOPE_BLOCKED; positive rules incomplete |
| 7C baseline | 0.3 | docs/finance/workpapers/p4h/7c/WP06-P4H-7C-COA-RECONCILIATION-001_v0.3.md | a09b83f47a69d26ce976031091f579752b108b7f46de59d4e6ff1271dc37f6f5 | 12 baseline Open Items; internal header/version mismatch recorded |
| 7C execution addendum | 0.1 | docs/finance/workpapers/p4h/integration/MDG-WP06-5-STAGE-EXECUTION-REPORT_v0.1.md | f8d9700bc094dd1fed7ae972004bce204d15d08e6d98c73157912c95be1bc079 | Extends Open Items to 16; v1.4 row-level reconciliation blocked |
| 7D Test Registry | 0.2 | docs/finance/workpapers/p4h/7d/WP06-P4H-7D-TEST-REGISTRY-001_v0.2.md | 596ed924816876d9a64ed08b39c96735ba8c17b2e31202f354d0136508a470a2 | 67 Test IDs; positive journal assertions blocked; NOT RUN |
| Integrated 7B/7C/7D | 0.5 | docs/finance/workpapers/p4h/integration/MDG-WP06-7B-7C-7D-INTEGRATED-001_v0.5.md | 4bfe815dfa644ab31b96373559ebf9a5c6a06aa4c362fff7ec45fa1c3c3ca340 | Prior integrated draft, supplemented by this report |
| Five-stage execution report | 0.1 | docs/finance/workpapers/p4h/integration/MDG-WP06-5-STAGE-EXECUTION-REPORT_v0.1.md | f8d9700bc094dd1fed7ae972004bce204d15d08e6d98c73157912c95be1bc079 | Partial execution; acceptance blockers recorded |

### 8.1 Structural validation
- Validator: `scripts/ci/validate_finance_workpapers.py`
- Workflow: `.github/workflows/finance-workpaper-validation.yml`
- Scope: 39 FE rows, 16 7C Open Items, 16 COA gate tests, 12 shared controls, 67 unique Test IDs.
- This validator checks document structure/cross-references only; it does not execute accounting behavior.
- Runtime Accounting tests remain NOT RUN.

### 8.2 Acceptance
- O-01: OPEN.
- Gate 3 / Gate 4: OPEN / OPEN.
- Evidence: NOT ACCEPTED.
- Independent review on exact final SHA: PENDING.
- Posting Release: NOT GRANTED.
