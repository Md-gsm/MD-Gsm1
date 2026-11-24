# O20-E Technical Blocker Closure Matrix — Exact-Head Evidence and Release Gates

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-BLOCKER-CLOSURE-MATRIX-001 |
| Version / Date | 1.2 / 2026-11-22 |
| Status | PRIOR EXACT-HEAD DISPOSABLE PROOFS PASS; LATEST CATALOG/LOCK-ORDER HARDENING CI PENDING; EXTERNAL GATES OPEN |
| Repository / Branch | Md-gsm/MD-Gsm1 / security/o20-o21-final-closure |
| Prior all-green baseline | `99f28b63a179a4a54b377bca2a9174a819883c28` |
| Latest code hardening test head | `58dd0d052fd2b004d9b36913008c38ce1bd0f8a4` (243/243 unit tests and checksum/spec gate passed; full PostgreSQL/Windows/CodeQL/preview workflows still pending at last retrieval) |
| PR | #92 Draft / unmerged |
| Governing readiness | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v2.9 |
| Related policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |
| Related decision packet | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.1 |

## 1. Exact-head CI evidence

All results below are for the same source/test SHA `99f28b63a179a4a54b377bca2a9174a819883c28`:

| Gate | Run | Result |
|---|---:|---|
| MD-Gsm Persons Module Gates | 45051449948 | SUCCESS — all 13/13 jobs |
| CodeQL | 45051449934 | SUCCESS |
| Windows Persons Preview Installer | 45051449938 | SUCCESS |
| Project Charter Owner Gate | 45051449965 | SUCCESS |

Primary workflow evidence:
- Release build: 0 warnings, 0 errors.
- .NET unit suite: 242/242 passed.
- Marked disposable PostgreSQL 18 base integration suite: 58/58 passed.
- Linux and Windows PostgreSQL migration runs: 8 applied on first run; 0 new migrations on repeat.
- SHA256SUMS verifier: 643 tracked files matched; spec/policy/lint gate passed.
- PII-free, commit-bound CI evidence artifact: artifact ID `14765768333`, SHA-256 `061167f054c6676c5335a4190ab10081f78ff5d5e76e37f8f4af117552ba982b`.

The PostgreSQL job also ran explicit class-filter proof steps, all PASS:
- O20-DB support identity and runtime LOGIN mapping;
- O20-F emergency access;
- O20-E OD-01/OD-02/OD-03, Warehouse-Branch immutable history and Legal Hold eligibility;
- assignment maker-checker / synthetic Product Owner policy;
- Branch/Warehouse/assignment cross-aggregate races and bounded retry;
- local AuditIntent/Outbox atomicity, idempotent durable-ACK fixture replay and payload-conflict quarantine;
- assignment idempotency and deterministic local lock acquisition;
- outbox-worker least-privilege object/column boundary;
- IdentityAccess live-session permission, revocation linearization and transaction-scoped authorization guard against actual disposable `identity.*` tables.

These are disposable PoCs, not production Organization, AuditSecurity, Product Owner binding, global Platform lock order or G1 acceptance.

## 2. Gate-by-gate disposition

