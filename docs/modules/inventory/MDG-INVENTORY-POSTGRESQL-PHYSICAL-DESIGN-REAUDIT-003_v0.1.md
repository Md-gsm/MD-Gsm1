# MDG Inventory PostgreSQL Physical Design v0.3 — Residual Blocker Re-Audit

| Field | Value |
|---|---|
| Audit ID | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-003 |
| Version | 0.1 |
| Date | 2026-11-20 |
| Audited document | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 v0.3 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| References | Physical Design v0.1/v0.2; Stage 05 v0.1; Logical ERD v0.2; Re-audit 002 v0.1 |
| Review type | Internal adversarial design re-audit; not independent external attestation |
| Result | PD-AUD-01..12 CLOSED IN DESIGN; NOT IMPLEMENTATION/RELEASE APPROVAL |

## 1. Executive result

The v0.3 supplement was reviewed against all six residual findings from re-audit 002 and checked for consistency with the six design closures in v0.2 and governing Inventory semantics.

- PD-AUD-07 projection generation/rebuild/catch-up/cutover: CLOSED IN DESIGN.
- PD-AUD-08 StockPosition identity and NULL normalization: CLOSED IN DESIGN.
- PD-AUD-09 draft/commit parent-child boundary: CLOSED IN DESIGN.
- PD-AUD-10 reservation expiry persistence and equality boundary: CLOSED IN DESIGN.
- PD-AUD-11 outbox operations, alerting, ownership and reconciliation: CLOSED IN DESIGN.
- PD-AUD-12 naming/nullability data dictionary: CLOSED IN DESIGN.

Together with PD-AUD-01..06 already closed in design by v0.2, all PD-AUD-01..12 are CLOSED IN DESIGN. No residual finding in this audit series remains OPEN.

This does not mean PostgreSQL design is implemented or proven. It is not authority to freeze DDL, create migrations, grant permissions, activate Inventory, close O-35 or release Posting.

## 2. Method and limitation

Reviewed v0.3 and compared each closure to v0.2 idempotency/audit/immutability/lock/sequence/stocktake contracts; Stage 05 quantity equations, custody and stocktake; logical ERD v0.2 ownership and relationships; and v3.0.3 authority/release boundaries.

This is an internal adversarial pass by the same assistant workflow that authored the supplement. It is not independent reviewer attestation, code review, PostgreSQL execution, benchmark, privilege test or proof of runtime behavior. No SQL, DDL, application code or database was executed.

Final integration consistency checks also verified that OperationContractVersion appears consistently in the command-receipt uniqueness key, idempotency scope and rank-0 lock ordering; that projection cutover performs pre-catch-up outside the stock lock and aborts/retries when the remaining locked tail exceeds its approved budget; and that StockPosition grain/nullability remains aligned with Stage 05 and logical ERD v0.2.

## 3. Finding-by-finding result

### PD-AUD-07 — Projection rebuild and cutover

**CLOSED IN DESIGN.** The design separates authoritative movement facts from derived generations; captures per-scope W0 under the canonical lock; builds a shadow generation through W0; catches up only contiguous sequence entries; prevents readers from seeing BUILDING generations; and performs short locked cutover by atomically changing generation state and active pointer. Multi-scope projections use a vector. Failed builds preserve the active pointer; rollback requires prior generation caught up and reconciled.

Adversarial checks: no long-lived stock lock during bulk build; no dual-write to every shadow generation; no timestamps/MAX(id)/UUID as watermark; no pointer switch with unapplied entries; no rollback to stale generation.

Remaining proof: PostgreSQL concurrent writer/build/cutover, reader consistency, rollback and restore tests.

### PD-AUD-08 — StockPosition identity and NULL normalization

**CLOSED IN DESIGN.** Full Stage 05 grain is specified. Optional dimensions have explicit applicability states. NULL Section semantics are limited to UNLOCATED. Lot/StockUnit nullability is constrained by tracking state. PostgreSQL 18 NULLS NOT DISTINCT or equivalent collision-proof database uniqueness is required. Grain identity is immutable.

Adversarial checks: unknown and not-applicable cannot silently share one NULL meaning; zero UUID/magic sentinels prohibited; serialized identity is not collapsed into bulk quantity; Warehouse × Variant parent lock unchanged.

Remaining proof: actual constraints and concurrent duplicate-key tests.

### PD-AUD-09 — Draft/commit boundary

**CLOSED IN DESIGN.** Draft header/lines are workflow-only; AggregateVersion provides optimistic concurrency; parent lock serializes line mutation and commit; physical movements require at least one valid line; non-stock events cannot fabricate empty movement headers; immutable header/lines/scope entries are written atomically through the restricted v0.2 commit boundary.

Adversarial checks: drafts never enter OnHand/ATP/valuation; no partially committed header/child; stale clients fail closed; database enforcement, not application convention, owns completeness.

Remaining proof: deferred constraints, privilege and concurrent commit tests.

### PD-AUD-10 — Reservation expiry

**CLOSED IN DESIGN.** Expiry mode/policy/version and ExpiresAtUtc are persisted; finite expiry requires timestamp and NONE requires NULL; comparisons use trusted UTC after aggregate lock; equality is expired; expiry is explicit and idempotent. Sales TTL remains OPEN/configurable and no duration is invented. Repair reservation has no automatic TTL.

