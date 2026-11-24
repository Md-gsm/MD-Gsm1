# IdentityAccess — Stage 02 Bootstrap Readiness and Blocking Register

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-STAGE-02-BOOTSTRAP-READINESS-001 |
| Version / Date | 0.1 / 2026-11-24 |
| Status | **STAGE 02 REVIEW COMPLETE — BOOTSTRAP IMPLEMENTATION BLOCKED BY EXPLICIT DESIGN/PROOF GATES; NO DDL AUTHORIZED** |
| Repository / Branch | `Md-gsm/MD-Gsm1` / `security/o20-o21-final-closure` |
| PR | #92 — Draft / open / unmerged |
| Reviewed HEAD at entry | `59899139b798bd8ef105bd931c88a5cff0fe93d6` |
| Controlling owner decision | O-46 owner decisions and JRN-SEC-01 / JRN-REC-01 reconciliation, 2026-11-24 |

## 1. Purpose and boundary

This document records the Stage 02 Bootstrap technical review, reconciles the current O-46 design artifacts and identifies the exact gates preventing a safe implementation. Stage 02 is **not closed**: a design-only approval is not an implementation or deployment authorization, and the existing contracts deliberately prohibit creating DDL, roles, grants, credentials, routines, a Wizard, a Setup Service or an Owner binding until their gates are satisfied.

The review covered the O-46 physical schema/ACL proposal and decision register, challenge issuance protocol, signed maintenance package contract, DB provisioning path and owner decisions, local IPC threat review, restart-safe idempotency proposal/addendum, migration registry, Stage 01 audit, PR #92 and WG-0 Issue #93.

No source code, SQL, migration, DDL, PostgreSQL role/grant, key, credential, challenge, Windows service, Named Pipe, Wizard, Reconciliation View, Owner account/binding or runtime path was created or enabled.

## 2. Current approved design constraints

The following are accepted constraints and must not be reopened or weakened without a new Owner decision:

1. Only the interactive local Setup Wizard on the designated central Windows server may create the first Product Owner. No API, Desktop client, admin UI, CLI, script, seed, migration, support or direct-DB alternate path.
2. The owner enters the final `username`, `display_name`, `email` and final password in the Wizard. No phone/mobile field. The first Product Owner login does not force a password/profile change.
3. InstallationId is UUIDv7 and must survive reinstall, Repair, update and restore. The Product Owner binding is immutable to the original AccountId; no transfer, replacement, second owner or re-enrollment.
4. Setup challenge is exactly 32 CSPRNG bytes, delivered once through authenticated local IPC; PostgreSQL stores only SHA-256 verifier digest. Purpose is fixed, lifetime is at most 30 minutes and PostgreSQL time governs expiry/consumption.
5. Exactly two PostgreSQL principals are permitted: `mdgsm_identity_bootstrap_fn_owner` (NOLOGIN) and `mdgsm_bootstrap_executor` (initially NOLOGIN, zero memberships). No third principal, deployment LOGIN or migration membership.
6. Exactly two fixed-signature SECURITY DEFINER capabilities are permitted: `identity_bootstrap.issue_initial_owner_challenge_v1(...)` and `identity_bootstrap.claim_initial_product_owner_v1(...)`. Executor receives no direct table/sequence privilege and no generic dispatcher.
7. Privileged database provisioning is a separately invoked, interactive, local maintenance operation using only the already-existing PostgreSQL cluster-superuser root. It is not the Wizard, Setup Service, Named Pipe, API, DbMigrator, scheduled service or remote endpoint. Cluster credentials never enter the application or Setup Service.
8. The Setup Named Pipe is a separate Wizard↔Setup Service channel for authenticated operator SID attestation and one-time challenge transport only. It never carries cluster-superuser credentials or performs DDL/role provisioning.
9. Function-owner ACLs are aggregate across the shared role, not isolated per function. The issuer/claim side-effect separation is therefore a code-integrity boundary requiring fixed reviewed bodies, signed package/definition fingerprints, PostgreSQL 18 adversarial tests and explicit independent risk acceptance.
10. No role/permission seed, session, Organization, FiscalPeriod or business data is created by the initial Owner claim.

## 3. Stage 02 gate matrix

