# Stage 08 Security Register — O20 / O21 Exact-Head Validation

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 6.9 / 2026-11-23 |
| Supersedes | v6.8 |
| Supersedes | v6.6 |
| Status | OWNER POLICY SUCCESSORS RECORDED; CONSOLIDATED EXACT-HEAD CI PENDING; O20-E / O-20 / O-21 OPEN; G1 NOT GRANTED |
| Branch | security/o20-o21-final-closure |
| Previous exact-green source/test SHA | `4bcb227109d6a3349a5cbfb7f133f3623f5ef9cb` |
| Consolidated successor head | To be taken from final PR #92 head after document/checksum commits |
| PR | #92 Draft / unmerged |

## 1. Previous exact-green validation (historical baseline)

The exact source/test SHA `4bcb227109d6a3349a5cbfb7f133f3623f5ef9cb` completed the full required workflow set:
- MD-Gsm Persons Module Gates run 45070130837 — SUCCESS, 13/13 jobs;
- CodeQL run 45070130838 — SUCCESS;
- Windows Preview Installer run 45070130892 — SUCCESS;
- Project Charter Owner Gate run 45070956303 — SUCCESS.

The primary run confirmed Release build 0 warnings/0 errors, .NET tests 249/249, marked PostgreSQL 18 base integration 62/62, Linux and Windows application-owned migrations 8 applied first execution / 0 on repeat, and SHA256 source validation 704/704 tracked files. All explicit O20-E / IdentityAccess / O20-D / O20-F / O20-G / O21 / Accounting PostgreSQL proof steps were completed successfully.

### 1.1 Specific O20-E proof methods observed

- All pinned OD-01 White_Space/Cc/Cf scalar conformance, .NET/PostgreSQL canonicalization equality and OD-02/OD-03 Warehouse identity/lifecycle methods passed.
- Warehouse relationship-history append-only proof passed.
- Legal Hold/retention eligibility and concurrent hold-placement serialization methods passed.
- Cross-aggregate Branch/Warehouse/assignment tests passed, including bounded lock-timeout retry, Branch/Warehouse reassignment race and stale approval rejection.
- Non-owner maker-checker and synthetic bound-owner direct-policy test passed.
- Local AuditIntent/Outbox atomicity, EventId/hash replay, payload-conflict quarantine and competing worker-claim tests passed.
- Assignment idempotency and three deterministic local lock-order candidate tests passed.
- UUIDv7 PostgreSQL native UUID round-trip passed.
- Actual IdentityAccess live-session, permission-revocation linearization and transaction-scoped guard tests passed. The transaction guard proof observed seven concurrent account/session/membership/grant/permission mutations blocked until the supplied transaction committed.
- O21 hash-chain and O20 worker least-privilege object/column ACL proofs passed.

These are disposable fixture proofs only; no production Organization UoW, AuditSecurity durable ACK endpoint, trusted Product Owner binding, Platform global lock-order acceptance, legal disposal service or G1 authorization is implied.

## 2. Current gate state

- OD-01 through OD-05 owner policy decisions remain OWNER-ACCEPTED within their stated scope; bounded PostgreSQL proofs pass.
- IdentityAccess Contracts and transaction guard are source-published and exact-head tested; IdentityAccess module-owner acceptance remains OPEN.
- The Platform global lock order is still a proposal. Local candidate ordering proofs do not establish a total cross-module rank.
- Product Owner binding remains unavailable/fail-closed; owner policy now fixes one immutable, non-transferable installation-created AccountId and same-principal-only recovery. No binding implementation exists.
- Organization management permission namespace/role mapping remains unapproved; no unapproved `organization.*` key has been added.
- N.6 five logical DB identity categories remain unchanged. The outbox worker is a synthetic ACL proof only; no sixth identity/secret/grant is approved or provisioned.
- Production AuditSecurity API/durable ACK, retry/quarantine worker, WORM Object Lock Compliance and KMS/HSM custody are absent.
- Owner internal retention baselines are recorded: 10 years for listed commercial/financial evidence and sensitive permission/role/ownership changes, 2 years for purely technical/security logs, and Legal Hold until formal release. Iran counsel/records authority must validate actual operator/record classes and lawful operations; automatic deletion remains disabled.
- No current Organization/Inventory/Reporting Branch/Warehouse RowScope consumer exists; ReportingReadOnly remains absent.
- Independent exact-SHA review is requested but not submitted. WG-0 and G1 remain not authorized.

