# IdentityAccess ↔ Organization Principal / Permission Contract

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 |
| Version / Date | 1.1 / 2026-11-22 |
| Supersedes | v1.0 |
| Status | PUBLIC CONTRACT v1.1 SOURCE-PUBLISHED; REQUEST-TIME LIVE PERMISSION IMPLEMENTED; TRANSACTION-SCOPED GUARD IS PROPOSED ONLY |
| Repository / Branch | Md-gsm/MD-Gsm1 / security/o20-o21-final-closure |
| Related design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v1.1 |
| Related platform register | MDG-PLATFORM-LOCK-ORDER-REGISTRY-001 v0.2 |

## 1. Existing public contract

The public v1.1 interface is in `src/Modules/IdentityAccess/Contracts/IdentityAccessAuthorizationContract.cs`:
- current principal resolves only from server middleware-authenticated claims;
- current permission evaluates the exact key against the same AccountId/SessionId/credential version and active server-side grant;
- unknown keys deny and dependency errors return Unavailable;
- Product Owner authority is a typed decision which currently returns Unavailable until protected binding exists.

Host supplies validated principal facts through `IValidatedRequestPrincipalAccessor`; IdentityAccess Infrastructure owns the adapter and permission query. Organization must consume the Contracts assembly only.

## 2. Request-time decision is not commit-time authorization

The current `EvaluateCurrentPermissionAsync` query is one PostgreSQL statement. It checks the live session, lease, revocation, current account/credential version and active permission grant. The statement snapshot is internally consistent, but its row locks are not retained through a later Organization transaction. A permission result returned before the mutation transaction is therefore not sufficient evidence that a concurrent session or grant revocation has been serialized against the mutation.

Organization must never treat the existing Allowed result as a commit-time guard for a later protected write.

## 3. Candidate transaction-scoped guard

A disposable PostgreSQL proof was added at `tests/MDGsm.Domain.Tests/Security/IdentityAccessAuthorizationLinearizationPostgresProofTests.cs`. It models a candidate shared transaction:
1. begin the future Organization write transaction;
2. resolve exact AccountId + SessionId + credential version + exact permission;
3. acquire `FOR SHARE` locks on the matched account, session, account-role, role-permission and permission rows in a documented deterministic order;
4. execute the protected Organization mutation and insert its audit intent in the same transaction;
5. hold these row locks through commit;
6. concurrent session revocation, account disable, role assignment removal or permission-grant deletion must serialize behind the guard; after the mutation commits, the revocation proceeds;
7. any subsequent authorization query observes the revoked/removed grant and denies.

The PoC specifically asserts a protected mutation transaction holds a shared lock set while independent PostgreSQL transactions attempt to revoke the session and remove the permission grant. Both revocations must remain blocked until the protected transaction commits. A fresh post-revocation query returns no Allow.

## 4. Proposed cross-module integration contract (not accepted / not implemented)

A future Organization use case needs a transaction-participant contract which binds the IdentityAccess permission decision to the *same* PostgreSQL connection and transaction as the Organization state transition. The contract must:
- accept a server-authenticated current principal and one exact permission key, not arbitrary AccountId/role/owner claims;
- join the already-open local transaction; it must not open an unrelated connection;
- acquire and retain the authorization row locks until caller transaction completion;
- return Denied/Unavailable on missing, stale, revoked, disabled, expired or unknown state;
- define cancellation, rollback and failure behavior;
- provide a typed lease/token that cannot be confused with a cached request-time PermissionDecision;
- participate in the Platform Global Lock-Order Registry;
- require every IdentityAccess mutation that changes those protected rows to use a compatible lock order and transaction boundary;
- prove the behavior with two independent writers and crash/unknown-commit/replay tests.

The exact public C# abstraction and connection/transaction ownership are still OPEN. Do not pass IdentityAccess private DbContext/entity types to Organization. A shared transaction adapter must use only public contract abstractions and a platform-owned transaction coordinator or approved provider-neutral transaction port.

## 5. Remaining limitations

The current application has no Organization module or protected Organization write consumer. The new proof uses synthetic PostgreSQL tables and does not prove:
- the live IdentityAccess tables and real `PostgresIdentityAccessStores` method keep locks through Organization commit;
- every session/permission/account mutation path follows the same global lock order;
- IdentityAccess permission-management and session-revocation flows are tested against the same lock protocol;
- Organization aggregate state, maker-checker approval, domain history and outbox are atomic with the authorization guard;
- Platform's complete cross-module lock ranks are accepted.

**Disposition:** request-time live-session permission SOURCE-IMPLEMENTED; transaction-scoped authorization proof added as a disposable candidate; public transaction-join contract, IdentityAccess mutation integration, real Organization consumer and Platform lock-order acceptance remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — IdentityAccess ↔ Organization contract v1.1**
