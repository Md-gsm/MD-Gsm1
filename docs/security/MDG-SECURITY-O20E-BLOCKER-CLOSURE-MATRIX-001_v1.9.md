# O20-E Technical Blocker Closure Matrix — Exact-Head Evidence and Release Gates

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-BLOCKER-CLOSURE-MATRIX-001 |
| Version / Date | 1.9 / 2026-11-23 |
| Supersedes | v1.8 |
| Supersedes | v1.6 |
| Supersedes | v1.5 |
| Supersedes | v1.4 |
| Status | UUIDv7 PROOF / DI TEST PASS AT 014B559; WINDOWS MIGRATION GATE CANCELLED AND RETRY REQUIRED; RETENTION LEGAL CROSSWALK v0.6 ADDED; FINAL HEAD CI PENDING |
| Repository / Branch | Md-gsm/MD-Gsm1 / security/o20-o21-final-closure |
| Prior all-green baseline | `99f28b63a179a4a54b377bca2a9174a819883c28` |
| Exact source/test head inspected | `014b5594b0da020e9e6fee9b7a0c3eb03264eed6` (249/249 .NET tests; 62/62 base PostgreSQL tests; explicit UUIDv7 PostgreSQL round-trip PASS; Linux migrations 8/8 + idempotent 0; CodeQL, Windows Preview and Owner Gate SUCCESS; Windows migration job CANCELLED; CI evidence gate failed because Windows job was cancelled). Retention v0.6 and design/readiness successors are on the newer branch tip; full exact-head validation is pending. |
| PR | #92 Draft / unmerged |
| Governing readiness | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v3.5 |
| Related policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.6; MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.6 |
| Related decision packet | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.3 |

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
| Legal schedule / Legal Hold operations | OPEN — source crosswalk v0.4 added | Iran Commercial Code Article 13, AML Articles 142/149 and VAT Executive Regulation Article 103 have different scopes; none proves one universal Organization-history period. Operator/entity/merchant/small-merchant/VAT/AML classification, per-record mapping, Legal Hold authority/scope/release and backup/replica/export handling require counsel/records-owner approval. Automatic deletion remains disabled. |
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


## 7. Authorization guard multi-role completeness — source correction and new proof

The transaction guard was hardened after review found it read only the first matching row from a `FOR SHARE` result set. In a multi-role union, this does not establish that all grant paths remain locked until commit. The adapter now drains the entire result set and validates every row before returning Allow.

The updated disposable PostgreSQL proof:
- seeds two distinct roles and two `reports.read` grants for one principal;
- verifies both role-membership rows and both role-permission rows remain locked while the caller transaction is open;
- concurrently attempts session revoke, account disable/credential-version change, deletion of each membership, deletion of each grant and permission deactivation;
- performs synthetic Branch/Warehouse/assignment version changes and appends history/outbox rows in the same transaction as the actual IdentityAccess guard;
- commits and verifies all local facts durable, followed by a fresh permission denial after queued revocations complete.

These synthetic Organization rows are temporary test tables only. The proof is not a production Organization module/UnitOfWork, and the Platform-wide lock-order registry remains non-normative. Exact-head CI for this adapter/test addition is pending.

**Disposition:** multi-role lock-completeness source correction applied and expanded integrated disposable proof added; latest CI pending; IdentityAccess owner / Platform acceptance and all production gates remain OPEN.

**END — O20-E blocker closure matrix v1.3**


## 8. Exact Linux PostgreSQL outcome and bounded Windows CI remediation

### 8.1 Integrated transaction guard

At source/test head `f5a38a2004fb5958c4d6bc17210b0965c60931cf`, the Linux PostgreSQL job passed the expanded transaction-guard integration proof. The same caller transaction locked a command receipt, used the actual IdentityAccess transaction guard (two independent grants, complete result-set consumption), locked a synthetic approval request, changed temporary Branch/Warehouse/Assignment rows, and appended domain history plus a local pending outbox row. Seven competing account/session/role-membership/grant/permission mutations were held until commit. The post-commit proof checked all facts and then confirmed a fresh Deny after revocations.

