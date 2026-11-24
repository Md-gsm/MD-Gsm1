# O20 / O21 Owner and Platform Decision Packet

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 |
| Version / Date | 0.7 / 2026-11-24 |
| Supersedes | v0.6, only for the audit-delivery-worker identity binding |
| Status | OWNER POLICY DECISIONS ACCEPTED; AUDIT DELIVERY WORKER TECHNICALLY SELECTED; OWNER RATIFICATION REQUIRED BEFORE PROVISIONING; PRODUCTION PROVISIONING AND INDEPENDENT GATES OPEN |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.4 |
| Repository / Branch | Md-gsm/MD-Gsm1 / security/o20-o21-final-closure |
| PR | #92 Draft / open / unmerged |
| Owner decision record | PR #92 comment 7094120301 |
| Prior binding decisions | Product Owner identity decision in PR #92 comment 7093819016; PLATFORM-APPROVAL-SCOPE-01 in comment 7093866229 |

## 1. Purpose, authority and precedence

This successor records the Product Owner's answers to the remaining O20-E / O-20 / O-21 policy questions. It supersedes conflicting recommendation/status wording in v0.5 only within the explicit scope of this document. Unaffected DP-02 bootstrap trust controls, DP-03 user recovery controls, DP-04 production worker provisioning controls, DP-06 cross-module lock-order gates and all existing technical safety constraints remain in force unless a later controlled decision changes them.

These are owner policy decisions, not independent Platform/IdentityAccess/Organization/AuditSecurity acceptance, legal opinion, implementation evidence, production authorization or G1 approval. Technical correctness and legal compliance must still be demonstrated. No document may convert an unrun or failed test into PASS.

### 1.1 Product Owner identity precedence

The earlier Product Owner decision remains controlling: the installation-created account is the sole Product Owner principal for that installation; its AccountId/principal binding is immutable and non-transferable. A statement in the supplied answer table suggesting a formal ownership-transfer path is corrected and superseded. There is no transfer, successor or replacement route. Recovery may restore only the same bound AccountId/principal. If secure recovery of that same principal cannot be proved, privileged Product Owner operations remain unavailable and fail closed.

### 1.2 Narrow owner exception

The Product Owner's previously accepted exception remains limited to Organization Branch/Warehouse assignment create/change/revoke, including self-assignment, without a second approver. It does not extend to IdentityAccess credential, session, role or permission mutation, Emergency Access self-approval, database privilege, WORM administration or any other security boundary. All owner actions remain authenticated, reasoned, server-authorized, version-checked, idempotent and audited.

## 2. Organization management authority — APPROVED-CONSTRAINT

The Product Owner is the final authority for access-governance policy. A System Manager has only expressly delegated and bounded permissions.

| Operation | Product Owner | System Manager |
|---|---|---|
| Create/deactivate ordinary users | Authorized | Only within delegated scope |
| Reset user credentials | Authorized under recovery controls | Only within delegated scope and required controls |
| Assign predefined roles | Approves policy and supervises | Only within approved delegation |
| Define a new role | May authorize under security rules | Not permitted |
| Grant sensitive administrative/security/accounting privileges | Final Product Owner approval | Only within delegation and independent second-person approval |
| Change managers' access scope | Final approval | Only within delegated scope and required independent approval |
| Assign/increase access to own account | Only the narrow previously approved Organization assignment exception | Prohibited |
| Change, delete, disable or replace the Product Owner binding | No ordinary lifecycle operation; binding is immutable/non-transferable | Prohibited |
| Change non-overridable security invariants | Not permitted | Not permitted |
| Technical infrastructure settings | Only under a separate applicable authority | Only within delegated technical scope |

The Product Owner account is a protected identity. Ordinary System Manager/user-management paths cannot delete, disable, replace or change its protected owner status. Protection must be enforced at the API, domain-service and database boundaries, not only by hiding UI controls. Credential rotation or profile maintenance must preserve the same AccountId/principal binding.

