# O20-F Emergency Access — Owner-Approved 60-Minute Policy and Disposable Proof

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20F-EMERGENCY-ACCESS-LIFECYCLE-001 |
| Version / Date | 0.4 / 2026-11-23 |
| Supersedes | v0.3 |
| Status | OWNER POLICY ACCEPTED; DISPOSABLE PROOF UPDATED; EXACT-HEAD CI / TRUSTED IDENTITY / PRODUCTION CAPABILITY / AUDIT ACK OPEN |
| Scope | Emergency support identity lifecycle within O-20; no production access |
| Governing policy | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6 |
| Owner decision record | PR #92 comment 7094120301 |

## 1. Controlling owner decision

This successor supersedes the v0.3 rule requiring two approvers for every activation and its 30-minute ceiling. The controlling owner policy is:

- Every request names the requester, incident, reason, exact operation, resource and scope.
- An activation requires approval by the Product Owner or an authorized independent approver. The approver must be distinct from both the requester and the emergency operator. A System Manager cannot grant emergency access to itself.
- The ordinary emergency grant lifetime is no more than **60 minutes**, measured using trusted database/server UTC. A shorter grant may be requested. Extension requires a new reason and fresh approval.
- Authorization is denied synchronously when `now >= expires_at`, even while a session remains open. Cleanup is housekeeping only.
- The Product Owner's narrow Organization Branch/Warehouse assignment exception is not an Emergency Access self-approval exception. If the Product Owner is the requester or emergency operator, that same principal cannot approve the request.
- Ordinary Emergency Access activation requires one authorized independent approver, not two.
- **Two-person approval remains mandatory for isolated Backup restore.** The previously accepted O-31 offline two-part recovery package also remains: when both System Managers require emergency recovery, two independent trusted persons hold separate components and must both be present/cooperate.

## 2. Allow-list

The permitted emergency operations are limited to:
1. unlock a locked account within the approved recovery process;
2. revoke an invalid session;
3. reset a credential only with the separately required independent controls;
4. restore Backup only in an isolated environment and with two-person approval.

The following are expressly denied:
- sales, purchasing or inventory mutation;
- Journal creation or Posting;
- changing, replacing, disabling or transferring the Product Owner binding;
- changing or shortening WORM/Object Lock retention;
- deleting Audit evidence or Movement Ledger history;
- granting permanent administrative access or converting an emergency grant into a standing role.

The emergency identity is a temporary, scoped capability, not a permanent Super Admin account. It is limited to an exact person, operation, resource/database/schema and time window. A valid role alone is not enough.

## 3. Activation and authorization invariants

Activation must be serialized against the grant record and be single-use. The activation transaction records:
- EmergencyAccessId;
- requester, approver and emergency operator principals;
- incident, reason, operation and exact resource/scope;
- requested/approved lifetime;
- trusted activation and expiry timestamps;
- state and revocation/termination metadata;
- immutable local audit intent and delivery/reconciliation references.

At authorization time, the server rechecks current trusted requester/approver/operator eligibility, exact operation/resource/database/schema, active state, current revocation state, installation/database binding and expiry. For Backup restore, two distinct eligible approvers must be recorded; for other allow-listed operations exactly one independent approver is sufficient. The requester's identity and approver's identity cannot be equal, and the operator cannot equal the approver.

Emergency token/credential revocation and session closure must occur at termination. If authorization state, trusted identity resolution, clock, scope, audit-intent persistence or expiry cannot be verified, the operation is denied.

## 4. Disposable PostgreSQL proof — bounded scope

The branch proof fixture is updated to:
- enforce a maximum `requested_seconds` of 3600;
- permit one authorized independent approver for ordinary emergency operations;
- permit an explicitly listed Product Owner to approve only when distinct from requester and operator;
- require two distinct approved principals for `BACKUP_RESTORE`;
- reject self-approval, operator approval, expired approver eligibility, unlisted operation/resource/database/schema, second activation/replay and expired grants;
- accept the 3600-second boundary and reject 3601 seconds;
- prove expiry denial at `now >= expires_at` without relying on cleanup;
- retain atomic rollback when local audit-intent insertion fails;
- keep the local Outbox pending when no canonical AuditSecurity ACK is simulated.

The allow-list principals in this fixture remain synthetic identities. The ephemeral PostgreSQL emergency LOGIN remains deliberately unprivileged, with no actual production object/function capability. The Product Owner and approver rows are test fixtures, not live IdentityAccess principals.

## 5. Proof limitations and production gates

This disposable PostgreSQL proof does not establish:
- trusted production resolution of requester, approver, Product Owner or emergency-operator identities through IdentityAccess;
- protected production allow-list administration, incident intake, expiry/extension/revocation lifecycle or recovery;
- a real narrowly scoped database/API capability for each allow-listed operation;
- production token/session invalidation, connection revocation or operator isolation;
- canonical AuditSecurity API, authenticated durable ACK, retry/quarantine/reconciliation, alerting or runbook;
- actual isolated Backup restore, O-31 package controls or production operational readiness.

Before O20-F can close, these controls need implementation/evidence, exact-head Linux/Windows PostgreSQL validation, module-owner review and independent Security review. The owner policy does not provision a LOGIN, secret, grant, credential, route, migration, external resource or production access.

## 6. Disposition

The 60-minute policy and approver cardinality are OWNER-APPROVED. The source/test update is bounded disposable evidence pending exact-head CI. Trusted identity mapping, production allow-list/capability enforcement, real restore controls, canonical AuditSecurity ACK and operational evidence remain OPEN.

**O20-F remains OPEN. Overall O-20 remains OPEN. Organization G1 is NOT GRANTED.**

**END — MDG-SECURITY-O20F-EMERGENCY-ACCESS-LIFECYCLE-001 v0.4**
