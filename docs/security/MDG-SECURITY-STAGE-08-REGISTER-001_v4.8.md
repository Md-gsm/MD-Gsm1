# Stage 08 Security Register — O20 / O21 Closure Status

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 4.8 / 2026-11-22 |
| Supersedes | v4.7 |
| Status | O20 / O21 OPEN — VALIDATION AND OWNER GATES INCOMPLETE |
| Branch | security/o20-o21-final-closure |
| PR | #92 Draft / not merged |

## 1. Authorization linearization proof refinement

The dedicated disposable PostgreSQL test `IdentityAccessAuthorizationLinearizationPostgresProofTests` has been strengthened:
- it locks account, session, account-role, role, role-permission and permission rows;
- stages the protected write inside that transaction;
- starts separate session revocation and grant-removal writers;
- explicitly observes both writers blocked in PostgreSQL lock wait before allowing the write transaction to commit;
- verifies both revocations complete afterward and a fresh authorization query denies.

This validates a single synthetic lock protocol. It does not change the public IdentityAccess adapter to retain locks across a future Organization command, and it does not define the total Platform lock order.

## 2. Runtime LOGIN binding

The marked-disposable `O20RuntimeLoginMappingPostgresProofTests` class tests a candidate LOGIN-to-NOLOGIN runtime ACL mapping using exactly one SET-only membership. This is a technical experiment only; accepted N.6 logical five identities are unchanged and no production connection credentials or role memberships are created.

## 3. Current gate state

- IdentityAccess request-time v1.1 public contract: source implemented; exact current CI and module-owner acceptance pending.
- Commit-time authorization participant: proof of candidate lock mechanism added; production shared-transaction API/use-case integration OPEN.
- Product Owner binding: UNAVAILABLE until trusted binding lifecycle exists.
- Assignment maker-checker, relationship history, idempotency/outbox/lock tests: disposable PostgreSQL sources present; exact-head CI is mandatory.
- Runtime DB identity mapping: candidate PoC only; Platform/DB decision and deployed binding remain OPEN.
- AuditSecurity/WORM/key custody/verifier/restore/operations: production components remain OPEN.
- Legal schedule/Legal Hold and future RowScope consumer tests: OPEN.
- Independent Architecture/Security exact-SHA review: NOT PERFORMED.

**Disposition:** O20-E OPEN; O-20 OPEN; O-21 OPEN; Organization G1 NOT READY / NOT GRANTED. No production DDL/grant/credential/provider or merge is authorized.

**END — Stage 08 register v4.8**