No user may create or increase permissions for their own account, except the Product Owner's narrow Branch/Warehouse assignment exception. Sensitive grants require a distinct second person; the requester cannot approve their own operation. The Owner exception must never be generalized beyond the explicitly accepted Organization assignment commands.

## 3. O-20 sensitive identity separation — APPROVED-CONSTRAINT

No single identity may independently combine access-governance control, business-data mutation, immutable-evidence deletion and full recovery.

| Identity | Permitted responsibility | Non-negotiable restrictions |
|---|---|---|
| Product Owner | Product/access policy decisions, sensitive-grant approval, owner-level product decisions | No direct DB administration, ledger mutation or WORM-evidence deletion |
| System Manager | Delegated user/session/credential administration and approved technical settings | No automatic business authority, Product Owner mutation, Posting, stock-limit override or Audit deletion |
| DBA | PostgreSQL maintenance, monitoring, approved migrations and controlled technical recovery | No sales approval, accounting Posting, business-role assignment or independent external-evidence deletion |
| Service Identity | Contracted service operations | No interactive login, human role or unrelated data access |
| Worker Identity | One assigned background task/queue/outbox/job boundary | No broad all-jobs authority, human session or System Manager credentials |
| Backup Identity | Produce backup and write to the designated destination | Cannot delete immutable versions or shorten retention |
| Recovery Identity | Read backup and restore to a controlled destination | Cannot independently approve the restore, change ownership or erase evidence |
| ANCHOR_WRITER | Create new signed evidence objects/checkpoints/versions | Cannot delete prior objects or shorten retention |

Backup and Recovery are separate identities. Restore approval must come from an independent person. A DBA cannot independently delete immutable backup/evidence. Object Storage administration does not confer business or application-security authority. Exact database LOGINs, grants, secret custody and production provisioning remain separately gated.

## 4. Reports and organization data — APPROVED-CONSTRAINT

The effective authorization predicate is:

`EffectiveAccess = Permission \cap OrganizationScope \cap DataClassification`

A role alone is insufficient. The user must hold the operation permission, be within the data's explicit organization scope and be authorized for its classification.

Explicit scope dimensions are:
- `LegalEntityScope`
- `BranchScope`
- `WarehouseScope`
- `DepartmentScope`
- `AssignedRecordScope` where access is limited to assigned customers/cases/records.

An empty scope means deny, never global access. Scope does not propagate from one module to another. Backend enforcement is mandatory; PostgreSQL enforcement should be used as an additional control where practical. Export/print of sensitive information requires a separate permission and Audit evidence. Aggregated management reports must not leak out-of-scope personal detail through filters, drill-down, pagination or export.

The initial report policy is:
- Financial ledger/reporting: Accountants, Product Owner and specifically authorized finance managers; System Manager receives no access solely by virtue of a technical role.
- User data: System Manager only the fields needed for user administration; secrets, passwords, tokens and credentials are never readable.
- Audit reports: Product Owner and authorized security-auditor roles; System Manager sees only technical events necessary for its task.
- Inventory/warehouse: authorized inventory roles, restricted to WarehouseScope.
- Inventory valuation/cost: accountants and authorized finance roles; warehouse operators do not receive full cost data by default.
- Sales: seller, relevant manager and accountant, limited to permitted customer and operational scope.
- Purchasing: purchasing staff, accountant and authorized manager, limited to purchasing and organization scope.
- Aggregated management reporting: Product Owner and authorized managers; person-level details require a separate permission.

Product Owner access to aggregate management reports may be organization-wide. This is not blanket authorization to inspect or export every sensitive personal record.

## 5. Emergency Access — OWNER-APPROVED POLICY; IMPLEMENTATION OPEN

Emergency Access is a temporary, operation-specific grant, not a permanent Super Admin role.