Adversarial checks: lock-wait cannot use stale pre-lock time; issue and expiry serialize; repeated expiry cannot release twice; no read-time implicit release.

Remaining proof: just-before/equal/after and lock-wait PostgreSQL race tests.

### PD-AUD-11 — Outbox observability and operations

**CLOSED IN DESIGN.** Ownership is explicit between Inventory local outbox and AuditSecurity canonical AuditEvent. States, leases, durable attempt history, exponential backoff, quarantine, authorized re-drive, metrics, thresholds and daily reconciliation are specified. Quarantine is immediately critical; evidence is not deleted. Defaults are configuration-driven, not embedded in code.

Adversarial checks: ambiguous remote commit retries same EventId; no silent-drop state; re-drive cannot create second business effect; reconciliation identifies missing/duplicate acknowledgements; restore resumes pending delivery.

Remaining proof: worker crash, outage, alert, reconciliation and restore tests. AuditSecurity must accept downstream contract before DDL.

### PD-AUD-12 — Naming and nullability

**CLOSED IN DESIGN.** snake_case and semantic suffix rules are defined; ambiguous generic names prohibited; common requiredness and per-entity applicability tabulated; NULL must have one documented meaning and legal lifecycle combination; magic sentinel values prohibited.

Adversarial checks: nullable FK is not overloaded for unknown/not-applicable/pending/deleted; server timestamps are authoritative; ActorId is typed and non-null for autonomous workers; owning module contracts remain source of truth for ID types and requiredness.

Remaining proof: schema lint, nullability/state constraints, sentinel scans and owner-contract compatibility.

## 4. Cross-document consistency

No design-level contradiction found with:
- ATP = Sellable − EffectiveReserved.
- Custody separate from Reservation and excluded from ATP.
- Atomic paired Section transfer; FE-31 remains R2/not enabled.
- Sales TTL OPEN/configurable; Repair has no automatic TTL.
- D-258 negative-stock limits and feature OFF.
- Warehouse × Variant parent lock/sequence across Sections.
- Per-observation stocktake watermark; multi-scope vector, not false global instant.
- Exact NUMERIC/no floats; integer Rial and one-time document-total AwayFromZero, maximum 1 Rial/document.
- 110801 sole Inventory GL control; no COA/Posting inference.
- v3.0.3 sole accepted baseline; abandoned nine-sheet COA workbook not a dependency.

## 5. Final finding matrix

| Finding | Severity | Result |
|---|---|---|
| PD-AUD-01 durable idempotency | HIGH | CLOSED IN DESIGN (v0.2) |
| PD-AUD-02 audit durability/ownership | HIGH | CLOSED IN DESIGN (v0.2; downstream contract pending) |
| PD-AUD-03 committed-ledger immutability | HIGH | CLOSED IN DESIGN (v0.2) |
| PD-AUD-04 global lock hierarchy | HIGH | CLOSED IN DESIGN (v0.2) |
| PD-AUD-05 canonical sequence scope | HIGH | CLOSED IN DESIGN (v0.2) |
| PD-AUD-06 stocktake watermark algorithm | HIGH | CLOSED IN DESIGN (v0.2) |
| PD-AUD-07 projection rebuild/cutover | HIGH | CLOSED IN DESIGN (v0.3) |
| PD-AUD-08 StockPosition identity/nulls | MEDIUM | CLOSED IN DESIGN (v0.3) |
| PD-AUD-09 Draft/Commit boundary | MEDIUM | CLOSED IN DESIGN (v0.3) |
| PD-AUD-10 reservation expiry | MEDIUM | CLOSED IN DESIGN (v0.3) |
| PD-AUD-11 outbox observability | MEDIUM | CLOSED IN DESIGN (v0.3) |
| PD-AUD-12 naming/nullability | LOW | CLOSED IN DESIGN (v0.3) |

## 6. Remaining gates — not residual design findings

1. AuditSecurity must publish/accept ingestion authentication, EventId deduplication/acknowledgement, retention/privacy and incident contract.
2. Catalog must publish quantity precision/base UOM/conversion, tracking and identifier normalization contracts.
3. Platform must publish UUID generation, database-role ownership, worker identity, configuration/secrets and alert routing.
4. PostgreSQL implementation must prove constraints, function privileges, watermark, projection concurrency, reservation expiry, outbox delivery, backup/restore and performance on exact SHA.
5. Independent reviewer must review exact document SHA and implementation evidence.
6. O-35 remains OPEN until exact-SHA PostgreSQL watermark/race/replay/tie-out evidence is accepted.

## 7. Final decision

**All twelve PD-AUD findings are CLOSED IN DESIGN.** No PD-AUD residual remains open in this design audit series. The physical design is ready for independent technical review, not DDL freeze or implementation release.

No SQL, DDL, EF mapping, migration, grants, API/runtime, PostgreSQL tests, O-35 closure, Posting Rule, Posting Release, production migration or Go-Live was performed or authorized. All PD-T-001..039 remain NOT RUN.

**END — MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-003 v0.1**
