# O-46 — Physical Schema and Bootstrap Boundary Reconciliation Register

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001 |
| Version / Date | 0.4 / 2026-11-23 |
| Status | **OWNER-APPROVED PROFILE DECISION RECORDED; PHYSICAL SCHEMA/FINGERPRINT PROPOSALS OPEN FOR REVIEW; PROOFS OPEN; NO IMPLEMENTATION AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | v0.2 as the current cross-document index |

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
