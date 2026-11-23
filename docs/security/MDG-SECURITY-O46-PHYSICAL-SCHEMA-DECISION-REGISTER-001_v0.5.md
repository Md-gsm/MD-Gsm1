# O-46 — Physical Schema and Bootstrap Boundary Reconciliation Register

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001 |
| Version / Date | 1.1 / 2026-11-24 |
| Status | **OWNER-APPROVED PROFILE, JOURNAL AND TWO-PRINCIPAL RECONCILIATION TARGETS RECORDED; PHYSICAL/SECURITY PROOFS OPEN; NO IMPLEMENTATION AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | v0.7 as the current cross-document index |

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


## 10. Static Grant Matrix inspection — Reconciliation path remains OPEN (2026-11-24)

The current checked-in evidence was inspected without connecting to or changing any live PostgreSQL database:

- `docs/security/MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001_v1.8.md` classifies `mdgsm_runtime` as the NOLOGIN runtime ACL role; its associated `runtime_app` LOGIN is described as a disposable/CI binding candidate, not a proven production deployment identity. The same matrix explicitly lists `reporting_ro` as not provisioned.
- `docs/security/MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001_v0.7.md` states that the full five-identity mapping and complete object/ACL matrix remain open. Its evidence is disposable CI, not a live production catalog.
- `database/operations/identityaccess-runtime-role-grants.sql` grants `mdgsm_runtime` SELECT on selected existing IdentityAccess base tables, including `identity.accounts`; it does not define the O-46 Reconciliation View and contains no SELECT grant on that View. The View does not yet exist.
- The earlier disposable catalog report in O-20 audit v1.6 observed `mdgsm_schema_owner` as owner of the then-inventoried target objects and as NOLOGIN. This is only a candidate existing View owner; it is not a fresh catalog proof for the current target and does not establish a SELECT execution path.

**Finding:** the checked-in matrix identifies a possible existing application runtime route (`runtime_app` → `mdgsm_runtime`) and a possible existing NOLOGIN owner (`mdgsm_schema_owner`), but it does not prove that either is currently authorized for the not-yet-created Reconciliation View. `reporting_ro` is unavailable, and `mdgsm_bootstrap_executor` is expressly excluded. Existing base-table SELECT does not automatically confer SELECT on a new View.

Therefore no existing, already-authorized SELECT path for the Reconciliation View has been demonstrated. Do not grant View SELECT to `mdgsm_runtime`, provision `reporting_ro`, add a new role, or expand Executor privileges under this decision. The exact View owner and reader execution identity remain unresolved pending a current PostgreSQL 18 role/ACL/ownership inventory and an explicit approved mapping. JRN-REC-01 is design-approved but execution-open.

This inspection is static repository evidence only. It is not a live database audit, effective-privilege proof, View security proof or implementation test.


## 11. Exact-head PostgreSQL 18.6 disposable catalog evidence — reader path not present (2026-11-24)

A fresh O-20-G catalog inventory artifact from the PR #92 PostgreSQL CI job was downloaded and inspected. Evidence provenance:

| Field | Value |
|---|---|
| PR head SHA | `d90edccd460b722885bb5839a5c6492621c60ad9` |
| PR test merge SHA checked out by the workflow | `c4af735a570d13570dfc66906ab805047f2ed168` |
| Workflow run | `45265705287` |
| PostgreSQL job | `138012180417` — `persons-postgres-migrations`, completed successfully |
| Catalog inventory step | Step 9 — completed successfully |
| Artifact | `o20-g-catalog-inventory`, artifact ID `14862936525` |
| Server version | `180006` — PostgreSQL 18.6 |
| Artifact JSON SHA-256 | `5ff69d034f943c8981790bd913fe7bfea93556677f9e6aea1d2b945c43dc9def` |

This is a marked disposable CI database catalog, not a production/live database. The O-20-G inventory contains 20 roles, 4 membership edges, 5 schemas, 522 relations and 227 effective-privilege entries.

### 11.1 Existing NOLOGIN owner candidate

`mdgsm_schema_owner` exists as NOLOGIN, non-superuser, non-CREATEROLE, non-CREATEDB, non-BYPASSRLS. In this fixture it owns the `identity` schema and every inventoried IdentityAccess base table/index/routine object (24 ownership entries). It is therefore an existing technical owner candidate, but **not yet accepted as the Reconciliation View owner**.

