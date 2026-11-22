# O20-E Organization Assignment and RowScope — Threat Model and Evidence Mapping

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-THREAT-MODEL-001 |
| Version / Date | 0.6 / 2026-11-22 |
| Supersedes | v0.5 |
| Supersedes | v0.4 |
| Status | INTERNAL THREAT MODEL — BOUNDED EXACT-HEAD CI PASS; INDEPENDENT SECURITY REVIEW NOT PERFORMED |
| Evidence baseline | Exact source/test head 5eb769cda992d05a784bc9bebc4fe9e30084f81a; bounded CI run 45063596273 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |

## 1. Scope and evidence semantics

This successor maps the 15 threat scenarios from v0.1 to repository evidence and states what each test does and does not prove. Evidence labels:
- PASS — bounded disposable proof: an executable test passed on a marked disposable PostgreSQL database/test host.
- PARTIAL: a bounded component is implemented/tested, but production consumer or full invariant is absent.
- NOT APPLICABLE — consumer absent: the source path does not exist at the inspected SHA; this is not a security pass for a future path.
- OPEN: required control, owner decision, production implementation, or independent review remains absent.

A policy document or disposable schema is not proof of a production implementation. Organization, Inventory, Reporting and AuditSecurity production modules/routes do not exist at the inspected source baseline. No production Organization DDL, migration, role, grant, credential, route, outbox worker or G1 authorization is created by this threat model.

## 2. Protected assets and invariants

Assets: Branch/Warehouse masters and relationship history; exact AccountBranchAssignment/AccountWarehouseAssignment facts; actor/subject/maker/approver/Product Owner decisions; live IdentityAccess principal, session, permission and credential-version state; source-side SQL RowScope; Organization domain history/local outbox; canonical AuditSecurity events and durable ACK; OD-05 retained records and future Legal Hold.

Normative invariants:
1. Actor/principal/role/permission/owner authority comes from current server-side trusted sources, never client fields.
2. Branch assignment does not imply Warehouse assignment. Warehouse visibility binds to exact active WarehouseId and valid current Warehouse/Branch relationship.
3. Non-owner assignment operations require a distinct approver who is neither actor nor subject. Product Owner direct execution is separate from self-approval.
4. IdentityAccess permission and Organization assignment predicates are conjunctive. IdentityAccess permissions do not widen resource scope.
5. Revocation, session expiry, disabled account, credential-version mismatch, unknown permission, unavailable principal/binding and transaction mismatch fail closed.
6. Domain history is append-only; OD-05 prohibits automatic deletion until lawful retention schedule and hold/disposal controls are approved.
7. Local outbox intent is atomically committed with local domain mutation. Synthetic PoC ACK is not canonical AuditSecurity durable ACK.
8. No future read/query/search/count/aggregate/print/export/worker path may rely on client-side filtering for RowScope.

## 3. Trust boundaries

| Boundary | Trusted evidence | Untrusted input / prohibited inference |
|---|---|---|
| Client → API | Opaque bearer token after SessionAuthenticationMiddleware validates live session | Client AccountId, ActorId, SubjectId, BranchId/WarehouseId scope, role, approver, owner flag |
| API → IdentityAccess | Public IIdentityAccessAuthorizationContract with validated request principal | Private-table query by consumer, stale claims, caller-supplied principal, cached Allow |
| Organization → source module | Published authoritative BranchId/WarehouseId and query contract | Inferred IDs, client filters, unrelated private module schema |
| Organization → AuditSecurity | Immutable local intent, stable EventId, canonical payload/hash and verified durable ACK | HTTP 2xx as durable ACK, changed payload with reused EventId |
| Runtime / worker → PostgreSQL | Approved least-privilege identity and object/column grants | DDL, ownership, broad base-table access, history hard-delete, unrelated module data |
| Operations / legal | Attributable independently authorized hold/disposal decisions | Unrecorded manual SQL, unreviewed purge, self-authorized deletion |

## 4. Threat-to-control evidence matrix