## 3. PR / main coordination

- Main remains `2a52a8b0629769953ff9671a0cf220401614ce9f`; no direct main change was made.
- PR #91 is open, non-Draft and unmerged as the full revert proposal for PR #86.
- PR #92 is Draft, open and unmerged, based on current pre-revert main. If #91 merges first, #92 must be reconciled/rebased and all validation repeated.
- Reviewer `mrdeljoe4` is requested on PR #92; no review submission exists at this snapshot.

## 4. Final disposition

The prior 4bcb exact source/test CI validation was green for its tested source set. New owner-aligned policy and O20-F test changes are on the consolidated successor branch and require exact-head validation. This does not complete external or production gates. O20-E, O-20 and O-21 remain OPEN. Organization G1 is NOT READY / NOT GRANTED. No production Organization schema/runtime/migration, permission seed, database grant, owner binding, worker identity, credential, WORM/KMS resource, ReportingReadOnly role or production data access was created.

**END — Stage 08 register v6.7**


## 5. Idempotency recovery proof expansion — 2026-11-23 (v6.8)

Added two cases to the existing explicitly invoked `O20OrganizationIdempotencyConcurrencyPostgresProofTests`:
1. The command commits its receipt and assignment, the first caller-visible return is discarded, and a same-key/same-fingerprint replay recovers the original AssignmentId without duplicating the assignment.
2. A transaction stages both receipt and assignment then rolls back before commit; neither persists, and a retry then creates exactly one durable result.

The previous exact-head 4bcb source/test validation remains PASS for the code it covered. This test expansion and its new SHA256 manifest entry are on a newer branch head and require fresh exact-head CI. The test is a bounded disposable fixture and does not claim production process/network crash coverage.

**Current disposition:** idempotency unknown-commit-style replay and pre-commit rollback PoCs added; exact-head CI pending. No production Organization command runtime or UoW is authorized. O20-E/O-20/O-21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v6.9**


## 6. Owner policy successors and consolidated validation — 2026-11-23 (v6.9)

The Product Owner policy responses were recorded in decision packet v0.6. Controlled successors on this branch:
- Product Owner binding proposal v0.2 records the immutable installation-created owner AccountId and same-principal-only recovery. No transfer, successor or replacement path is accepted.
- O20-E data ownership v0.7 and Organization logical/physical addenda v0.7/v1.4 reconcile the narrow Product Owner self-assignment/direct execution exception; all non-owner self-assignment remains prohibited and requires a distinct second approver for assignment create/change/revoke.
- O20-F lifecycle v0.4 and owner decision acceptance v0.3 change ordinary Emergency Access to one independent approver and a 60-minute maximum, with synchronous expiry. Isolated Backup restore still requires two independent approvers; O-31 two-part offline recovery remains.
- O20-F disposable PostgreSQL proof now enforces the approved-operation allow-list, one independent approver for ordinary operations, two for Backup restore, the 60-minute ceiling, 3601-second rejection, synchronous expiry, revocation and local audit-intent rollback. It remains a disposable fixture; it does not prove live IdentityAccess principal resolution or production database/API capability.
- GOV-OWNER-AUTH-001 v0.1 records the owner's final product decision authority; governing Master integration and formally assigned specialist owners remain open.

The consolidated successor branch includes the above policy, design, test, blocker-matrix and readiness updates plus refreshed SHA256 entries. All required CI must complete on the final PR #92 head recorded in GitHub; prior 4bcb success is historical and does not validate these changes.

PR #91 remains open and unmerged; PR #92 remains Draft/open/unmerged. No production Organization schema/runtime/grant, Product Owner binding, Emergency Access grant/LOGIN, Worker credential, WORM/KMS resource, reporting grant or deletion worker was created. Independent exact-SHA review has not been submitted. O20-E/O-20/O-21 remain OPEN; G1 NOT GRANTED.

**END — Stage 08 register v6.9**
