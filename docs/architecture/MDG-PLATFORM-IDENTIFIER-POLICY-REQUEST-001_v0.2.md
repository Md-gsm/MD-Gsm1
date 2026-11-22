# Platform Identifier Policy — Organization ID Generation Decision Request

| Field | Value |
|---|---|
| Document ID | MDG-PLATFORM-IDENTIFIER-POLICY-REQUEST-001 |
| Version / Date | 0.2 / 2026-11-22 |
| Supersedes | v0.1 |
| Status | PLATFORM DECISION REQUEST — UUIDv7 IMPLEMENTATION CONFORMANCE PoC PASS; GLOBAL STANDARD / MULTI-INSTANCE POLICY NOT ACCEPTED |
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


## 7. Repository source crosswalk and bounded UUIDv7 evidence — 2026-11-22 (v0.2)

### 7.1 Existing source usage

The current source tree already generates these new internal identifiers with `Guid.CreateVersion7()`:
- `AccountId.New()` in IdentityAccess;
- `PartyId.New()` in Persons.

The implementation stores them as .NET `Guid` and PostgreSQL native `uuid`. The identity migration declares `identity.accounts.account_id uuid PRIMARY KEY` without a database-side UUID generator; the application owns ID creation and sends the value to persistence. The existing `AccountId.From(Guid)` boundary rejects `Guid.Empty` but does not itself enforce the UUID version; generation policy and persisted-ID validation are therefore separate concerns and must not be silently conflated.

Microsoft's .NET API documentation states that `Guid.CreateVersion7()` uses `UtcNow` as the Unix-epoch timestamp source and random data for rand_a/rand_b. Its timestamp overload accepts an explicit `DateTimeOffset` and rejects values before the Unix epoch. This is an RFC 9562 UUIDv7 generator, but its output must not be treated as a globally monotonic sequence or a commit-order watermark.

### 7.2 New unit conformance tests

Added `tests/MDGsm.Domain.Tests/IdentityAccess/UuidV7IdentifierConformanceTests.cs`, covering:
- AccountId and PartyId generated identifiers have non-empty UUIDv7 version nibble and RFC variant;
- the explicit timestamp overload encodes the exact Unix epoch milliseconds;
- 2,048 IDs produced for one fixed millisecond are unique despite sharing the timestamp prefix;
- pre-Unix-epoch input is rejected;
- 20,000 concurrent AccountId generations are unique and conform to UUIDv7.

These checks are algorithm/fixture conformance, not a collision probability proof at deployment-scale throughput.

### 7.3 Native PostgreSQL round-trip proof

Added `tests/MDGsm.Domain.Tests/Security/O20OrganizationUuidV7PostgresRoundTripProofTests.cs` and an explicit Linux disposable-PostgreSQL CI step. It writes 512 generated UUIDv7 values into an isolated schema column of native PostgreSQL type `uuid`, reads them back through Npgsql and compares exact Guid identity, version and variant.

The test uses only the marked disposable IdentityAccess database and removes its uniquely named schema in `finally`. It does not create an Organization table/migration, a production ID service or a production schema.

### 7.4 Exact limitations

This phase tests one .NET process and one disposable PostgreSQL instance. It does **not** test:
- separate machines/processes under a shared peak-load profile;
- time-service rollback/skew across nodes or restore/reinstall;
- database index/write amplification under expected production volume;
- multi-version deployment, generator rotation or a future non-.NET client;
- a Platform-approved global identifier abstraction or rule for every future module.

The existing .NET generator deliberately includes random fields and timestamp bits; tests must not demand strictly increasing Guid order. A business sequence, audit order, event order, legal timestamp or commit watermark must remain a separately persisted authoritative value.

**Recommendation to Platform:** standardize RFC 9562 UUIDv7 for new application-generated opaque internal surrogate IDs, using the supported .NET `Guid.CreateVersion7()` implementation for the current Windows/.NET deployment, while retaining native PostgreSQL `uuid`. This is a recommendation based on current code consistency and bounded tests; it is **not** a Platform decision or authorization. Platform must explicitly accept or reject it and define multi-instance/clock/version compatibility.

**Disposition:** current AccountId/PartyId UUIDv7 conformance and native PostgreSQL round-trip receive bounded PoC coverage; exact-head CI pending. Platform-wide standard, distributed collision/clock evidence and generator ownership remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — Platform Identifier Policy Request v0.2**
