# MDG Inventory Cross-Module Acceptance Test Specification
## Version 1.1 — Owner Follow-Up Acceptance and Cutover Tests

| Field | Value |
|---|---|
| Document ID | MDG-INVENTORY-CROSS-MODULE-TEST-SPEC-001 |
| Version / Date | 1.1 / 2026-11-20 |
| Status | CONTROLLED TEST SPECIFICATION — ALL TESTS NOT RUN |
| Authority | Owner answers 2026-11-20; OWN-INV-019 v1.0; O-64-SCOPE; D-15-CUTOVER-GUARD-01; WATERMARK-WH-01; RES-TTL-SALES-01 |
| Target | PostgreSQL 18 for persistence/concurrency/security |
| Runtime authorization | NOT GRANTED |

All cases begin NOT_RUN. PASS requires actual run, deterministic fixture, observed result, logs/query snapshots and exact commit SHA. Concurrency cases require independent PostgreSQL sessions; security cases require six independent credentials. Structural CI is not PostgreSQL proof. Failed cases remain in evidence. This registry grants no SQL/DDL/EF/migration/GRANT/runtime/Posting/Go-Live authorization.

## 1. Catalog and identifiers

| ID | Scenario | Required assertion | Status |
|---|---|---|---|
| CAT-01 | Change conversion after committed movement | Old movement retains factor/version/Base-UOM amount; new movement uses only new effective version. | NOT_RUN |
| CAT-02 | Fractional count UOM | Reject when precision=0 or FractionalQuantityAllowed=false; no rounding. | NOT_RUN |
| CAT-03 | Fractional decimal UOM | Accept only within declared precision/capacity. | NOT_RUN |
| CAT-04 | Inexact/overflow conversion | Reject atomically; no silent truncation/rounding. | NOT_RUN |
| CAT-05 | Same Serial in same Manufacturer+DeviceType namespace | Second active claim rejected. | NOT_RUN |
| CAT-06 | Same Serial under different Manufacturer/DeviceType | Distinct namespace only when both Catalog references are valid. | NOT_RUN |
| CAT-07 | Unicode/whitespace/control/separator normalization | NFC+trim; control chars rejected; separators preserved; policy version stable. | NOT_RUN |
| CAT-08 | Manufacturer uppercase rule | Case fold only when explicitly enabled. | NOT_RUN |
| CAT-09 | IMEI length/ASCII/Luhn | Invalid rejected; valid 15-digit ASCII accepted. | NOT_RUN |
| CAT-10 | IMEI uniqueness/dual-SIM | Global active uniqueness; maximum two active IMEIs per StockUnit. | NOT_RUN |
| CAT-11 | Serial/IMEI correction | Old ID inactive history; reason, Actor, authorization and independent approval preserved. | NOT_RUN |
| CAT-12 | Contract version/hash change | Unsupported hash/version rejected; old movement snapshot unchanged. | NOT_RUN |

## 2. Audit Intent, Outbox and AuditSecurity

| ID | Scenario | Required assertion | Status |
|---|---|---|---|
| AUD-01 | Successful movement | Movement/lines, projection, Idempotency, local AuditIntent and Outbox commit atomically. | NOT_RUN |
| AUD-02 | Local AuditIntent/Outbox write failure | Entire transaction rolls back, including movement/projection/counter/receipt. | NOT_RUN |
| AUD-03 | AuditSecurity outage after local commit | Local movement/intent/outbox durable; retry resumes without audit loss. | NOT_RUN |
| AUD-04 | AuditSecurity commits but ACK is lost | Same EventId/hash retry returns durable ACK; one canonical AuditEvent. | NOT_RUN |
| AUD-05 | Repeated/concurrent same EventId+Hash | Deduplicated receipt; no duplicate canonical event. | NOT_RUN |
| AUD-06 | Same EventId with different hash | Integrity conflict; original preserved; alert/incident evidence. | NOT_RUN |
| AUD-07 | HTTP success before durable commit | Not accepted as ACK; Outbox not delivered. | NOT_RUN |
| AUD-08 | Worker delivery of user event | Initiating Actor preserved; ExecutorServiceIdentity separately recorded. | NOT_RUN |
| AUD-09 | Transient failure enters REQUIRES_ATTENTION | Attention raised, retries continue with same EventId/hash. | NOT_RUN |
| AUD-10 | Integrity/non-retryable failure | QUARANTINED preserves envelope/history; unsafe automatic delivery stops. | NOT_RUN |
| AUD-11 | Authorized re-drive | Permission/reason/incident required; same EventId/hash and no duplicate effect. | NOT_RUN |
| AUD-12 | Retry/backoff/alerts | Exponential+jitter; 15m cap, 15m warning, 60m critical configurable/evidenced. | NOT_RUN |
| AUD-13 | Retention/Legal Hold/sensitive export | Holds/retention honored; secrets absent; sensitive access separately audited. | NOT_RUN |
| AUD-14 | Restore Outbox after partial delivery | Durable ACK recognized; same identity retried safely; no loss/duplicate. | NOT_RUN |