Important limitation: the sole project-role membership edge is `migration_runner` → `mdgsm_schema_owner` with `SET TRUE`, `INHERIT FALSE`, `ADMIN FALSE`. Thus the migration identity can explicitly SET ROLE to the broad schema owner. Reusing this owner for a security-sensitive Reconciliation View would place View ownership/change authority inside the existing migration/schema-owner trust boundary. This must be assessed and explicitly accepted; NOLOGIN alone does not make the role a narrowly scoped reader or an isolated owner.

### 11.2 No existing authorized reader path

The same inventory shows:

- `mdgsm_runtime` exists as NOLOGIN and has USAGE on `identity` plus SELECT on nine existing IdentityAccess base tables. It has no LOGIN identity in this catalog; `runtime_app` is absent.
- `reporting_ro` is absent.
- `mdgsm_bootstrap_executor` and `mdgsm_identity_bootstrap_fn_owner` are absent, consistent with the no-provisioning boundary.
- No O-46 `identity_bootstrap` schema, O-46 bootstrap tables, or O-46 routines are present.
- No View exists in the `identity` schema. No O-46 Reconciliation View exists anywhere in the catalog.
- No effective SELECT privilege entry exists for a Reconciliation View. SELECT on existing base tables does not authorize a future View.
- `migration_runner` → `mdgsm_schema_owner` SET-only membership is a migration/ownership path, not an approved application Reconciliation reader path. The superuser `postgres` is not an acceptable application reader.

### 11.3 Disposition

The catalog evidence confirms one pre-existing NOLOGIN owner candidate (`mdgsm_schema_owner`) but does not establish it as an acceptable dedicated View owner because of its broad ownership scope and migration_runner SET ROLE edge. It proves no pre-existing application execution identity is currently authorized to SELECT an O-46 View. `mdgsm_runtime` is only a NOLOGIN ACL role in this catalog, and there is no existing View grant or LOGIN route to rely on.

Therefore:
1. Do not create the View or grant SELECT to `mdgsm_runtime`.
2. Do not reuse `migration_runner` or `postgres` as the Reconciliation reader.
3. Do not expand `mdgsm_bootstrap_executor` or create a third PostgreSQL principal.
4. Keep JRN-REC-01 design-approved but execution-open.
5. A further Owner/Platform decision is required if the only existing owner candidate, `mdgsm_schema_owner`, is to own the View despite the migration trust-boundary overlap. Independently, an already-authorized reader execution path must be identified; this catalog does not contain one.

This evidence supersedes the earlier statement in §10 that a fresh PostgreSQL 18 catalog proof had not yet been performed. It does not supersede the conclusion that no already-authorized Reconciliation SELECT route has been demonstrated. No production connection, SQL/DDL, role, grant, View, routine, migration, secret or activation was created or changed.


## 12. Function-owner suitability review for JRN-REC-01 — no acceptable owner or reader path (2026-11-24)

The proposed alternative of assigning the Reconciliation View to `mdgsm_identity_bootstrap_fn_owner` was checked against the controlling O-20 role/object contract and O-46 physical-schema/ACL proposal.

### 12.1 Function-owner is not an eligible View owner under the current contract

The checked-in O-20 role/object matrix v1.8 constrains `mdgsm_identity_bootstrap_fn_owner` to ownership of only the isolated `identity_bootstrap` schema and the exact versioned bootstrap routines. It expressly assigns no IdentityAccess/business-table ownership and rejects a `migration_runner` membership edge. O-46 physical-schema/ACL proposal v0.4 independently states that both approved SECURITY DEFINER routines are owned by this principal and that the function-owner's grants are an aggregate role-wide ACL shared by both routines.

Consequently, making this principal own a Reconciliation View would be a contract expansion beyond its accepted isolated schema/routine ownership. If the View were placed in `identity_bootstrap`, that would also cross the defined separation between the bootstrap function-owner's isolated routine surface and the reconciliation projection. If placed in `identity`, it would extend ownership into the IdentityAccess schema. Either choice changes the approved ownership boundary and cannot be treated as a harmless reuse of an existing role.

The shared aggregate ACL is an additional concern: a View owned by the function-owner would combine View-owner read authority with the aggregate privileges needed by both SECURITY DEFINER routines. This would enlarge the consequences of a defect or unintended invocation and complicate the independent routine side-effect boundary. No PostgreSQL 18 adversarial proof exists to justify that combination.

**Disposition:** `mdgsm_identity_bootstrap_fn_owner` is not an accepted or eligible JRN-REC-01 View owner under the current contract. No change to its ownership scope, schema, grants or routine surface is authorized.

