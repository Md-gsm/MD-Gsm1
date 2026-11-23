# O-46 — Physical Schema and Bootstrap Boundary Reconciliation Register

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001 |
| Version / Date | 0.6 / 2026-11-24 |
| Status | **OWNER-APPROVED PROFILE, JOURNAL AND TWO-PRINCIPAL RECONCILIATION TARGETS RECORDED; PHYSICAL/SECURITY PROOFS OPEN; NO IMPLEMENTATION AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | v0.5 as the current cross-document index |

## 1. Controlling owner decisions

The following are binding design constraints, not implementation authorization:

1. Use the already-existing PostgreSQL cluster-administrator/superuser root only through a separately invoked, explicitly local maintenance operation on the central Windows Server. No new deployment LOGIN is created.
2. Exactly two PostgreSQL principals are accepted: `mdgsm_identity_bootstrap_fn_owner` (NOLOGIN) and `mdgsm_bootstrap_executor` (initially NOLOGIN, zero memberships).
3. Exactly two fixed-signature SECURITY DEFINER routines are approved as a design constraint: `identity_bootstrap.issue_initial_owner_challenge_v1(...)` and `identity_bootstrap.claim_initial_product_owner_v1(...)`. Both are owned by the function-owner; executor may receive only schema USAGE and EXECUTE on those exact signatures after separate activation authorization.
4. Executor has no direct table/sequence privilege, schema CREATE, DDL, role administration, membership or object ownership.
5. The function-owner's approved challenge-issuer ACL delta is exact challenge-column INSERT and conditional UPDATE of `revoked_at_utc` only, in addition to the already defined claim verification and conditional `consumed_at_utc) transition and exact Owner-claim writes.
6. PostgreSQL ACLs are aggregate at shared function-owner role level, not per function. Issuer/claim side-effect separation is therefore code-integrity controlled and requires fixed reviewed bodies, signed package integrity, separate definition fingerprints, adversarial PostgreSQL 18 proof and explicit independent Security/Architecture risk acceptance.
7. The Setup Named Pipe is solely the authenticated local Wizard↔Setup Service channel for operator SID attestation and one-time challenge delivery. It is not the privileged maintenance channel and never carries the cluster-superuser credential.

Owner decisions are recorded in Bootstrap DB Provisioning Decision v0.2, Bootstrap Database Principal Decision v0.2 and PR #92 comment 7110645790.

## 2. Current reconciled documents

The following v0.4 documents are the current reconciliation layer and must be read together:

- `MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001_v0.4.md` — three candidate IdentityAccess tables, exact role/routine boundaries, aggregate ACL caveat, migration separation and unresolved physical details.
- `MDG-SECURITY-O46-BOOTSTRAP-PROVISIONING-PACKAGE-CONTRACT-001_v0.4.md` — signed local maintenance package, typed operation surface, dormant state machine, install/verify/revoke and no automatic executor activation.
- `MDG-SECURITY-O46-CHALLENGE-ISSUANCE-PROTOCOL-PROPOSAL-001_v0.4.md` — two-routine issue/claim semantics, 32-byte CSPRNG challenge, SHA-256 verifier, one-use/idempotency, 30-minute maximum, operator SID binding and Setup Named Pipe security constraints.
- `MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-PATH-001_v0.4.md` — existing cluster-superuser maintenance path, role/routine provisioning, WG-0 migration boundary, executor activation separation and cleanup.

The earlier v0.2 documents remain historical design baselines. If wording differs, the v0.4 reconciliation documents control only within their stated scope; the explicit owner decisions remain authoritative.

## 3. Mandatory separation of channels and responsibilities

| Boundary | Owner / actor | Permitted purpose | Prohibited purpose |
|---|---|---|---|
| Privileged maintenance | Existing PostgreSQL cluster superuser, local interactive maintenance operation | Provision/verify the two roles; install/upgrade/verify exact routine pair; post-claim executor revocation | Wizard/Owner claim, challenge delivery, application runtime, remote/API/Named Pipe execution |
| Setup IPC | Setup Wizard and Setup Service over authenticated local Windows Named Pipe | Attest local operator SID; deliver raw challenge once; orchestrate local setup | Cluster credential custody, DDL, role creation, routine installation, remote clients |
| Bootstrap DB execution | `mdgsm_bootstrap_executor`, only after separate activation approval | Execute only the two approved routine signatures | Direct table/sequence access, DDL, role administration, generic dispatch |
| Schema migration | Existing DbMigrator and unchanged `migration_runner → mdgsm_schema_owner` SET-only path | Apply only WG-0-reserved IdentityAccess table/constraint migration | Function installation, role creation, migration membership expansion, owner claim |