| Gate | Exact status | Evidence / remaining closure condition |
|---|---|---|
| OD-01 code normalization | OWNER-ACCEPTED; bounded PoC PASS | .NET/PostgreSQL canonicalization and collision-safety test passed; cross-runtime/production compatibility still needs independent review. |
| OD-02 Warehouse identity/reassignment | OWNER-ACCEPTED; bounded PoC PASS | WarehouseId remains stable; inactive-only reassignment; exact assignment identity retained; concurrent revocation/reassignment PoCs passed. Production Organization implementation remains gated. |
| OD-03 terminal archive | OWNER-ACCEPTED; bounded PoC PASS | ARCHIVED terminal/no physical delete behavior passed in disposable PostgreSQL. Production lifecycle implementation remains gated. |
| OD-04 emergency access | OWNER-ACCEPTED; O20-F bounded PoC PASS | Two independent approvers, one-use/30-minute policy and fail-closed lifecycle tested. Production emergency service and operational ACK remain separately gated. |
| OD-05 retention baseline | OWNER-ACCEPTED; Legal Hold eligibility PoC PASS | Retain-by-default/no auto-purge; eligibility fails closed on missing schedule/authority/hold state; concurrent hold placement blocks eligibility. Duration and production legal workflow remain open. |
| PermissionCatalog | APPROVED MATRIX SOURCE-ALIGNED; immutable catalog hardening ADDED / exact-head full CI PENDING | Full approved ten-role matrix remains exact; supplemental `persons.sensitive.read` remains super_admin-only. Public role/permission read-only sets are now frozen and a test attempts mutation to prove consumers cannot add permissions/roles by casting IReadOnlySet. Foundation run on `58dd0d0` passed 243/243; full workflows pending. No `organization.*` grant is authorized. |
| IdentityAccess request-time contract | SOURCE-IMPLEMENTED; bounded tests PASS | Public Contracts v1.2 checks current server principal/session and exact permission. Module-owner acceptance remains open. |
| IdentityAccess transaction guard | SOURCE-IMPLEMENTED; real-schema disposable proof PASS | Actual Infrastructure adapter locks account/session/membership/role/grant/permission rows in caller transaction; PostgreSQL observed competing mutations blocked until commit. Cross-module use awaits accepted total lock order and module-owner acceptance. |
| Product Owner binding | FAIL-CLOSED ONLY; NOT IMPLEMENTED | Contract returns Unavailable because no protected binding exists. Synthetic fixture tests direct assignment semantics only; no bootstrap/enrollment/transfer/recovery/disable/restore lifecycle. |
| Organization permission namespace | OPEN / NOT ADDED | Exact role mapping and module-owner policy acceptance absent. No unapproved `organization.*` keys or grants have been invented. |
| Maker-checker | Bounded policy PoC PASS | Non-owner self-assignment denied; actor/subject cannot approve; independent approver accepted; Product Owner direct path tested only with synthetic binding. Production binding/command implementation absent. |
| Warehouse-Branch history | Bounded PostgreSQL PoC PASS | History versioned and append-only in fixture. Production Organization history table and access controls do not exist. |
| Idempotency / unknown commit | Bounded PostgreSQL PoC PASS | Concurrent same-key/same-payload and duplicate active-fact resolution tested; production Organization command receipt not implemented. |
| Local lock ordering | Prior homogeneous and three-class candidate PoCs PASS at prior exact-head baseline; new full local transaction-envelope PoC ADDED / CI PENDING | New method orders command receipt → approval row → Branch → Warehouse → Assignment → append-only history → local outbox in one disposable transaction with reversed caller input. It does not include the real IdentityAccess guard in the same transaction and does not approve the Platform-wide order. |
| AuditIntent/Outbox | Bounded PostgreSQL PoC PASS | Atomic local intent, competing worker claim, same EventId/hash replay, altered-payload quarantine tested with synthetic AuditSecurity endpoint. No production canonical AuditSecurity API/durable ACK/worker exists. |
| Outbox worker privilege | Bounded disposable PostgreSQL PoC PASS | Synthetic LOGIN has SET-only membership and column-scoped delivery-metadata update; no production sixth identity is approved or provisioned. |
| RowScope source inventory | Exact-head inventory PASS; no consumer | No Organization/Inventory/Reporting Branch/Warehouse query/report/export consumer exists. Positive/negative runtime RowScope tests are NOT APPLICABLE until a real consumer is authorized. ReportingReadOnly remains absent. |
| Platform identifier standard | OPEN | UUIDv7 is a recommendation, not a Platform decision. No standard is asserted. |
| Platform global lock order | OPEN | Platform and affected module owners have not accepted a total cross-module resource rank/linearization protocol. |
| Legal schedule / Legal Hold operations | OPEN | No jurisdictional record-class duration, legal authority identity, backup/replica/export treatment, hold lifecycle or production disposal control is accepted/implemented. Automatic deletion remains disabled. |
| Threat model | DRAFT / independent review pending | Threat-to-test map updated; residual risks and owner acceptance require independent Security review at exact SHA. |
| WG-0 / Organization migration | NOT RESERVED | No schema/migration/Organization runtime is permitted before the explicit G1 implementation gate. |
| Independent exact-SHA review | REQUESTED / NOT RECEIVED | Reviewer `mrdeljoe4` requested on PR #92; review list currently empty. |
| Organization G1 | NOT GRANTED | Requires all preceding owner/platform/security/legal gates, exact-SHA review and explicit Product Owner decision. |

## 3. External decisions that cannot be self-certified by CI

The following need the named accountable authority; a green workflow cannot substitute for them:

1. IdentityAccess module-owner acceptance of public Contracts v1.2 and the transaction guard.
2. Platform and affected module-owner acceptance of the global lock-order and revocation linearization protocol.
3. Product Owner + IdentityAccess + Platform + Security acceptance of Product Owner binding cardinality, installation scope, attended bootstrap, independent change/recovery and emergency disable.
4. Product Owner + Platform + DB + Security decision on a dedicated sixth outbox-worker identity, or explicit choice to keep production delivery disabled. The N.6 five-identity baseline remains unchanged until accepted.
5. Platform decision on UUIDv7 or another identifier standard, generator, clock and multi-instance behavior.
6. AuditSecurity selection/acceptance of canonical event schema, durable ACK, worker trust, retries/quarantine, external Object Lock Compliance anchor and KMS/HSM key custody.
7. Records/legal authority approval of jurisdiction-specific retention schedule, Legal Hold authority/scope/release, backup/replica/export treatment and lawful disposal.
8. WG-0 reservation and explicit Organization G1 implementation authorization.
9. Independent Security/Architecture review on one exact final SHA, with findings and retests.
10. Explicit Product Owner G1 decision after the above evidence.

