# O-46 — Physical Schema and Bootstrap Boundary Reconciliation Register

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001 |
| Version / Date | 0.3 / 2026-11-23 |
| Status | **OWNER-APPROVED BOUNDARIES RECONCILED; PHYSICAL CONTRACT AND PROOFS OPEN; NO IMPLEMENTATION AUTHORIZED** |
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

The following v0.3 documents are the current reconciliation layer and must be read together:

- `MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001_v0.3.md` — three candidate IdentityAccess tables, exact role/routine boundaries, aggregate ACL caveat, migration separation and unresolved physical details.
- `MDG-SECURITY-O46-BOOTSTRAP-PROVISIONING-PACKAGE-CONTRACT-001_v0.3.md` — signed local maintenance package, typed operation surface, dormant state machine, install/verify/revoke and no automatic executor activation.
- `MDG-SECURITY-O46-CHALLENGE-ISSUANCE-PROTOCOL-PROPOSAL-001_v0.3.md` — two-routine issue/claim semantics, 32-byte CSPRNG challenge, SHA-256 verifier, one-use/idempotency, 30-minute maximum, operator SID binding and Setup Named Pipe security constraints.
- `MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-PATH-001_v0.3.md` — existing cluster-superuser maintenance path, role/routine provisioning, WG-0 migration boundary, executor activation separation and cleanup.

The earlier v0.2 documents remain historical design baselines. If wording differs, the v0.3 reconciliation documents control only within their stated scope; the explicit owner decisions remain authoritative.

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

Exact columns, constraints, fingerprint canonicalization, lock primitive/key derivation and final routine signatures remain OPEN. Migration 0009 remains RESERVED for Accounting. Candidate 0010 is NOT RESERVED; WG-0 must allocate the authoritative number and approve the table-only migration before any SQL is written. No routine, principal, grant, credential or activation is included in that migration.

## 5. Remaining gates — all OPEN / NOT RUN

- IdentityAccess owner acceptance of exact physical columns, profile fields, fingerprint algorithm, routine signatures and data-access order.
- Trusted operator SID attestation and Windows IPC DACL/process/remote-client proof.
- PostgreSQL 18 catalog/effective-privilege proof, including RLS, PUBLIC/default ACL, ownership and membership.
- Adversarial proof that fixed issuer and claim bodies preserve their distinct side-effect contracts under aggregate shared-role privileges.
- InstallationId-scoped lock primitive, collision analysis and issue-vs-claim/concurrent-claim tests.
- WG-0 reservation and migration registry update.
- Secret custody, SCRAM/client authentication, complete HBA ordering and negative remote/non-target database tests before executor LOGIN can even be considered.
- Independent Security/Architecture review of one exact SHA and explicit acceptance of residual risk.

## 6. Non-authorization

No SQL, DDL, migration reservation/application, role/schema/routine/grant creation, credential, secret, Owner account/binding, challenge, executor LOGIN, Setup Wizard execution, Recovery, Organization, Inventory, G1 or production activation is authorized. All technical acceptance tests remain NOT RUN / NOT VERIFIED.

**END — v0.3**