The exact same head also had Release build 0 warnings/0 errors, 243/243 .NET tests, 61/61 base PostgreSQL integration tests, all explicit O20-E/O20-F/O20-D/O20-G/O21/IdentityAccess proof steps and Linux migration idempotence (8 applied first, 0 new second). CodeQL, Windows Preview and Project Charter Owner Gate passed.

### 8.2 Windows migration job status

The separate Windows PostgreSQL 18.6-5 install/migration job was still in progress when the next push superseded/cancelled that run. Do not record it as PASS or as a migration failure. The script now has a 240-second download timeout and 15-minute installer-process deadline with process-tree termination. The policy validator and unit test require these limits. New exact-head CI is pending for these changes.

### 8.3 Current authoritative blocker classes

The only items considered technically test-passed here are bounded disposable proofs. The following external/release gates remain open:
- IdentityAccess module-owner approval of public contract v1.2 / guard;
- Platform and affected data-owner approval of total lock order and cross-module revocation linearization;
- protected Product Owner binding bootstrap/cardinality/change/recovery/emergency-disable lifecycle;
- Organization assignment-management permission namespace/role policy;
- production AuditSecurity canonical API/durable ACK, worker identity decision, retry/quarantine/runbook, WORM Object Lock Compliance and KMS/HSM custody;
- Platform identifier/version/generator decision;
- legal retention schedule, Legal Hold authority/scope/release and backups/replicas/exports;
- WG-0 reservation and explicit G1 implementation authorization;
- independent exact-SHA Architecture/Security review and Product Owner G1 decision.

No production Organization module/UoW/schema/migration, permission seed, reporting role, service identity, secret, WORM or key service is created.

**Disposition:** local proof sources and exact Linux PostgreSQL output PASS at `f5a38a...`; Windows timeout hardening source added; final exact-head CI and external gates OPEN. O20-E/O-20/O-21 OPEN; G1 NOT GRANTED.

**END — O20-E blocker closure matrix v1.4**


## 9. Exact-head validation successor — 2026-11-22


## Exact-head bounded CI evidence — 2026-11-22

All results below refer to the same source/test commit `5eb769cda992d05a784bc9bebc4fe9e30084f81a` on `security/o20-o21-final-closure`:

| Validation | Run | Result |
|---|---:|---|
| MD-Gsm Persons Module Gates | 45063596273 | SUCCESS — 13/13 jobs |
| CodeQL | 45063596261 | SUCCESS |
| Windows Persons Preview Installer | 45063596250 | SUCCESS |
| Project Charter Owner Gate | 45063631382 | SUCCESS |

Primary workflow evidence:
- Release build succeeded with 0 warnings and 0 errors.
- .NET test suite: 243/243 passed.
- Marked disposable PostgreSQL 18 integration suite: 61/61 passed.
- SHA256 verifier: 669 tracked files matched `SHA256SUMS.txt`.
- Linux and Windows PostgreSQL 18.6-5 migration tests: 8 migrations applied on first run and 0 new migrations on repeat; the database session used `migration_runner` with effective owner `mdgsm_schema_owner`.
- O20-G catalog inventory artifact: ID `14770677123`, SHA-256 `09fd2abcfad01a58aaeb7ad0cf1a622260d5eb4639d9a23a3cc24d45e5aab5a3`.
- PII-free CI evidence artifact: ID `14769978817`, uploaded artifact SHA-256 `165f08b510e5b1ef34f9d7350e8f9dcc4cf147ce1b52d68a4dc804b55061dabd`.