| ID | Threat | Existing control / evidence | Status | Residual work |
|---|---|---|---|---|
| TM-01 | Forged AccountId/ActorId/SubjectId | Session middleware and Contracts v1.2 derive principal from validated server session; adapter accepts no caller AccountId/session/role/owner flag. Tests: SessionAuthenticationMiddlewareTests, IdentityAccessAuthorizationContractAdapterTests, IdentityAccessLiveSessionPermissionPostgresProofTests. Organization approval PoC is not a production HTTP route. | PARTIAL | Add forged-field API tests when Organization endpoint exists; ignore caller actor fields on every route. |
| TM-02 | Forged owner/super_admin claim | O20OrganizationAssignmentApprovalPostgresProofTests denies forged owner hint and super_admin without synthetic protected binding. Public contract returns Unavailable while real binding store is absent. | PARTIAL | Accept and implement binding ownership/cardinality, bootstrap, change, independent recovery, revocation, emergency disable and restore. |
| TM-03 | Self-approval / maker equals approver or subject | O20OrganizationAssignmentApprovalPostgresProofTests denies actor/subject as approver and allows a distinct synthetic approver. | PASS — bounded policy PoC | Add full maker-checker matrix, command fingerprint/version validation and concurrent edit/approval tests in real Organization transaction. |
| TM-04 | Branch assignment treated as Warehouse access | OD-02 policy/history PoCs preserve exact WarehouseId identity; source inventory v0.6 shows no actual Branch/Warehouse query consumer. | PARTIAL / future consumer absent | No RowScope pass is claimed. For every new consumer require SQL predicate before materialization across detail/list/search/count/aggregate/pagination/print/export/worker. |
| TM-05 | Warehouse reassignment broadens or silently transfers grants | O20OrganizationPolicyPostgresProofTests and O20OrganizationRelationshipHistoryPostgresProofTests cover inactive-only reassignment, WarehouseId continuity, rejected-attempt rollback and append-only relationship history. | PARTIAL | Add two-writer reassignment vs assignment create/change/revoke and Branch deactivate/archive races. |
| TM-06 | Stale permission/assignment after revocation | IdentityAccessLiveSessionPermissionPostgresProofTests, IdentityAccessAuthorizationLinearizationPostgresProofTests and IdentityAccessTransactionScopedAuthorizationGuardPostgresProofTests use actual IdentityAccess schema/adapter. The guard holds FOR SHARE locks; five competing IdentityAccess mutations were observed blocked by pg_blocking_pids(). | PASS — IdentityAccess boundary; PARTIAL cross-module | No Organization UoW exists. Platform must accept global lock ranks; a real consumer must hold guard and Organization mutation in one approved transaction. |
| TM-07 | Counts/aggregates/pagination leak despite filtered rows | No Reporting/Organization/Inventory query, report, dashboard aggregate or export consumer exists at source baseline. | NOT APPLICABLE — consumer absent | Inventory and test every new query before release. No current RowScope runtime PASS. |
| TM-08 | Export worker uses stale principal/scope | No report/export worker or artifact-delivery endpoint exists at source baseline. | NOT APPLICABLE — consumer absent | Revalidate current session, permission and Organization assignment at artifact creation/delivery; test revocation races. |
| TM-09 | History rewritten/deleted or prematurely purged | O20OrganizationRelationshipHistoryPostgresProofTests rejects UPDATE/DELETE in disposable history. OD-05 and records policy v0.1 establish retain-by-default/no-auto-delete. New O20OrganizationLegalHoldRetentionPostgresProofTests adds a fail-closed eligibility and concurrent hold race PoC; exact-head CI pending. | PARTIAL | No production history/Hold resolver exists. Legal schedule, hold authority/scope/release, media treatment and lawful disposal implementation remain open. |
| TM-10 | Outbox retry mutates payload or reuses EventId with different hash | O20OrganizationAuditOutboxPostgresProofTests proves same EventId/same hash replay returns same synthetic ACK; changed hash is quarantined without overwriting original. Worker PoC denies mutation of immutable event payload/hash/EventId. | PASS — bounded disposable PoCs | Synthetic receiver is not AuditSecurity. Implement real canonical endpoint, authenticated delivery, durable ACK and production quarantine/reconciliation. |
| TM-11 | Audit outage/crash silently loses intent | Outbox PoC proves local fact/PENDING intent atomicity, rollback, replay and competing worker claims. O21 hash-chain PoC is a separate disposable ledger simulation. | PARTIAL | No production AuditSecurity writer/ACK API, source outbox, worker credential, retry schedule, alerting, recovery or cross-service reconciliation. |
| TM-12 | Concurrent grant/revoke/reassignment deadlock or lost update | O20OrganizationIdempotencyConcurrencyPostgresProofTests, O20OrganizationDeterministicLockOrderPostgresProofTests, IdentityAccess linearization and transaction guard cover bounded local race shapes. | PARTIAL | Platform-wide order and full LO-01..LO-12 cross-module matrix (timeout/deadlock/retry/cancel/crash/unknown commit) not accepted or proven. |
| TM-13 | Reporting role or broad DB grant bypasses RowScope | O20-G catalog/effective privilege digest and support identity proof inspect marked DB; five logical identities catalogued; no ReportingReadOnly role/grant/view provisioned. Worker object PoC separately denies business data. | PASS — bounded catalog/PoC | Re-run on each approved production-like schema/release; never provision ReportingReadOnly before approved source/query boundary. |
| TM-14 | Unknown Legal Hold state while purge runs | OD-05 says active/unresolved hold blocks disposal and no automatic purge. New Legal Hold test proves unavailable resolver/unknown schedule/active hold deny eligibility and that concurrent hold placement blocks decision; exact-head CI pending. | POLICY ACCEPTED / PoC PENDING | No production Hold resolver, legal schedule, disposal workflow or concurrent production enforcement exists. Keep deletion disabled. |
| TM-15 | Organization reads IdentityAccess private tables | Contracts assembly exposes typed principal/permission boundary; ArchitectureBoundaryTests enforces dependency direction. No Organization module exists at baseline. | PARTIAL / future consumer absent | When authorized, reference Contracts only; add real module/UoW boundary test and review final source. |

