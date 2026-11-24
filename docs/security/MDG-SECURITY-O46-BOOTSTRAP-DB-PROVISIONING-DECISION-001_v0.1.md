# O-46 Bootstrap DB Provisioning Path — Owner Decision

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-DECISION-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **OWNER-APPROVED CONSTRAINT — IMPLEMENTATION AND ACTIVATION NOT AUTHORIZED** |
| Owner | Product Owner |
| Proposal accepted | MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-PATH-001 v0.1 |
| Related principal decision | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DECISION-001 v0.1 |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |

## 1. Decision

The Product Owner approves use of the **already-existing PostgreSQL cluster-administrator / superuser root of trust**, exclusively through a separate, explicitly invoked, local maintenance/provisioning operation on the central Windows server, to provision and verify the two previously accepted bootstrap principals and to install, verify, upgrade or revoke the isolated IdentityAccess bootstrap `SECURITY DEFINER` routine.

No new persistent deployment LOGIN is to be created. No membership edge is to be added to `migration_runner`. The existing five N.6 operational identities and the current `migration_runner → mdgsm_schema_owner` SET-only membership remain unchanged.

The approved maintenance actor is not an application component, worker, API, Setup Wizard or Setup Service. The cluster-administrator credential must never be delivered to or readable by any of those processes.

## 2. Seven binding restrictions

1. **Local-only execution:** only on the designated central Windows server, through an explicitly invoked and reviewed maintenance operation. No network endpoint, API, Named Pipe, scheduled service, remote listener or background application path.
2. **Strict separation:** the cluster-administrator credential is never stored in application configuration, Setup Service storage, Wizard, API, Desktop client, DbMigrator, process arguments, environment variables, source, logs or evidence.
3. **Fixed operations:** only signed/versioned, integrity-checked, typed and allow-listed provisioning packages may run. No arbitrary SQL, caller-selected database/object names, generic command dispatcher or free-form input.
4. **Exact ownership and ACL:** `identity_bootstrap` and the exact approved routine are owned by `mdgsm_identity_bootstrap_fn_owner`; the function has a pinned safe `search_path`, fully qualified references, no dynamic SQL, no PUBLIC EXECUTE, and only the exact executor receives schema USAGE and routine EXECUTE. Effective privileges must be catalog-verified.
5. **No privilege expansion:** no new LOGIN, SUPERUSER, CREATEROLE, migration membership, schema-owner membership, object ownership or broad table grants. `mdgsm_bootstrap_executor` remains a single-purpose, zero-membership, non-elevated LOGIN capability and cannot install, alter or disable itself.
6. **Independent cleanup:** after the initial claim, the trusted maintenance actor—not the executor or function—sets executor to NOLOGIN, terminates and verifies absence of its sessions, verifies the immutable claim receipt and owner binding, and confirms the Setup Service erases its credential and closes its bootstrap path. Cleanup failure remains fail-closed and retryable.
7. **Auditable evidence:** every maintenance operation records package/version/hash, authenticated maintenance identity, target installation/database, operation identifier, timestamp, pre/post role and ownership inventory, routine definition/owner/ACL fingerprint, verification results and cleanup result. No secret, password, verifier, private key or raw challenge may enter the evidence.

## 3. Explicit non-authorizations

This decision approves a deployment boundary only. It does **not** authorize:
- creating or altering PostgreSQL roles or memberships;
- creating the schema, routine, grants, credentials or secrets;
- writing or applying an IdentityAccess migration;
- enabling the executor LOGIN or Setup Wizard claim path;
- enabling IdentityAccess recovery, Organization, Inventory, G1, O-20/O-21 or production runtime;
- consuming Migration 0009 (still RESERVED for Accounting) or assigning a migration number without WG-0; or
- treating the owner decision as independent reviewer approval, test evidence or release authorization.

The approved path remains subject to controlled implementation, a WG-0 migration reservation where required, disposable PostgreSQL 18 proof, Windows central-server acceptance, exact-head CI, independent review and all applicable security/release gates.

## 4. Required acceptance evidence

All tests remain **NOT RUN / NOT VERIFIED** until executed against the exact final implementation commit:

- cluster superuser preflight accepts only the pre-existing approved identity and rejects database-owner-only, migration and runtime identities;
- no new deployment LOGIN and no change to the N.6 role set or migration membership graph;
- exact role attributes, ownership, schema ACL, function owner, pinned `search_path`, PUBLIC/default ACL and executor effective privileges;
- no credential exposure to Wizard, Setup Service, API, DbMigrator, process environment/arguments, logs or evidence;
- atomic installation/update/rollback and no intermediate PUBLIC-executable function state;
- independent post-claim NOLOGIN, backend termination, secret erasure and idempotent cleanup;
- mismatch, partial failure, interrupted setup, reinstall, repair, update and restore all fail closed without a second Owner claim;
- exact PostgreSQL 18 disposable-catalog proof and supported Windows deployment proof; and
- independent review of exact commit, SQL, catalogs, test logs and evidence.

## 5. Current disposition

- Deployment-path policy: **OWNER-APPROVED CONSTRAINT**.
- O-46 bootstrap principal pair: **OWNER-ACCEPTED CONSTRAINT**.
- DDL / role / grant / schema / function / secret / migration: **NOT CREATED / NOT AUTHORIZED**.
- WG-0 migration reservation: **OPEN; 0009 remains RESERVED for Accounting**.
- O46-G04: **OPEN pending implementation and proof**.
- O-20: **OPEN**.
- IdentityAccess G1 and production activation: **NOT GRANTED**.

**END — MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-DECISION-001 v0.1**