### 12.2 Existing schema-owner candidate remains unsuitable without a separate Owner decision

The exact-head PostgreSQL 18.6 disposable catalog evidence in §11 shows `mdgsm_schema_owner` as a NOLOGIN owner with broad ownership of the `identity` schema and 24 IdentityAccess objects. It also shows the explicit SET-capable membership edge from `migration_runner` to `mdgsm_schema_owner`. Assigning the security-sensitive View to this role would place its ownership/change authority inside the existing migration/schema-owner trust boundary. NOLOGIN alone does not make this a dedicated or isolated owner.

The O-20 role matrix further states that this role's existing purpose and ownership graph are unchanged. Reusing it for the View therefore requires a specific Owner/Platform decision that explicitly accepts the broad ownership and migration SET ROLE relationship, plus a PostgreSQL 18 proof of the View's RLS, base-table privilege and security semantics. That approval is not present.

### 12.3 Reader execution identity remains independently absent

The §11 catalog contains no LOGIN identity for `mdgsm_runtime`, no `reporting_ro`, no O-46 View and no effective SELECT privilege for such a View. The `mdgsm_bootstrap_executor` remains excluded from SELECT and may not be expanded. The function-owner is not an application reader identity. Thus selecting or changing a View owner would not establish a valid application SELECT route.

### 12.4 Binding conclusion and next decision gate

No currently inventoried role satisfies both the approved narrow View-owner boundary and an already-proven Reconciliation reader route:

1. Do not use `mdgsm_identity_bootstrap_fn_owner` as View owner under its current ownership/ACL contract.
2. Do not assign the View to `mdgsm_schema_owner` unless the Product Owner explicitly accepts its broad ownership and `migration_runner` SET ROLE trust-boundary overlap.
3. Do not use `mdgsm_runtime` as View owner or infer that its base-table SELECT grants authorize the future View.
4. Do not use `migration_runner`, `postgres` or `mdgsm_bootstrap_executor` as the Reconciliation application reader.
5. Do not create a third principal, new LOGIN, View, grant, routine, schema, membership or migration to work around this gap.

JRN-REC-01 remains **design-approved / execution-open**. Two independent prerequisites remain unresolved: (a) an Owner-approved existing NOLOGIN View owner with an accepted ownership/trust boundary, and (b) an existing application execution identity with a proven authorized SELECT path. Any proposed change to the function-owner contract or schema-owner reuse must return for explicit Product Owner and independent Security/Architecture review. No SQL, DDL, grant, View, role, credential, migration or activation is authorized.

This is a document/contract review grounded in the checked-in O-20 role matrix v1.8, O-46 physical-schema/ACL proposal v0.4 and the disposable PostgreSQL 18.6 catalog evidence in §11. It is not a live database audit or a PostgreSQL View security test.


## 13. Independent owner decision — Reconciliation View ownership (JRN-REC-02) (2026-11-24)

**Product Owner disposition: CONDITIONAL APPROVAL IN PRINCIPLE.**

The Product Owner accepts `mdgsm_schema_owner` as the preferred existing candidate to own the dedicated O-46 Reconciliation View, but only after its actual role contract and ownership/migration boundary are verified and reconciled. This is a View-ownership decision only. It does not identify or authorize a reader, grant SELECT, or make the owner role an application identity.

Conditions that must all be proven before any View DDL:
1. The role already exists in the target database; no new PostgreSQL principal or LOGIN is created.
2. Its actual attributes, schema ownership, object ownership, memberships, SET ROLE paths, default ACLs and effective privileges are inventoried on the exact target PostgreSQL 18 database. NOLOGIN alone is insufficient.
3. The O-20 role/object contract is explicitly reconciled to permit ownership of this one dedicated Reconciliation View while preserving the role's other ownership and migration responsibilities. The current O-20 matrix v1.8 says its purpose/ownership graph remains unchanged; therefore this condition is NOT YET SATISFIED.
4. The View definition can be installed, replaced and rolled back only through versioned, reviewed migrations under the already-approved migration trust boundary. Neither application runtime nor `mdgsm_bootstrap_executor` may alter/drop the View, change its owner or grant/revoke its privileges.
5. PostgreSQL 18 proofs establish View owner behavior, non-updatability, RLS/security-barrier semantics, base-table privilege behavior, installation scoping, and that no unnecessary direct Journal base-table access is introduced.
6. The existing `migration_runner → mdgsm_schema_owner` SET-capable edge is explicitly included in the threat model and accepted by the required Owner/Platform and independent Security/Architecture review. No membership or migration privilege expansion is implied.