## 5. High-risk invariants and explicit exceptions

- Assignment binds to exact AccountId and exact BranchId/WarehouseId.
- Product Owner direct execution is limited to Organization assignment create/change/revoke and self-assignment. It still requires authenticated principal, protected binding, reason, idempotency, expected version, valid exact resource, immutable history and audit intent.
- Non-owner approver cannot be actor or subject. PRODUCT_OWNER_DIRECT is not an approval record and cannot be inferred from a role.
- IdentityAccess permission AND Organization assignment are conjunctive. No permission or Branch assignment broadens Warehouse scope.
- Missing, stale, ambiguous, unsupported, unavailable or version-mismatched authority produces Deny/Unavailable.
- Organization domain history and local outbox do not replace canonical AuditSecurity ledger.
- OD-05 duration remains UNSET; retain/no-automatic-deletion is current rule.
- No RowScope consumer is authorized until source ownership, authoritative resource ID, all data surfaces and SQL-before-materialization tests are defined.

## 6. Evidence boundary and residual risk

Bounded exact-head CI baseline at source/test SHA `99f28b63a179a4a54b377bca2a9174a819883c28` (primary run `45051449948`):
- 242/242 .NET tests passed;
- 58/58 marked disposable PostgreSQL integration tests passed, with all explicit O20-E and IdentityAccess proof-class invocations also passing;
- Linux and Windows PostgreSQL 18 migration jobs applied 8 migrations first run and 0 on repeat;
- O20-D backup/isolated restore, O20-G catalog digest, O20-E policy/history/approval/outbox/idempotency/local lock candidate tests, O20-F emergency lifecycle, O21 hash-chain and IdentityAccess live permission/revocation/transaction guard passed;
- CodeQL, Windows Preview Installer and Project Charter Owner Gate passed on the same source/test SHA.

At the same exact head, the explicit worker least-privilege, Organization Legal Hold/retention, Warehouse-Branch immutable history, maker-checker/Product Owner policy, cross-aggregate race, idempotency, local outbox/replay/quarantine, deterministic lock-order, IdentityAccess live permission, revocation linearization and transaction-guard tests all passed. The run completed all 13 primary jobs, both Linux and Windows PostgreSQL 18 migration jobs, CodeQL, Windows Preview Installer and Project Charter Owner Gate.

## 7. Independent review and closure conditions

Final independent review must record exact reviewed SHA, code/test locations, output, residual risk, severity, disposition and hashes. Disposable PoCs alone do not close production threats. Critical/High findings must be fixed or formally risk-accepted by the appropriate authority.

External decisions/deployment inputs still required:
- IdentityAccess module-owner acceptance of Contracts v1.2 and transaction guard;
- protected Product Owner binding cardinality, bootstrap designation and independent lifecycle authority;
- Platform and affected module-owner global lock-order/linearization acceptance;
- sixth outbox-worker identity decision, retaining N.6 five-role model until accepted;
- Platform UUID generator/version and clock behavior;
- AuditSecurity API/event schema/durable ACK;
- actual KMS/HSM custody and independent Object Lock Compliance provider/account;
- jurisdiction-specific records schedule and Legal Hold authority;
- independent exact-SHA Security/Architecture review;
- WG-0 reservation and explicit Product Owner G1 authorization.