## 3. Platform role privilege tests

| ID | Scenario | Required assertion | Status |
|---|---|---|---|
| PLT-01 | Inspect six role attributes/membership | Schema Owner NOLOGIN; Runtime/Worker cannot SET ROLE to Owner/Migration. | NOT_RUN |
| PLT-02 | Runtime DDL/CREATE ROLE | All forbidden DDL denied. | NOT_RUN |
| PLT-03 | Runtime direct ledger/Journal/AuditEvent mutation | UPDATE/DELETE/TRUNCATE denied. | NOT_RUN |
| PLT-04 | Runtime cross-module private-table access | Denied; official contract path works. | NOT_RUN |
| PLT-05 | Worker attempts Movement/Journal mutation | Denied; only narrow queue functions work. | NOT_RUN |
| PLT-06 | Worker marks delivery without matching ACK | Denied; EventId/hash must match durable ACK. | NOT_RUN |
| PLT-07 | Observer DML/side-effect function | Denied; approved views only. | NOT_RUN |
| PLT-08 | Migration Executor scope | No default superuser; cannot serve Runtime; 0009 unavailable. | NOT_RUN |
| PLT-09 | Backup/Recovery credential | Isolated audited runbook only; unavailable to app host. | NOT_RUN |
| PLT-10 | SECURITY DEFINER review | NOLOGIN owner, safe search_path, qualified names, no unsafe dynamic SQL, PUBLIC EXECUTE revoked. | NOT_RUN |
| PLT-11 | Credential rotation/revocation | Rotation/revocation without privileged runtime reuse or log exposure. | NOT_RUN |

## 4. Idempotency, lock and WarehouseId watermark

| ID | Scenario | Required assertion | Status |
|---|---|---|---|
| INV-01 | Same key/payload concurrently | One business effect; loser receives original durable result. | NOT_RUN |
| INV-02 | Same key/different fingerprint | Conflict; no second effect; first receipt immutable. | NOT_RUN |
| INV-03 | Lost response/retry after restart | Original committed response replayed; no duplicate. | NOT_RUN |
| INV-04 | Lock order across every command | All paths use total rank order and deterministic sorting. | NOT_RUN |
| INV-05 | Different Locations/Lots, same Variant/Warehouse issue concurrently | Shared parent lock serializes; aggregate shortage respects D-258. | NOT_RUN |
| INV-06 | Same Variant in different Warehouses | Independent scopes; one Warehouse cannot consume another's limit. | NOT_RUN |
| INV-07 | Negative eligibility matrix | Integer nonserial -2 only behind gate; decimal and tracked/serial zero. | NOT_RUN |
| INV-08 | SYSTEM_MANAGER-only or Emergency Session threshold change | Denied; no policy change; audit evidence. | NOT_RUN |
| INV-09 | GENERAL_MANAGER changes limit within an active owner-approved assignment | Server accepts only the exact assigned WarehouseId×VariantId pair; before/after/reason/Actor/assignment version/approval reference/time audited. | NOT_RUN |
| INV-10 | UI grants but server context lacks permission | Denied. | NOT_RUN |
| INV-11 | Concurrent movements, one Warehouse, different Variants | Contiguous increasing Warehouse counter; no duplicate/gap. | NOT_RUN |
| INV-12 | Increment then rollback | No committed sequence advancement survives. | NOT_RUN |
| INV-13 | Multi-line/multi-Variant movement | One Warehouse sequence per movement; every affected projection handles it. | NOT_RUN |
| INV-14 | Projection reads unrelated Variant event | Verified no-op advances cursor; no false gap. | NOT_RUN |
| INV-15 | Future multi-Warehouse operation | Counter locks ascending WarehouseId; both sides atomic; FE-31 still R2 disabled. | NOT_RUN |
| INV-16 | Deadlock/serialization failure | Whole tx rolls back; retry same key; no partial side effects. | NOT_RUN |
| INV-17 | First scope/counter provisioning race | No duplicate scope/counter or lost sequence. | NOT_RUN |
| INV-18 | Stocktake vs concurrent movement | Observation watermark under counter lock; movement wholly before/after cutoff; replay exact. | NOT_RUN |

## 5. Reservation