The explicit PostgreSQL proof steps in the same primary run all completed successfully:
- OD-01 exhaustive pinned White_Space/Cc/Cf scalar conformance; OD-02/OD-03 Warehouse identity, reassignment and terminal archive;
- immutable Warehouse-to-Branch relationship history;
- fail-closed retention/Legal Hold eligibility and concurrent hold placement;
- cross-aggregate Branch/Warehouse/assignment races, bounded retry and stale approval rejection;
- non-owner maker-checker, self-assignment denial and synthetic Product Owner direct-policy path;
- local AuditIntent/Outbox atomicity, same-EventId/hash replay, changed-payload quarantine and competing worker claims;
- assignment idempotency and deterministic local lock-order envelope;
- outbox-worker object/column least-privilege boundary;
- O20-D backup/isolated restore, O20-G catalog inventory, O20-F emergency lifecycle, O21 hash-chain;
- IdentityAccess live-session permission, revocation linearization and the transaction-scoped authorization guard.

The IdentityAccess transaction guard proof used the actual adapter against disposable `identity.*` tables, drained the complete multi-role permission result set, held the relevant row-share locks through the caller transaction, and verified seven competing revocation/disable/grant mutations remained blocked until commit. The same disposable transaction also wrote synthetic receipt, approval, Branch, Warehouse, assignment, history and pending outbox facts.

**Evidence boundary:** all Organization-shaped tables and the canonical-audit endpoint in these proofs are disposable fixtures. These results do not create or prove a production Organization module/UnitOfWork, production AuditSecurity API/durable ACK, trusted Product Owner binding, Platform-wide accepted lock order, legal disposal service, WORM/KMS custody or G1. The later documentation-only successor commits are not covered by this run and must receive their own PR checks.


## 10. Current blocker disposition after exact-head validation

| Blocker | Current result | What remains before closure |
|---|---|---|
| OD-01 canonicalization | OWNER-ACCEPTED; exhaustive pinned-scalar .NET/PostgreSQL disposable proof PASS | Independent Unicode/runtime compatibility review; production Organization implementation is still G1-gated |
| OD-02 Warehouse identity/reassignment | OWNER-ACCEPTED; exact WarehouseId/reassignment and cross-aggregate race PoCs PASS | Production Organization consumer, owner/module acceptance and G1 |
| OD-03 terminal archive | OWNER-ACCEPTED; ARCHIVED terminal/no physical delete PoC PASS | Production Organization consumer and G1 |
| OD-05 retention baseline | OWNER-ACCEPTED; Legal Hold eligibility and concurrent placement PoCs PASS | Jurisdiction/record-class schedule, Legal Hold authority/scope/release, backup/replica/export treatment, lawful disposal controls |
| IdentityAccess public contract | Contracts v1.2 / transaction guard source-published; exact-head CI PASS | IdentityAccess module-owner acceptance and versioned contract publication/ownership sign-off |
| Authorization linearization | Actual adapter locks complete multi-role permission fact set through caller transaction; bounded two-writer proof PASS | Platform and affected owners must accept the total cross-module order and revocation linearization; no Organization UoW exists |
| Organization assignment permission namespace | No unapproved `organization.*` permission added | IdentityAccess + Product Owner role mapping acceptance; no new key/seed/grant until accepted |
| Product Owner authority | Fail-closed `Unavailable`; synthetic fixture only proves direct-policy behavior when a binding is seeded | Binding cardinality, installation scope, attended bootstrap, independent change/recovery, emergency disable/restore and implementation |
| Maker-checker | Bounded disposable policy proof PASS | Production binding, actual command authorization, permission and transaction integration |
| Assignment history | Append-only relationship history disposable PostgreSQL proof PASS | Production Organization table, access-control and retention implementation after G1 |
| Idempotency / unknown commit | Concurrent same-key replay and uniqueness disposable proof PASS | Actual Organization command receipt/UnitOfWork after G1 |
| Local outbox | Atomic intent/replay/quarantine/claim disposable proof PASS | Real AuditSecurity canonical API, durable ACK verification, production worker/retry/reconciliation/runbook |
| Worker identity | Disposable object/column privilege proof PASS; N.6 five-identity model unchanged | Product Owner/Platform/DB/Security decision on additional identity; keep production delivery disabled until accepted |
| Platform identifier | No standard selected | Platform must decide UUIDv7 or alternative, generator, clock and multi-instance behavior |
| Platform global lock order | Candidate local ordering and integrated disposable participant proof PASS | Platform + all affected module-owner acceptance of one total order, cross-module race matrix and production participant obligations |
| RowScope | Exact source inventory confirms no current Branch/Warehouse consumer | Inventory and SQL-before-materialization tests for every future query/report/count/export/worker; ReportingReadOnly remains absent |
| Legal retention | No automatic deletion; fail-closed PoC PASS | Legal authority and schedules, hold lifecycle, media scope and production enforcement |
| O21 production trust | Hash-chain PoC PASS | Production AuditSecurity, external Object Lock Compliance anchor, KMS/HSM key custody, independent verifier, restore, alerts and runbook |
| Independent review | Reviewer `mrdeljoe4` requested; no submitted review | Independent exact-SHA Security/Architecture findings and retests |
| WG-0 / G1 | NOT RESERVED / NOT GRANTED | WG-0 reservation, explicit Product Owner implementation authorization and release decision |