**Disposition:** threat-to-evidence map is recorded; bounded disposable tests pass in stated scope; new Legal Hold PoC is awaiting exact-head CI; production controls and owner/platform/legal acceptance remain OPEN. O20-E OPEN; O20/O21 OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — MDG-SECURITY-O20E-THREAT-MODEL-001 v0.2**


## 8. Exact-head test status and remaining residuals — 2026-11-22 (v0.3)

The current threat/evidence map was rechecked at `99f28b63a179a4a54b377bca2a9174a819883c28`:
- `TM-01` forged principal/actor: current host principal is derived from IdentityAccess-validated session middleware; public contract accepts no caller AccountId. Organization runtime tests remain future work.
- `TM-02` forged Product Owner: API deliberately returns Unavailable until protected binding exists; synthetic assignment PoC confirms client hint and super_admin role do not grant owner authority. Enrollment/change/recovery/disable/restore remain absent.
- `TM-03` self-approval: disposable PostgreSQL proof denies actor/subject as approver and requires an independent approver for non-owner path; production command flow remains absent.
- `TM-04/05` Branch/Warehouse scope and reassignment: exact WarehouseId and reassignment races passed in disposable PostgreSQL. No real source consumer/Organization runtime exists.
- `TM-06` stale session/permission: live IdentityAccess adapter and transaction-scoped row-lock guard passed against actual disposable `identity.*` schema; a future cross-module consumer must adopt an accepted Platform lock order.
- `TM-07/08` report aggregates/export workers: no current Organization/Inventory/Reporting Branch/Warehouse consumer exists; source inventory v0.6 is exact-head evidence. Future consumers require SQL-before-materialization and worker revalidation tests.
- `TM-09/14` history tampering/Legal Hold: append-only history and fail-closed hold eligibility/concurrent placement proofs passed; no legal schedule or production hold/disposal workflow exists.
- `TM-10/11` audit replay/outage: local outbox/replay/quarantine and worker object/column least-privilege fixtures passed; no production AuditSecurity canonical ACK or external WORM service exists.
- `TM-12` concurrency/deadlock: bounded local race and deterministic ordering PoCs passed; total Platform cross-module rank and broader module participation remain unaccepted.
- `TM-13` database privilege escalation: O20-G catalog digest and worker ACL PoC pass in marked disposable DB; no reporting role/grant/view is provisioned.
- `TM-15` cross-module private-table access: public Contracts and ArchitectureBoundary checks are present; no Organization consumer exists to verify its dependency direction at runtime.

### 8.1 External review boundary

This remains an internal draft, not an independent review. A designated reviewer has been requested on PR #92; no review submission or findings disposition exists yet. The reviewer must inspect the exact current SHA and map findings to source, tests, evidence and residual risk. The bounded CI does not authorize G1.

### 8.2 Remaining external gates

IdentityAccess owner acceptance; Platform/module-owner total lock order; Product Owner binding/bootstrap decision; sixth outbox worker decision or continued disablement; Platform UUID standard; AuditSecurity durable ACK/WORM and KMS/HSM provider/custody; legal retention schedule/Hold authority; WG-0 reservation; explicit G1; independent exact-SHA review.

**Disposition:** threat-to-test evidence updated and bounded exact-head CI PASS; production/owner/platform/legal/independent gates remain OPEN. O20-E OPEN; O-20/O-21 OPEN; G1 NOT READY / NOT GRANTED.

**END — O20-E Threat Model v0.3**


## 9. Multi-role permission-union race hardening — 2026-11-22 (v0.4)

A source review of the IdentityAccess transaction guard identified that its first implementation read only one row of a `FOR SHARE` result set before returning Allow. Because permission is the union of role grants, an account may have more than one active membership/grant path for the same key. Failing to consume the complete result set could leave later contributing membership/grant rows unlocked.

**Source correction:** the adapter now reads every result row, checks exact AccountId/SessionId/permission-key values on every row, and only returns Allow after the complete result has been drained. A mismatch fails as Unavailable. This is an explicit defense against TM-06 stale/revoked grant races.

**New bounded test:** the actual Infrastructure adapter is now exercised with two distinct roles both granting `reports.read`. Seven independent concurrent PostgreSQL writers attempt session revoke, account disable/credential-version change, deletion of each role membership, deletion of each grant row and permission deactivation. They must all remain blocked until the caller transaction commits. The same caller transaction mutates synthetic Branch/Warehouse/assignment rows and appends domain-history and outbox rows; after commit those facts are verified durable, then a new permission check denies.