The Windows Setup Named Pipe must meet the local IPC threat review: native `CreateNamedPipeW` with `PIPE_REJECT_REMOTE_CLIENTS`, explicit protected DACL and first-instance protection are the candidate; `CurrentUserOnly` is not an acceptable substitute for the required multi-SID ACL. Both peers must be authenticated. Exact SID enrollment and DACL/process proofs remain open.

## 4. Physical and migration disposition

Candidate IdentityAccess-owned objects remain:
- `identity.product_owner_bindings` — immutable original InstallationId-to-AccountId binding;
- `identity.bootstrap_challenges` — verifier-only, one-use, expiring/revocable challenge state;
- `identity.bootstrap_operations` — append-only idempotent claim receipt.

The Account profile input mapping is Product Owner-approved. The three-table column/constraint set and fingerprint v1 serialization are technical proposals for review, not Product Owner-approved implementation contracts. Lock primitive/key derivation, exact routine signatures, UUIDv7 provider proof, final ACL matrix and PostgreSQL 18 proof remain OPEN. Migration 0009 remains RESERVED for Accounting. Candidate 0010 is NOT RESERVED; WG-0 must allocate the authoritative number and approve the table-only migration before any SQL is written. No routine, principal, grant, credential or activation is included in that migration.

## 6. Remaining gates — all OPEN / NOT RUN

- Restart-safe preservation of the exact salted password-hash output/fingerprint across an uncertain claim and process restart; safe abandonment/revocation before any new SetupOperationId.

- Product Owner acceptance of the three-field Owner profile is recorded. Fingerprint v1, exact columns, serialization test vectors, routine signatures and data-access order remain proposed/open for review.
- Trusted operator SID attestation and Windows IPC DACL/process/remote-client proof.
- PostgreSQL 18 catalog/effective-privilege proof, including RLS, PUBLIC/default ACL, ownership and membership.
- Adversarial proof that fixed issuer and claim bodies preserve their distinct side-effect contracts under aggregate shared-role privileges.
- InstallationId-scoped lock primitive, collision analysis and issue-vs-claim/concurrent-claim tests.
- WG-0 reservation and migration registry update.
- Secret custody, SCRAM/client authentication, complete HBA ordering and negative remote/non-target database tests before executor LOGIN can even be considered.
- Independent Security/Architecture review of one exact SHA and explicit acceptance of residual risk.

## 5. Owner profile decision and technical proposals in v0.4

The Product Owner explicitly accepted the initial Owner profile as exactly three fields: `username`, `display_name`, and `email`. The Product Owner has not yet approved the fingerprint algorithm or the physical table contract. The username maps to the existing `login_name`/normalization contract; display_name is a new nullable-for-legacy `identity.accounts.display_name` column, required for the bootstrap account; email maps to the existing `identity.accounts.email`, required for bootstrap. Phone/mobile is excluded from the bootstrap form.

Payload fingerprint v1 is a technical proposal, not a Product Owner-approved decision; it is proposed as a domain-separated SHA-256 digest over the deterministic binary envelope: versioned domain tag; InstallationId, SetupOperationId and AccountId in RFC 4122 byte order; length-prefixed strict UTF-8 values for normalized username, validated display_name, validated email and exact stored password-hash output; and password_hash_version. No raw password, raw challenge or secret is included. Exact serialization and cross-language test vectors remain proof obligations.

The three candidate tables and exact column/constraint proposal are in Physical Schema v0.4 and remain subject to technical review. The fingerprint serialization is proposed, not owner-approved. Restart-safe idempotency for salted password hashes is an additional unresolved design gate. Candidate migration 0010 remains unreserved and must be IdentityAccess-owned/table-only, including only the approved nullable display_name column/check on accounts in addition to the three bootstrap tables and their constraints/indexes. No SQL is authorized.

## 7. Non-authorization

No SQL, DDL, migration reservation/application, role/schema/routine/grant creation, credential, secret, Owner account/binding, challenge, executor LOGIN, Setup Wizard execution, Recovery, Organization, Inventory, G1 or production activation is authorized. All technical acceptance tests remain NOT RUN / NOT VERIFIED.

**END — v0.4**


## 8. Owner decision reconciliation — JRN-SEC-01 and JRN-REC-01 (2026-11-24)

The Product Owner has approved the following architecture targets, without authorizing implementation:

- **JRN-SEC-01:** CNG with Microsoft Platform Crypto Provider and TPM 2.0 is the primary cryptographic protection target for the local Bootstrap Operation Journal. DPAPI is limited to separately approved ancillary local secrets. ECDSA P-256/SHA-256 is the initial checkpoint-signature target, subject to TPM/provider PoC.
- **JRN-REC-01:** a dedicated read-only reconciliation View and logical `JOURNAL_RECONCILIATION_READER` responsibility are approved. No third SECURITY DEFINER routine and no increase to `mdgsm_bootstrap_executor` privileges are permitted.

