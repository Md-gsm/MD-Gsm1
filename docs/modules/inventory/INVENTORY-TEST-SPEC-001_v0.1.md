# Inventory Test Specification — Acceptance Proposal
Version: 0.1 | Status: TEST DESIGN — QA/DB/OWNER REVIEW REQUIRED
Authority: OWN-INV-018 v1.0; Inventory contract v0.2. Target: PostgreSQL 18 (verify exact CI server version).

## 1. Evidence rule
A test definition is not a test result. Unexecuted = NOT_RUN. Acceptance requires real PostgreSQL, exact source/build/test SHA, migration/schema version, deterministic fixtures, multi-connection evidence, and database evidence for ledger/projection/valuation/audit/outbox. Mock/InMemory is supplemental only. Do not consume migration 0009 before PR #70 is independently reviewed and merged to main and subsequent schema authorization is granted.

## 2. Owner-approved baseline (34 tests)
| TestId | Scenario | Expected result |
|---|---|---|
| INV-T-001 | Goods receipt | Only accepted physical quantity increases stock |
| INV-T-002 | Committed issue | Correct stock decreases; movement persists |
| INV-T-003 | Sales reservation | ATP decreases; on-hand unchanged |
| INV-T-004 | Reservation release | Remaining reservation released; ledger unchanged |
| INV-T-005 | Partial/multiple fulfillment | SalesLine linkage retained; over-fulfillment rejected |
| INV-T-006 | Non-serialized shortage | Default -2 and authorized override enforced |
| INV-T-007 | Serialized shortage | No eligible StockUnit = reject; no override |
| INV-T-008 | Duplicate serial | Active duplicate in namespace rejected by DB |
| INV-T-009 | Duplicate IMEI | One active IMEI cannot map to two units; dual IMEI on one unit allowed |
| INV-T-010 | Transfer dispatch | Source decreases; in-transit increases atomically |
| INV-T-011 | Partial transfer receipt | Destination increases only by accepted quantity |
| INV-T-012 | Transfer discrepancy | Unreceived amount remains in transit/discrepancy |
| INV-T-013 | Customer return | Pending inspection; excluded from ATP |
| INV-T-014 | Issue quarantined/defective | Ordinary issue rejected |
| INV-T-015 | Stocktake count | Observation alone does not change ledger |
| INV-T-016 | Approved adjustment | Separate immutable movement applies variance |
| INV-T-017 | Reversal | Linked compensating movement; original retained; bounded |
| INV-T-018 | Moving Weighted Average | Exact average for eligible receipt/issue |
| INV-T-019 | Specific Identification | Per-StockUnit cost preserved |
| INV-T-020 | True-up | Difference split retained inventory vs issued cost |
| INV-T-021 | Duplicate EventId | Same payload retry has one effect |
| INV-T-022 | Conflicting idempotency payload | Reject and audit |
| INV-T-023 | Concurrent issue | No overspend beyond approved threshold |
| INV-T-024 | Rollback | No partial ledger/projection/audit/outbox |
| INV-T-025 | Outbox retry | At-least-once delivery, idempotent consumer |
| INV-T-026 | Ledger/projection reconciliation | Projection equals committed ledger replay |
| INV-T-027 | Valuation reconciliation | Valuation evidence reconciles to stock value |
| INV-T-028 | Module grants | Non-Inventory principal cannot write Inventory data |
| INV-T-029 | Repair reservation | No automatic TTL |
| INV-T-030 | Sales reservation | Configured TTL releases remaining reservation |
| INV-T-031 | Internal location transfer | Warehouse total unchanged |
| INV-T-032 | Serialized return | Same StockUnit/IMEI and original cost lineage retained |
| INV-T-033 | Partial purchase receipt | Accepted/rejected/pending quantities separate |
| INV-T-034 | Receipt after shortage | Traceable receipt settles identified shortage |

