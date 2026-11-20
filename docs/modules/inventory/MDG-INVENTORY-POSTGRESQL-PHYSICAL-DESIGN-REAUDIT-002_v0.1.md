# MDG Inventory PostgreSQL Physical Design v0.2 — Internal Re-Audit

| Field | Value |
|---|---|
| Audit ID | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-002 |
| Version | 0.1 |
| Date | 2026-11-20 |
| Audited document | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 v0.2 |
| Audited document blob SHA | a9ad8207032c8517f8dfd9d39c645d4d5150c775 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| References | Stage 05 v0.1; Logical ERD v0.2; prior audit MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-AUDIT-001 v0.1 |
| Review type | Internal adversarial design re-audit; not external independent attestation |
| Overall disposition | FOUR PRIOR BLOCKERS CLOSED AT DESIGN LEVEL; NOT READY FOR DDL/MIGRATION FREEZE |

## 1. Executive result

The v0.2 correction was re-read against the four original blocker findings and the related sequence/watermark/stocktake requirements. The four blockers now have explicit, mutually consistent design contracts:

- PD-AUD-01: durable command receipt and idempotency for movement and non-movement commands;
- PD-AUD-02: Inventory-local transactional audit intent, with AuditSecurity retaining exclusive canonical AuditEvent ownership and at-least-once acknowledged delivery;
- PD-AUD-03: database-enforced immutable committed facts through a restricted commit boundary, privilege denial and mutation-protection triggers;
- PD-AUD-04: one total lock hierarchy, with deterministic ordering and restart-from-beginning retry.

The associated sequence and stocktake ambiguity is also resolved at design level: one parent Warehouse × Variant sequence/lock scope across Sections and a single-scope READ COMMITTED observation algorithm. Multi-scope stocktakes use a vector of per-scope watermarks and do not claim a globally simultaneous snapshot.

**Result:** PD-AUD-01 through PD-AUD-06 are CLOSED IN DESIGN. They are not closed by runtime evidence. PD-AUD-07 and PD-AUD-08..12 remain open. The physical design as a whole is therefore NOT READY for DDL/migration freeze.

## 2. Re-audit method and limitations

Reviewed v0.2 in full and compared its replacement clauses with physical design v0.1, Stage 05 equations/custody/stocktake semantics, logical ERD v0.2 and the original audit. The review tested internal consistency of:
- command key scope, unique claim, transaction outcome and replay behavior;
- audit ownership, local atomicity, remote delivery, retry and deduplication;
- committed parent/child immutability and runtime privilege boundary;
- total lock order across workflow aggregates, stock scopes, serial identities, projections and outbox workers;
- scope creation, sequence allocation, rollback, committed prefix and stocktake observation.

This is an internal adversarial re-audit by the same assistant workflow that authored the correction. It is not an independent external reviewer, a code review, a PostgreSQL execution or proof of actual database behavior. No SQL or runtime code was executed.

## 3. Finding-by-finding result

### PD-AUD-01 — Durable idempotency

<badge> CLOSED IN DESIGN </badge>

**Verified in v0.2:** command_receipt is a durable unique command identity scoped by authenticated ActorId, stable OperationCode, OperationContractVersion and IdempotencyKey; the database uniqueness key and rank-0 lock ordering include the same dimensions. Operation contract version, canonicalization version and digest algorithm are recorded. Every mutating command is covered, including commands without StockMovement. Receipt and all effects are in one transaction; no committed PROCESSING receipt exists. Unique-key contention has explicit commit/rollback behavior. Same digest returns the stored stable response; different digest fails closed. Restore retains uniqueness.

**Adversarial check:** EventId, MovementId and source identity remain distinct. The response snapshot is restricted from storing credentials/tokens/recovery secrets. This closes the principal duplicate-effect and ambiguous-timeout gap at design level.

**Remaining implementation proof:** concurrent unique-index behavior, lost-response replay, non-movement command coverage, rollback and backup/restore.

### PD-AUD-02 — Audit durability and ownership

<badge> CLOSED IN DESIGN </badge>

**Verified in v0.2:** AuditSecurity remains sole canonical AuditEvent owner. Inventory commits its audit-intent envelope in the same local transaction as command receipt/domain effects/projection. Downstream delivery is at-least-once, uses stable EventId, has explicit PENDING/LEASED/RETRYABLE_FAILURE/DELIVERED_ACKNOWLEDGED/QUARANTINED states and never claims a distributed transaction. Remote outage after Inventory commit cannot erase movement or local audit intent.

**Adversarial check:** the selected pattern removes the impossible requirement for atomic commit across two independent module transactions while preserving atomic local evidence. The document distinguishes local durable intent from canonical AuditEvent acknowledgement.

**Remaining dependency:** AuditSecurity must publish ingestion schema/authentication, deduplication/acknowledgement, retention/privacy, quarantine and escalation contracts before DDL. Runtime retry/dedup/quarantine tests remain required.

### PD-AUD-03 — Committed-ledger immutability

<badge> CLOSED IN DESIGN </badge>

**Verified in v0.2:** committed facts are separated from mutable draft workflow; the runtime principal cannot directly mutate or insert committed facts; a narrowly scoped SECURITY DEFINER boundary performs validated commit; function owner is non-login; search_path/pg_temp, schema qualification, PUBLIC execute and schema CREATE protections are addressed; mutation-protection triggers cover parent and child facts; reversal is a new linked record.

**Adversarial check:** the revised model prevents the original application-only immutability assumption and explicitly denies direct runtime DML. It also covers committed child lines and scope entries rather than only the parent row.

