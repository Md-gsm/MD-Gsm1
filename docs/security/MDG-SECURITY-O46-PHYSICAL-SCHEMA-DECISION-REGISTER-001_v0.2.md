# O-46 — Physical Schema Decision Register for WG-0

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001 |
| Version / Date | 0.2 / 2026-11-23 |
| Status | **OWNER-APPROVED CHALLENGE-ISSUANCE CONSTRAINT RECORDED; OTHER SCHEMA DECISIONS / IMPLEMENTATION GATES OPEN** |
| Related design | Physical Schema and ACL Proposal v0.2; Challenge Issuance Protocol Proposal v0.2; Provisioning Package Contract v0.2 |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001 v0.1 |

## 1. Disposition summary

This register records the Product Owner's exact bounded approval for the challenge-issuance caller/function boundary. It does not convert unresolved schema details, engineering recommendations or proof obligations into approvals. Product Owner approval is a policy/design constraint, not test evidence, independent review, migration reservation or implementation authorization.

| # | Question | Current disposition | Rationale / boundary |
|---:|---|---|---|
| 1 | Canonical tables, columns and payload fingerprint | **TABLE SET RECOMMENDED; EXACT PHYSICAL CONTRACT / FINGERPRINT OPEN** | The proposed IdentityAccess-owned tables remain `identity.product_owner_bindings`, `identity.bootstrap_challenges` and `identity.bootstrap_operations`. Challenge schema now includes `challenge_request_id` as the issuer idempotency key, unique within InstallationId + SetupOperationId. The receipt fingerprint must be a versioned canonical envelope binding all material Owner/account/profile inputs and the final password-hash output; it must not contain raw password, raw challenge, password verifier or secret. Keyed HMAC-SHA-256 is preferred only if a separately managed key can be supplied without expanding the approved principal/secret boundary. Otherwise an explicit threat review is required; no unkeyed fingerprint of a low-entropy profile/password-only payload is accepted as final. |
| 2 | Challenge issuance ownership and caller | **OWNER-APPROVED CONSTRAINT — DESIGN ONLY** | IdentityAccess owns issue, expiry, verifier persistence, revocation, single-use consumption and Owner claim. Two separate fixed-signature SECURITY DEFINER routines are approved: `identity_bootstrap.issue_initial_owner_challenge_v1(...)` and `identity_bootstrap.claim_initial_product_owner_v1(...)`. Both are executable only by the existing `mdgsm_bootstrap_executor`; no new principal; no direct executor table/sequence grants; no generic dispatcher. The function-owner delta includes only exact challenge INSERT columns and conditional UPDATE of `revoked_at_utc`, in addition to the previously proposed claim verification/consumption privileges. Decision source: PR #92 comment 7110645790. No routine or grant is installed. |
| 3 | Windows operator SID attestation | **RECOMMENDED BASELINE; PROOF OPEN** | Setup Service obtains the OS-authenticated client token over the local Named Pipe, extracts the SID only for identity inspection, reverts impersonation, and supplies the attested value over its authenticated database channel. It does not accept SID from Wizard payload. Setup Service re-attests the same SID on claim. DACL, process authentication, channel exclusivity and PostgreSQL trust-chain proof remain NOT RUN. |
| 4 | Initial Owner profile fields | **OWNER INPUT REQUIRED; OPEN** | Existing `identity.accounts` contains nullable email and phone but no canonical display/profile name columns. The accepted Wizard policy requires the final profile but has not specified exact fields or requiredness. Do not infer that email/phone are sufficient or invent fields. This is a blocker to final Account input contract and any migration DDL that depends on profile storage. |
| 5 | UUIDv7 generation and audit vocabulary | **RECOMMENDED BASELINE; CODE/DB PROOF OPEN** | Platform supplies stable InstallationId and SetupOperationId; IdentityAccess generates AccountId and audit EventId through the approved UUIDv7 provider at a trusted server boundary. Proposed audit event is `identity.product_owner.bootstrap_claimed`, outcome `success`. The audit row is an in-transaction intent, not proof of external AuditSecurity durable acknowledgement. Generator compatibility and event vocabulary require tests. |
| 6 | `bootstrap_completed_at_utc` semantics | **RECOMMENDED BASELINE; MIGRATION PROOF OPEN** | Existence of the immutable `identity.product_owner_bindings` row is authoritative. The singleton marker is compatibility/diagnostic only, is set atomically with the claim, and never authorizes Owner creation alone. Any mismatch among marker, binding and receipt is an integrity failure and fail-closed. |
| 7 | PostgreSQL 18 evidence | **POST-RESERVATION ACCEPTANCE EVIDENCE; NOT RUN** | Column-level ACL, FK/index/trigger/RLS behavior, SECURITY DEFINER semantics, the aggregate shared function-owner privilege boundary, fixed routine-body separation, lock serialization, concurrent issue-vs-claim, concurrent claims, idempotent retries, unknown commits, expiry/replay, revocation, rollback and no-second-Owner behavior require disposable PostgreSQL 18 proof. All remain NOT RUN / NOT VERIFIED. |

