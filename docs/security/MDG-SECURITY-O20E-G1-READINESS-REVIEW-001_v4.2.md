# O20-E G1 Readiness Review — Exact-Head Technical Validation and Remaining Release Gates

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 4.2 / 2026-11-23 |
| Supersedes | v4.1 |
| Supersedes | v3.9 |
| Status | OWNER POLICY RECONCILIATION RECORDED; CURRENT EXACT-HEAD CI IN PROGRESS; O20-E OPEN PENDING EXTERNAL OWNER / PLATFORM / LEGAL / PRODUCTION / INDEPENDENT GATES |
| Branch | security/o20-o21-final-closure |
| Previous exact-green source/test SHA | `4bcb227109d6a3349a5cbfb7f133f3623f5ef9cb` |
| Current source/test head | `604b411b113c0a1bcd861f8aaa42ab6e5a1514f0` — required workflows in progress |
| PR | #92 Draft / unmerged |
| Governing blocker matrix | MDG-SECURITY-O20E-BLOCKER-CLOSURE-MATRIX-001 v2.2 |
| Decision packet | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6 |

## 1. Previous exact-green verification (historical baseline)

All four required GitHub Actions gates passed on the exact SHA above:
- Primary workflow 45070130837: SUCCESS, all 13/13 jobs;
- CodeQL 45070130838: SUCCESS;
- Windows Persons Preview Installer 45070130892: SUCCESS;
- Project Charter Owner Gate 45070956303: SUCCESS.

Verified build/test/database outputs:
- .NET Release build: 0 warnings / 0 errors;
- .NET test suite: 249/249 passed;
- PostgreSQL 18 base integration suite: 62/62 passed;
- explicit O20-E policy, history, retention/Legal Hold, cross-aggregate concurrency, approval, outbox, idempotency, deterministic lock-order and UUIDv7 round-trip tests passed;
- actual IdentityAccess request-time, revocation-linearization and transaction-scoped guard proofs passed against the marked disposable IdentityAccess schema;
- O21 hash-chain PoC, outbox worker least-privilege ACL, O20-D backup/restore, O20-G effective privilege inventory, O20-F emergency lifecycle and Accounting AP-03/AP-04 proofs passed;
- Linux and Windows PostgreSQL migrations: 8 applied on first run and 0 new on repeat;
- SHA256SUMS verifier: 704 tracked files matched.

The exact method-level outcomes and boundaries are recorded in blocker matrix v2.0. No result above is interpreted as approval of a production Organization module or G1.

## 2. Current ordered remaining blockers

| Order | Blocker | Current state | Required closure |
|---:|---|---|---|
| 1 | IdentityAccess public contract | Source-published; exact CI PASS | IdentityAccess module-owner acceptance of Contracts v1.2 and its public transaction guard |
| 2 | Commit-time authorization | Actual adapter + seven-mutation/multi-role disposable proof PASS | Platform total order acceptance and a real, G1-authorized Organization UoW participant; no current consumer |
| 3 | Product Owner authority | Owner policy accepted: installation-created immutable/non-transferable AccountId; runtime remains fail-closed Unavailable; no production binding | IdentityAccess/Platform/Security acceptance and proof of bootstrap, protected binding persistence, same-AccountId-only recovery and backup/restore continuity; no transfer/successor/replacement path |
| 4 | Assignment approval | Maker-checker synthetic PostgreSQL PoC PASS | Real IdentityAccess principal/permission/binding and command fingerprint/version/idempotency in one authorized local transaction |
| 5 | Permission catalog | Exact approved matrix and immutable views; CI PASS | IdentityAccess owner acceptance; Organization permission namespace remains unapproved and absent |
| 6 | History / retention | Append-only history and fail-closed Legal Hold eligibility PoCs PASS; owner internal 10-year commercial/financial and sensitive permission/role/ownership baselines plus 2-year technical/security baseline recorded | Counsel/records authority must classify operator and record classes, confirm legal basis/triggers, Hold operations, backups/replicas/exports/media and lawful disposal; automatic deletion disabled |
| 7 | Assignment concurrency | Bounded cross-aggregate, idempotency and deterministic local order PoCs PASS | Platform global ranks and full cross-module timeout/deadlock/cancel/crash/unknown-commit/replay approval |
| 8 | AuditSecurity / outbox | Synthetic atomicity, replay, quarantine and worker ACL proofs PASS | Real canonical AuditSecurity API/durable ACK, event registration, worker identity approval, retry/quarantine/reconciliation and operations |
| 9 | DB identity matrix | Five logical N.6 categories unchanged; role/LOGIN proofs are disposable | Owner-approved physical login/pool/secret/target DB binding and sixth worker identity decision or continued disabled delivery |
| 10 | Platform identifiers | UUIDv7 generator/round-trip PoCs PASS | Platform acceptance of standard, clock/rollback, multi-instance, cross-language and compatibility |
| 11 | RowScope | No current Branch/Warehouse reporting consumer | Keep ReportingReadOnly absent; re-inventory and SQL RowScope tests on every future authorized consumer |
| 12 | Legal retention | OD-05 baseline accepted; legal-source crosswalk recorded | Counsel/records authority classifies operator and each record class, approves period/trigger/Hold/media schedule |
| 13 | O21 production | Disposable hash-chain and worker ACL tests PASS | Production AuditSecurity writer, external WORM Compliance anchor, KMS/HSM custody, independent verifier, restore, alerts/runbook |
| 14 | Independent review / G1 | No submitted independent review; G1 not granted | Independent exact-SHA review, retests, WG-0 reservation and separate explicit Product Owner G1 authorization |