The Organization-shaped rows are temporary disposable test tables, not production Organization schema or a production consumer. This remains a bounded proof, and exact-head CI is pending.

**Disposition:** TM-06 source correction applied; multi-role and same-transaction proof added, awaiting exact-head CI. Independent Security review remains NOT PERFORMED. Platform lock order/IdentityAccess owner acceptance remain OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — O20-E Threat Model v0.4**


## 10. Exact Linux proof outcome and Windows runner containment — 2026-11-22 (v0.5)

At exact source/test SHA `f5a38a2004fb5958c4d6bc17210b0965c60931cf`, the expanded IdentityAccess multi-role transaction guard proof passed in the marked disposable PostgreSQL job. Two roles independently granted the same permission; seven competing writers attempted session revoke, account/credential change, membership removal for each role, permission-grant removal for each role, and permission deactivation. The actual adapter consumed the full result set and retained all relevant row-share locks through the caller transaction. The transaction then wrote temporary Branch/Warehouse/assignment facts and appended domain history/outbox. Post-commit state was checked, and the fresh permission evaluation denied after the blocked revocations completed.

The same source/test head had 243/243 .NET tests, 61/61 base PostgreSQL tests, all explicit O20-E/O20-F/O20-D/O20-G/O21 and IdentityAccess proof commands, and Linux migration idempotence (8 first run, 0 second run) pass. CodeQL, Windows Preview and Project Charter Owner Gate passed. The Windows PostgreSQL 18 installer step remained in progress when a later commit superseded that run; it must not be called a failure or a pass.

A separate CI hardening change now gives the Windows installer download a 240-second timeout and unattended process a 15-minute maximum, with process-tree termination on timeout. The policy validator and unit test enforce both constraints. These latest changes still require exact-head CI.

**Disposition:** TM-06 multi-role guard correction and combined local transaction proof PASS within disposable fixture scope; Windows installer timeout guard added; current-head CI pending. Independent Security review, IdentityAccess owner acceptance, Platform total lock order, Product Owner binding, production AuditSecurity, legal schedule and G1 remain OPEN.

**END — O20-E Threat Model v0.5**


## 11. Exact-head threat-to-evidence update — 2026-11-22 (v0.6)


Exact source/test validation was completed on `5eb769cda992d05a784bc9bebc4fe9e30084f81a` by primary workflow `45063596273` (13/13 jobs SUCCESS), CodeQL `45063596261` SUCCESS, Windows Preview `45063596250` SUCCESS and Project Charter Owner Gate `45063631382` SUCCESS. Release build: 0 warnings/0 errors; .NET tests 243/243; marked PostgreSQL 18 integration base suite 61/61; Linux and Windows migrations 8 applied on first run and 0 new on repeat; checksum verifier: 669 tracked files matched. This CI evidence covers the bounded tests only, not a production Organization consumer or external owner/platform/legal acceptance.


The current exact-head evidence updates the disposable threat coverage as follows:
- Forged actor/owner inputs and self-assignment: synthetic PostgreSQL maker-checker proof PASS; the real Product Owner binding remains absent and the public contract returns Unavailable.
- Stale session/permission and multi-role grants: actual IdentityAccess Infrastructure adapter plus PostgreSQL row-lock/revocation proofs PASS; Platform total cross-module lock order and owner acceptance remain open.
- Branch/Warehouse assignment boundaries, terminal lifecycle and reassignment races: disposable PostgreSQL proofs PASS; no production Organization source consumer exists.
- History tampering and Legal Hold/disposal eligibility: append-only history and fail-closed hold tests PASS; production legal schedule, authority and disposal mechanism remain open.
- Outbox replay, changed-payload quarantine and worker privilege: synthetic local tests PASS; no real canonical AuditSecurity API/durable ACK/deployed worker exists.
- Counts, aggregates, report exports and worker RowScope: NOT APPLICABLE to current source because no Organization/Inventory/Reporting Branch/Warehouse consumer exists. This is not a pass for future consumers.
- Cross-module deadlocks/retry: tested local candidate cases PASS; no Platform-approved global rank across actual module participants exists.

**Independent review remains outstanding.** The reviewer is requested on PR #92, but no submitted review exists on PR #92 or PR #91. This threat model remains an internal draft and must not be represented as independent certification.

**Disposition:** bounded threat evidence PASS within the tested fixtures; residual production threats and cross-owner acceptance remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E Threat Model v0.6**