1. The request identifies the requester, incident, reason, exact operations, resources and scope.
2. Activation requires approval by the Product Owner or an authorized independent approver. The approver must be distinct from the requester and emergency operator; the Product Owner may approve when not acting as requester/operator. A System Manager cannot grant emergency access to itself. The narrow Product Owner Branch/Warehouse assignment exception does not apply.
3. The initial grant lasts no more than **60 minutes**. Extension requires a new reason and fresh approval. Authorization must expire synchronously at expiry even if a session remains open; background cleanup is not the security boundary.
4. The allow-list is limited to: unlocking a locked account; revoking an invalid session; credential reset with independent controls; and isolated Backup restore with two-person approval.
5. Emergency Access must deny: sales, purchases and inventory mutation; Journal/Posting; Product Owner binding changes; WORM retention changes; Audit or Movement Ledger deletion; and permanent administrative elevation.
6. Every action records EmergencyAccessId, requester, approver, incident/reason, operation/resource scope, start/expiry/revocation, action and result. Emergency credentials/tokens are revoked and the session is closed at termination. Evidence is committed to local Audit and delivered to the External Anchor.

For recovery when both System Managers are unavailable, the previously approved O-31 rule remains: offline two-part recovery material is held separately by two trusted persons and requires both persons' presence and cooperation.

This owner decision supersedes the earlier O20-F draft policy's 30-minute/two-approver baseline for ordinary Emergency Access activation. The O-31 two-person recovery condition and the explicit two-person Backup-restore approval remain. O20-F documents, runtime predicates and tests must be updated before this policy is considered implemented.

## 6. Worker and service identities — APPROVED

Define separate identities by trust boundary, data domain and sensitive task, not by each C# class. The approved conceptual set is:
- `inventory-worker`: only assigned Inventory jobs and approved Inventory Outbox/Inbox operations.
- `audit-delivery-worker`: only Audit event delivery and durable ACK processing.
- `platform-worker`: only assigned Platform jobs.
- `backup-writer`: backup read/production and write to the designated backup destination.
- `recovery-runner`: controlled backup read and restore to the approved destination.
- `anchor-writer`: create signed manifests/checkpoints and new immutable versions only.

Each Worker has an independent identity and secret; no interactive login, password login, human session or use of Product Owner/System Manager credentials is permitted. Permissions are least-privilege by resource, operation and schema. Secrets come from approved Secret Custody, never source, plaintext configuration or logs. Rotation and revocation are independent. Audit records preserve both the Worker identity and initiating human actor. Restart, connectivity loss and lease expiry must permit safe recovery; replay must not create duplicate business effects. Each Worker can be disabled independently. No Worker can change user roles, run general migrations or delete Audit evidence.

This conceptual approval does not approve an additional production PostgreSQL LOGIN, credential, grant, pool binding or live delivery activation. The existing N.6 database-identity baseline and AuditSecurity acceptance gates remain in force until separately reconciled.

## 7. O-21 External Anchor / WORM — APPROVED-CONSTRAINT

### 7.1 Provider capability

Use independent Object Storage with an S3-compatible API and demonstrated support for:
- Object Versioning;
- Object Lock in **COMPLIANCE** mode;
- Legal Hold;
- denial of deletion/rewrite of locked versions and denial of retention shortening, including by provider/root administrators;
- audit of access and administrative changes;
- administration independent of the main application/server operators;
- encryption in transit and at rest.

GOVERNANCE mode alone is not sufficient. “S3-compatible” is not proof of WORM. No vendor is selected by this decision. The chosen provider must pass real provider-specific negative tests for locked-version deletion and retention shortening, including tests using the highest available provider administrator. The actual service contract, legal terms, region, failure model, support, accessibility and cost remain to be assessed.

### 7.2 Region and administration

The policy region is **Iran**. The primary Anchor must be outside the physical location of the central business server, preferably in another Iranian city, with independent administrative access. A second WORM copy in another geographically and administratively independent location is preferred. Cross-border transfer of data or evidence requires prior legal review and independent Product Owner approval; no cross-border transfer is implied or authorized.

