# MDG-OPEN-ISSUE-REGISTER-001
## Canonical O-01..O-65 Register — Version 1.2

| Field | Value |
|---|---|
| Document ID | MDG-OPEN-ISSUE-REGISTER-001 |
| Version | 1.2 |
| Status | CONTROLLED CANONICAL REGISTER — v3.0.3 BASELINE; OWN-INV-019 OWNER DECISIONS RECORDED 2026-11-20 |
| Architecture baseline | MDG-ARCH-MASTER-001 v3.0.3; owner accepted 2026-11-20 |
| Scope | All canonical open-issue identifiers O-01 through O-65 |
| Register revision date | 2026-11-20 |
| Source branch | docs/inventory-cross-module-contracts-20261120 |
| Source baseline commit | 10c8a8ba8f9aad54c4e2ff05fad49ce12511380f |
| Control rule | This register is authoritative for current O-item status. Master Appendix R.2/R.3 and Section 23 are source/history snapshots and must not be treated as a competing live status ledger. |
| Merge status | PR #73 is merged in main; this v1.2 register revision is proposed on docs/inventory-cross-module-contracts-20261120 in PR #82 and is not yet merged. |

## 1. Purpose and governing rules

This register consolidates O-01..O-65 into one controlled ledger. It does not change accepted owner decisions, invent missing account attributes, create Posting Rules, grant release permission, or claim tests/evidence that have not been demonstrated.

Each O-item has six independent status dimensions:

- **Decision:** owner/product/accounting policy disposition.
- **Design:** completeness of the required model, contract, rules and acceptance criteria.
- **Implementation:** actual code, database, deployment or operational implementation.
- **Test:** execution result of the required tests on an identified build/database/SHA.
- **Evidence:** existence, integrity, freshness, traceability and acceptance of supporting evidence.
- **Gate:** whether the item's specified gate criteria are actually met.

A status in one dimension never implies a status in another. In particular, OWNER_APPROVED does not mean DESIGN_COMPLETE, IMPLEMENTED, TEST_PASS, EVIDENCE_ACCEPTED or GATE_PASS.

### 1.1 Status vocabulary

| Status | Meaning |
|---|---|
| ACCEPTED / RESOLVED | Decision/policy dimension only, unless another dimension is explicitly named |
| PARTIAL | Some defined work exists; required completion criteria remain |
| OPEN | Work or decision remains unresolved |
| NOT IMPLEMENTED | No verified implementation for this item |
| NOT RUN / NOT VERIFIED | Required tests have not been proven to pass |
| PARTIAL EVIDENCE | Some artifacts exist, but are incomplete, stale, scoped or not accepted |
| NOT ACCEPTED | Evidence has not been formally accepted |
| BLOCKED / OPEN | Gate cannot pass while required criteria remain |
| DEFERRED | Explicitly deferred from current release scope; not silently closed |
| NOT REQUIRED | A historical recovery/dependency is explicitly not required for the stated scope |

### 1.2 Closure rule

An O-item may be marked **CLOSED** only when all applicable dimensions have explicit closure evidence and its closure criteria are met. If only a policy decision is resolved, the item remains open/partial overall and the resolved Decision dimension is recorded separately. Gate status is never inferred from a design document, PR approval, generic CI success or an unrelated test.

### 1.3 Current source authority and evidence boundaries

- Architecture: MDG-ARCH-MASTER-001 v3.0.3.
- COA coding/GL authority: COA v1.2 master; COA v1.5 controlled supplementary reconstruction/crosswalk. COA v1.4 and v1.3 are not current supplementary authority.
- Finance: current 7B/7C/7D controlled workpapers, Stage 04 workpaper, and the current Finance Document Register; historical audit snapshots remain historical.
- Inventory: Stage 04 sales/returns/GRNI/CreditExposure design, Stage 05 equations/custody/stocktake, Stage 06 logical ERD, Stage 07 PostgreSQL physical design and Stage 08 security design are design artifacts, not proof of runtime acceptance.
- Gate D evidence: EVD-GATE-D-EXECUTION-001 v0.2 exists on a separate evidence branch and is not integrated into PR #72; it is partial evidence only and must be reconciled against the exact current run/SHA. Gate D remains open.
- No content, code, schema, migration or artifact from legacy RepairDesk is an authority or source for this register.

## 2. Canonical O-01..O-65 register

Status columns are ordered Decision / Design / Implementation / Test / Evidence / Gate. “Open” in a row means the item is not closed overall.

