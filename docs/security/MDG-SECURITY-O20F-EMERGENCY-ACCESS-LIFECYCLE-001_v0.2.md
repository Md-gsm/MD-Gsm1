# O20-F Emergency Access — Allow-list and Time-Bounded Activation Design

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20F-EMERGENCY-ACCESS-LIFECYCLE-001 |
| Version / Date | 0.2 / 2026-11-22 |
| Status | OWNER-ACCEPTED POLICY; DISPOSABLE PROOF AND TECHNICAL ACCEPTANCE OPEN |
| Scope | Emergency support identity lifecycle within O-20; no production access |
| Related baseline | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 v1.4 |

## 1. Security objective and identity boundary

Emergency access is disabled by default. The logical identity emergency_operator is not provisioned in production or CI by this document. No LOGIN, credential, membership, grant, role, allow-list row or production connection is created. The purpose is to specify a fail-closed activation lifecycle for a separately controlled emergency operator, not to create a standing DBA identity.

The emergency operator is distinct from runtime_app, migration_runner and backup_operator. It cannot inherit broad owner/superuser rights. Any eventual DB role must be non-superuser, non-createdb, non-createrole, non-replication, NOINHERIT, have no object ownership and receive only the narrow emergency capability explicitly approved for the incident. No broad schema grant or permanent membership is permitted.

## 2. Allow-list record and activation request

The allow-list is a protected security-control record containing: immutable EmergencyGrantId; exact emergency principal/identity reference; allowed operation set; resource/database scope; incident/ticket reference; reason; requested-by principal; two distinct approver principals; approved-at UTC; not-before UTC; expires-at UTC; policy version; state; activation/revocation event IDs; correlation and idempotency keys. Secrets and credential verifiers are never stored in the allow-list or audit payload.

A request alone grants no access. Activation requires two distinct authorized approvers; neither may be the requester or emergency operator. No person approves their own request. The Product Owner self-assignment exception for Organization assignments does not apply to emergency activation. The emergency operator must be independently identified and cannot approve or activate its own access.

## 3. Time boundary and one-use semantics

PROPOSED maximum activation window: 30 minutes, using database/server UTC only. expires_at must be greater than activation time and no more than 30 minutes after activation. The grant is incident-scoped and one-use; a second activation requires a new request and two new approvals. No automatic extension or future-dated standing grant. At now >= expires_at, every protected operation is denied even if cleanup has not run. The 30-minute maximum is a proposal requiring owner/security acceptance before implementation.

## 4. State machine

REQUESTED → APPROVED → ACTIVE → EXPIRED or REVOKED. REJECTED and CANCELLED are terminal. Only a two-approver authorization can move APPROVED to ACTIVE. The database authorization boundary checks allow-list state, exact principal, operation/resource scope and server time on every privileged operation. Expiry denies synchronously; asynchronous cleanup is housekeeping only. Revocation is immediate and invalidates active sessions/connections or denies their next operation; it is not dependent on a scheduled worker.

Any missing, duplicate, stale, malformed, unavailable or conflicting allow-list record denies. The emergency path never falls back to ordinary runtime role membership. A grant is not activated if audit intent cannot be durably recorded.

## 5. Audit and evidence

Every request, approval, rejection, activation, use, expiry, revocation, failed attempt and cleanup is recorded through a transactional local audit-intent outbox and delivered to AuditSecurity's canonical event ingestion boundary. Stable EventId deduplicates delivery. The local outbox records immutable attempts, lease, retry, quarantine and durable acknowledgement reference. Delivery outage raises a critical alert and reconciliation item; it does not erase intent. If the operation's required audit intent cannot be committed locally, activation/use is denied.

Minimum event fields: EventId, EmergencyGrantId, principal reference, actor/requester, both approvers, incident reference, allowed operation/resource scope, decision/outcome, reason, policy version, server UTC time, expiry, correlation and idempotency identifiers. No credentials, passwords, bearer tokens or raw secrets.

## 6. Revocation, cleanup and recovery

Revocation may be initiated by either approver, a designated security authority or the emergency controller; it is recorded durably and takes effect synchronously. Expiry is enforced by the authorization predicate, not by cleanup. Cleanup removes only ephemeral grants/sessions after deny state is committed and verified. Reconciliation compares allow-list state, DB role memberships/ACLs, active sessions, audit outbox and canonical AuditSecurity acknowledgements. Any mismatch is a security incident and keeps O20-F OPEN.

## 7. Disposable PostgreSQL proof matrix required

The O20-F proof must run only on a uniquely named, marked disposable PostgreSQL 18 database and ephemeral identities, and must assert:

1. no allow-list entry → denied;
2. request without two approvals → denied;
3. same approver twice, requester as approver, or emergency operator as approver → denied;
4. exact allow-list principal/operation/resource permits only the approved action;
5. different principal, database, schema, operation or resource → denied;
6. now exactly equal to expires_at → denied;
7. delayed cleanup cannot extend expired access;
8. immediate revocation denies the next operation and invalidates active session/connection capability;
9. replayed activation/idempotency request cannot create a second grant;
10. audit-intent insert failure prevents activation/use;
11. AuditSecurity outage retains outbox intent, retries idempotently, alerts and reconciles;
12. DB role flags, memberships, object ownership, ACLs and default ACLs remain within the approved least-privilege matrix;
13. cleanup removes all ephemeral roles/memberships/grants and proves absence;
14. no production host, database, secret or credential is reachable by the test.

The proof must retain machine-readable catalog/effect evidence and artifact digest. Exact-head Linux and Windows PostgreSQL 18 runs and independent security review are required. No PASS may be claimed from a design document.

## 8. Acceptance and non-authorization

The 30-minute maximum, approver eligibility/authority, emergency operator identity binding, exact DB capabilities, event schema/ACK, alert thresholds, recovery runbook and retention require explicit owner/security/Platform acceptance. The proof matrix has not yet been executed. No emergency identity, LOGIN, credential, membership, grant, schema, table, migration or production access is created by this design.

**Disposition: O20-F remains OPEN. Overall O-20 remains OPEN.**

**END — MDG-SECURITY-O20F-EMERGENCY-ACCESS-LIFECYCLE-001 v0.1**


## 9. Owner acceptance addendum — 2026-11-22 (v0.2)

OD-04 in `MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.1` is OWNER-ACCEPTED:

- Every emergency activation requires two distinct authorized approvers. Duplicate approver identities are invalid. The requester and emergency operator cannot approve their own activation; approver eligibility and trusted identity binding must be server-enforced.
- Each activation is one-use and has a maximum 30-minute lifetime measured using trusted database/server UTC. A second activation requires a new request and two new approvals.
- At `now >= expires_at`, every protected operation is denied synchronously; cleanup is housekeeping and cannot extend access. Revocation is also synchronous at the authorization boundary.
- The Organization Product Owner self-assignment exception does not apply to emergency access.

The acceptance is policy-only. The disposable PostgreSQL 18 proof matrix in section 7 remains NOT RUN. Emergency identity binding, exact DB capabilities, durable audit ACK, alerting, recovery runbook, retention, Linux/Windows evidence and independent review remain open. No emergency LOGIN, credential, role, membership, grant, allow-list record or production access is created. O20-F and overall O-20 remain OPEN.

**END — O20-F v0.2 owner acceptance addendum**