### 10.1 PR and release coordination

- PR #91 is the non-Draft, open, unmerged full revert proposal for prematurely merged PR #86. Its current checks are green and it is mergeable, but no independent review has been submitted. It must not bypass the required review/branch-protection controls.
- PR #92 remains Draft and unmerged, based on the current `main` that still contains PR #86. If PR #91 is merged first, PR #92 must be reconciled/rebased onto the resulting `main` and all checks rerun.
- No production Organization runtime/schema/DDL/migration, permission seed/grant, Product Owner binding, extra service identity, production outbox worker, WORM/KMS resource, or ReportingReadOnly grant was created.

**Final disposition:** all listed bounded disposable technical proofs and exact-head CI are PASS within the stated fixture scope. External owner/platform/legal/production/independent gates remain OPEN. O20-E, O-20 and O-21 remain OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E Technical Blocker Closure Matrix v1.5**


## 11. Iran legal-source crosswalk added — 2026-11-22

The controlled records-retention policy successor v0.4 now records a source review of the Iran Commercial Code Articles 1, 2, 6, 13 and 19; the current consolidated AML Executive Regulation Articles 1(5), 1(7), 142 and 149 (amendments 1404/07/27 and 1404/12/03); and VAT Executive Regulation Article 103 (retrieved English PDF marked unofficial translation).

This materially narrows the legal question but does not close the schedule:
- Commercial Code Article 13 may require ten-year retention for the statutory Article 6 books, but Article 6 excludes small merchants and Article 19 leaves classification to an implementing decree. Applicability to the operator's actual legal form/activity must be determined.
- AML Article 142's current text applies the ten-year obligation to financial institutions; Article 149 extends specified AML obligations to specified non-financial professions/activities. Generic mobile retail/repair is not expressly named in the categories reviewed. Any residual high-risk designation must be supported by an effective Council decision; no such designation was identified for mobile repair in this source review.
- VAT Article 103 has a separate five-year general rule and specific capital-asset / real-estate periods in the retrieved unofficial translation. Counsel must verify the controlling Persian text and applicability.

No single period is adopted for Organization assignment/Branch/Warehouse history because these sources do not expressly classify that domain history as a statutory commercial book, a VAT record or an AML record. The accepted OD-05 retain-by-default / no-automatic-deletion baseline remains unchanged.

**Current legal disposition:** research crosswalk COMPLETE; per-record legal classification, numerical schedule, Legal Hold operating authority and media handling OPEN. O20-E/O-20/O-21 remain OPEN; G1 NOT GRANTED.

**END — blocker closure matrix v1.6**


## 12. Exact current-head CI confirmation and DI fail-closed test addition — 2026-11-23

### 12.1 Completed exact-head validation

The branch source/test head `83b8bf35345b14faf6ce549d80640180aca1c28e` completed the full required GitHub Actions validation:

