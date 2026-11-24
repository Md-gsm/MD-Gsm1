# O20-E Technical Blocker Closure Matrix — Latest Exact-Head Validation

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-BLOCKER-CLOSURE-MATRIX-001 |
| Version / Date | 2.1 / 2026-11-23 |
| Supersedes | v2.0 |
| Supersedes | v1.9 |
| Status | EXACT-HEAD CI PASS FOR BOUNDED TESTS; PRODUCTION / EXTERNAL OWNER GATES OPEN |
| Repository / Branch | Md-gsm/MD-Gsm1 / security/o20-o21-final-closure |
| Previous exact-green source/test SHA | `4bcb227109d6a3349a5cbfb7f133f3623f5ef9cb` |
| New source/test head | Pending full validation after unknown-commit / rollback test addition |
| PR | #92 Draft / open / unmerged |
| Current main SHA | `2a52a8b0629769953ff9671a0cf220401614ce9f` |
| Previous full blocker history | MDG-SECURITY-O20E-BLOCKER-CLOSURE-MATRIX-001 v1.9 |
| Readiness successor | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v4.0 |
| Decision packet | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.5 |

## 1. Exact-head CI evidence

The following required workflows completed successfully on the same exact source/test SHA `4bcb227109d6a3349a5cbfb7f133f3623f5ef9cb`:

| Gate | Run | Result |
|---|---:|---|
| MD-Gsm Persons Module Gates | 45070130837 | SUCCESS — 13/13 jobs |
| CodeQL | 45070130838 | SUCCESS |
| Windows Persons Preview Installer | 45070130892 | SUCCESS |
| Project Charter Owner Gate | 45070956303 | SUCCESS |

Retrieved exact-run evidence:
- Release build: 0 warnings, 0 errors.
- .NET test suite: 249/249 passed.
- Marked disposable PostgreSQL 18 base integration suite: 62/62 passed.
- Linux PostgreSQL application migration runner: 8 migrations applied on first run; 0 new on repeat; session login `migration_runner`; effective owner `mdgsm_schema_owner`.
- Windows PostgreSQL 18.6-5 application migration runner: 8 migrations applied on first run; 0 new on repeat; checksum/history validation succeeded.
- SHA256 source verifier: 704 tracked files matched `SHA256SUMS.txt`.
- Project Charter Owner Gate, CodeQL and Windows preview installer succeeded on the same SHA.

### 1.1 Explicit O20-E / IdentityAccess PostgreSQL proof steps

All listed proof steps were completed successfully in the PostgreSQL job on the exact SHA:

| Proof class | Explicit result observed |
|---|---|
| OD-01 / OD-02 / OD-03 Organization policy | 3 methods passed, including full pinned White_Space/Cc/Cf scalar comparison, .NET/PostgreSQL canonicalization equivalence, and Warehouse identity / terminal archive policy |
| Warehouse-to-Branch history | 1 method passed; versioned history appends and UPDATE/DELETE are denied |
| Legal Hold and retention eligibility | 2 methods passed; missing/unknown schedule/authority/hold state fails closed; concurrent hold placement serializes before eligibility |
| Cross-aggregate concurrency | 4 methods passed; Branch/Warehouse and assignment race cases, stale approval rejection and bounded lock-timeout retry |
| Assignment maker-checker / Product Owner policy | 1 method passed; non-owner self-assignment and actor/subject approval are denied; synthetic bound-owner path is separately labelled as a fixture |
| Local AuditIntent/Outbox | 2 methods passed; atomic local intent, same EventId/hash replay, changed-payload quarantine and competing worker claims |
| Assignment idempotency | 1 method passed; concurrent same-key replay resolves to one assignment fact |
| Deterministic local lock-order candidate | 3 methods passed; homogeneous and Branch/Warehouse/Assignment ordering plus atomic receipt/approval/aggregate/history/outbox envelope |
| UUIDv7 PostgreSQL round-trip | 1 method passed; native PostgreSQL uuid preserves UUIDv7 bits and identity |
| IdentityAccess live-session permission | 1 method passed against the marked disposable IdentityAccess database |
| IdentityAccess revocation linearization | 1 method passed; actual PostgreSQL blocking relationships observed |
| IdentityAccess transaction-scoped guard | 1 method passed against actual disposable `identity.*` tables; complete multi-role result set is drained and seven competing mutations remain blocked until commit |
| O21 hash-chain | 1 method passed in the disposable cryptographic/hash-chain fixture |
| O20 outbox-worker object/column ACL | Explicit worker least-privilege step passed |
| O20-D / O20-G / O20-F | Backup/isolated restore, effective privilege catalog inventory/digest and emergency lifecycle steps passed |
| Accounting AP-03 / AP-04 | Disposable PostgreSQL proof passed |

