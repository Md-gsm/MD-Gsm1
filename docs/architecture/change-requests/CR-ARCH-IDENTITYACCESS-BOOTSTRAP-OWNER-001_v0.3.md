# CR-ARCH-IDENTITYACCESS-BOOTSTRAP-OWNER-001 — Version 0.3
## CHG-101 Master-authority reconciliation

| Field | Value |
|---|---|
| Change ID | CHG-101 |
| Version | 0.2 |
| Decision / amendment date | 2026-11-24 |
| Status | **OWNER-APPROVED CHANGE CONTROL — v3.0.3 REMAINS THE SOLE OFFICIAL MASTER** |
| Governing Master | MDG-ARCH-MASTER-001 v3.0.3 |
| Related historical derivative | MDG-ARCH-MASTER-001 v3.0.4 |
| Supersedes | v0.1 only for its designation of v3.0.4 as the governing Master successor; v0.1 remains the historical record of the original CHG-101 decision and technical scope |

## 1. Purpose and controlling owner direction

This v0.2 amendment reconciles the Master-version designation after the Product Owner's explicit direction on 2026-11-24:

**MDG-ARCH-MASTER-001 v3.0.3 remains the sole accepted, official and governing Architecture/Accounting Master for MD-Gsm.**

The v3.0.4 document is retained as a historical working derivative only. It is not the official Master, is not registered as the governing Master in `main`, and does not supersede v3.0.3.

## 2. CHG-101 decisions remain scope-limited

The seven first-owner Bootstrap/Recovery constraints accepted under CHG-101 remain owner-approved within their explicitly recorded scope and source records. This amendment does not withdraw those bounded decisions. It clarifies that:

1. Their acceptance as individual owner decisions does not constitute acceptance of v3.0.4 as a Master successor.
2. The v3.0.4 file is a traceability snapshot of a proposed/controlled derivative, not the governing architecture.
3. Any conflict between the v3.0.3 text and a later explicit CHG-101 owner decision is resolved by the accepted owner decision only within that decision's stated scope, consistent with the Master hierarchy.
4. Incorporating those decisions into a future consolidated Master requires a separately controlled version and explicit owner acceptance. No automatic version promotion is implied.

## 3. Preserved technical boundaries

All non-acceptances and technical gates from v0.1 remain in force. In particular, owner decisions do not authorize runtime implementation, production Bootstrap/Recovery activation, schema or migration changes, SQL/DDL, PostgreSQL grants/roles/credentials, listener exposure, IdentityAccess G1, production migration, release or Go-Live.

The exact technical threat review, implementation design, schema/migration reservation, deployment-principal boundary, PostgreSQL concurrency/idempotency/restore evidence, secret-sentinel proof and independent review remain separately gated according to their current records.

## 4. Repository reconciliation

- The v3.0.3 Master text and current registration record identify v3.0.3 as the sole official baseline.
- Registration record v1.2 is the current repository authority for Master status.
- Registration record v1.1 and the v3.0.4 derivative are retained as historical records, not deleted or silently rewritten.
- Current PR descriptions and active documents must not identify v3.0.4 as the governing Master or state that v3.0.3 awaits repository registration.
- Historical snapshots retain their original wording for traceability; their former status assertions do not override the current registration record.

## 5. Revision history

| Version | Date | Change |
|---|---|---|
| 0.1 | 2026-11-23 | Original CHG-101 owner decision and bounded Bootstrap/Recovery reconciliation. |
| 0.2 | 2026-11-24 | Preserves CHG-101 owner decisions while reaffirming v3.0.3 as the sole official Master and classifying v3.0.4 as a historical derivative only. |

**END — CR-ARCH-IDENTITYACCESS-BOOTSTRAP-OWNER-001 v0.2**


## Product Owner authority and recovery amendment — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. Earlier v3.0.3-only authority statements in prior records are historical and superseded by baseline registration v1.3 and project authority control v1.0.

The Product Owner's recovery decision is now explicit:
1. The initial Product Owner identity and binding are immutable. Recovery may restore only the original AccountId and may not create a second owner, replacement, transfer or successor.
2. Owner recovery has two alternative methods: (a) a recovery code chosen by the Product Owner in the initial Setup Wizard, with only a securely salted/versioned verifier/hash persisted and bounded attempts/audit; or (b) a single-use Email OTP sent to the verified owner email.
3. The Product Owner's recovery does not require approval by the Product Owner account itself. Recovery of ordinary user accounts remains under Product Owner authority.
4. The product uses one centrally managed Google/Gmail sender account owned by MD-Gsm. Store customers enter only the recipient email address and do not configure SMTP, sender mailbox credentials or OAuth. The central sender account and authorization are created/configured during final release preparation, not customer setup.
5. Sender credentials/tokens must not be embedded in distributed binaries, source control or store databases. A protected central sending component/service, secure transport, OAuth/API authorization, token custody, delivery monitoring, abuse controls and privacy/retention rules remain design, implementation and test gates. The owner-defined recovery code remains independent of email-service availability.
6. This policy decision does not authorize a live Gmail account, OAuth client/secret, production sender service, runtime endpoint, database migration, grant, activation or Go-Live.

This amendment changes owner-policy and architecture authority only. All implementation, schema reservation, technical threat review, exact-head tests, independent review and release gates remain independently open unless separately evidenced.

**END — CR-ARCH-IDENTITYACCESS-BOOTSTRAP-OWNER-001_v0.3.md**
