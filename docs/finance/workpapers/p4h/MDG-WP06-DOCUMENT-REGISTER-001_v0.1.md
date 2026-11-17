# MDG-WP06-DOCUMENT-REGISTER-001
## رجیستر نسخه‌ها و وضعیت پذیرش بسته O-01

| مشخصه | مقدار |
|---|---|
| Version | 0.1 |
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
| WP06-P4H-7B-POSTING-MATRIX-001 | 0.2 | docs/finance/workpapers/p4h/7b/WP06-P4H-7B-POSTING-MATRIX-001_v0.2.md | 039533851f21028503bd85c0b94b04cb2cdd07d85e36bc736ab2fa4792817639 | DRAFT; 39 event rows; posting rule detail partial |
| WP06-P4H-7C-COA-RECONCILIATION-001 | 0.2 | docs/finance/workpapers/p4h/7c/WP06-P4H-7C-COA-RECONCILIATION-001_v0.2.md | 78ceaf0351a422d62fe5fdfb14af2134e7a920dd2c855d4b5ae6315e98a0d466 | DRAFT; source verified; canonical v1.4 row reconciliation open |
| WP06-P4H-7D-TEST-REGISTRY-001 | 0.2 | docs/finance/workpapers/p4h/7d/WP06-P4H-7D-TEST-REGISTRY-001_v0.2.md | 4626a5d8ae121c061cc6f6c090c342770ad3f3dbe7372666abf29883457e1f72 | DRAFT; 39 gate cases; positive journal assertions pending; NOT RUN |
| MDG-WP06-7B-7C-7D-INTEGRATED-001 | 0.4 | docs/finance/workpapers/p4h/integration/MDG-WP06-7B-7C-7D-INTEGRATED-001_v0.4.md | 2f8f04da881810482a436dc5238ec2b358d11f53e0ecc41acb2828cda36970bd | DRAFT; integrated crosswalk and governance baseline |
| MDG-WP06-7B-7C-7D-CROSS-AUDIT-001 | 0.2 | docs/finance/workpapers/p4h/integration/MDG-WP06-7B-7C-7D-CROSS-AUDIT-001_v0.2.md | a983d22553209c95f2815587c6ec8b375551caae3efcea53a59f112b0b295765 | STRUCTURAL CHECKS RECORDED; independent review pending |
| MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001 | 0.1 | docs/finance/workpapers/p4h/integration/MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001_v0.1.md | ae321f2d2a4d553d537eec9e7decfa45d51be72eef4ddaa7bfaaa34938695928 | PROGRESS REGISTER; O-01 OPEN |

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
- 7C v0.1 is superseded by 7C v0.2.
- 7D v0.1 is superseded by 7D v0.2.
- Integrated v0.2 and v0.3 are historical interim baselines; integrated v0.4 is the current integrated draft.
- Source row register v0.1 is superseded by v0.2.
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