| ID | Issue / required outcome | Owner role | Sev. | Target gate | Decision | Design | Implementation | Test | Evidence | Gate | Remaining closure criteria / notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| O-01 | Controlled replacement of 7B/7C/7D and FE↔COA↔Test crosswalk | Finance Architecture | C0 | B-final | ACCEPTED scope/CR | PARTIAL | PARTIAL | NOT RUN / NOT VERIFIED | PARTIAL / NOT ACCEPTED | OPEN | 39 event dispositions and test IDs exist; canonical account attributes, complete positive rules, end-to-end crosswalk, independent review and accepted test evidence remain. Zero accepted positive Posting Rules; no Posting Release. |
| O-02 | FE-09 pre-delivery repair asset/subledger and counter-account | Inventory + Finance | C0 | B-final | PARTIAL policy boundary | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | BLOCKED / OPEN | Complete asset/subledger/COA/ERD/Posting/test contract. FE-09 R1 remains disabled; no ordinary repair expense before delivery; no guessed 5102/5105 posting. |
| O-03 | 110807 recognition criteria, title and supporting evidence | Finance | C1 | B-final | PARTIAL | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Define measurable recognition event, start/end/reassessment, valuation and evidence; prohibit automatic expense recognition before delivery. |
| O-04 | FE-20 outsourcing counterparty/liability lifecycle | Finance + Purchasing/Repair | C0 | B-final | PARTIAL policy boundary | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Complete lifecycle, liability preservation, no 5103 before delivery, journal cases and tests. |
| O-05 | Exact replacement code for 6204 | Finance/COA | C1 | B-final | OPEN | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Owner/source-backed exact code, effective date and crosswalk; do not recycle the prior code or infer one. |
| O-06 | Technical qualification of 5106 rounding-loss account | Finance/COA | C1 | B-final | ACCEPTED role boundary only | PARTIAL | NO POSTING | NOT RUN | PARTIAL | OPEN | Canonical attributes, Parent/Level, Nature, NormalBalance, Postable/DirectPostingAllowed, no-collision proof, source event, allowed/forbidden FE, PostingPurpose/Rule, idempotency and independent tests. Never infer Nature from code prefix. |
| O-07 | 110404/1105 cheque subledger and 110302 PSP two-sided treatment | Finance | C1 | B-final | PARTIAL policy mapping | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Complete Subledger IDs, cheque lifecycle, source-title/attribute crosswalk and PSP two-sided tie-out tests; do not conflate policy mapping with source authority. |
| O-08 | CreditExposure formula and allocation | Sales + Finance | C0 | B-final | ACCEPTED policy boundaries | DESIGN SPECIFIED | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Stage 04 specifies PartyId×Item, cheque replacement counted once, explicit allocation and returned-cheque behavior; runtime calculation, reconciliation and test vectors remain. |
| O-09 | Monetary threshold policy | Product Owner | C1 | Product/Gate | RESOLVED: no 100,000 IRR threshold; 1 Rial base | RESOLVED policy | NOT APPLICABLE as threshold | POLICY VECTORS only; runtime test status separate | OWNER DECISION RECORDED | DECISION CLOSED; other monetary controls separate | The 100,000 IRR threshold is rejected/removed. Monetary base is 1 Rial. This does not close unrelated rounding, posting or amount-control tests. |
| O-10 | StockBalance projection and deterministic rebuild | Inventory | C1 | B-final | ACCEPTED projection principle | PARTIAL / SPECIFIED | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Complete ERD, Movement replay, projection rebuild, discrepancy reporting, concurrency and tie-out tests. |
| O-11 | Serialized/non-serialized custody lifecycle | Inventory | C1 | B-final | ACCEPTED boundary | PARTIAL / SPECIFIED | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Complete custody transitions, stock-unit tie-out and ATP exclusion with executable tests. |
| O-12 | Multiple identifiers / two IMEIs for one StockUnit | Catalog + Inventory | C1 | B-final | ACCEPTED requirement direction | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | ADR/ERD, uniqueness scope, identifier lifecycle and no-double-count tests. |
| O-13 | Reorder/ADC/LeadTime/SafetyStock calculation window | Inventory + Purchasing | C1 | R1 release | PARTIAL | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Formula Register and 30/60/90-day vectors; no automatic purchase order. |
| O-14 | D-028 WAL archive, monitoring, gap detection and restore chain | Platform Operations | C0 | Gate D | ACCEPTED requirement | PARTIAL | PARTIAL / NOT VERIFIED | NOT RUN end-to-end | PARTIAL | OPEN | Production WAL configuration, monitoring, gap alerts, PITR rehearsal and accepted restore evidence. |
| O-15 | Official CalendarDay source/signature | Platform Operations | C1 | Gate D/Legal | OPEN | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Authoritative source, version, checksum/signature and offline validation evidence. |
| O-16 | TradeIn/Fixed Assets R1-R2 release crosswalk | Product + Finance | C1 | Release Gate | PARTIAL | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Two linked documents, ownership/lifecycle, Gate/release assignment and scope sign-off. |
| O-17 | Payroll Part 5 and FE-37/FE-23 separation | Payroll + Repair Operations + Finance | C1 | R2 Gate | PARTIAL | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Ownership matrix, commission entitlement, wage deduction, COA and tests. |
| O-18 | Historical test-ID provenance and replacement test registry | QA/Finance | C1 | B-final | NOT REQUIRED for O-01 legacy text recovery | PARTIAL | NOT APPLICABLE to historical recovery | PARTIAL / NOT VERIFIED | PARTIAL | OPEN for provenance/crosswalk | Preserve legacy SOURCE-ID values; assign distinct RECONSTRUCTED IDs to new cases and complete FE↔COA↔Test mapping. Historical source-text recovery is not an O-01 prerequisite. |
| O-19 | Legacy 4C/4D/4E/4G test crosswalk | QA/Architecture | C1 | B-final | OPEN | OPEN | NOT APPLICABLE | NOT RUN | NOT ACCEPTED | OPEN | Map from actual available content, preserve historical IDs and publish mapped/unmapped matrix. |
| O-20 | Actual DB Permission Matrix | Security/DBA | C0 | Gate C | ACCEPTED security direction | PARTIAL | NOT VERIFIED | NOT RUN with real credential matrix | PARTIAL | BLOCKED / OPEN | Runtime/migrator/backup/read-only credentials, object/row scope and denied-command proof. |
| O-21 | Hash Chain two-writer/checkpoint/key recovery PoC | AuditSecurity | C0 | Gate C | ACCEPTED requirement | PARTIAL | NOT VERIFIED | NOT RUN | NOT ACCEPTED | BLOCKED / OPEN | No-fork two-writer proof, external checkpoint, key rotation/recovery and verification evidence. |
| O-22 | Idempotency/lock/isolation/crash/retry PoC | Architecture + Platform | C0 | Gate C | ACCEPTED transaction principles | PARTIAL | PARTIAL domain slice only | NOT RUN end-to-end | PARTIAL | BLOCKED / OPEN | Request and business idempotency, concurrency, rollback, replay and crash/retry proof on PostgreSQL. |
| O-23 | Legal retention of financial/tax/personal records | Legal/Product | C1 | Legal | OPEN | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Current authoritative legal sources, record-class retention matrix, reviewer and dated evidence. |
| O-24 | Multi-system API/DB/File/Server topology | Architecture/Platform | C1 | B0/Gate C/D | PARTIAL | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | ADR and PoC preserving local PostgreSQL atomic boundary; deployment topology and failure-mode evidence. |
| O-25 | Operational permissions and report access scope | IdentityAccess + Reporting | C1 | Gate C | ACCEPTED policy | PARTIAL | PARTIAL | NOT RUN / NOT VERIFIED | PARTIAL | OPEN | Complete Role/Permission/RowScope matrix and report/export authorization tests. |
| O-26 | ApprovalRequest ownership and contract | Application + Module Owners | C2 | B-final | ACCEPTED ownership direction | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Consumer module owns its request; no generic workflow engine without ADR. |
| O-27 | Cutting UOM / area conversion | Catalog | C2 | Deferred | DEFERRED | DEFERRED | NOT IMPLEMENTED | NOT RUN | NOT REQUIRED for current scope | DEFERRED | Keep outside current release. Reopen only with independent requirement, units, conversion rules and tests. |
| O-28 | Backup/Restore/Installer/Rollout evidence | Platform Operations | C0 | Gate D | ACCEPTED requirement | PARTIAL | PARTIAL | PARTIAL: scoped PoCs/runners passed; full Gate D not proven | PARTIAL / NOT ACCEPTED | OPEN | Eight logical restore PoCs and 36 PostgreSQL integration tests passed; five-client capacity and hosted install/uninstall checks passed. Full business DB restore, WAL/PITR, RPO≤15m/RTO≤4h, encrypted off-site backup, external file/key restore, real five physical clients, signing, upgrade/rollback and app launch remain. |
| O-29 | D-269 draft lock/lease/single-editor/recovery | Owning Module + Platform | C1 | B-final | ACCEPTED no-side-effect/single-editor direction | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Lease/lock lifecycle, stale lock, crash recovery, cache/version conflict and archive tests. |
| O-30 | Device/token/rate-limit threat model | IdentityAccess/Security | C0 | Gate C | PARTIAL | OPEN | NOT VERIFIED | NOT RUN | NOT ACCEPTED | BLOCKED / OPEN | Threat register, expiry/rotation/revocation/binding and abuse tests. |
| O-31 | Simultaneous lockout of both System Managers | Security/Operations | C0 | Gate C/D | ACCEPTED recovery requirement | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | BLOCKED / OPEN | Physical/offline dual-control recovery, evidence and tested runbook. |
| O-32 | Supplemental Source Register and provenance | Architecture Governance | C1 | Gate A | ACCEPTED control rule | PARTIAL | PARTIAL | Lint/validation incomplete | PARTIAL | OPEN | Every input needs file/version/date/hash/section/authority/supersedes and registry lint. |
| O-33 | API/CT registry, error catalog and paging contracts | Application + API Owners | C1 | B-final | ACCEPTED contract direction | PARTIAL | PARTIAL | Contract tests NOT VERIFIED | PARTIAL | OPEN | Versioned commands/queries, deterministic paging, stable error catalog and contract tests for all endpoints. |
| O-34 | Two-layer idempotency retention | Platform + Finance | C1 | Gate C | RESOLVED policy: Request/API TTL only; posting identity/history permanent | PARTIAL | NOT VERIFIED | PoC/tests NOT RUN | PARTIAL | OPEN | Document replay window and operational retention; prove PostingIdentity/PostingRecord uniqueness survives TTL and no duplicate posting after cleanup. |
| O-35 | Inventory conservation equations and stocktake cutoff/race model | Inventory | C0 | B-final | ACCEPTED principles; owner-directed WarehouseId watermark approved | PDS v0.4 + ERD v0.3 + Cross-Module Contract v1.1 prepared; independent review and source-owner acceptance pending | NOT VERIFIED | PD-T-001..048 and WarehouseId watermark/race/replay/tie-out PostgreSQL tests NOT RUN | PARTIAL | OPEN | Verify per-WarehouseId contiguous counter, no-op projection cursor, multi-Warehouse atomicity, per-observation stocktake vector, zero-gap rollback/replay and exact ledger tie-out on PostgreSQL 18; independent exact-SHA acceptance remains required. |
| O-36 | Reservation TTL for Sales and Repair lifecycle | Sales + Repair + Inventory | C1 | B-final | OWNER RESOLVED 2026-11-20: Sales default 24h from server-confirmed reservation, configurable/versioned and snapshotted; Repair has no automatic TTL | PARTIAL — ERD v0.3/PDS v0.4/Cross-Module Contract v1.1 prepared; v3.0.3 crosswalk and source-owner reconciliation pending | NOT VERIFIED | RSV-01..12 NOT RUN on PostgreSQL/runtime | PARTIAL | OPEN | Implement server-created expiry snapshot; test policy change only affects new reservations, exact expiry boundary, lock-wait race, Worker-offline ATP exclusion and idempotent partial consume/release/expire. |
| O-37 | Catalog entities, ConditionGrade and group hierarchy | Catalog | C1 | B-final | ACCEPTED direction | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Logical ERD, maximum three group levels, cycle prevention, history preservation and grade-sensitive pricing tests. |
| O-38 | GRNI receive-before-invoice/invoice-before-receipt/sellability | Purchasing + Finance | C0 | B-final | ACCEPTED ownership/account boundaries | DESIGN SPECIFIED in Stage 04 | NOT VERIFIED | Stage 04 cases defined; runtime tests NOT RUN | PARTIAL | OPEN | Ownership truth table, 2103/110805/physical-stock distinction and executable receipt/invoice/sellability cases. |
| O-39 | LandedCost shared expense allocation/materiality | Purchasing + Finance | C1 | B-final | PARTIAL | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | OPEN | Direct attribution, allocation basis per cost type, authoritative materiality source and numeric test vectors. |
| O-40 | Sales lifecycle, partial delivery, return/refund/backorder | Sales + Inventory + Finance | C1 | B-final | RESOLVED policy/contract direction | DESIGN SPECIFIED in Stage 04 | NOT VERIFIED | Runtime tests NOT RUN | PARTIAL | OPEN | Immutable source-linked Delivery/Return/Adjustment, allocation snapshots, cumulative return cap and exact cumulative integer-IRR reversal tests. |
| O-41 | Suspense matrix for all control accounts | Finance | C0 | B-final | OPEN | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | BLOCKED / OPEN | Allowed source, normal side, aging, owner, clearing trigger, statement and close-blocker tests for every control account. |
| O-42 | Opening/import source controls and period-close checklist | Finance + Platform | C1 | B-final | PARTIAL | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Source proof, cutoff, serials, totals, rejected rows, close/reopen and year-close tests. |
| O-43 | R1/R2 report dictionary and profit formulas | Reporting + Finance | C1 | R1/R2 release | PARTIAL | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Grain/source/formula/access/tie-out; R1 excludes repair profit; R2 formula and tests. |
| O-44 | BusinessTimeZone and calendar effective-date semantics | Platform Operations | C1 | Gate D | OPEN | PARTIAL | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Deployment timezone configuration and DST/calendar tests; no host-timezone assumption. |
| O-45 | Session heartbeat/stale cleanup/token rotation/revocation | IdentityAccess | C0 | Gate C | ACCEPTED session policy direction | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | BLOCKED / OPEN | Threat-model-based lifecycle; crash/reconnect/revoke and concurrent-session tests. |
| O-46 | Bootstrap and recovery capture-then-invalidate/logging | IdentityAccess | C0 | Gate C | ACCEPTED security direction | PARTIAL | NOT VERIFIED | NOT RUN | PARTIAL | BLOCKED / OPEN | Unique one-time bootstrap, ten recovery codes, no raw-secret logs and recovery tests. |
| O-47 | Installer signature/offline dependency mirror/upload validation | Platform + DocumentFile | C1 | Gate D/C | ACCEPTED requirement | PARTIAL | PARTIAL: installer exists; signing/validation incomplete | Install/uninstall runner tests PASS; security validation incomplete | PARTIAL | OPEN | Trusted signer/chain, package hashes, MIME/path/malware checks and offline install. Current installer evidence does not establish trusted signing. |
| O-48 | NFR benchmark, observability and restore objectives | Platform Operations | C1 | Gate D | ACCEPTED objectives | PARTIAL | NOT VERIFIED | RPO/RTO and 3× load NOT RUN | PARTIAL | OPEN | p95/Trial Balance benchmark, 3× load, alert/runbook proof and measured RPO/RTO. |
| O-49 | Document navigation, cross-reference and keyboard/RTL accessibility | Architecture Docs + UI Owner | C2 | Gate A/R1 UX | PARTIAL | PARTIAL | PARTIAL: text artifact exists; interactive navigation not verified | Visual/keyboard QA NOT RUN | PARTIAL | OPEN | Stable TOC/headings/tables/citations, keyboard menus, focus behavior and final RTL visual QA. |
| O-50 | PostingPurpose closed-value registry and FE mapping | Accounting + Application/API Owners | C1 | B-final | PARTIAL policy clarification | PARTIAL | PARTIAL domain contracts; no accepted positive rules | Negative/fail-closed coverage partial; full contract tests NOT VERIFIED | PARTIAL | OPEN | Closed server-derived Purpose values, complete FE mapping, unknown/duplicate/replay tests; zero positive Posting Rules accepted. |
| O-51 | Soft Close / Hard Reopen and FY-01 command policy | Accounting + Finance + Product Owner | C1 | B-final | RESOLVED policy direction | PARTIAL | PARTIAL | Matrix/period tests NOT VERIFIED | PARTIAL | OPEN | Open/Soft/Hard matrix, command permissions, independent approval and period-level tests. |
| O-52 | AR/AP transaction matrix and GL/Subledger/Treasury reconciliation | Finance-AR/AP + Accounting + Treasury | C1 | B-final | RESOLVED ownership boundary | PARTIAL | NOT VERIFIED | Reconciliation matrix/tests NOT RUN | PARTIAL | OPEN | Cash/credit sales, partial payment, allocation, cheque accept/bounce, advances, refunds/returns and purchase balances. |
| O-53 | Sales transfer-of-control / RecognitionTime evidence and contract | Sales + Inventory + Accounting | C1 | B-final | RESOLVED policy direction | DESIGN SPECIFIED in Stage 04 | NOT VERIFIED | Runtime evidence/tests NOT RUN | PARTIAL | OPEN | Actor, handover acknowledgement, movement, partial/rejected delivery, RecognitionTime and Journal contract with runtime evidence. |
| O-54 | TradeIn orchestration and partial-failure handling | Product + TradeIn + Sales/Purchasing + Application | C1 | Release Gate | PARTIAL | OPEN | NOT VERIFIED | NOT RUN | NOT ACCEPTED | OPEN | Aggregate sequence, intermediate states, retries, partial failure and TradeInId linking; never net the two documents. |
| O-55 | Accounting framework and NRV scope | Product Owner + Finance/Accounting | C1 | B-final/Legal | OPEN | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Select authoritative framework and NRV scope/version, deviation sources and test vectors; do not cite an unapproved standard. |
| O-56 | 110807 nature/title/recognition criteria | Finance/COA + Accounting | C2 | B-final | PARTIAL | PARTIAL | NO POSTING | NOT RUN | PARTIAL | OPEN | Align terminology/recognition with approved framework and canonical attributes; no posting until accepted. |
| O-57 | Purchase-tax input and partial returns under TaxMode=0 | Purchasing + Inventory + Finance/Accounting | C1 | B-final | PARTIAL | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Define TaxLine/TaxPosting for total amount input, cost allocation and partial returns in every path; zero-tax path must be explicit. |
| O-58 | P-18 guarantee cheque and 7101 account reference | Treasury + Finance/COA | C2 | B-final | OPEN | OPEN | NO 7101 posting/cash/AR/exposure effect | NOT RUN | NOT ACCEPTED | OPEN | Authoritative P-18/COA source, Nature/NormalBalance/statement mapping; until then no 7101 and no cash/AR/Exposure effect. |
| O-59 | Finance Gate 1..4 definitions | Finance Architecture + Product Owner | C1 | Gate A/B-final | PARTIAL | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Versioned source and measurable criteria/owner/evidence for every Gate; never infer PASS from a label. |
| O-60 | Document-number allocation, gap/void, rollback and retry | Platform + Owning Module + Accounting | C1 | Gate C/B-final | PARTIAL | OPEN | NOT IMPLEMENTED | NOT RUN | NOT ACCEPTED | OPEN | Choose model; define concurrency/crash/retry, gap register or sequence rule, transaction behavior and tests; no unsupported legal claim. |
| O-61 | WAC vectors, fractional non-monetary quantity and IRR conservation | Inventory + Accounting/QA | C1 | B-final | ACCEPTED conservation direction | DESIGN SPECIFIED in Stage 05 | NOT VERIFIED | Full numeric/replay/tie-out tests NOT RUN | PARTIAL | OPEN | 1/3/partial/final issue, multi-cost purchases, fractional quantity, Movement replay, integer output and total-value tie-out. |
| O-62 | Two-sided PSP 110302 reporting without balance netting | Treasury + Accounting + Reporting | C1 | B-final | RESOLVED two-sided policy | PARTIAL | NOT VERIFIED | Reporting/tie-out tests NOT RUN | PARTIAL | OPEN | Separate debit/credit balances and two report lines; reconcile each side independently to Subledger and GL. |
| O-63 | COA 2114/5106 source/hash/version crosswalk | Finance/COA + Architecture Governance | C1 | B-final | RESOLVED authority: v1.2 master + v1.5 supplement | PARTIAL | NOT VERIFIED | Crosswalk/attribute tests NOT RUN | PARTIAL | OPEN | Source/hash/effective status; exactly one canonical reference row per ID; no invented parent/level/normal balance/postability/PostingRule. |
| O-64 | Authorized role and scope to change negative-stock limit | Product Owner + Inventory + IdentityAccess | C1 | B-final / Negative-stock enablement | OWNER RESOLVED 2026-11-20 (O-64-SCOPE): GENERAL_MANAGER only within explicitly owner-approved assigned WarehouseId×VariantId pairs; scope creation/change requires owner approval + Audit; SYSTEM_MANAGER/Emergency denied; Serial/IMEI remains zero-negative | PARTIAL — logical scope assignment, cross-module contract v1.1 and source-event proposal v0.3 specified | Server enforcement NOT VERIFIED | SCOPE-01..07 NOT RUN | PARTIAL | OPEN | Enforce authenticated GENERAL_MANAGER and active pair assignment server-side; prohibit self-assignment; require owner approval/Audit for every assignment change; deny outside scope/System Manager/Emergency; prove Serial/IMEI absolute prohibition and exact-SHA evidence. |
| O-65 | Mixed-sign D/P/N allocation contract | Product Owner + Finance/Accounting | C1 | Gate A decision; B-final/Gate C implementation | ACCEPTED / DECISION CLOSED (2026-11-19) | SPECIFIED in P.4 | PARTIAL generic allocation exists; full O-65 integration NOT VERIFIED | O-65-specific C#/DB/Journal tests NOT VERIFIED | OWNER DECISION EVIDENCE; runtime evidence pending | OPEN for implementation/test/gate | Preserve one monetary basis, separate D/P/N pools, exact Rational weights, canonical tie-break, destination-account identity and fail-closed behavior. This does not alter one-time document-net AwayFromZero rounding, approve a PostingRule or grant Posting Release. |

