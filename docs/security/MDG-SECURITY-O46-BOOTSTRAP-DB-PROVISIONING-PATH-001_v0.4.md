# O-46 — Bootstrap DB Provisioning Path

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-PATH-001 |
| Version / Date | 0.4 / 2026-11-23 |
| Status | **OWNER-APPROVED MAINTENANCE BOUNDARY — DESIGN ONLY; NO IMPLEMENTATION AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | v0.2 where reconciled below |

## 1. Controlling decision

Use only the already-existing PostgreSQL cluster-administrator/superuser root of trust, through a separately invoked, explicitly local maintenance operation on the designated central Windows Server, to provision and verify the accepted bootstrap principals, install/upgrade/verify the exact routine pair and perform independent post-claim executor revocation.

No new persistent deployment LOGIN is created. No CREATEROLE/SUPERUSER or role membership is granted to `migration_runner`, `mdgsm_schema_owner`, runtime, Setup Service or any application identity. The existing five N.6 identities and existing `migration_runner → mdgsm_schema_owner` SET-only membership remain unchanged.

The accepted principals are exactly:
- `mdgsm_identity_bootstrap_fn_owner`: NOLOGIN, owns only the isolated `identity_bootstrap` schema and the two exact routines;
- `mdgsm_bootstrap_executor`: initially NOLOGIN, zero memberships, no object ownership and no direct table/sequence privileges.

Exactly two fixed-signature SECURITY DEFINER routines are in scope:
- `identity_bootstrap.issue_initial_owner_challenge_v1(...)`;
- `identity_bootstrap.claim_initial_product_owner_v1(...)`.

Both routines share the existing NOLOGIN function-owner and are executable only by the executor after separate activation gates. No third routine, generic dispatcher or additional principal is implied.

## 2. Channel separation

The term “local” denotes two separate boundaries:

1. **Privileged maintenance boundary:** an explicitly invoked interactive local operation on the central server, authenticated as the pre-existing approved cluster superuser. It provisions principals, creates/updates the isolated routine schema and routines, verifies catalog state and performs post-claim executor disable/termination. It is not a Named Pipe, Setup Service, Wizard, API, DbMigrator, scheduled task, service, background worker or remote endpoint. It does not accept Wizard or Owner credentials.
2. **First-run Setup boundary:** the authenticated local Windows Named Pipe between Setup Wizard and Setup Service. It is used only for local operator SID attestation and one-time challenge delivery/claim orchestration. It never carries the cluster-superuser credential and cannot create roles, apply DDL, install routines or perform DBA maintenance.

The Setup Service is not a DBA actor. It can use the executor connection only after separately approved credential custody, loopback-only connection rules, complete effective `pg_hba.conf` ordering and negative connection tests. Executor LOGIN remains NOLOGIN until a distinct explicit authorization.

## 3. Provisioning sequence

### 3.1 Read-only preflight

Before mutation, verify package signature/hash/provenance, designated host and interactive local session, authenticated `session_user` equals the pre-existing approved cluster superuser, PostgreSQL 18, exact target database/InstallationId, expected migration state, backup/change-control preconditions and complete role/membership/ownership/schema/routine/ACL/default-ACL inventory. Database-owner-only, migration, runtime, executor and ordinary identities are rejected. Any unexpected object or attribute is a hard stop; no automatic repair/takeover.

### 3.2 Provision dormant principals

In a controlled transaction, create only the two accepted principals with exact flags. Both are initially NOLOGIN; executor receives no password or credential. No membership, ownership outside the exact function-owner scope, extra LOGIN, database-wide grant or N.6 role change is permitted. Existing objects are accepted only if exact catalog equality is proven.

### 3.3 Isolated schema and routine pair

The local maintenance actor uses transaction-local `SET LOCAL ROLE mdgsm_identity_bootstrap_fn_owner` under its already-authenticated superuser session; this creates no persistent membership edge. It creates/updates only `identity_bootstrap` and the exact two versioned fixed-signature SECURITY DEFINER routines. The routine pair is installed atomically, with safe pinned `search_path` (`pg_temp` last), fully qualified object references, no dynamic SQL, no caller-selected identifiers, no generic dispatcher and no extra callable routine.

PUBLIC schema access and PUBLIC EXECUTE are revoked; default privileges prevent future PUBLIC EXECUTE. Executor receives only schema USAGE and EXECUTE on the two exact signatures, and remains NOLOGIN. Separate routine definition fingerprints, owner, security attributes, exact ACLs and effective privilege catalogs are verified before commit. Any mismatch rolls back the transaction.

### 3.4 IdentityAccess migration and table grants

The durable IdentityAccess tables and their exact constraints/indexes, plus nullable `identity.accounts.display_name` and the bootstrap-only profile CHECK constraint, are installed only through an IdentityAccess-owned migration after WG-0 has reserved the sequence number and approved the migration contract. That migration uses the existing DbMigrator path and existing SET-only `migration_runner → mdgsm_schema_owner` mapping. It may grant the function-owner only the exact reviewed table/column privileges required by the two routines. It must not create roles, memberships, SECURITY DEFINER routines, executor LOGIN, credentials, Owner seed, role/permission seed or production activation.