| ID | Gate | Current evidence / finding | Disposition |
|---|---|---|---|
| B-01 | Physical schema and migration path | Three proposed tables: `identity.product_owner_bindings`, `identity.bootstrap_challenges`, `identity.bootstrap_operations`; nullable legacy-compatible `identity.accounts.display_name` plus bootstrap-only CHECK. Candidate 0010 remains unreserved. | **BLOCKED** — exact schema/constraints and fingerprint contract still require technical acceptance; WG-0 must reserve the exact path/number before SQL is authored. |
| B-02 | Exact routine signatures and ACL | Two routine names and purpose boundaries accepted; exact parameter types/order, return types, lock key and final per-column grants not frozen. Shared function-owner aggregate ACL is an acknowledged residual risk. | **BLOCKED** — freeze typed signatures and exact privilege matrix; independent review and adversarial PG18 proof required. |
| B-03 | Restart-safe idempotency | Current Identity V3 PBKDF2 has a fresh random salt per Hash call. Rehashing after restart changes the encoded hash/fingerprint. | **DESIGN TARGET ACCEPTED, PROOF OPEN** — CNG/TPM-backed encrypted local operation journal is the approved direction; exact encryption/wrapping construction, atomic persistence, recovery and anti-rollback limitations remain unproven. |
| B-04 | Fingerprint v1 | Domain-separated SHA-256 envelope proposal binds InstallationId, SetupOperationId, AccountId, normalized username, validated display name/email, exact encoded password hash and hasher version. | **OWNER APPROVAL OPEN** — proposal is not an accepted implementation contract; cross-language vectors and strict encoding proof absent. |
| B-05 | CNG/TPM Journal confidentiality and integrity | Owner target: Microsoft Platform Crypto Provider + TPM 2.0; AES-256-GCM candidate for confidentiality; separate ECDSA P-256/SHA-256 checkpoint-signing key. Signing does not encrypt. | **BLOCKED** — Windows/TPM provider PoC not run; DEK wrapping/key agreement, ACLs, key lifecycle and no-fallback behavior require proof. No rollback-proof claim without trusted external anchor. |
| B-06 | Reconciliation View owner | Owner conditionally accepted `mdgsm_schema_owner` as preferred View owner, subject to role-contract reconciliation and explicit acceptance of its broad ownership and `migration_runner → mdgsm_schema_owner` SET ROLE edge. | **BLOCKED** — O-20 role/object matrix v1.8 still says ownership graph unchanged; target catalog proof and independent review absent. |
| B-07 | Reconciliation SELECT identity | Logical `JOURNAL_RECONCILIATION_READER` is an application capability only. Current disposable PG18.6 evidence has no existing application LOGIN/read path for the View; `runtime_app` and `reporting_ro` are absent. Executor is expressly excluded. | **BLOCKED / HARD STOP** — no existing authorized reader path proven. Do not create a third principal, grant Executor SELECT or silently broaden runtime grants. Requires exact target catalog + live API connection identity or a new Owner architecture decision. |
| B-08 | Shared InstallationId serialization | Issuer and claim must use one InstallationId-scoped lock; claim exclusive, reconciliation shared; fresh READ COMMITTED snapshot after shared lock. | **BLOCKED** — lock primitive, namespace/key derivation, collision analysis and all-path participation not finalized or tested. |
| B-09 | Windows IPC / operator SID | Native `CreateNamedPipeW`, `PIPE_REJECT_REMOTE_CLIENTS`, explicit protected multi-SID DACL and first-instance protection are design constraints. `CurrentUserOnly` is not acceptable. | **BLOCKED** — exact enrolled SID lifecycle, same-SID multiple logon/RDP policy, service SID, process authentication, impersonation/revert and SafeHandle proof not run. |
| B-10 | Signed maintenance package | Typed operations and signed package boundary documented; no arbitrary SQL/dispatch. | **BLOCKED** — independent trust anchor, signer lifecycle, canonical manifest, cluster-superuser authentication mechanics, atomic install/rollback and package PoC not implemented. |
| B-11 | WG-0 reservation | Registry says 0009 RESERVED for Accounting and 0010+ UNALLOCATED. Issue #93 is open; it is not a reservation. | **BLOCKED** — WG-0 has not recorded RESERVED with exact path/scope. No SQL or migration file may be created. |
| B-12 | PostgreSQL 18 and Windows acceptance | Existing CI is general repository evidence, not O-46 implementation proof. | **NOT RUN** — catalog/effective ACL, RLS, SECURITY DEFINER, concurrent claim, issue-vs-claim, unknown commit, restart, Windows pipe, CNG/TPM and secret-sentinel tests remain absent. |
| B-13 | Independent review | Shared ACL trust boundary, CNG/TPM, View owner/reader and Windows trust model require independent Security/Architecture acceptance. | **MISSING** — reviewer evidence must cite the exact final implementation SHA; design review cannot be substituted by CI. |

## 4. Specific technical conclusions

### 4.1 Restart-safe claim must preserve the exact encoded hash

The current hasher's random salt means password re-entry cannot recreate the same encoded hash. The approved architecture target is a protected local Bootstrap Operation Journal, encrypted/authenticated before any claim request can be dispatched. It must preserve the exact validated payload, AccountId, exact encoded password hash and fingerprint for the same SetupOperationId.

The approved protection direction is CNG with Microsoft Platform Crypto Provider and TPM 2.0. The Journal needs separate confidentiality and integrity mechanisms: authenticated encryption (AES-256-GCM is a candidate) with a protected DEK, plus a distinct TPM-backed signing key (ECDSA P-256/SHA-256 candidate). The signing key is not an encryption key. No software, exportable-key or DPAPI fallback is authorized for the primary Journal protection. If the selected TPM/provider cannot perform the exact operation, stop and return for a new decision.