## 4. Change and release safety

- Current `main` remains `2a52a8b0629769953ff9671a0cf220401614ce9f`; no direct main change is made by this matrix.
- PR #92 remains Draft/unmerged and is based on current main.
- PR #91 is the full revert proposal for prematurely merged PR #86; it is open and unmerged. If PR #91 merges first, PR #92 must be reconciled/rebased onto the resulting main and all CI rerun.
- No production schema, DDL, migration, Organization module, route, DB grant, permission seed, owner binding, service identity, credential, WORM/KMS resource or reporting role is created by this work.
- O20-E, O-20 and O-21 remain OPEN until the accountable external gates are complete.

**END — MDG-SECURITY-O20E-BLOCKER-CLOSURE-MATRIX-001 v1.0**


## 5. Latest bounded hardening additions — 2026-11-22

### 5.1 Permission catalog immutability

The public `PermissionCatalog.Catalog`, per-role permission sets and `RoleCatalog.All` were previously backed by mutable `HashSet<string>` instances exposed through `IReadOnlySet<string>`. A consumer could cast the interface to a mutable collection and attempt to widen the permission/role view in-process. The latest source uses .NET frozen collections (`FrozenSet` / `FrozenDictionary`) so the public views are immutable. The full golden test now also asserts `ICollection<T>.IsReadOnly` and verifies Add throws `NotSupportedException`, while the unapproved Organization permission remains unknown and denied.

At intermediate head `58dd0d052fd2b004d9b36913008c38ce1bd0f8a4`, Release build succeeded with 0 warnings / 0 errors and 243/243 .NET tests passed; spec/checksum gate confirmed 650 tracked files match `SHA256SUMS.txt`. That head predates the expanded total-order test method, and the full PostgreSQL/Windows/CodeQL/Preview jobs were still in progress at last retrieval. The final resulting head must pass every workflow again.

### 5.2 Expanded local transaction-order PoC

The latest lock-order test source adds a local-envelope candidate sequence across idempotency receipt, approval, Branch, Warehouse, Assignment, history and outbox. It is explicitly not a Platform global order and does not include the actual IdentityAccess guard in the same transaction. The new method awaits exact-head PostgreSQL CI.

### 5.3 Release interpretation

These additions are source hardening and disposable proof improvements. They do not change any owner decision or authorize production access. The total Platform order, actual Organization UnitOfWork consumer, Product Owner binding lifecycle, AuditSecurity durable ACK/worker, UUID generator decision, legal schedule/Legal Hold operation and independent review remain open.

**END — O20-E blocker closure matrix v1.1**


## 6. OD-01 pinned-Unicode boundary conformance test added — 2026-11-22

A new disposable PostgreSQL test method has been added to `O20OrganizationPolicyPostgresProofTests`:

`Od01_all_pinned_whitespace_control_and_format_scalars_match_postgresql`

It derives the full pinned scalar corpus from the same explicit .NET ranges used by `CanonicalizeDotNet`:
- Unicode White_Space;
- C0/C1 controls (Cc);
- the explicitly pinned format/bidi controls (Cf).

For every non-NUL forbidden scalar, the test verifies both .NET and PostgreSQL reject the same embedded scalar with the expected failure. For every White_Space scalar it verifies exterior trimming. For immediate valid neighbors of the forbidden set, it verifies both implementations accept the same string and return identical UTF-8 bytes. It also adds NFC equivalence vectors for Latin, Greek, Arabic and Hangul canonical sequences.

U+0000 is checked at the .NET boundary but deliberately not sent to PostgreSQL text: PostgreSQL text cannot represent a zero byte, so the application must reject it before constructing/sending the SQL text parameter. This boundary is explicit rather than hidden as a database equivalence claim.

This strengthens the previous representative OD-01 vectors into a full pinned-scalar corpus for the declared White_Space/Cc/Cf contract. It is still a disposable policy PoC; it does not create a production Organization code type/schema or decide a new Unicode version. Exact Unicode normalization compatibility across deployed runtime/ICU versions remains for independent review and deployment conformance.

**Exact-head CI status:** the new method has been added to the test source, and its checksum must be updated. CI for the resulting head is pending; no PASS is claimed until the PostgreSQL logs show this method passed.

**Disposition:** OD-01 source-level cross-runtime test coverage expanded to all pinned forbidden scalars/boundaries; exact-head CI pending. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E blocker closure matrix v1.2**