Migration 0009 remains RESERVED for Accounting. Candidate 0010 is not reserved. No migration SQL or file may be created until WG-0 records the authoritative reservation. The maintenance package must refuse to install routines unless the reserved schema migration and exact table/column grants are already present and catalog-verified.

### 3.5 Executor activation — separate decision

Provisioning and routine installation leave executor NOLOGIN. Activation requires a separate decision and evidence for secret generation/custody, Setup Service SID isolation, SCRAM or separately approved client authentication, loopback-only connection, complete effective HBA ordering, denial from remote hosts and non-target databases, no runtime access to the secret, and independent review. This v0.3 document does not select a secret technology or authorize LOGIN.

### 3.6 Post-claim cleanup

After the exact claim receipt and immutable Owner binding are independently verified, the same existing cluster-superuser maintenance boundary sets executor NOLOGIN, terminates all executor backends and verifies none remain. Setup Service separately proves its executor credential was erased and the Setup Named Pipe setup path closed. The maintenance actor does not receive or inspect the Setup Service secret. Cleanup is idempotent and retryable against the same InstallationId/SetupOperationId; failure remains fail-closed and never permits a second claim.

### 3.7 Accepted Account profile and proposed payload fingerprint v1

The initial Owner input contract is exactly `username`, `display_name`, and `email`. These map respectively to `identity.accounts.login_name` plus the existing normalized login value, new `identity.accounts.display_name`, and existing `identity.accounts.email`. All three are required for this one bootstrap account. Phone/mobile is excluded; `phone_e164` remains untouched. The existing base schema has no display-name column, so candidate migration 0010 must add nullable `display_name` for compatibility and enforce non-empty display_name/email when `is_bootstrap_account=TRUE`. The Owner row sets `is_bootstrap_account=TRUE` and `must_change_password=FALSE`. No role, permission, session or phone value is created.

The proposed fingerprint v1 is SHA-256 over this deterministic binary envelope, computed by the trusted IdentityAccess server boundary:

1. ASCII domain tag `MDGSM-O46-OWNER-CLAIM-FP`, followed by unsigned 16-bit big-endian version `1`.
2. InstallationId, SetupOperationId and AccountId as 16-byte RFC 4122/network-order UUID values, in that order.
3. The following UTF-8 strings, each prefixed by its unsigned 32-bit big-endian byte length: `login_name_normalized`, validated `display_name`, validated `email`, and the exact stored `password_hash` output, in that order.
4. `password_hash_version` as unsigned 16-bit big-endian integer.

String bytes are strict UTF-8 of the exact post-validation values; no additional trimming, case folding or Unicode normalization is performed by the fingerprint layer. Username normalization remains governed by the existing IdentityAccess username policy. The envelope excludes raw password, raw challenge, challenge digest, operator SID, private keys and transport secrets. It includes the salted/versioned password-hash output so that the fingerprint binds the actual credential result and is not merely a digest of low-entropy profile fields. Fingerprint is an idempotency/integrity identifier, not a password verifier or authentication factor.

If accepted, the receipt will store `payload_fingerprint_version=1` and exactly 32 digest bytes. Same SetupOperationId plus identical v1 fingerprint is a reconciliation match; different fingerprint is a hard conflict and never updates or replaces the prior receipt. Implementations must share test vectors across .NET and PostgreSQL-facing contract code. If the hash implementation cannot be reproduced identically from the exact persisted password-hash output, the claim must fail closed rather than fall back to a different serialization.
## 4. Function-owner ACL limitation

The function-owner grants are aggregate at role level, not isolated per SECURITY DEFINER routine. The approved issuer extension is limited to exact challenge-column INSERT and conditional `revoked_at_utc)-only UPDATE, in addition to claim's previously approved verification and conditional `consumed_at_utc) transition and exact Owner-claim writes. The executor has no direct table/sequence access. Issuer/claim side-effect separation depends on fixed reviewed routine bodies, signed package integrity, separate definition fingerprints and adversarial PostgreSQL 18 proof. Independent Security/Architecture must explicitly accept this residual risk; otherwise stop and return for a new owner decision. No third role is implied.

## 5. Audit evidence and tests

Each maintenance action records package/version/hash/signer, source commit, authenticated maintenance identity, local host, target InstallationId/database, operation ID, PostgreSQL version, before/after catalog fingerprints, role/membership/ownership/ACL inventory, routine definition fingerprints, migration version, pre/postconditions, transaction outcome and cleanup result. It must exclude all credentials, password hashes, raw challenge, verifier, private keys and connection strings.

All O46-DEP tests remain **NOT RUN / NOT VERIFIED**: approved-superuser preflight; no third LOGIN/membership delta; exact role flags; superuser SET LOCAL ROLE without membership; routine owner/signature/search_path/SECURITY DEFINER/PUBLIC ACL; aggregate privilege and side-effect tests; HBA/loopback negative tests; secret custody; atomic install/upgrade; post-claim revocation; unknown commit; restore continuity; Windows host and PostgreSQL 18 proof; independent review.

## 6. Explicit non-authorization

No role, schema, table, routine, grant, membership, credential, secret, migration, Account, Owner binding, challenge, executor LOGIN, Setup Wizard execution, recovery, G1 or production activation is authorized. This is an architecture/design record only.

**END — v0.4**
