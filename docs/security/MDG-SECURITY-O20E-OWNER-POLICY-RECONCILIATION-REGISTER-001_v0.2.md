# O20-E Owner Policy Reconciliation Register

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-OWNER-POLICY-RECONCILIATION-REGISTER-001 |
| Version / Date | 0.2 / 2026-11-23 |
| Status | OWNER POLICY DECISIONS RECORDED; RETENTION SUCCESSOR v0.7 ADDED; EXACT-HEAD CI / MODULE ACCEPTANCE / LEGAL / PRODUCTION EVIDENCE OPEN |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner decision source | PR #92 comments 7093819016, 7093866229 and 7094120301 |
| Primary decision packet | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6 |

## 1. Purpose and decision boundary

This register is the navigation and reconciliation index for the Product Owner's answers to the outstanding O20-E, O-20 and O-21 policy questions. It identifies the controlling decision documents and the bounded technical changes made on the PR #92 branch.

It does not replace the governing Master, public module contracts, legal advice, technical-owner acceptance, independent review or release gates. It does not grant Organization G1, production access, DDL/migration, production identities/credentials, cloud resources, data deletion, Go-Live or PR merge.

## 2. Controlling owner decisions

### 2.1 Product Owner and Organization governance

- The Product Owner is the account created and bound during installation, represented by one immutable installation-scoped AccountId.
- The Product Owner identity is non-transferable. There is no transfer, successor or replacement path. Recovery restores only the same AccountId/principal. If continuity cannot be proved, privileged owner actions fail closed.
- The Product Owner is the final product decision authority; specialists remain responsible for correct domain work, technical evidence, tests and risk reporting. Owner authority does not waive law, required technical evidence, independent review or mandatory release gates.
- System Manager authority is limited to explicit delegation. Sensitive privilege grants require a distinct second person; a requester cannot self-approve.
- The Product Owner's only approved self-assignment/second-approver exception is Organization Branch/Warehouse assignment create/change/revoke, including assignments to the Product Owner's own account. This does not extend to IdentityAccess credentials, sessions, roles or permission grants, DBA, WORM administration or unrelated business actions.

### 2.2 Sensitive identities and reporting

- Product Owner, System Manager, DBA, Service, Worker, Backup, Recovery and ANCHOR_WRITER are separate authority categories. Backup and Recovery are distinct identities.
- No single identity may independently combine access governance, business-data mutation, immutable evidence deletion and full recovery.
- Report authorization is `EffectiveAccess = Permission ∩ OrganizationScope ∩ DataClassification`. Empty scope denies. Scope does not propagate between modules. Backend/SQL RowScope and separate audited export permission are required.
- The Product Owner's access to aggregate management reporting does not create unlimited access to or export of sensitive person-level data.

### 2.3 Emergency Access

- Ordinary emergency activation requires one eligible approver: the Product Owner or an authorized independent approver. The approver must be distinct from requester and emergency operator; System Manager cannot grant emergency access to itself.
- Ordinary grant lifetime is at most 60 minutes, expires synchronously at `now >= expires_at`, and requires fresh approval for extension.
- The allow-list is locked-account unlock, invalid-session revocation, credential reset with additional independent controls, and isolated Backup restore.
- Isolated Backup restore keeps two-person approval. O-31 offline two-part recovery for both System Managers also remains two-person.
- Sales/purchase/inventory mutation, Journal/Posting, owner-binding changes, WORM retention changes, Audit/Movement Ledger deletion and permanent administrative elevation are forbidden.

### 2.4 Worker and O21

- Workers are separated by trust boundary and task, have independent secrets, least privilege, non-interactive execution, independent rotation/revocation, attributable audit and idempotent replay.
- O21 uses an independent S3-compatible Object Storage category with Versioning and proven Object Lock COMPLIANCE, Legal Hold, independent administration and encryption. Provider selection and destructive negative proof remain open.
- Owner-approved internal retention baselines are 10 years for listed commercial/financial evidence and sensitive permission/role/ownership changes, 2 years for purely technical/security logs not linked to commercial/financial records, and until formal release for evidence under Legal Hold. Iran counsel/records authority must validate actual operator and record classes, triggers and media/hold/disposal operations. Automatic deletion remains disabled.
- External Anchor stores signed manifests/hash checkpoints, not wholesale customer or commercial payloads. Real WORM, KMS/HSM custody, verifier, restore, alerting and runbook remain open.

## 3. Controlled successor documents on PR #92