| ID | Scenario | Required assertion | Status |
|---|---|---|---|
| RSV-01 | Confirmed Sales reservation | ExpiresAtUtc=server creation+24h default; policy/version snapshotted. | NOT_RUN |
| RSV-02 | Change TTL configuration | New reservations only; old expiry unchanged. | NOT_RUN |
| RSV-03 | Repair reservation | ExpiryMode=NONE; no automatic TTL. | NOT_RUN |
| RSV-04 | Draft/Backorder | No reservation. | NOT_RUN |
| RSV-05 | Issue just before expiry | Valid if lock decision is before expiry. | NOT_RUN |
| RSV-06 | Issue exactly at expiry | Expired; issue rejected. | NOT_RUN |
| RSV-07 | Expired while Worker offline | EffectiveReserved excludes it before cleanup. | NOT_RUN |
| RSV-08 | Request waits across expiry | Re-evaluate time after lock; expiry wins. | NOT_RUN |
| RSV-09 | Partial consume then expire | Consumed amount remains; remainder released once. | NOT_RUN |
| RSV-10 | Duplicate consume/release/expire | Idempotent; no duplicate quantity/status. | NOT_RUN |
| RSV-11 | Competing reservations for final capacity | No over-reservation; loser conflicts after revalidation. | NOT_RUN |
| RSV-12 | Cancel after partial consume | Only remainder cancelled; committed movement unchanged. | NOT_RUN |

## 6. Projection, valuation and Finance

| ID | Scenario | Required assertion | Status |
|---|---|---|---|
| VAL-01 | Same entity/Variant in two Warehouses | Same WAC PoolId/balance under VAL-POOL-01. | NOT_RUN |
| VAL-02 | Two Variants in one entity | Distinct pools; no cross-Variant averaging. | NOT_RUN |
| VAL-03 | Two legal entities (future only) | Separate pools; does not enable multi-company R1. | NOT_RUN |
| VAL-04 | Receipt with landed cost | Only eligible directly attributable cost enters WAC. | NOT_RUN |
| VAL-05 | Concurrent receipts/issues from different Warehouses, same pool | Pool lock serializes WAC updates. | NOT_RUN |
| VAL-06 | Serial/IMEI issue/return | Specific Identification follows StockUnit lineage. | NOT_RUN |
| VAL-07 | 2 units at 100 provisional, 110 actual; one remains/one consumed | +10 IRR remaining inventory, +10 issued cost lineage, total 20; no prior rewrite. | NOT_RUN |
| VAL-08 | True-up retry same key | One append-only adjustment and one Finance effect. | NOT_RUN |
| VAL-09 | Missing source/cost evidence | Fail closed; no guessed price/zero cost/Journal. | NOT_RUN |
| VAL-10 | Non-integral True-up split | Integer-IRR total conserved by deterministic Finance-accepted allocation. | NOT_RUN |
| VAL-11 | Finance consumes valuation event | Version/hash/lineage acknowledged; unsupported version rejected. | NOT_RUN |
| VAL-12 | Inventory attempts account/Journal/PostingRule selection | Denied; Inventory emits evidence only. | NOT_RUN |
| VAL-13 | FE-08 scope compatibility | BusinessEntity×Variant accepted and FE-08 updated before unblocking. | NOT_RUN |
| VAL-14 | Shadow build with concurrent Warehouse writers | Contiguous Warehouse prefix; no missing/duplicate source. | NOT_RUN |
| VAL-15 | Tail exceeds either independent cutover budget | Switch aborts; old pointer remains; locks released; background Catch-up resumes. | NOT_RUN |
| VAL-16 | Projection switch/rollback | Atomic only when both budgets pass, all pre-cutover movements are applied, quantity difference is zero and each Warehouse watermark difference is zero; rollback only to reconciled generation. | NOT_RUN |

## 7. Shared contracts

| ID | Scenario | Required assertion | Status |
|---|---|---|---|
| X-01 | Unsupported ContractVersion | Explicit fail-closed error. | NOT_RUN |
| X-02 | Timeout/unknown commit | Same IdempotencyKey resolves prior result or retries after rollback exactly once. | NOT_RUN |
| X-03 | Client forges Actor/time | Authenticated Actor/server time prevail. | NOT_RUN |
| X-04 | Foreign private-table access | Denied; official application contract works. | NOT_RUN |
| X-05 | Breaking change without MAJOR/consumer approval | Release validation blocks. | NOT_RUN |
| X-06 | Same EventId/altered payload | Integrity violation; first event remains immutable. | NOT_RUN |
| X-07 | Replay after consumer restart | Same event identity produces one effect. | NOT_RUN |
| X-08 | Finance/Inventory contract version mismatch | No inferred account/Journal; block/retry/quarantine per policy. | NOT_RUN |

## 8. D-15 and O-35