## 3. Additional mandatory cases
| TestId | Scenario | Expected result |
|---|---|---|
| INV-T-035 | Warehouse hierarchy | Unknown/inactive location rejected; Inventory cannot create master |
| INV-T-036 | Location reconciliation | Location sums reconcile to warehouse |
| INV-T-037 | Variant separation | Variants under one Product never share stock |
| INV-T-038 | UOM version | New conversion prospective; old movement retains old version |
| INV-T-039 | UOM precision/overflow | Invalid fraction/overflow rejected; display rounding has no stock effect |
| INV-T-040 | Lot issue | Lot-tracked issue without valid Lot rejected |
| INV-T-041 | Dual IMEI | Two IMEIs may map to one unit; duplicate across units rejected |
| INV-T-042 | Serial namespace | Uniqueness follows approved manufacturer/type/model namespace |
| INV-T-043 | Reservation/issue race | Expiry racing issue cannot double-release/consume |
| INV-T-044 | Concurrent transfer receipts | Cumulative accepted receipt cannot exceed dispatch |
| INV-T-045 | Transfer conservation | Dispatch = receipt + in-transit + resolved discrepancy |
| INV-T-046 | SELLABLE authorization | Only authorized Inventory actor can set SELLABLE |
| INV-T-047 | Return missing cost | No current-price fallback; exception blocks auto-valuation |
| INV-T-048 | Stocktake concurrent movement | Freeze/cut-off/delta policy prevents stale overwrite |
| INV-T-049 | Partial reversal | Cumulative reversal cannot exceed original |
| INV-T-050 | Duplicate True-up | Same identity applies once; changed payload fails closed |
| INV-T-051 | True-up mixed quantities | Retained/issued split deterministic |
| INV-T-052 | Landed cost | Eligible direct costs capitalize; excluded costs do not |
| INV-T-053 | 30-day shortage | Warning only; no auto-expense/close |
| INV-T-054 | Threshold change | Unauthorized actor rejected; System Manager change audited |
| INV-T-055 | FE-09/R1 repair guard | No synthetic RepairCase/ServiceLine or invalid issue |
| INV-T-056 | Event version | Unknown type/version rejected without partial effects |
| INV-T-057 | Stale source/aggregate version | Rejected; no lost update |
| INV-T-058 | Deadlock retry | Stable lock order; same idempotency identity; one effect |
| INV-T-059 | Migration failure | Prior schema consistent; no partial migration |
| INV-T-060 | Backup/restore/replay | Ledger, projections, audit and outbox reconcile after restore |
| INV-T-061 | Publish timing | No event visible before commit |
| INV-T-062 | Actor spoofing | Client ActorReference cannot override server identity |
| INV-T-063 | Cross-module write | DB grants deny direct Inventory writes by other modules |
| INV-T-064 | Transfer financial neutrality | No revenue/expense; in-transit mapping only after Accounting approval |
| INV-T-065 | Opening balance | No opening stock without approved source/quantity/value evidence |
| INV-T-078 | Per-observation stocktake watermark | Each physical count captures its own committed ledger sequence under a consistent DB snapshot; reconciliation replays only later committed movements; pre-observation movements are excluded; concurrent count/movement race is serialized; rollback cannot expose an uncommitted sequence |

## 4. PostgreSQL suite
Run against actual target PostgreSQL: atomicity/rollback at each persistence boundary; isolation/serialization conflicts; row/advisory locks at Warehouse+Variant; unique serial/IMEI constraints; concurrent reservation/issue/transfer/TrueUp; deadlock/retry; transactional outbox and consumer replay; migration apply/failure; backup/restore; ledger/projection/valuation reconciliation; grants and denial of direct writes.

## 5. Per-test evidence
Record TestId/spec version, exact source/build/test SHA, PostgreSQL version, migration/schema version and fingerprint, fixtures/preconditions, Actor/role/permission, steps, expected/actual result, Pass/Fail/Blocked/NOT_RUN, query evidence for movement/projection/valuation/audit/outbox, logs/correlation IDs, timestamp and executor. Evidence on another SHA does not prove current SHA.

## 6. Acceptance
All 34 owner cases plus applicable additional cases pass. PostgreSQL atomicity/concurrency/replay/recovery, ledger/projection and valuation reconciliation, security grants and independent review pass on exact SHA. No Critical/High open defect. Release Authority separately grants operational release.

Current execution: NOT_RUN. Inventory runtime: NOT_IMPLEMENTED / NOT_VERIFIED. Operational release: NOT_GRANTED.