| Gate | Run | Result |
|---|---:|---|
| MD-Gsm Persons Module Gates | 45066929461 | SUCCESS — 13/13 jobs |
| CodeQL | 45066929413 | SUCCESS |
| Windows Persons Preview Installer | 45066929420 | SUCCESS |
| Project Charter Owner Gate | 45067172747 | SUCCESS |

Evidence retrieved from the exact run:
- Release build: 0 warnings / 0 errors.
- .NET unit tests: 243/243 passed.
- Marked PostgreSQL 18 base integration suite: 61/61 passed.
- Linux and Windows PostgreSQL migrations: 8 applied on first execution, 0 new on idempotent repeat; session login `migration_runner`, effective owner `mdgsm_schema_owner`.
- SHA256 source verifier: 683 tracked files matched.
- O20-D isolated backup/restore and O20-G complete catalog/effective-privilege inventory passed.
- Explicit O20-E methods passed for OD-01/OD-02/OD-03, immutable Warehouse-Branch history, Legal Hold fail-closed eligibility/concurrent hold, cross-aggregate Branch/Warehouse/assignment races, bounded retry, stale approval rejection, maker-checker and owner direct policy, outbox atomicity/replay/quarantine/worker claims, idempotency, deterministic local lock ordering and worker least privilege.
- Explicit IdentityAccess methods passed for live session permission, revocation linearization and transaction-scoped guard; explicit O21 hash-chain proof passed.
- The actual IdentityAccess adapter transaction test used the real disposable `identity.*` schema and confirmed seven competing account/session/membership/grant/permission mutations stayed blocked behind the caller transaction until commit. Organization-shaped rows in the same transaction were synthetic disposable fixtures, not a production Organization UoW.

### 12.2 Additional source/test change after that green SHA

A further test was added to `tests/MDGsm.Domain.Tests/PersistenceRegistrationTests.cs`:
`Public_authorization_contract_is_registered_and_fails_closed_without_database`.

It proves that `AddMdGsmPersistence` registers the public `IIdentityAccessAuthorizationContract` even when the database connection string is absent, and that the contract returns Unavailable (never Allow) for both permission evaluation and Product Owner authority in that state. The source test's SHA256 entry was updated. Exact-head CI for `6a07f13fb8cfe00ddf317acf7e86157055eebeed` is pending; no PASS is claimed for this added test until its run completes.

### 12.3 Updated blocker disposition

| Gate | State at current snapshot |
|---|---|
| OD-01 through OD-03 bounded policy proofs | PASS at 83b8bf… |
| OD-05 baseline and Legal Hold eligibility PoC | OWNER-ACCEPTED / PASS as bounded PoC; legal schedule and production Hold open |
| IdentityAccess public contract/transaction guard | Source-published and bounded PostgreSQL-tested; module-owner acceptance OPEN |
| Authorization transaction guard integration | Disposable actual IdentityAccess adapter proof PASS; no production Organization consumer/UoW |
| Initial role/permission catalog | Source aligned and exact golden tests pass; Organization assignment namespace/role mapping OPEN |
| Product Owner binding | Fail-closed Unavailable; lifecycle and trusted enrollment NOT IMPLEMENTED |
| Candidate lock-order and concurrency | Local disposable proofs PASS; Platform total-order acceptance OPEN |
| Outbox and worker ACL | Disposable tests PASS; canonical AuditSecurity API/durable ACK/production worker OPEN |
| N.6 identity baseline | Five logical identities unchanged; additional worker identity not approved |
| Platform identifier standard | UUID generator/version decision OPEN |
| RowScope | No current Organization/Inventory/Reporting consumer; future source/query tests OPEN |
| Legal retention | Source crosswalk recorded; record-class schedule and Legal Hold operating authority OPEN |
| O21 production | Disposable hash-chain proof PASS; external HSM/KMS/WORM/verifier/restore/operations OPEN |
| Independent review/G1 | Reviewer requested; no review submitted; G1 NOT GRANTED |