## 3. Production boundary

The codebase still has no executable Organization module or Organization UnitOfWork. The source/test branch must not create one before the G1 implementation gate. Synthetic Branch/Warehouse/assignment tables in the PostgreSQL PoCs are deliberately test-only. The actual IdentityAccess transaction guard is implemented inside IdentityAccess and exposed through the Contracts assembly; it has no production Organization consumer.

The accepted Product Owner exception remains narrow: only Organization Branch/Warehouse assignment create/change/revoke, including self-assignment, authenticated by the immutable installation-scoped owner binding. It does not grant credential/session, IdentityAccess role/permission or RowScope bypass. No client owner flag or super_admin role is an owner proof. Because the binding lifecycle is not implemented, runtime owner resolution remains Unavailable and fails closed.

OD-05 remains retain-by-default/no-auto-delete. Product Owner internal baselines are recorded: 10 years for listed commercial/financial evidence and sensitive permission/role/ownership changes, 2 years for purely technical/security logs, and Legal Hold until formal release. These are not final legal classifications of the actual operator or every record row; counsel/records authority must validate scopes, triggers, holds and media treatment. Automatic deletion remains disabled.

## 4. PR coordination and release state

- PR #92 is Draft, open and unmerged. On current exact head `604b411b113c0a1bcd861f8aaa42ab6e5a1514f0`, CodeQL, Persons Module Gates and Windows Preview are in progress; Project Charter Owner Gate is successful.
- PR #91 is open, non-Draft and unmerged as the full revert of prematurely merged PR #86.
- PR #92 was created from the pre-revert main. If PR #91 merges first, reconcile/rebase #92 onto the resulting main and rerun all workflows.
- PR #92 has reviewer `mrdeljoe4` requested, but the review list is empty at this snapshot.
- Main was not modified; no production role, grant, credential, schema, migration, worker, Product Owner binding, KMS/HSM or WORM provider was provisioned.

**Disposition:** the previous 4bcb source/test baseline remains historical PASS. The current 604b411 head includes new owner-aligned policy and O20-F proof changes; exact-head validation is in progress. O20-E, O-20 and O-21 remain OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E Readiness Review v4.0**


## 5. Additional idempotency recovery proof — current validation pending (2026-11-23)

The current branch adds disposable PostgreSQL coverage for an unknown-commit-style replay and pre-commit rollback to the existing O20-E idempotency test:
- same-key retry after the original transaction committed but the first caller-visible result is discarded returns the durable original AssignmentId and does not duplicate the fact;
- explicit rollback after staging receipt and assignment leaves no state; retry then creates one durable result.

The test is not a production crash/transport test and does not replace the real Organization command processor, which is still absent. The explicit O20-E idempotency class-filter step already exists in the primary workflow and will validate the expanded test when run on the new head.

**Disposition:** previous exact-head validation remains PASS for SHA 4bcb…; the expanded idempotency proof source is added and awaits current exact-head CI. Production UoW, Platform global order, IdentityAccess owner acceptance, Product Owner binding lifecycle, AuditSecurity production integration, legal schedule, independent review and G1 remain OPEN.

**END — O20-E readiness review v4.2**


## 6. Owner-policy alignment and current exact-head state — 2026-11-23 (v4.2)

The controlling Product Owner decisions are in decision packet v0.6 and PR #92 comment 7094120301. The current source branch has reconciled the documents as follows:
- Binding: Product Owner is the account created during installation, bound by immutable AccountId to the installation; no transfer, successor or replacement. Recovery only restores the same account; unavailable continuity denies privileged owner actions.
- Assignment authority: the bound Product Owner may self-assign and manage any Branch/Warehouse assignment without a second approver. Other users cannot self-assign and require a distinct second approver. The exception grants no IdentityAccess credential/role/permission mutation or RowScope bypass.
- Emergency Access: one eligible independent approver for ordinary allow-listed operations; maximum 60 minutes; synchronous expiry; single activation. Two-person approval remains for isolated Backup restore; O-31 offline two-part recovery remains unchanged.
- The disposable PostgreSQL proof now tests the 60-minute boundary, 3601-second rejection, explicit operation allow-list, independent approver rules, Product Owner approver path, Backup restore two-person path, expiry and local audit-intent atomicity. Production identity/capability and canonical AuditSecurity ACK remain unproven.
- GOV-OWNER-AUTH-001 v0.1 is registered as a controlled architecture change request, not yet integrated into the governing Master.

Current exact PR head `604b411b113c0a1bcd861f8aaa42ab6e5a1514f0`: CodeQL, Persons Module Gates and Windows Preview Installer are in progress; Project Charter Owner Gate succeeded. The old 4bcb green result does not validate these changes.

No production owner binding, Emergency Access grant/LOGIN, Worker credential, External WORM/KMS resource, Organization runtime/schema, report grant or automatic disposal was provisioned. Independent exact-SHA review is still not submitted. O20-E/O-20/O-21 remain OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v4.2**