## 3. Cross-item status summary

This summary is derived from the 65 detailed rows and must not be maintained as a second source of truth.

- O-09: owner decision resolved — the 100,000 IRR threshold is removed; 1 Rial is the monetary base. This does not imply unrelated implementation or test closure.
- O-18: historical source-text recovery is not required for O-01. O-18 provenance/replacement-test registry work remains partial; O-19 is a separate open crosswalk.
- O-27: explicitly deferred; not an R1 blocker and not closed as implemented.
- O-33/O-50: partially specified; contract completion and tests remain open.
- O-34: policy clarified; retention PoC and replay/cleanup tests remain open.
- O-35: Stage 05/PDS v0.1-v0.3 are specified; Cross-Module Contract v1.0 records the owner-directed per-WarehouseId counter, but a PDS successor, independent review and PostgreSQL proof remain open. PD-T-001..039 are NOT RUN; O-35 remains OPEN.
- O-36: owner decision sets configurable Sales default TTL to 24h from confirmed reservation; Repair has no automatic TTL; baseline reconciliation, implementation and tests remain open.
- O-38: Stage 04 GRNI/sellability design specified; runtime tests remain open.
- O-40: Stage 04 lifecycle/return contract specified; runtime tests/evidence remain open.
- O-49: text corrections recorded; visual/keyboard/RTL QA remains open.
- O-51: Hard Reopen policy clarified; matrix and tests remain open.
- O-52: ownership boundary clarified; AR/AP reconciliation matrix remains open.
- O-53: transfer-of-control policy clarified; runtime evidence remains open.
- O-62: PSP two-sided handling clarified; reporting and tie-out remain open.
- O-63: COA source authority clarified; crosswalk and canonical attributes remain open.
- O-64: owner decision specifies GENERAL_MANAGER only; SYSTEM_MANAGER/Emergency Session alone are denied; server enforcement/tests remain open.
- O-65: owner decision closed; implementation, O-65-specific tests and evidence remain open.
- O-28: scoped restore/integration/installer tests have passed, but Gate D is still open because full operational and recovery criteria are not all evidenced.
- O-01: remains open; zero accepted positive Posting Rules and no Posting Release.