**Disposition:** bounded disposable technical tests and exact CI PASS at 83b8bf…; one additional fail-closed DI registration test is pending on 6a07f13…. O20-E, O-20 and O-21 remain OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E blocker closure matrix v1.7**


## 13. Platform UUIDv7 conformance and Iran retention-catalog successor — 2026-11-23 (v1.8)

### 13.1 Identifier conformance PoC

The branch now contains:
- `tests/MDGsm.Domain.Tests/IdentityAccess/UuidV7IdentifierConformanceTests.cs`;
- `tests/MDGsm.Domain.Tests/Security/O20OrganizationUuidV7PostgresRoundTripProofTests.cs`;
- `docs/architecture/MDG-PLATFORM-IDENTIFIER-POLICY-REQUEST-001_v0.2.md`.

The unit PoC checks AccountId and PartyId UUIDv7 version/variant bits, the explicit timestamp overload, 2,048 generated IDs sharing one timestamp prefix, rejection before Unix epoch, and 20,000 parallel AccountId generations for uniqueness within one process. The PostgreSQL PoC uses the marked disposable database to round-trip 512 UUIDv7 values through native PostgreSQL `uuid` and Npgsql, preserving exact Guid identity/version/variant.

The workflow has an explicit `O20OrganizationUuidV7PostgresRoundTripProofTests` step. These sources were found missing from `SHA256SUMS.txt` during exact-head lint on `3df84c8302c61bf7736d0a42996a842df7d3ddbf`; all four missing entries (identifier policy v0.2, retention policy v0.5, UUIDv7 unit test and PostgreSQL proof) are now registered on successor head `0c2543708b7b92e8848a6d58b007ccb58036a11d`. Exact-head CI for that successor is pending. No UUIDv7 PASS is claimed at the new exact head yet.

The PoC is limited to one .NET process and one disposable PostgreSQL instance. Multi-host/process peak concurrency, clock rollback/skew across nodes, restore/reinstall, non-.NET generator compatibility, multi-version rollout and index/write amplification remain unproved. UUIDv7 order is not commit order, event order, causality, legal time or a DB sequence.

**DP-05 disposition:** UUIDv7 is better supported by bounded source/test evidence and remains a Platform recommendation only. The Platform-wide generator/version standard is not accepted.

### 13.2 Records-retention legal-source successor

The retention policy successor `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.5` expands the Iran legal-source crosswalk and record-class checklist, including:
- operator classification and activity distinctions;
- Commercial Code merchant/small-trader and statutory bookkeeping scope;
- AML obliged-person scope, Articles 142/149 and the Article 158 interaction requiring counsel/regulator classification;
- VAT Article 103 and separate tax record classes;
- the need to classify each record type and its trigger date, extension/hold conditions and accountable disposal authority.

This research does not classify the current mobile repair/retail operator, decide its applicable category, or set any Organization-history duration. No 5-, 10- or 15-year schedule is adopted. OD-05 continues to require retain-by-default/no-automatic-deletion until accountable Iran legal/records authority approves an exact record-class schedule and Legal Hold operating model.

### 13.3 Exact validation state and blocker disposition

- Last complete all-green source/test baseline remains `83b8bf35345b14faf6ce549d80640180aca1c28e` with 13/13 primary jobs, CodeQL, Windows Preview, Owner Gate, 243/243 .NET, 61/61 PostgreSQL base tests and Linux/Windows migration idempotence.
- The subsequent DI test, UUIDv7 tests, policy successors and checksum corrections are on `0c2543708b7b92e8848a6d58b007ccb58036a11d`. Its Primary/CodeQL/Windows Preview runs are pending; the Project Charter Owner Gate is SUCCESS.
- No new production code, Organization runtime, schema, grant, migration, Product Owner binding, provider resource or legal deletion process was created.

**Disposition:** bounded UUIDv7 conformance evidence added pending exact-head CI; legal crosswalk improved with no numeric schedule selected. O20-E/O-20/O-21 OPEN; G1 NOT READY / NOT GRANTED.

