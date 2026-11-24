# Stage 08 Security Register — O20 / O21 Closure Status

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 4.7 / 2026-11-22 |
| Supersedes | v4.6 |
| Status | O20 / O21 OPEN — TEST AND OWNER GATES INCOMPLETE |
| Branch | security/o20-o21-final-closure |
| PR | #92 Draft / unmerged |

## 1. Transaction-scoped authorization PoC

Added `tests/MDGsm.Domain.Tests/Security/IdentityAccessAuthorizationLinearizationPostgresProofTests.cs` and an explicit workflow step. In the disposable PostgreSQL fixture:
- a candidate authorization query locks the exact account/session/role-membership/role-permission/permission rows with `FOR SHARE`;
- a protected write is staged under the same transaction;
- independent concurrent transactions attempting session revocation and permission-grant removal must block until the protected transaction commits;
- after those revocations commit, a new authorization query returns deny.

This proves one candidate mechanism only. The actual public IdentityAccess contract still returns a request-time decision and does not retain row locks. There is no Organization runtime/use case to join the transaction. Platform global lock order, the public transaction-participant abstraction, and real session/permission mutation integration remain open.

## 2. Runtime LOGIN mapping proof

A second disposable PostgreSQL proof, `O20RuntimeLoginMappingPostgresProofTests`, demonstrates a candidate connection binding where a synthetic LOGIN principal has only SET-only membership to a NOLOGIN runtime ACL role. It tests inherited privilege denial, expected session_user/current_user, explicit SELECT/INSERT only, denied DDL/mutation and inability to SET ROLE to owner/support identities.

This does not change the approved five logical identity policy or approve a production runtime login, credential, membership edge or pool setting. Platform/DB owner acceptance and actual deployment evidence remain open.

## 3. Gate status

- O20-E runtime/security policies: several disposable PoCs are source-present; latest exact SHA must be fully CI validated.
- IdentityAccess public v1.1 interface and request-time live-session permission: source implemented; commit-time transaction participant not implemented.
- Product Owner binding: fail-closed/Unavailable until protected lifecycle exists.
- O20 support roles: production binding, credentials and actual effective grants unproven.
- Platform ID and lock order: open.
- AuditSecurity/WORM/key custody and O21 operations: open.
- Retention schedule/Legal Hold: open beyond OD-05 baseline.
- RowScope: no current Branch/Warehouse consumer; future inventory and tests required.
- Independent review and G1: not performed/not granted.

**Disposition:** O20-E OPEN; O-20 OPEN; O-21 OPEN; Organization G1 NOT READY / NOT GRANTED. No merge or production grant is authorized.

**END — Stage 08 register v4.7**