## 4. Global controls that remain in force

1. No 100,000 IRR monetary threshold. Monetary base is 1 Rial.
2. Monetary amounts are integer IRR. Exact signed document net is rounded once at document level using AwayFromZero; maximum absolute difference is 1 Rial per document. No raw row-by-row monetary rounding.
3. COA v1.2 is master authority; v1.5 is controlled supplement. Do not invent missing canonical account attributes or Posting Rules.
4. Positive accepted Posting Rules: 0. Posting Release: NOT GRANTED.
5. Gate C and Gate D remain OPEN / NOT VERIFIED. No production migration or Go-Live is authorized by this register.
6. No item may be closed solely because a policy paragraph was added, a design document was written, a PR was approved, or unrelated CI checks passed.

## 5. Change control

- This is the sole canonical live O-01..O-65 status register.
- Any status change must identify: O-ID, dimension changed, previous status, new status, exact source/evidence reference, version/SHA, test run where relevant, reviewer/owner authority where required, and date.
- Do not overwrite historical evidence. Add a dated change entry and retain prior status.
- Master Appendix R.2/R.3 and Section 23 are summary/history views; they must link to this register and must not diverge from it.
- A future revision must preserve all 65 IDs. No renumbering, silent deletion, or merging of semantically distinct O-items is allowed.