D-15 policy decision is CLOSED/APPROVED. Numeric values remain NOT_SET for the two independent parameters MAX_CUTOVER_TAIL_COUNT and MAX_CUTOVER_LOCK_DURATION. Derive both separately from representative dataset/movement volume, Warehouse count, target PostgreSQL 18 configuration and target server hardware, concurrent writer rate, shadow-build throughput, catch-up rate, lock wait, Tail processing, reconciliation and rollback. No guessed values are permitted. Operational Cutover Gate remains unsatisfied until both values are measured, approved, versioned and tested.

O-35 remains OPEN until exact-SHA PostgreSQL evidence proves contiguous per-Warehouse sequence, rollback no-gap, multi-Warehouse atomic locks, per-Variant projections/no-op cursor, stocktake cutoff/replay, movement/valuation conservation, atomic local Outbox/AuditIntent/Idempotency, restore/replay/tie-out and independent exact-SHA acceptance.

## 9. Evidence record

Record Test ID/version, repo/build SHA, PostgreSQL version/config, fixture, credential role, session count, command/input, expected/actual result, SQLSTATE/error, logs, before/after movement/projection/counter/outbox/valuation/idempotency snapshots, PASS/FAIL/BLOCKED, reviewer, UTC time and evidence SHA-256. Preserve failures and link corrected reruns.

## 10. Owner follow-up acceptance tests — all NOT_RUN

### 10.1 O-64-SCOPE: scoped negative-stock authority

| ID | Scenario | Required assertion | Status |
|---|---|---|---|
| SCOPE-01 | GENERAL_MANAGER changes limit within assigned Warehouse×Variant | Accepted only for the exact active owner-approved pair; full before/after/Actor/approval/Audit lineage. | NOT_RUN |
| SCOPE-02 | GENERAL_MANAGER changes limit outside assigned pair | Denied server-side; no mutation or side effect. | NOT_RUN |
| SCOPE-03 | Create/expand/reassign scope without explicit owner approval | Denied; no assignment created or modified. | NOT_RUN |
| SCOPE-04 | Owner-approved scope creation/change | New version/interval and immutable Audit evidence recorded; previous assignment history preserved. | NOT_RUN |
| SCOPE-05 | SYSTEM_MANAGER alone | Denied regardless of UI or submitted role. | NOT_RUN |
| SCOPE-06 | Emergency Session alone | Denied; emergency elevation cannot grant this permission. | NOT_RUN |
| SCOPE-07 | Serial/IMEI negative movement with any role/assignment | Always denied; no override path. | NOT_RUN |

### 10.2 D-15: dual cutover budgets and mandatory consistency

| ID | Scenario | Required assertion | Status |
|---|---|---|---|
| CUT-01 | Tail count within MAX_CUTOVER_TAIL_COUNT and lock duration within MAX_CUTOVER_LOCK_DURATION | Budget conditions alone permit further gate evaluation; no bypass of consistency guards. | NOT_RUN |
| CUT-02 | Tail count exceeds MAX_CUTOVER_TAIL_COUNT | Abort/rollback; previous Pointer unchanged; release locks; Catch-up continues. | NOT_RUN |
| CUT-03 | Measured lock duration reaches/exceeds MAX_CUTOVER_LOCK_DURATION | Abort/rollback before Pointer commit; previous Pointer unchanged. | NOT_RUN |
| CUT-04 | One budget passes and the other fails | Pointer Swap prohibited; both independent limits are mandatory. | NOT_RUN |
| CUT-05 | Unprocessed movement exists before cutover point | Pointer Swap prohibited; every pre-cutover movement must be applied. | NOT_RUN |
| CUT-06 | Candidate quantity reconciliation | Exact zero quantity difference at every canonical stock grain versus Ledger replay. | NOT_RUN |
| CUT-07 | Candidate Warehouse watermark reconciliation | Exact zero watermark difference for every affected Warehouse. | NOT_RUN |
| CUT-08 | Quantity or watermark difference is non-zero | Pointer Swap prohibited; prior Pointer retained; Catch-up continues. | NOT_RUN |
| CUT-09 | Multi-Warehouse cutover attempt | Tail count is measured across affected Warehouses; counter locks sorted by WarehouseId; per-Warehouse watermark vector exactly reconciled. | NOT_RUN |
| CUT-10 | Budget calibration and approval | Both values derived from representative load/rollback tests on target PostgreSQL 18/hardware, versioned, approved and evidence-hashed; guessed values rejected. | NOT_RUN |

All tests in v1.0 and v1.1 remain NOT_RUN until executed with the required PostgreSQL credentials, concurrent sessions, target hardware and exact-SHA evidence. Structural CI or document review does not convert any test to PASS.

**END — MDG-INVENTORY-CROSS-MODULE-TEST-SPEC-001 v1.1**