**END — O20-E Blocker Closure Matrix v1.8**


## 14. Legal-source correction and exact validation boundary — 2026-11-23 (v1.9)

### 14.1 Current Iran retention-source crosswalk

The controlling retention crosswalk is now `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.6`, with corresponding successors:
- data ownership policy v0.6;
- logical Organization design v0.6;
- physical PostgreSQL design v1.2.

The corrected Article 103 VAT periods are record-class-specific:
- ordinary taxable books/records: five years from end of relevant Tax Period;
- capital-asset records: five years from the end of the Tax Period in which the adjustment period ends;
- real-estate records: fifteen years from end of relevant Tax Period;
- possible notified extension up to five additional years.
The source PDF marks its English translation unofficial. The controlling Persian text and applicability still require Iran counsel/tax adviser confirmation.

The former ten-year Article 34 rule in the 1387 VAT Act is not a current blanket rule after the Article 55 repeal clause of the VAT Act 1400. Commercial Code Article 13's ten-year period concerns defined commercial books and is subject to small-trader classification. AML Article 7, amended regulation Articles 142/149/158 and the 1404 amendments require classification of actual operator/activity. No numeric Organization history period is chosen.

### 14.2 Exact CI at UUIDv7/DI source head 014b559…

The run set on exact SHA `014b5594b0da020e9e6fee9b7a0c3eb03264eed6` produced:
- Release build 0 warnings / 0 errors;
- .NET unit tests 249/249 passed, including UUIDv7 conformance and public contract DI fail-closed test;
- PostgreSQL 18 Linux integration base suite 62/62 passed;
- explicit `O20OrganizationUuidV7PostgresRoundTripProofTests` PASS;
- Linux migrations: 8 applied first run, 0 new on repeat; session `migration_runner`, effective owner `mdgsm_schema_owner`;
- SHA256 manifest: 696 tracked files matched;
- CodeQL, Windows Persons Preview Installer and Project Charter Owner Gate SUCCESS;
- Windows PostgreSQL migration job was CANCELLED. The evidence-enforcement job therefore failed because a required job was cancelled. This is **not** a Windows migration PASS.

A retry was requested for the cancelled Windows job. The branch then received additional document commits, so the retry result and a new full CI must be checked against the final SHA after the legal-source successor/checksum update. Do not mark the full gate green on the basis of the Linux pass alone.

### 14.3 Current remaining disposition

| Blocker | State |
|---|---|
| OD-05 retain/no-auto-delete baseline | OWNER-ACCEPTED |
| Legal entity/operator/record-class classification | OPEN — Iran counsel/records authority |
| Numeric retention schedule and Legal Hold operations | OPEN |
| VAT/commercial/AML legal-source mapping | Crosswalk improved; applicability not yet approved |
| UUIDv7 single-process + PostgreSQL round-trip | BOUNDED PoC PASS at 014b559… |
| Platform multi-host/clock/rollback ID standard | OPEN |
| IdentityAccess source/DI/live transaction guard | BOUNDED TEST PASS; module-owner acceptance still OPEN |
| Platform global lock-order acceptance | OPEN |
| AuditSecurity production canonical API/durable ACK/worker | OPEN |
| Product Owner protected binding/bootstrap/recovery | NOT IMPLEMENTED; fail-closed |
| Organization permission namespace/role mapping | OPEN; no keys/grants added |
| Production RowScope consumer | None currently; future route/source validation OPEN |
| Windows migration gate at 014b559… | CANCELLED; retry required |
| Latest-head full CI after v0.6 legal update | PENDING |
| Independent Security/Architecture review | NOT SUBMITTED |
| WG-0 / G1 | NOT GRANTED |

**Disposition:** the legal-source correction and bounded UUIDv7/DI tests are recorded. No production retention schedule is approved. O20-E/O-20/O-21 remain OPEN; G1 NOT READY / NOT GRANTED.

**END — O20-E Blocker Closure Matrix v1.9**