| Area | Current controlled document | What it reconciles |
|---|---|---|
| Consolidated owner answers | `MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6` | Full Product Owner policy answer set and explicit limits |
| Owner binding | `MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.2` | Installation-created immutable AccountId, no transfer, same-principal recovery |
| O20-E ownership | `MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.7` | Assignment, self-assignment exception, reports and retention policy |
| Organization logical design | `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.7` | Corrected owner-direct assignment and maker-checker rules |
| Organization physical design | `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v1.4` | Corrected physical command/transaction requirements |
| O20-F policy/proof | `MDG-SECURITY-O20F-EMERGENCY-ACCESS-LIFECYCLE-001 v0.4` | 60-minute ordinary emergency access, one approver, two-person restore |
| Owner acceptance record | `MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.3` | Formal amendment of O20-F activation policy |
| Product governance CR | `GOV-OWNER-AUTH-001 v0.1` | Product Owner final authority and specialist evidence responsibilities |
| Closure tracking | Blocker Matrix v2.2; Readiness Review v4.2; Stage 08 Register v6.9 | Latest owner policy and remaining technical/legal/review gates |

Historical document versions remain unchanged and are superseded only within the explicit successor scope.

## 4. Bounded source/test change

The disposable PostgreSQL test `O20EmergencyAccessPostgresProofTests` was aligned to the updated policy:
- grant duration is constrained to 1–3600 seconds;
- ordinary emergency operations require one approved person distinct from requester and operator;
- the synthetic Product Owner approver path is tested only where the owner is independent of requester/operator;
- `BACKUP_RESTORE` requires two distinct eligible approvers;
- database/schema/operation/resource scope, approver eligibility, one-use activation, replay, expiry and revocation remain checked;
- only `UNLOCK_ACCOUNT`, `REVOKE_INVALID_SESSION`, `RESET_CREDENTIAL` and `BACKUP_RESTORE` can be represented as grant operations;
- denied operations include sales/purchase/inventory mutation, Journal/Posting, owner-binding change, WORM retention change, Audit deletion and permanent administrative elevation;
- exact 60-minute boundary, 3601-second rejection, synchronous expiry and local audit-intent rollback are covered.

**Evidence boundary:** all principals are synthetic disposable fixtures; the ephemeral emergency PostgreSQL LOGIN receives no production object/function capabilities; the local audit outbox does not prove canonical AuditSecurity durable ACK. The test must pass on the final PR head before its source change is called validated.

## 5. Current remaining gates

1. IdentityAccess module-owner acceptance of its public permission/transaction guard contract, with Organization concurrence and version lifecycle.
2. Trusted binding implementation owned by formally designated IdentityAccess/Platform maintainers, preserving immutable AccountId and same-principal recovery; no production binding exists.
3. Organization permission namespace/role mapping and a real G1-authorized Organization consumer/UoW; no Branch/Warehouse reporting consumer exists and ReportingReadOnly remains absent.
4. Emergency Access trusted requester/approver/operator mapping, real narrowly scoped capabilities, credential/session revocation, operational runbook and canonical AuditSecurity durable ACK.
5. Worker identity approval, production secret custody/rotation/revocation and DB grants.
6. Inventory and cross-module architecture acceptance of global lock ranks, transaction boundaries, isolation, timeout/deadlock, cancellation and retry semantics.
7. AuditSecurity production event/ACK, retry/quarantine/reconciliation, verifier continuity, alerts and runbook.
8. Provider-specific WORM Compliance negative tests, KMS/HSM key custody, independent verifier and recovery.
9. Iran legal/records classification, Legal Hold authority/lifecycle, backup/replica/export/media scope and lawful disposal.
10. Independent exact-SHA Security/Architecture review, findings/retests, WG-0 reservation and a separate explicit Product Owner G1 decision.

## 6. Non-authorization

No production Organization runtime/schema/DDL/migration, permission seed/grant, owner binding, Emergency Access grant/LOGIN, Worker credential, Backup/Recovery LOGIN, WORM/KMS/HSM resource, reporting grant, production Audit delivery, automatic deletion or purge worker was created or authorized. PR #92 remains Draft/open/unmerged; PR #91 remains the separate open/unmerged full-revert proposal for PR #86. O20-E, O-20 and O-21 remain OPEN. Organization G1 is NOT READY / NOT GRANTED.

**END — O20-E Owner Policy Reconciliation Register v0.1**


## 7. Dedicated records-retention successor

The earlier records-retention document v0.6 had retained the previous statement that numeric duration was entirely unset. That statement is superseded, within the internal owner-policy scope, by:

`MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.7`

The v0.7 successor records the Product Owner's internal baseline:
- 10 years from financial-year end for listed commercial/financial evidence;
- 10 years for sensitive permission, role and ownership changes;
- 2 years for purely technical/security logs not linked to commercial/financial records;
- until formal authorized release for judicial/audit/Legal Hold evidence.

This is an internal owner schedule, not a legal determination for every record or operator. Iran counsel/records authority still must classify the actual operator and record classes, confirm legal bases/triggers and approve Hold, media, backup/replica/export and lawful-disposal controls. OD-05 retain-by-default remains effective; expiration never directly authorizes deletion; automatic disposal remains disabled.

**Updated disposition:** owner-policy retention baseline is recorded; legal classification and operational controls remain OPEN. O20-E / O-20 / O-21 OPEN; G1 NOT GRANTED.

**END — O20-E Owner Policy Reconciliation Register v0.2**