## 6. Source documents reviewed for this revision

- `docs/architecture/MDG-ARCH-MASTER-001_v3.0.3.txt` — Appendix R.2/R.3, Appendix S/T/U/V and v3.0.3 control statements.
- `docs/finance/workpapers/p4h/MDG-WP06-DOCUMENT-REGISTER-001_v0.7.md` — finance register and current Stage 04 overlay; historical cover metadata requires the v0.8 update.
- `docs/finance/workpapers/p4h/integration/MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001_v0.2.md` — O-01 bounded closure and unresolved design/test gaps.
- `docs/finance/workpapers/p4h/integration/MDG-WP06-7B-7C-7D-CROSS-AUDIT-001_v0.3.md` — historical cross-audit; not a live status register.
- `docs/finance/workpapers/p4h/integration/MDG-WP06-STAGE-04-SALES-RETURNS-GRNI-CREDIT-EXPOSURE-TIEOUT-001_v0.1.md` — Stage 04 design contract.
- `docs/modules/inventory/MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001_v0.1.md` — Stage 05 design.
- `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001_v0.1.md`, `..._v0.2.md`, `..._v0.3.md`, `MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-AUDIT-001_v0.1.md`, `MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-002_v0.1.md` and `...REAUDIT-003_v0.1.md` — Inventory PostgreSQL physical design; re-audits are internal design reviews, not independent attestation or runtime proof.
- Gate D evidence: `docs/operations/evidence/EVD-GATE-D-EXECUTION-001_v0.2.md` exists on separate branch `gate-d-execution-evidence-record` at commit `1b1163eb68c059400a58953b8fb9da7af98c11ff`; it is NOT part of PR #72's current branch tree and must be reconciled against the exact current PR/run SHA before acceptance. It is scoped evidence, not Gate D closure.