The controlling technical reconciliation and constraints are in `MDG-SECURITY-O46-RESTART-SAFE-IDEMPOTENCY-DECISION-ADDENDUM-001_v0.2.md`. In particular:
1. ECDSA signatures do not encrypt the stored password hash. Confidentiality requires a separate authenticated-encryption DEK and a TPM-backed CNG wrapping/key-agreement mechanism, independently proven. Signing and encryption key purposes must remain separate.
2. The O-46 Journal is the local Bootstrap Operation Journal, not the Accounting ledger. Debit/Credit/PostingStatus fields are out of scope.
3. Hash chains/signatures alone do not prevent truncation/rollback; no WORM External Anchor is assumed or approved yet.
4. A View SELECT alone cannot prove absence of an in-flight claim. Reconciliation must use the same InstallationId-scoped serialization barrier as claim and a fresh READ COMMITTED snapshot after the barrier.
5. `JOURNAL_RECONCILIATION_READER` is an application-layer logical identity/capability only; it is not a PostgreSQL role, LOGIN, membership or principal. The exact-two-PostgreSQL-principal rule is unchanged. Reconciliation must use a pre-existing, already-authorized database execution identity for `SELECT`; the executor is explicitly excluded. The exact role and effective `SELECT` path must be proven from the current Grant Matrix. If no such path exists, Reconciliation remains open; no grant or identity is to be invented or expanded.
6. View owner, RLS, security-barrier behavior, installation scoping, base-table ACLs and non-updatability require PostgreSQL 18 proof.

All PoCs, Windows/PostgreSQL tests and independent review remain NOT RUN / NOT VERIFIED. No CNG key, TPM key, Journal, View, principal, grant, routine, SQL, DDL or migration has been created or authorized. Candidate migration 0010 remains NOT RESERVED.

**Precedence:** this v0.6 register controls the O-46 Journal/Reconciliation architecture-target decisions above; it preserves exactly two PostgreSQL principals, exactly two routines, no Executor privilege expansion and the no-implementation boundary.


## 9. Owner reconciliation — JRN-REC-01 exact-two-principal decision (2026-11-24)

The Product Owner has explicitly resolved the former database-identity conflict:

| Item | Binding decision |
|---|---|
| PostgreSQL principal count | Exactly two new principals; unchanged |
| Accepted principals | `mdgsm_identity_bootstrap_fn_owner` and `mdgsm_bootstrap_executor` only |
| `JOURNAL_RECONCILIATION_READER` | Logical application-layer identity/capability only; never a PostgreSQL role or principal |
| Reconciliation View | Dedicated read-only View is an approved design target; no View has been created |
| View owner | A pre-existing, controlled, non-login PostgreSQL role must be selected from the current role inventory; no new owner role |
| SELECT execution identity | A pre-existing execution identity may read only if the current Grant Matrix proves it is already authorized; no assumed or newly invented grant |
| Executor | No direct View SELECT and no privilege expansion |
| Routine count | Exactly the two approved SECURITY DEFINER routines; no third routine |

The logical application identity is authorized by application policy to invoke the Reconciliation capability. It has no direct PostgreSQL credentials, database connection, table access, routine execution grant, write capability or private-key access. The application must route the read through an already-existing execution identity whose database authorization is independently established; logical application authorization does not itself create or imply a database grant.

The View must expose only the minimum bootstrap operation reconciliation metadata. It must not expose password hashes, challenge verifier material, raw challenges, profile PII, secrets, keys or unrelated attributes. It must be non-updatable and have no DML path. View owner, RLS behavior, security-barrier use, installation scoping, effective privileges, base-table grants and the exact existing SELECT execution identity require PostgreSQL 18 catalog/effective-privilege proof.

The Reconciliation reader and claim path must share the same InstallationId-scoped serialization barrier. Claim takes the exclusive transaction-level form; Reconciliation takes the compatible shared transaction-level form and, after acquiring it, reads under a fresh READ COMMITTED snapshot. The lock primitive, namespace derivation, collision behavior and proof that every claim path participates remain open. The lock must not create a durable write or a third routine.

**Open implementation gate:** inspect the current role inventory and Grant Matrix and prove (a) which pre-existing NOLOGIN role owns the View, and (b) which pre-existing execution identity already has an authorized SELECT route. If either cannot be established without creating a third principal or expanding Executor privileges, do not create the View or grant; keep JRN-REC-01 design-approved but execution-open and return with the exact missing authorization path.

This Owner reconciliation supersedes the former unresolved alternative that proposed a third PostgreSQL Reader role or a Windows-SID-to-new-role mapping. It does not authorize SQL, DDL, View creation, grants, routine changes, migration, credentials or activation.

**END — v0.6**