### 7.3 Owner-approved internal retention baseline

| Evidence class | Owner-approved baseline |
|---|---|
| Commercial/financial evidence for sales, purchases, receipts, payments, inventory, valuation and ledger | 10 years from the end of the relevant financial year |
| Sensitive permission, role and ownership changes | 10 years |
| Purely technical/security logs not linked to a commercial/financial record | 2 years |
| Evidence under judicial, audit or Legal Hold | Until formal authorized release |

These are Product Owner internal policy baselines, not a blanket legal opinion or automatic classification of every database row, personal record or log. Iran counsel/records authority must classify the actual operator and each record class, verify the legal basis and trigger, and approve Hold/media/export/replica/backup scope and lawful disposal. Where classification is uncertain, retain and fail closed; automatic deletion remains disabled until the legal and operational controls are accepted. Retention of a locked version cannot be shortened.

### 7.4 Anchored evidence format

External Anchor is not a second copy of all customer/commercial payloads. It stores signed manifests and hash checkpoints. Each manifest includes the evidence time range, source identifier/scope, first and last sequence, event count, root/Merkle hash, previous checkpoint hash, hash algorithm, signing-key identifier, trusted creation/signing time and digital signature. Passwords, tokens, secrets, unnecessary personal data and sensitive raw payloads are excluded.

A specific provider, storage account, region endpoint, signing-key custody, KMS/HSM, independent custodians, key backup/recovery/rotation/revocation and verifier/operations service are not selected or provisioned by this decision. These remain O21 closure gates.

## 8. Product Owner governance — GOV-OWNER-AUTH-001 — OWNER-APPROVED

The Product Owner is the highest final decision authority for the MD-Gsm product and its controlled changes. This includes architecture and product scope; module/contract changes; access-governance policy; database and migration decisions; release and capability activation; priorities and project/capability stoppage; appointment/replacement of technical and specialist responsibilities; and acceptance of residual risk within legal and security constraints.

Specialist responsibilities remain explicit. The responsible specialists must perform their domain review, design, implementation, tests, audit and evidence production, and may identify a proposal as technically invalid or unsafe. They are accountable for the correctness of their specialist work. The Product Owner makes the final product decision after receiving the required specialist evidence: approve, reject, return for correction, prioritize or stop the proposed change.

The following eight workstreams are specialist evidence responsibilities with final product acceptance by the Product Owner:
1. Inventory lock order and transaction-boundary semantics — Inventory technical owner and cross-module architect prepare the design/evidence.
2. Transaction Participant and Unit of Work implementation — module teams implement and prove it.
3. UUIDv7 conformance — Platform proves conformance and runtime compatibility.
4. Public IdentityAccess contract — IdentityAccess and Organization owners establish the technical contract and evidence.
5. Durable AuditSecurity ACK — AuditSecurity and the event consumer establish and prove the durable acknowledgement semantics.
6. Isolated Backup/Restore — infrastructure/security owners prove isolation and restore safety.
7. Legal retention/records classification — qualified legal/records authority classifies and approves within applicable law.
8. Independent exact-SHA Security review — an independent reviewer records findings, severity, disposition and retests.

The owner-governance decision does not turn specialist evidence into optional advice, remove required Platform/module/legal constraints, or allow the Product Owner to self-certify an independent security review. The Product Owner cannot falsify evidence, report an unrun/failed test as passed, waive applicable law, delete immutable evidence or bypass mandatory release gates.

Every material change requires a request, rationale, responsible executor, Product Owner decision, version and Audit record. Release is permitted only after all applicable technical, security, legal, review and repository gates pass. This policy is a controlled governance amendment candidate for the governing architecture Master; it does not silently rewrite the Master or grant implementation/Go-Live authority.

## 9. Existing Platform and module-owner decisions retained

