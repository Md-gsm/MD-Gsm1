# Stage 08 / Gate D — Independent Technical Re-Audit Follow-up

| Field | Value |
|---|---|
| Review ID | MDG-STAGE-08-GATED-REVIEW-001 |
| Version | 0.2 |
| Date | 2026-11-20 |
| Base review | v0.1 |
| Exact code SHA tested | `716cac7a91844dbbd868b9f670a6b3a5732b2f18` |
| Execution run | `44718235960` |
| Restore artifact | `14620337392` |
| Artifact digest | `sha256:d3c7fcc89aef9d2de7b6af5f9c5e1f73576db960db01dbaeacfbc4d6fd9ffe4e` |
| Disposition | **F-01 CLOSED; F-02/F-03 PARTIALLY CLOSED; F-04..F-08 OPEN. Gate D NOT ACCEPTED.** |

## 1. New exact-SHA execution evidence

On SHA `716cac7a91844dbbd868b9f670a6b3a5732b2f18`:
- PostgreSQL migration application and idempotent repeat: PASS (8 applied, then 0 new / 8 existing).
- Eight logical restore PoCs: 8/8 PASS.
- PostgreSQL integration suite: 36 passed, 0 failed, 0 skipped.
- Dedicated run-scoped PostgreSQL LOGIN test: allowed reads passed; DDL, account provisioning and audit mutation were rejected with permission-denied responses.
- The exact run's spec-lint checksum gate still reported the changed restore script as stale in `SHA256SUMS.txt`; that manifest entry must be refreshed and rerun before declaring the full CI gate green.
- Windows PostgreSQL migration, CodeQL and preview-installer workflows were not yet terminal at the time of this follow-up.

## 2. Finding disposition

| Finding | Updated status | Review conclusion |
|---|---|---|
| F-01 EVD stale versus reviewed SHA | **CLOSED for the recorded run** | EVD v0.2 now records exact SHA 716..., run 44718235960, restore artifact ID/digest and 8/8 + 36/36 results. |
| F-02 restore snapshot/catalog integrity | **PARTIALLY CLOSED** | Snapshot, schema DDL, CHECK-constraint inventory/metadata, owner/ACL/default ACL and role-membership parity are now tested. Textual CHECK expressions are excluded from the DDL digest due PostgreSQL dump/restore pretty-print differences; expression-level semantic equivalence is not yet proven. |
| F-03 dedicated runtime LOGIN test | **PARTIALLY CLOSED** | A real PostgreSQL LOGIN role is exercised directly against the database with positive and negative privilege tests. The running application/API has not yet been tested end-to-end using this identity; CI service authentication is trust-only. |
| F-04 account recovery | **OPEN — DESIGN ONLY** | Verified-email, OTP delivery, external HMAC key provisioning/rotation, quotas, replay and provider-failure runtime tests remain absent. |
| F-05 canonical AuditSecurity Hash Chain | **OPEN — DESIGN ONLY** | Canonical event stream, hash chaining, signed external checkpoints, WORM anchoring and verifier runtime remain absent. |
| F-06 full Gate D recovery | **OPEN — GATE BLOCKER** | No full business-data restore, WAL/PITR, measured RPO/RTO, off-site encrypted retention, external key/file restore or full accounting/inventory tie-out. |
| F-07 trusted installer and lifecycle | **OPEN — GATE BLOCKER** | Preview installer remains unsigned; launch/health, bootstrap, upgrade/forward-fix/rollback and physical deployment are unproven. |
| F-08 external independent review and owner acceptance | **OPEN — GOVERNANCE BLOCKER** | This review remains a ChatGPT technical review, not an authenticated external attestation. No external reviewer or owner acceptance is inferred. |

## 3. Nine-domain re-evaluation

1. Architecture authority / v3.0.3 consistency — **PASS, design scope**.
2. PostgreSQL role and transport boundary — **PARTIAL**; dedicated LOGIN tested, production TLS and application-level runtime identity not proven.
3. Session/token/lockout/capacity — **PARTIAL**; prior selected tests remain valid, but not a full five-Desktop deployment.
4. Account recovery — **NOT IMPLEMENTED / NOT RUN**.
5. AuditSecurity Hash Chain — **NOT IMPLEMENTED / NOT RUN**.
6. Backup/restore — **PARTIAL**; 8/8 logical PoCs for current Infrastructure/IdentityAccess schema only.
7. Windows installer/client deployment — **PARTIAL**; hosted runner compatibility only, unsigned and lifecycle incomplete.
8. Exact-SHA CI/evidence — **PARTIAL**; restore and integration pass on SHA 716..., but checksum gate and remaining workflows must reach terminal success.
9. Independent review / acceptance governance — **BLOCKED**.

## 4. What can be accepted now

- The restore PoC has materially stronger schema and security catalog parity than v0.1.
- The runtime database role has a tested least-privilege boundary at the PostgreSQL LOGIN layer.
- EVD v0.2 is bound to the exact tested SHA and artifact digest.

These are accepted as **test evidence only**. They do not authorize production, activate recovery, implement Hash Chain, close Gate D, or satisfy external independent-review requirements.

## 5. Final acceptance decision

| Scope | Decision |
|---|---|
| Stage 08 integrated security design | Technically acceptable as design artifact only |
| Restore structural parity PoCs | Accepted for tested Infrastructure/IdentityAccess schema, with CHECK-expression limitation |
| Database-level runtime LOGIN boundary | Accepted as database-level test evidence only |
| Application-level runtime identity | Not accepted / pending |
| Account recovery / Hash Chain | Not accepted / not implemented |
| Gate D full recovery | **OPEN / NOT VERIFIED** |
| G1 / production / Posting Release / Go-Live | **NOT AUTHORIZED** |
| Authenticated external reviewer attestation | Pending |
| Owner formal acceptance | Pending |

**END — MDG-STAGE-08-GATED-REVIEW-001 v0.2**