## 7. Explicit non-claims

This register does not claim that all 65 items are closed, that every design has been implemented, that all tests have passed, that evidence has been independently accepted, that Gate C/D has passed, that the finance posting runtime is production-ready, or that the project is authorized for production migration/Go-Live.


## 8. Owner decision supplement and cross-module successor package — 2026-11-20

The explicit follow-up owner decisions are recorded in `docs/governance/OWN-INV-019-INVENTORY-OWNER-DECISION-SUPPLEMENT-001_v1.0.md`. Controlled successor artifacts prepared in PR #82 are:

- `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001_v0.4.md`
- `docs/modules/inventory/MDG-INVENTORY-LOGICAL-ERD-001_v0.3.md`
- `docs/modules/inventory/MDG-INVENTORY-CROSS-MODULE-TECHNICAL-CONTRACT-001_v1.1.md`
- `docs/modules/inventory/MDG-INVENTORY-CROSS-MODULE-TEST-SPEC-001_v1.1.md`
- `docs/modules/inventory/INVENTORY-SOURCE-EVENT-CONTRACT-PROPOSAL-001_v0.3.md`
- `docs/security/MDG-PLATFORM-DB-ROLE-GRANT-MATRIX-001_v0.1.md`

Owner decisions recorded: D-15-CUTOVER-GUARD-01 (policy decision CLOSED/APPROVED; independent MAX_CUTOVER_TAIL_COUNT and MAX_CUTOVER_LOCK_DURATION remain NOT_SET pending target-hardware PostgreSQL measurement); O-64-SCOPE (GENERAL_MANAGER only within owner-approved assigned WarehouseId×VariantId pairs; every scope change requires owner approval + Audit; SYSTEM_MANAGER/Emergency denied; Serial/IMEI zero-negative); WATERMARK-WH-01 (per-WarehouseId contiguous counter); RES-TTL-SALES-01 (Sales default 24h, configurable/versioned/snapshotted; Repair no automatic TTL); AUD-ATTENTION-01 (REQUIRES_ATTENTION non-terminal; retry continues). Earlier owner decisions VAL-POOL-01, SERIAL-NS-01 and NEG-STOCK-SCOPE-01 remain in force.