The exact-head disposable catalog previously observed `mdgsm_schema_owner` as NOLOGIN, owning the `identity` schema and 24 IdentityAccess objects, with `migration_runner` able to SET ROLE to it. This confirms existence and the trust-boundary facts in that fixture; it does not by itself prove the production/target catalog or satisfy conditions 3–6.

**Disposition:** JRN-REC-02 is **OWNER-CONDITIONALLY-APPROVED / TECHNICAL-CONTRACT-AND-PROOF-OPEN**. This is not DDL authorization and does not make View creation executable.

## 14. Independent reader-path decision and candidate assessment (JRN-REC-03) (2026-11-24)

**Product Owner disposition: NO NEW PRINCIPAL; reuse only a pre-existing, independently authorized read connection.**

The logical `JOURNAL_RECONCILIATION_READER` remains an application-layer capability only. It is not a PostgreSQL Role, LOGIN, membership or principal. Reader identity and View ownership are independent: the View owner does not become its reader merely by owning it.

### 14.1 Candidate naming and current evidence

The canonical O-20 matrix name is `reporting_ro`; the shorthand `report_ro` is not established as a separate PostgreSQL identity by the controlling matrix. O-20 role/object matrix v1.8 explicitly classifies `reporting_ro` as **not provisioned**. The PostgreSQL 18.6 disposable catalog in §11 also contains no `reporting_ro`.

The same catalog contains `mdgsm_runtime` as a NOLOGIN support role, but no `runtime_app` LOGIN. It shows no O-46 View and no effective SELECT privilege on such a View. The O-20 privilege audit v0.7 records `runtime_app` as a disposable/CI binding candidate and explicitly says production identity mapping remains open. Neither base-table SELECT on `mdgsm_runtime` nor the mere existence of that support role establishes an authorized View reader.

### 14.2 Required reader proof

Before any reader mapping can be accepted, the exact target catalog and approved connection/deployment contract must establish:
1. An already-existing application connection identity and its LOGIN/role membership path; no new PostgreSQL principal or LOGIN.
2. Explicit authorization for SELECT on this exact Reconciliation View, without deriving it from base-table privileges.
3. No INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER, ownership, ALTER/DROP, schema CREATE or grant-management capability on the View or Journal base objects.
4. No direct Journal base-table access unless a separate, explicit data-owner contract approves it.
5. The application's `JOURNAL_RECONCILIATION_READER` capability is enforced independently; the database connection identity is mapped through an explicit reviewed contract.
6. PostgreSQL 18 effective-privilege and adversarial tests prove allowed View SELECT and denied direct-table/DML/DDL/privilege-escalation paths, including RLS and installation-scope behavior.

`mdgsm_bootstrap_executor` remains excluded from SELECT and receives no privilege expansion. `mdgsm_identity_bootstrap_fn_owner` remains limited to its two approved routines and is not a reader. `migration_runner` and `postgres` are not application readers. Do not provision `reporting_ro` merely to close this gap; doing so would be a new principal and is outside the approved ceiling.

**Disposition:** JRN-REC-03 is **OPEN / BLOCKED — NO EXISTING AUTHORIZED READER PATH PROVEN**. `reporting_ro` is a future candidate only if it is already present under the approved target contract; current evidence says it is not. If no existing connection identity satisfies the above, return an architecture decision to the Product Owner. Do not create a third principal, grant Executor SELECT, or silently broaden runtime grants.

## 15. Combined gate — independent decisions, independent evidence

JRN-REC-02 and JRN-REC-03 are separate gates and must be assessed separately:
- A valid View owner does not confer SELECT on the View to any application identity.
- A valid SELECT reader does not confer ownership, ALTER/DROP, or grant-management rights.
- The View may not be created until its owner gate and reader gate are each independently satisfied, and the View/RLS/serialization proof is complete.
- The two-principal ceiling, exact two SECURITY DEFINER routines, unchanged Executor ACL, no-third-principal rule and no-implementation boundary remain in force.

No Role, LOGIN, membership, View, grant, routine, schema, SQL, DDL, migration, credential or activation was created or changed by these decisions.


## 16. Read-only application-connection and PostgreSQL catalog recheck (2026-11-24)

This section records a read-only recheck for JRN-REC-03. It does not change the independent JRN-REC-02 View-owner disposition.

### 16.1 Repository connection and credential contract