- `PLATFORM-APPROVAL-SCOPE-01` remains `APPROVED-CONSTRAINT`: Platform accepted UUIDv7 as the identifier standard and committed to infrastructure support for Transaction Management, Unit of Work, Lock, Deadlock and Recovery.
- UUIDv7 source conformance, multi-instance clock/rollback, cross-language and compatibility evidence remain required.
- Global Inventory lock order and transaction semantics are accepted at the architecture level, but the Inventory and cross-module architecture owners must establish the concrete ranks, isolation/linearization, timeout/deadlock, cancellation, retry and compatibility contract. This is not an approval attributable solely to Platform.
- IdentityAccess public contract/transaction guard still requires formal IdentityAccess module-owner acceptance and Organization concurrence. The disposable PostgreSQL proof does not replace module integration.
- AuditSecurity production API, authenticated durable ACK, event acceptance, worker identity, retry/quarantine/reconciliation, verifier continuity, alerting and runbook remain open.

## 10. Required controlled document reconciliation

The following successor work is required; historical versions must remain unchanged:
1. Supersede Product Owner binding proposal v0.1 with an IdentityAccess/Platform/Security-reviewable successor that models the installation-created, immutable/non-transferable AccountId binding and same-principal-only recovery.
2. Reconcile O20-E data ownership, logical/physical assignment designs and permission contract against the accepted Product Owner authority matrix and the already accepted narrow Branch/Warehouse self-assignment/second-approver exception.
3. Supersede O20-F v0.3 with the 60-minute allow-list policy, approver separation, O-31 preservation, synchronous expiry and matching positive/negative tests.
4. Supersede O20-E retention v0.6 with the four owner-approved retention baselines, preserving the legal classification, Legal Hold and no-auto-delete gates.
5. Reconcile RowScope/report access inventory, blocker matrix, readiness review and Stage 08 register; distinguish policy acceptance from runtime implementation and legal/provider evidence.
6. Register `GOV-OWNER-AUTH-001` as a controlled change request/addendum under the sole current architecture Master. Do not directly mutate the Master outside its controlled amendment/review process.
7. Update source checksum manifests and run all exact-head CI, spec-lint, migration/checksum and required platform validation after the document/test successors are committed.

## 11. Remaining gates and non-authorization

The following remain open and must not be represented as completed:
- trusted Product Owner binding implementation, immutable lifecycle enforcement and same-AccountId recovery proof;
- formal IdentityAccess owner acceptance and Organization concurrence for the public contract/transaction guard;
- Organization permission namespace/role mapping and any production assignment/reporting consumer;
- production RowScope enforcement in backend queries, exports, reports and applicable database controls;
- real Emergency Access identity binding, allow-list capability, expiry/revocation, durable audit and operational runbook;
- real Worker identities, secret custody, production DB permissions and independently revocable credentials;
- Platform/module-owner global lock order and actual cross-module Unit of Work participant;
- canonical AuditSecurity API/durable ACK and operational delivery/reconciliation;
- provider-specific Object Lock Compliance destructive-negative tests, KMS/HSM signing-key custody, independent verifier, restore, alerts and runbook;
- Iran legal/records classification, Legal Hold authority/lifecycle, backup/replica/export/media scope and lawful disposal procedure;
- independent exact-SHA Security/Architecture review, WG-0 reservation and separate explicit Product Owner G1 authorization.

This packet creates no production Product Owner binding, schema/DDL/migration, permission seed/grant, Emergency Access grant, Worker credential, Backup/Recovery LOGIN, external WORM/KMS/HSM resource, reporting grant, production Audit delivery, purge worker or data deletion. It does not authorize merge, direct main change, review bypass, Organization G1 or Go-Live.

**Disposition:** the policy decisions in §§2–8 are OWNER-APPROVED within their stated limits. Technical implementation, module-owner acceptance, legal validation, production operations and independent review remain OPEN. O20-E, O-20 and O-21 remain OPEN. Organization G1 is NOT READY / NOT GRANTED.