These results prove only the code and disposable fixtures exercised by these exact-head CI jobs. They do not turn synthetic Organization-shaped rows into a production Organization module, nor a synthetic canonical ACK endpoint into AuditSecurity production infrastructure.

## 2. Gate-by-gate disposition after exact-head CI

| Gate | Current result | Remaining closure condition |
|---|---|---|
| OD-01 code normalization | OWNER-ACCEPTED; bounded exact-head .NET/PostgreSQL PoC PASS | Independent runtime/Unicode compatibility review and future production Organization implementation after G1 |
| OD-02 Warehouse identity/reassignment | OWNER-ACCEPTED; bounded exact-head PostgreSQL PoCs PASS | Production Organization consumer after G1; preserve exact WarehouseId and no implicit assignment transfer |
| OD-03 terminal archive | OWNER-ACCEPTED; bounded exact-head PostgreSQL PoC PASS | Production lifecycle implementation after G1 |
| OD-04 emergency access | OWNER-ACCEPTED; O20-F bounded exact-head PoC PASS | Production emergency workflow and durable operational audit remain separately gated |
| OD-05 retention baseline | OWNER-ACCEPTED; fail-closed Legal Hold eligibility/concurrency PoC PASS | Legal record-class schedule, hold authority/scope/release, backup/replica/export rules and production enforcement |
| PermissionCatalog | Approved ten-role matrix source-aligned; immutable collection hardening; exact-head CI PASS | IdentityAccess owner review; no Organization management permission keys until role mapping is explicitly accepted |
| IdentityAccess request-time contract | Public Contracts API source-published; exact-head CI PASS | IdentityAccess module-owner acceptance and lifecycle/version governance |
| IdentityAccess transaction guard | Actual Infrastructure adapter source-published; complete multi-role row-lock set and exact-head disposable PostgreSQL proof PASS | IdentityAccess module-owner acceptance, Platform total order acceptance and an authorized real consumer |
| Product Owner binding | FAIL-CLOSED / unavailable; no binding implementation | Accept cardinality, installation scope, attended bootstrap, binding transfer/change, independent recovery, emergency disable, revocation and restore; then implement and test |
| Assignment maker-checker | Bounded policy PoC PASS | Real trusted principal/binding, exact permission, request fingerprint/version, idempotency and one real transaction consumer |
| Warehouse-Branch history | Bounded append-only PostgreSQL PoC PASS | Production Organization table/access control and operational retention after G1 |
| Idempotency / unknown commit | Bounded concurrency PoC PASS | Real Organization command receipt/result persistence and consumer-level unknown-commit recovery after G1 |
| Local lock ordering | Bounded local candidate envelope/race proofs PASS | Platform and affected owners must accept a single total order covering all participating modules and revocation linearization |
| AuditIntent/Outbox | Bounded local atomicity/replay/quarantine/claim PoCs PASS | AuditSecurity canonical API, authenticated durable ACK verification, retry/quarantine/reconciliation worker, runbook and alerts |
| Outbox worker privilege | Bounded synthetic object/column ACL PoC PASS | Owner decision on a sixth logical identity or explicit continued production disablement; no credential/role provisioned |
| O20 DB identity matrix | Five logical N.6 identity categories remain unchanged; worker/runtime LOGIN shims are PoCs | Explicit owner/Platform/DB/Security approval for physical bindings, secrets, pools, database targets and any sixth identity |
| Platform identifier standard | UUIDv7 generation/serialization/round-trip PoCs PASS | Platform acceptance of UUIDv7 or alternative, multi-host clock/rollback, cross-language and compatibility requirements |
| Platform global lock order | Local candidate tests PASS; proposal remains NON-NORMATIVE | Platform plus affected module-owner acceptance of total ranks, isolation, retries, cancellation and full cross-module race evidence |
| RowScope | Current exact-head inventory: no Branch/Warehouse consumer | Do not invent consumer or grant. Re-inventory and implement SQL-before-materialization tests for each future authorized consumer |
| Legal schedule / Legal Hold operations | OD-05 baseline accepted; Iran source crosswalk v0.6 recorded; bounded eligibility PoC PASS | Iran counsel/records authority classifies operator and each record class; accepts schedule, trigger, hold authority/release and media scope |
| Threat model | Threat-to-test map exists; internal draft only | Independent Security review, residual-risk disposition and exact-SHA retests |
| O21 production trust | Disposable hash-chain and worker ACL proofs PASS | Production AuditSecurity writer/API, actual external Object Lock Compliance anchor, KMS/HSM custody, verifier, restore continuity, alerting and runbook |
| Independent exact-SHA review | Requested; no review submission recorded | Independent reviewer must inspect this exact SHA and record findings, severity, disposition and retests |
| WG-0 / Organization migration | NOT RESERVED | Separate WG-0 reservation after all prerequisites |
| Organization G1 | NOT GRANTED | Explicit Product Owner authorization after module-owner, Platform, DB, Security, legal and independent-review gates |