Status boundaries:
- O-35 remains OPEN pending independent review and exact-SHA PostgreSQL watermark/race/replay/tie-out evidence.
- O-36 remains OPEN pending controlled baseline/source reconciliation, implementation and reservation lifecycle tests.
- O-64 remains OPEN pending server-side enforcement, authorization tests and accepted evidence.
- D-15 policy decision is CLOSED/APPROVED; numeric budget values remain NOT_SET and operational Cutover remains prohibited until both values are measured, approved, configured and tested.
- All new cross-module acceptance tests remain NOT_RUN.
- Migration 0009 remains RESERVED; no SQL/DDL/EF/migration/GRANT/runtime/Posting/Go-Live authorization.

## 9. D-15-CUTOVER-GUARD-01 — policy closure versus operational gate

| Dimension | Status |
|---|---|
| Owner policy decision | CLOSED / APPROVED |
| MAX_CUTOVER_TAIL_COUNT | NOT_SET — target PostgreSQL/hardware measurement required |
| MAX_CUTOVER_LOCK_DURATION | NOT_SET — target PostgreSQL/hardware measurement required |
| All pre-cutover movements applied | Runtime proof NOT RUN |
| Projection quantity difference versus Ledger | Must equal exactly zero; runtime proof NOT RUN |
| Per-Warehouse watermark difference versus Ledger | Must equal exactly zero; runtime proof NOT RUN |
| Pointer Swap gate | NOT SATISFIED; Pointer Swap prohibited until both budgets and all consistency checks pass |

Exceeding either budget or failing any consistency check requires rollback, preservation of the previous active Pointer, lock release and continued background Catch-up. No numeric value may be inferred or guessed.

### Version history
- v1.2 — registers OWN-INV-019 v1.0, D-15 policy closure, O-64-SCOPE, and controlled successor designs/tests for O-35/O-36/O-64. v1.1 is retained as historical snapshot.