**END — MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6**


## 10. Audit delivery worker identity binding — WG-0 technical selection, 2026-11-24

This addendum records the WG-0 technical selection of a concrete identity name and trust boundary for the previously approved conceptual worker `audit-delivery-worker`. Owner ratification is required before any live PostgreSQL role, credential, grant, worker host, API listener or production delivery activation is provisioned.

### 10.1 Identity selection

| Layer | Canonical name | Classification |
|---|---|---|
| Logical operational identity | `audit-delivery-worker` | Sixth logical operational worker identity, separate from the original five N.6 categories |
| PostgreSQL LOGIN | `audit_delivery_worker` | Dedicated non-interactive LOGIN principal; NOINHERIT and non-elevated |
| PostgreSQL ACL role | `mdgsm_audit_delivery_worker` | NOLOGIN, NOINHERIT capability role; worker may enter it only through one SET-only membership |

The LOGIN-to-ACL membership must be exactly one edge with `ADMIN FALSE`, `INHERIT FALSE`, `SET TRUE`. The LOGIN has no useful application-table rights before `SET ROLE`; each connection must verify `session_user = audit_delivery_worker` and `current_user = mdgsm_audit_delivery_worker` after setup. Pool reset must clear role state and re-verify identity before reuse. No membership or privilege is granted to `mdgsm_runtime`, and its existing grants must not be broadened.

### 10.2 Exact source-Outbox capability

The identity may operate only the explicitly registered source-module Outbox tables, in the configured source database/schema(s):

- `SELECT` on immutable `audit_outbox_events` rows (event envelope, stable EventId, contract version and canonical intent digest);
- `SELECT` on `audit_outbox_delivery`;
- column-scoped `UPDATE` on delivery metadata only: state, attempts, next-attempt time, lease expiry/token, claimed-by identity, acknowledgement reference, last-failure code, quarantine reason/time and update timestamp.

It must not INSERT/UPDATE/DELETE/TRUNCATE immutable event rows; change EventId, intent digest or payload; delete Outbox or delivery rows; perform DDL/GRANT/role changes; access business aggregate tables, Organization/Branch/Warehouse data, IdentityAccess accounts/credentials/sessions/recovery material, canonical AuditSecurity event/stream-head tables, migration history, backup or reporting data; or assume runtime, migration, schema-owner, backup, reporting, emergency, anchor or other worker roles.

The worker is an outbound delivery agent only. It submits the exact source intent to the authenticated AuditSecurity ingestion API and stores the returned matching durable receipt in source-local delivery metadata. It is not a canonical-ledger database writer and does not possess a direct AuditSecurity database connection.

### 10.3 Credential and runtime boundary

The worker has its own independently revocable service credential/certificate, not a human password or Windows interactive account. Production credential issuance, storage, rotation, certificate binding/mTLS policy, service installation, connection-pool limits and deployment principal remain Platform/Security deployment gates. The application must not embed the credential in source, installer, ordinary configuration, logs or a store database. The worker can be disabled independently. No production LOGIN, secret, certificate, membership, schema grant or ACL grant is created by this decision.

### 10.4 Scope and acceptance

This identity decision does not approve a specific number of source modules, Outbox schema migration, retry schedule, operational alert threshold, API hosting topology or the separate AuditSecurity ingestion-service database principal. Those require their own technical and exact-head acceptance. Existing disposable worker tests are supporting least-privilege evidence only; they do not constitute production ACL acceptance.

**Decision effect:** the WG-0 technical selection of the logical identity and proposed PostgreSQL LOGIN/ACL names is recorded. Owner ratification remains required before provisioning. Credentials, grants, deployment, live API delivery, AuditSecurity service DB identity, O20 closure, O21 closure, Organization G1 and Go-Live remain separate gates.