## 2. Accepted challenge protocol — bounded interpretation

The owner-approved protocol is exactly the two-routine design in Challenge Issuance Protocol Proposal v0.2:

- Setup Service creates a 32-byte challenge using the OS CSPRNG. The raw value remains in memory and is delivered once to the local Wizard over the authenticated Named Pipe. Only its SHA-256 digest is sent to PostgreSQL.
- The issuer's typed inputs include InstallationId, SetupOperationId, OS-attested operator SID, exactly 32-byte digest and a challenge_request_id UUID. Purpose and maximum 30-minute lifetime are constants controlled by the routine; PostgreSQL time governs expiry.
- The issuer is idempotent for the same challenge_request_id and identical immutable inputs. Reuse with different inputs fails closed. A new request ID may atomically revoke one prior unconsumed challenge for the same setup operation by setting only revoked_at_utc, then insert a new verifier-only challenge.
- The claim routine accepts only the digest, verifies installation/operation/purpose/SID/binding/expiry/revocation/consumption, and atomically creates the Account, immutable binding, append-only receipt and audit intent while consuming the challenge.
- Both routines take the same InstallationId-scoped serialization lock before accessing bootstrap state. The exact lock primitive/key derivation, namespace, fixed data-access order and PostgreSQL 18 race proof remain open pre-DDL items.
- Both routines share the same NOLOGIN function-owner role. PostgreSQL ACLs are therefore aggregated at role level, not individually scoped per function; per-function EXECUTE does not imply per-function table grants. The issuer/claim side-effect split is code-integrity separation under fixed, reviewed function definitions and signed package fingerprints, not independent database privilege sandboxes. Static body review and adversarial PostgreSQL 18 tests plus explicit independent Security/Architecture risk acceptance are mandatory. If reviewers require DB-enforced privilege isolation per routine, return to Product Owner decision; no extra role is authorized.

This approval does not decide Owner profile fields, payload-fingerprint key custody, release-signing trust anchor, executor secret custody, HBA/loopback configuration, exact function signatures, or the PostgreSQL lock primitive.

## 3. WG-0 migration reservation

Issue #93 requests WG-0 consideration of candidate migration **0010**, proposed path:

`database/migrations/0010_identityaccess_owner_bootstrap.sql`

The requested migration remains IdentityAccess-owned and table-only: three bootstrap state tables plus constraints/indexes, including `challenge_request_id` idempotency state. It excludes routines, SECURITY DEFINER objects, roles/principals, memberships, grants, LOGIN activation, secrets, Account/Owner seed, role/permission seed, N.6 changes, Recovery activation and business records.

0009 remains reserved for Accounting. Candidate 0010 is **not reserved** until WG-0 verifies global number/path uniqueness, exact scope and dependencies and records the result in the authoritative registry. The issue must be updated to reflect the owner-approved two-routine boundary and challenge_request_id column; that does not expand the migration to include routines.

## 4. Remaining ordered gates

1. Product Owner must define exact initial Owner profile fields and requiredness before final Account input/schema design.
2. Complete payload fingerprint canonicalization and its key-management/threat model without widening the approved principal boundary.
3. Freeze exact table/column constraints, issuer/claim signatures, shared lock primitive/key derivation and fixed data-access order.
4. WG-0 decision on migration 0010 and exact path; no reservation is assumed.
5. Independent Security/Architecture review of schema, aggregate function-owner ACL, routine bodies, package manifest and Windows/DB trust boundary.
6. Only after the required reservations and approvals: author DDL and routines under controlled change; run disposable PostgreSQL 18 catalog/concurrency/idempotency proofs and Windows central-server proofs.
7. Exact-head CI, complete evidence and independent reviewer acceptance. PR #92 remains Draft until all required gates are complete.

## 5. Current status

- Two-routine challenge protocol: **OWNER-APPROVED CONSTRAINT; DESIGN ONLY**.
- Physical tables/columns and full ACL: **DESIGN PROPOSAL; NOT IMPLEMENTED/CATALOG-VERIFIED**.
- Owner profile contract: **OPEN**.
- Payload fingerprint/key strategy: **OPEN**.
- Shared serialization lock exactness: **OPEN / PROOF REQUIRED**.
- WG-0 issue #93: **OPEN; migration 0010 NOT RESERVED**.
- SQL/DDL/routines/principals/grants/secrets: **NOT AUTHORIZED / NOT CREATED**.
- PostgreSQL 18 and Windows proof: **NOT RUN**.
- O46-G04 and IdentityAccess G1: **OPEN / NOT GRANTED**.
- PR #92: **OPEN, DRAFT, UNMERGED**.

**END — MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001 v0.2**