- `PersistenceRegistration.AddMdGsmPersistence` reads one named `ConnectionStrings:MDGsmDatabase` value and uses that same value for both the EF Core `MdGsmDbContext` and the singleton `NpgsqlDataSource`. The reviewed API host registers this persistence path; no separate reporting/read-only connection name or reader-specific data source is defined in the reviewed registration/API source.
- Decision 0002 states that the WPF Desktop client does not connect directly to PostgreSQL; the API/server owns database credentials and connections.
- `docs/data/persistence-and-migrations.md` states that the runtime grant template creates only the `mdgsm_runtime` NOLOGIN group and that the actual API LOGIN is created outside the repository by the DBA. The repository therefore does not establish the live runtime LOGIN name, credential, or production membership mapping.
- The DbMigrator has a distinct deployment contract using `ConnectionStrings__MDGsmDatabase`, `MDGSM_MIGRATION_LOGIN_ROLE`, and `MDGSM_MIGRATION_OWNER_ROLE`; the verified migration path is `migration_runner → mdgsm_schema_owner` with SET-only membership. This is a migration identity, not an application read identity.
- Repository search found no implementation/configuration contract for `JOURNAL_RECONCILIATION_READER`, `reporting_ro` as a provisioned identity, `report_ro`, `ReportingConnection`, or `ReadOnlyConnection`.

### 16.2 Latest available PostgreSQL 18.6 catalog evidence

The latest catalog artifact available for this recheck is from the PR #92 PostgreSQL workflow:
- PR branch head at workflow event: `4409abe7050ebfb976ea8ddf16eb45d769de3672`.
- Workflow run: `45224017066`; the workflow checkout/merge-ref SHA is `b8b61a87cbc318492c96850767066e3a9ec99f5b`.
- Job: `137865835873` (`persons-postgres-migrations)); catalog inventory step and artifact upload completed successfully.
- PostgreSQL: `server_version_num=180006` (18.6).
- Artifact: `o20-g-catalog-inventory`, ID `14840518465`; ZIP SHA-256 `3bbcef63b0ceb9e8a8b918cbe662c8a2de13f02ed35d25d93b84a7ee41ae1422`; extracted JSON SHA-256 `5ff69d034f943c8981790bd913fe7bfea93556677f9e6aea1d2b945c43dc9def`.
- Inventory totals: 20 roles, 4 membership edges, 5 schemas, 522 relations and 227 effective-privilege records.

The exact disposable catalog reports:
- `reporting_ro` absent; `report_ro` absent; `runtime_app` absent.
- `mdgsm_runtime` exists as NOLOGIN / NOINHERIT and is the catalog's mapped support role for the logical `runtime_app` binding; it is not a LOGIN.
- `migration_runner` is LOGIN / NOINHERIT and has its single SET-capable membership to `mdgsm_schema_owner`; it is not an application reader.
- `mdgsm_schema_owner` is NOLOGIN. `postgres` is the cluster superuser and is not an acceptable application reader.
- No O-46 Reconciliation View or relation named for Journal/Reconciliation exists in this catalog. The `mdgsm_runtime` effective SELECT set is limited to nine IdentityAccess relations; there is no SELECT on an O-46 View.
- Neither bootstrap principal is present in this artifact, consistent with the explicit no-provisioning boundary.

### 16.3 Evidence boundary and disposition

The catalog above is a **disposable CI PostgreSQL database**, not the live central/target database. The workflow intentionally uses a privileged CI bootstrap connection and a separate migration LOGIN; it does not attest production credentials, the actual server API LOGIN, or any external deployment connection. No live target-cluster credential/connector was available in this read-only inspection, so the target catalog's actual role graph and effective privileges remain unverified.

**Result:** no existing authorized Reader path is established by the repository contract or the latest disposable catalog. In particular, `mdgsm_runtime` cannot be treated as the Reader: it is NOLOGIN in the observed fixture, no Reconciliation View exists there, and no effective SELECT on such a View is present. The `postgres` superuser and `migration_runner` are expressly excluded from the application-reader role.

**Disposition:** JRN-REC-03 remains **OPEN / BLOCKED — NO EXISTING AUTHORIZED READER PATH PROVEN** pending a sanitized, read-only catalog export from the actual target PostgreSQL 18 database and confirmation of the live API connection identity. Do not create `reporting_ro`, a new LOGIN, a third principal, a membership, a grant, or a new connection credential to close this gap. JRN-REC-02 remains independently **OWNER-CONDITIONALLY-APPROVED / TECHNICAL-CONTRACT-AND-PROOF-OPEN**.