**Remaining implementation proof:** inspect actual function source and ownership, default privileges, role membership, schema CREATE rights, trigger attachment and runtime-principal attack tests. Superuser/schema-owner break-glass remains outside runtime and must be controlled operationally.

### PD-AUD-04 — Global lock ordering

<badge> CLOSED IN DESIGN </badge>

**Verified in v0.2:** one strict rank order covers command receipt, workflow aggregates, stock scope, StockUnit, UnitIdentifier, other children, projections and outbox delivery metadata. All rows at a rank are sorted before advancing. Reservation and custody operations lock their workflow aggregate before stock scope. Multi-scope transfers sort Warehouse × Variant keys. Deadlock retry restarts the whole transaction with the same idempotency identity.

**Adversarial check:** no documented command requires taking a lower-ranked domain lock after a higher-ranked one. Outbox workers use short isolated transactions and do not hold delivery locks while acquiring domain locks.

**Remaining implementation proof:** inspect every implemented command path for conformance and run multi-session race/deadlock tests; design text alone cannot prove repository code follows the order.

### PD-AUD-05 — Canonical sequence scope

<badge> CLOSED IN DESIGN </badge>

**Verified in v0.2:** stock_scope_lock is both the Warehouse × Variant parent lock and transactional sequence allocator. It covers all Sections/status/tracking buckets. Scope rows are provisioned or created using the same insert-conflict-then-lock boundary. A movement gets one sequence per affected parent scope, not per line. Multiple scopes are locked in sorted order. Counter increment and scope entry roll back with the movement.

**Adversarial check:** this is consistent with v3.0.3 negative-stock threshold scope across Sections and avoids fine-grain counters that cannot prove a parent-scope prefix. A movement spanning multiple scopes gets a per-scope sequence vector and commits all entries atomically.

**Remaining implementation proof:** PostgreSQL multi-session proof that row-lock waiting, commit, rollback and first-row provisioning preserve the contiguous committed prefix.

### PD-AUD-06 — Stocktake observation algorithm

<badge> CLOSED IN DESIGN </badge>

**Verified in v0.2:** a short READ COMMITTED transaction claims idempotency, locks stocktake aggregate, locks parent stock scope, reads watermark in a fresh statement after lock acquisition, replays through that watermark while retaining the lock, inserts immutable observation and commits. Every movement writer uses the same scope lock through commit. Multi-scope counts explicitly retain a vector rather than claiming a global instant.

**Adversarial check:** writer-before-observation is included; writer-waiting-behind-observation is assigned W+1 or later; rollback contributes neither counter nor movement. The algorithm does not rely on MAX(id), timestamp ordering or PostgreSQL nextval().

**Remaining implementation proof:** test each interleaving on PostgreSQL 18 with multiple independent connections and verify replay/observation equality.

## 4. Residual findings — not closed by this re-audit

| Finding | Severity | Status | Required next action |
|---|---|---|---|
| PD-AUD-07 Projection rebuild/cutover concurrency | HIGH | OPEN | Define generation start watermark, shadow build, contiguous catch-up, writer coordination, short atomic cutover, active generation pointer and rollback/quarantine behavior |
| PD-AUD-08 StockPosition identity/null normalization | MEDIUM | OPEN | Choose canonical key representation and NULL/UNLOCATED/not-applicable normalization; prove no duplicate logical grain |
| PD-AUD-09 Draft/commit parent-child boundary | MEDIUM | OPEN | Specify draft line mutability, empty-line prohibitions by movement type, aggregate version checks and deferred completeness constraints |
| PD-AUD-10 Reservation expiry persistence | MEDIUM | OPEN | Define persisted ExpiresAtUtc, exact equality boundary, expiry policy/version and event identity without inventing Sales TTL |
| PD-AUD-11 Audit/outbox retry observability | MEDIUM | OPEN | Define operational alert thresholds, quarantine ownership, oldest-pending SLO and reconciliation cadence with AuditSecurity |
| PD-AUD-12 Naming/nullability normalization | LOW | OPEN | Publish final physical data dictionary and applicability matrix |

These residual findings do not reopen the four resolved blockers, but they prevent DDL/migration freeze.

## 5. Governing constraints rechecked

No conflict found with the current accepted design constraints:
- v3.0.3 remains sole accepted architecture baseline;
- ATP remains Sellable − EffectiveReserved;
- Custody is not Reservation and is excluded from ATP;
- FE-31 remains R2 / NOT ENABLED;
- Sales TTL remains OPEN/configurable; Repair has no automatic TTL;
- negative-stock feature remains OFF pending separate gate and D-258 limits are not broadened;
- quantity/valuation use exact numeric, no floating point;
- final IRR is integer Rial with one-time document-total AwayFromZero rounding, max absolute difference 1 Rial/document; no 100,000 IRR threshold or row-level rounding;
- 110801 remains sole Inventory GL control; no COA/Posting Rule is inferred;
- nine-sheet COA workbook remains abandoned and is not a dependency.

## 6. Final decision

**Four original blockers: CLOSED IN DESIGN.** Related sequence/watermark and stocktake design gaps PD-AUD-05/06: CLOSED IN DESIGN.

**Overall physical design: NOT READY FOR DDL/MIGRATION FREEZE** because PD-AUD-07 and PD-AUD-08..12 remain open and AuditSecurity/Catalog/platform contracts are outstanding.

No DDL, SQL, EF mapping, migration, grants, PostgreSQL tests, API/runtime implementation, Posting Rule, Posting Release, production migration or Go-Live was performed or authorized. O-35 remains open pending exact-SHA PostgreSQL watermark/race/replay/tie-out evidence.

**END — MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-002 v0.1**
