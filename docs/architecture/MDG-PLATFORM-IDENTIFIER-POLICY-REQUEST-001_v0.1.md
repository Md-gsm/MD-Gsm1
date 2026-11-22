# Platform Identifier Policy — Organization ID Generation Decision Request

| Field | Value |
|---|---|
| Document ID | MDG-PLATFORM-IDENTIFIER-POLICY-REQUEST-001 |
| Version / Date | 0.1 / 2026-11-22 |
| Status | PLATFORM DECISION REQUEST — UUID STORAGE PROPOSED; GENERATION VERSION NOT ACCEPTED |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |

## 1. Existing facts

Organization physical design proposes PostgreSQL native `uuid`. IdentityAccess AccountId also uses PostgreSQL `uuid`. Master explicitly defines PartyId as UUIDv7, but this does not make UUIDv7 a global rule for every module ID. No Platform policy currently establishes Organization ID generation version, implementation, clock behavior or upgrade semantics.

An ID is opaque identity, not authorization proof, a commit watermark or authoritative chronology.

## 2. Invariants regardless of selected UUID version

1. Generate IDs only in trusted server-side code; never trust client-supplied aggregate, actor or event IDs as authority.
2. Use opaque IDs with no embedded business meaning.
3. Persist aggregate/event IDs in the same local transaction as their corresponding state.
4. Use idempotency receipts to recover unknown-commit results; same-key retry must return original IDs and not create duplicate aggregates/events.
5. Keep server timestamps and transactional commit/watermark fields separate from identifier ordering.
6. Generator must be thread-safe, concurrency-tested and available on all supported deployment instances.
7. Clock rollback/skew must not cause collisions or false chronological assumptions.
8. .NET/API/PostgreSQL serialization must round-trip exactly.
9. Never encode secrets, account data, BranchCode or WarehouseCode in IDs.

## 3. Alternatives requiring Platform decision

| Option | Benefits | Open concerns |
|---|---|---|
| UUIDv7 | Time-ordered locality candidate; consistent with PartyId precedent | Clock monotonicity/rollback, same-millisecond entropy, multi-instance generation and collision evidence |
| UUIDv4 | Strong random opaque IDs and broad compatibility | Less index locality; provides no ordering signal |
| Platform generator abstraction | Stable contract and allows future implementation change | Platform-owned implementation, configuration, compatibility/versioning |

No option is selected here. PartyId UUIDv7 is precedent only.

## 4. Candidate application/storage boundary

- PostgreSQL native `uuid` unless Platform publishes another approved representation.
- Strong types per aggregate: BranchId, WarehouseId, AssignmentId and OrganizationAuditEventId, even when storage type is UUID.
- Server-side Platform abstraction is sole generation authority.
- ID order is never substituted for commit sequence.
- Unknown-commit retry first resolves the idempotency receipt and returns stored IDs; it does not mint duplicates.
- Record generator/version in controlled Platform configuration and evidence, not as sensitive data on the row.

## 5. Required acceptance tests

Parallel generation at expected maximum concurrency; multiple application instances; same-millisecond volume; clock rollback/forward jump; UUID .NET↔PostgreSQL round-trip; mixed-version deployment; idempotent unknown-commit recovery; client ID forgery; and index/load evidence on PostgreSQL 18.

## 6. Platform decision required

Platform must select UUID version/standard, implementation owner, supported runtime, clock and multi-instance behavior, database representation, version/upgrade policy, and test load/acceptance thresholds.

**Disposition:** native UUID storage remains a candidate; generator policy and Platform decision OPEN. No UUIDv7 global rule is inferred from PartyId. O20-E OPEN; G1 NOT GRANTED.

**END — MDG-PLATFORM-IDENTIFIER-POLICY-REQUEST-001 v0.1**
