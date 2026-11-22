# O20 Database Privilege Audit — Runtime LOGIN Binding Follow-up

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 |
| Version / Date | 1.7 / 2026-11-22 |
| Supersedes | v1.6 |
| Status | N.6 FIVE-IDENTITY POLICY RETAINED; DISPOSABLE LOGIN-SHIM PoC ADDED; DEPLOYMENT BINDING/OWNER ACCEPTANCE OPEN |
| Related evidence | O20SupportIdentityPostgresProofTests; O20RuntimeLoginMappingPostgresProofTests |
| Branch | security/o20-o21-final-closure |

## 1. Preserved owner-accepted baseline

This successor does not change the accepted N.6 five logical identity categories:
- runtime application ACL identity `mdgsm_runtime` (NOLOGIN);
- dedicated migration runner;
- backup/recovery operator;
- read-only observer;
- emergency operator, provisioned only for the approved one-use emergency lifecycle;
- separate `mdgsm_schema_owner` NOLOGIN schema owner.

The owner-accepted separation-of-duties model and no production credential/grant policy remain in force. The identity names in previous O20 evidence are not proof of a production credential, service principal or deployment binding.

## 2. Newly added disposable LOGIN-shim experiment

A new test-only class, `tests/MDGsm.Domain.Tests/Security/O20RuntimeLoginMappingPostgresProofTests.cs`, evaluates one candidate way to establish a database connection while keeping the runtime ACL role NOLOGIN:

- `runtime_app_<suffix>`: synthetic LOGIN role, NOINHERIT, non-superuser, non-createdb, non-createrole, non-replication, no BYPASSRLS;
- `mdgsm_runtime_<suffix>`: synthetic NOLOGIN, NOINHERIT ACL role;
- the only membership edge from the LOGIN role is SET-only to the runtime ACL role (`ADMIN FALSE, INHERIT FALSE, SET TRUE));
- before explicit SET ROLE, the login principal has no schema/table rights;
- after SET ROLE, `session_user` remains the LOGIN role and `current_user` is the ACL role;
- the synthetic runtime role receives only SELECT/INSERT on one disposable probe table; UPDATE/DELETE/TRUNCATE/DDL are denied;
- SET ROLE to synthetic schema owner, migration runner, backup operator, reporting observer or emergency operator is denied;
- the test verifies exact role attributes and the sole membership edge.

The fixture is restricted to a database with the `MDGSM_DISPOSABLE_TEST_DB;` marker and loopback host. It creates/drops only synthetic roles and schema in that disposable database.

## 3. What the PoC does not decide or prove

The LOGIN-shim is a candidate connection-binding mechanism, not a new owner-approved role policy and not a deployment credential configuration. It does not prove:
- which production secret/credential provider authenticates the application LOGIN;
- whether Platform permits the additional membership edge or requires a connection proxy/credential broker instead;
- whether connection pools reliably execute SET ROLE on every physical connection and reset it on pool return;
- the final production object grants, schema/database binding, session_user/current_user monitoring, rotation or failover;
- the actual runtime, migration, backup, reporting or emergency credentials exist.

The earlier N.6 role matrix must not be silently rewritten as though the LOGIN shim had already been approved. The logical five-identity policy is retained; the physical method for binding a LOGIN principal to the NOLOGIN runtime ACL identity remains a Platform/DBA decision.

## 4. Required production acceptance

Before any production LOGIN or membership edge:
1. Platform/DB owner explicitly selects LOGIN-shim, managed proxy or another authenticated connection model.
2. Credential storage/rotation/restore and deployment secret ownership are documented.
3. Connection-pool initialization and reset are proved for every physical connection.
4. Session identity telemetry records the expected `session_user` and `current_user`.
5. Effective privileges and default privileges are re-inventoried on the real target database.
6. Runtime denial tests prove no DDL, role creation, owner assumption, migration, backup, reporting or emergency authority.
7. Independent exact-SHA review accepts the actual deployment identity and grant evidence.

**Disposition:** five logical identity baseline preserved; disposable LOGIN-shim experiment added; exact-head CI pending; production binding/credential/role-membership approval OPEN. O20-C/G remain not production-proven; O-20 remains OPEN; G1 NOT GRANTED.

**END — MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 v1.7**