A hash chain/signature alone cannot prevent deletion, truncation or rollback. PostgreSQL binding/receipt remains authoritative; a restored local Journal must always be reconciled against current database state.

### 4.2 Reconciliation currently has no executable database read path

JRN-REC-01 approved a dedicated read-only View and logical application capability, not a new PostgreSQL principal. JRN-REC-02 conditionally accepts `mdgsm_schema_owner` as View owner only after its broad ownership and migration SET ROLE trust boundary are reconciled. JRN-REC-03 explicitly found no existing authorized application SELECT path in the disposable catalog evidence. The exact target DB and live API LOGIN remain unverified.

Therefore the Reconciliation View cannot safely be created or used yet. This is an independent hard blocker even if the schema and two routines were otherwise ready. Do not use `mdgsm_bootstrap_executor`, `migration_runner`, `postgres`, `mdgsm_runtime` without an already-proven grant, or a newly invented `reporting_ro` role as a workaround.

### 4.3 Locking must linearize issue, claim and reconciliation

A read-only View SELECT cannot prove a negative while a claim transaction is still in flight. Reconciliation must acquire the same InstallationId-scoped shared transaction lock used exclusively by claim, then read in a fresh READ COMMITTED statement snapshot. Every claim path must acquire the matching exclusive lock before mutation. Lock key derivation and collision behavior must be deterministic, documented and adversarially tested. Do not pick a lock primitive by convenience or use a durable row mutation merely to simulate a lock.

### 4.4 Windows Named Pipe is not the DBA maintenance channel

Use native remote-client rejection and an explicit protected DACL. Authenticate both the operator SID and expected server process/service SID. The server must revert impersonation before privileged work. The exact policy for the same SID in another Windows logon/RDP session remains undecided; the implementation must not assume either acceptance or denial.

### 4.5 Migration reservation is still absent

The authoritative registry records 0009 as Accounting-reserved and 0010+ as unallocated. Issue #93 requests a WG-0 decision; it does not itself reserve 0010. The migration, if later reserved, remains schema-only: three IdentityAccess tables and approved nullable `display_name` column/check. It must not create roles, grants, routines, credentials, Owner records, role/permission seeds or runtime activation.

## 5. Document reconciliation defects

1. The file `MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001_v0.4.md` declares body version 0.5. Filename and body version disagree. Do not silently treat them as the same revision; publish a correctly named successor and update references/checksums when the design is next revised.
2. `MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001_v0.5.md` declares body version 1.1 and says it supersedes v0.7. Filename/version lineage is inconsistent and must be reconciled in the controlled document index.
3. The register's older §4/§5 text describes v0.4 artifacts as the current reconciliation layer, while later sections add JRN-SEC-01/JRN-REC-01 decisions. A single current index must identify the actual controlling successors and avoid contradictory stale status.
4. O-46 Recovery audit v0.5 is a separate audit; its Recovery runtime findings are not bootstrap implementation evidence.
5. Stage 01 audit v0.1 remains the Stage 01 result; this Stage 02 document does not supersede it.

## 6. Stage 02 exit decision

**Stage 02 is NOT COMPLETE and must remain BLOCKED.** The policy boundaries are sufficiently defined to prevent unsafe implementation, but the physical contract, fingerprint owner acceptance, TPM/CNG PoC, Reconciliation owner/reader route, shared lock, Windows SID enrollment, WG-0 reservation, PostgreSQL 18 proof and independent review are not closed.

The next safe sequence is:

1. Reconcile O-46 physical-schema and decision-register filenames/version lineage; publish one controlling index.
2. Obtain explicit Product Owner disposition for fingerprint v1 and the unresolved same-SID/multi-session policy.
3. Complete the CNG/TPM PoC and exact encrypted Journal envelope; stop if provider capability fails.
4. Resolve JRN-REC-02 owner trust-boundary conditions and JRN-REC-03 existing SELECT execution identity from a sanitized exact-target catalog and live API connection contract. If no compliant route exists, return for a new Owner decision; do not add a principal.
5. Freeze the exact InstallationId lock primitive/key derivation and routine signatures; obtain independent review of aggregate function-owner ACL residual risk.
6. Submit the stable table-only migration request to WG-0; wait for explicit registry reservation before writing SQL.
7. Build disposable PostgreSQL 18 and Windows acceptance tests; require exact-head CI and independent review before any activation decision.

## 7. Non-authorization

This report is a Stage 02 readiness and blocker record only. It authorizes no SQL, DDL, migration reservation/application, role, grant, credential, secret, CNG/TPM key, Journal, View, routine, service, Named Pipe, Wizard, challenge, Owner account/binding, Executor LOGIN, Recovery, production listener, G1 or PR merge. PR #92 remains Draft/open/unmerged.

**END — IdentityAccess Stage 02 Bootstrap Readiness v0.1**