## 3. Remaining decisions that CI cannot grant

1. **IdentityAccess owner:** accept public Contracts version 1.2 semantics and the transaction-scoped guard.
2. **Platform and affected module owners:** accept one global lock rank/linearization protocol; the local candidate order is not normative.
3. **Product Owner + IdentityAccess + Platform + Security:** decide Product Owner binding cardinality, installation scope, attended first designation, independent transfer/recovery, emergency disable and restoration.
4. **Product Owner + Platform + DB + Security:** decide whether a dedicated sixth outbox-worker logical identity is approved. Until then production delivery remains disabled and N.6 remains the accepted five-identity model.
5. **Platform:** accept UUIDv7 or another generator standard, including multi-instance, clock and compatibility semantics.
6. **AuditSecurity owner:** accept canonical event schema, durable ACK, event registration, worker authentication, retry/quarantine/replay, retention and Legal Hold.
7. **Records/legal authority:** classify the actual operator and record classes, then approve a jurisdiction-specific schedule and Hold/media procedures. No numeric duration is inferred here.
8. **Independent Security/Architecture reviewer:** complete review against this exact SHA.
9. **WG-0 and Product Owner:** reserve Organization migration work and separately authorize G1 only after the above.

## 4. Branch and release safety

- Exact source/test branch head: `4bcb227109d6a3349a5cbfb7f133f3623f5ef9cb`.
- `main` remains `2a52a8b0629769953ff9671a0cf220401614ce9f`; no direct main change was made.
- PR #92 remains Draft / open / unmerged and is based on the current main containing PR #86.
- PR #91 remains open / non-Draft / unmerged as the full revert proposal for prematurely merged PR #86. If #91 merges first, #92 must be reconciled/rebased onto the resulting main and all required CI rerun.
- No production Organization schema/runtime/route/migration, permission seed/grant, Product Owner binding, worker credential, extra service identity, ReportingReadOnly grant, KMS/HSM or WORM resource was created.

**Final disposition:** all bounded exact-head CI and PostgreSQL proof steps listed above are PASS. This closes the current source/test validation slice only. External owner/platform/legal/production/independent gates remain OPEN. **O20-E, O-20 and O-21 remain OPEN; Organization G1 remains NOT READY / NOT GRANTED.**

**END — O20-E Technical Blocker Closure Matrix v2.0**


## 5. O20-E command idempotency recovery extensions — 2026-11-23 (v2.1)

The existing disposable PostgreSQL test `O20OrganizationIdempotencyConcurrencyPostgresProofTests` has been extended to cover two missing failure/recovery shapes:
- successful commit followed by a deliberately discarded caller-visible result, then same-key/same-fingerprint replay returning the receipt's original AssignmentId without a second assignment fact;
- failure before commit after receipt and assignment writes were staged, with rollback proving neither persists, followed by safe retry that creates exactly one receipt and assignment.

The same method still verifies two concurrent same-key/same-fingerprint callers converge, same-key/different-fingerprint replay is denied, and different keys for the same business assignment converge on one active fact.

The existing explicit workflow filter already invokes this test class. The source file checksum has been refreshed in `SHA256SUMS.txt`. The new exact source/test head is pending build and PostgreSQL validation; the previous 4bcb exact-head green result does not validate this new method expansion.

The unknown-commit case deliberately discards the first result after commit; it does not simulate a network packet loss or kill the backend exactly at commit acknowledgement. The rollback case explicitly rolls back the transaction and is not a process-crash test. They improve the policy fixture but do not constitute full production failure recovery.

**Updated disposition:** previous 4bcb bounded exact-head CI remains PASS for its tested source/test set; the new idempotency recovery additions are CI-PENDING. O20-E, O-20 and O-21 remain OPEN; G1 NOT GRANTED.

**END — O20-E blocker closure matrix v2.1**
