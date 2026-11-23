# Stage 08 Security Register — O20 / O21 Exact-Head Validation

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 6.8 / 2026-11-23 |
| Supersedes | v6.7 |
| Supersedes | v6.6 |
| Status | EXACT-HEAD BOUNDED CI PASS; O20-E / O-20 / O-21 OPEN; G1 NOT GRANTED |
| Branch | security/o20-o21-final-closure |
| Exact source/test SHA | `4bcb227109d6a3349a5cbfb7f133f3623f5ef9cb` |
| PR | #92 Draft / unmerged |

## 1. Latest exact-head validation

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
- Product Owner binding remains unavailable/fail-closed; cardinality, enrollment, transfer, independent recovery, emergency disable and restore are not implemented.
- Organization management permission namespace/role mapping remains unapproved; no unapproved `organization.*` key has been added.
- N.6 five logical DB identity categories remain unchanged. The outbox worker is a synthetic ACL proof only; no sixth identity/secret/grant is approved or provisioned.
- Production AuditSecurity API/durable ACK, retry/quarantine worker, WORM Object Lock Compliance and KMS/HSM custody are absent.
- OD-05 legal retention duration is unset; the legal crosswalk does not classify the actual operator/record classes. Automatic deletion remains disabled.
- No current Organization/Inventory/Reporting Branch/Warehouse RowScope consumer exists; ReportingReadOnly remains absent.
- Independent exact-SHA review is requested but not submitted. WG-0 and G1 remain not authorized.

## 3. PR / main coordination

- Main remains `2a52a8b0629769953ff9671a0cf220401614ce9f`; no direct main change was made.
- PR #91 is open, non-Draft and unmerged as the full revert proposal for PR #86.
- PR #92 is Draft, open and unmerged, based on current pre-revert main. If #91 merges first, #92 must be reconciled/rebased and all validation repeated.
- Reviewer `mrdeljoe4` is requested on PR #92; no review submission exists at this snapshot.

## 4. Final disposition

The latest exact source/test CI validation is complete and green. This does not complete the external or production gates. O20-E, O-20 and O-21 remain OPEN. Organization G1 is NOT READY / NOT GRANTED. No production Organization schema/runtime/migration, permission seed, database grant, owner binding, worker identity, credential, WORM/KMS resource, ReportingReadOnly role or production data access was created.

**END — Stage 08 register v6.7**


## 5. Idempotency recovery proof expansion — 2026-11-23 (v6.8)

Added two cases to the existing explicitly invoked `O20OrganizationIdempotencyConcurrencyPostgresProofTests`:
1. The command commits its receipt and assignment, the first caller-visible return is discarded, and a same-key/same-fingerprint replay recovers the original AssignmentId without duplicating the assignment.
2. A transaction stages both receipt and assignment then rolls back before commit; neither persists, and a retry then creates exactly one durable result.

The previous exact-head 4bcb source/test validation remains PASS for the code it covered. This test expansion and its new SHA256 manifest entry are on a newer branch head and require fresh exact-head CI. The test is a bounded disposable fixture and does not claim production process/network crash coverage.

**Current disposition:** idempotency unknown-commit-style replay and pre-commit rollback PoCs added; exact-head CI pending. No production Organization command runtime or UoW is authorized. O20-E/O-20/O-21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v6.8**
