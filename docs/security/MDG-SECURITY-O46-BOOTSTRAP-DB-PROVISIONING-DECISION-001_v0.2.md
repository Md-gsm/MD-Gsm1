# O-46 Bootstrap DB Provisioning Path — Owner Decision

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-DECISION-001 |
| Version / Date | 0.2 / 2026-11-23 |
| Status | **OWNER-APPROVED CONSTRAINT — TWO-ROUTINE DESIGN RECONCILED; IMPLEMENTATION AND ACTIVATION NOT AUTHORIZED** |
| Owner | Product Owner |
| Proposal accepted | MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-PATH-001 v0.2 |
| Related design | MDG-SECURITY-O46-BOOTSTRAP-PROVISIONING-PACKAGE-CONTRACT-001 v0.2 |
| Related principal decision | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DECISION-001 v0.2 |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-DECISION-001 v0.1 |

## 1. Decision

The previously approved deployment path remains controlling: use only the already-existing PostgreSQL cluster-administrator/superuser root of trust through a separate, explicitly invoked, local maintenance/provisioning operation on the central Windows server. No new persistent deployment LOGIN is created; no membership edge is added to `migration_runner`; the existing N.6 operational identities and current `migration_runner → mdgsm_schema_owner` SET-only membership remain unchanged.

The Product Owner additionally approved, as a design constraint only, exactly two fixed-signature SECURITY DEFINER routines:

1. `identity_bootstrap.issue_initial_owner_challenge_v1(...)`
2. `identity_bootstrap.claim_initial_product_owner_v1(...)`

Both routines are owned by the existing NOLOGIN `mdgsm_identity_bootstrap_fn_owner` and executable only by the existing `mdgsm_bootstrap_executor`. The executor has no direct table/sequence privilege, no membership, no schema CREATE, no DDL or role-administration capability, and no generic SQL/operation dispatcher.

The approved function-owner privilege delta is limited to INSERT on the exact challenge columns and conditional UPDATE of `revoked_at_utc` to revoke a prior unconsumed challenge, in addition to the previously proposed claim-verification and conditional `consumed_at_utc` privileges. No other table or column privilege is approved by this decision.

The explicit Product Owner approval of this challenge-issuance extension is recorded at PR #92 comment 7110645790. This successor reconciles the earlier deployment decision; it does not change the seven deployment restrictions or grant implementation authority.

## 2. Seven binding restrictions

1. **Local-only execution:** only on the designated central Windows server, through an explicitly invoked and reviewed maintenance operation. No network endpoint, API, Named Pipe, scheduled service, remote listener or background application path.
2. **Strict separation:** the cluster-administrator credential is never stored in application configuration, Setup Service storage, Wizard, API, Desktop client, DbMigrator, process arguments, environment variables, source, logs or evidence.
3. **Fixed operations:** only signed/versioned, integrity-checked, typed and allow-listed provisioning packages may run. No arbitrary SQL, caller-selected database/object names, generic command dispatcher or free-form input.
4. **Exact ownership and ACL:** the isolated schema and exactly the two approved SECURITY DEFINER routines are owned by `mdgsm_identity_bootstrap_fn_owner`; both use pinned safe `search_path`, fully qualified references, no dynamic SQL and no PUBLIC EXECUTE. Only `mdgsm_bootstrap_executor` receives schema USAGE and EXECUTE on the two exact signatures. Effective privileges are catalog-verified. The shared function-owner's grants are aggregate at role level; fixed routine-body separation must be established by review and PostgreSQL 18 adversarial tests.
5. **No privilege expansion:** no new LOGIN, SUPERUSER, CREATEROLE, migration membership, schema-owner membership, object ownership or broad table grants. Executor remains a zero-membership, non-elevated, initially NOLOGIN capability and cannot install, alter or disable itself.
6. **Independent cleanup:** after initial claim, the trusted maintenance actor—not the executor or function—sets executor NOLOGIN, terminates and verifies absence of its sessions, verifies the immutable claim receipt and owner binding, and confirms Setup Service credential deletion and bootstrap channel closure. Cleanup failure remains fail-closed and retryable.
7. **Auditable evidence:** every maintenance operation records package/version/hash, authenticated maintenance identity, target installation/database, operation identifier, timestamp, pre/post role and ownership inventory, both routine definitions/owners/ACL fingerprints, verification results and cleanup result. No secret, password, verifier, private key or raw challenge may enter evidence.

## 3. Explicit non-authorizations

This decision approves deployment and routine-boundary constraints only. It does **not** authorize creating or altering PostgreSQL roles or memberships; creating the schema, either routine, grants, credentials or secrets; writing or applying an IdentityAccess migration; enabling executor LOGIN or Setup Wizard claim execution; or enabling Recovery, Organization, Inventory, G1, O-20/O-21 or production runtime.

Migration 0009 remains RESERVED for Accounting. Candidate 0010 is not reserved; WG-0 must decide issue #93 and the authoritative registry before any migration is written. This decision is not independent reviewer approval, test evidence or release authorization.

## 4. Required acceptance evidence

All tests remain **NOT RUN / NOT VERIFIED** until executed against an exact implementation commit:

- cluster superuser preflight accepts only the pre-existing approved identity and rejects database-owner-only, migration and runtime identities;
- no new deployment LOGIN and no change to N.6 role set or migration membership graph;
- exact role attributes, ownership, schema ACL, both routine signatures/owners, pinned `search_path`, PUBLIC/default ACL and executor effective privileges;
- executor has no direct table/sequence, DDL, role administration or extra routine access;
- fixed issuer body can only read/revoke/insert challenge state; fixed claim body can only verify/consume challenge state and perform the atomic Owner claim;
- no credential exposure to Wizard, Setup Service, API, DbMigrator, process environment/arguments, logs or evidence;
- atomic installation/update/rollback and no intermediate PUBLIC-executable function state;
- independent post-claim NOLOGIN, backend termination, secret erasure and idempotent cleanup;
- mismatch, partial failure, interrupted setup, reinstall, repair, update and restore all fail closed without a second Owner claim;
- exact PostgreSQL 18 disposable-catalog proof, Windows central-server proof and independent review.

## 5. Current disposition

- Deployment-path policy: **OWNER-APPROVED CONSTRAINT**.
- Two-routine boundary: **OWNER-APPROVED CONSTRAINT — DESIGN ONLY**.
- Physical DDL / routines / role / grants / secrets / migration: **NOT CREATED / NOT AUTHORIZED**.
- WG-0 migration reservation: **OPEN; 0009 remains RESERVED for Accounting; 0010 not reserved**.
- O46-G04: **OPEN pending implementation and proof**.
- IdentityAccess G1 and production activation: **NOT GRANTED**.

**END — MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-DECISION-001 v0.2**
