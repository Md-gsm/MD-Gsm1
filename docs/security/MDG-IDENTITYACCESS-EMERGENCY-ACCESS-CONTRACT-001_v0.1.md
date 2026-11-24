# IdentityAccess Emergency Access — Module Owner Contract
| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-EMERGENCY-ACCESS-CONTRACT-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **IDENTITYACCESS MODULE-OWNER TECHNICAL CONTRACT — PROPOSED FOR FORMAL OWNER ACCEPTANCE; NOT YET PUBLISHED** |
| Policy authority | O20-F owner-approved policy in `MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6` |
| Related public contract | IdentityAccess Contracts API v1.2 |
| Scope | IA-01 trusted principal binding; IA-02 independent approval; IA-03 session-bound execution and revocation |
| Implementation boundary | Design/acceptance contract only. No endpoint, schema, migration, seed, grant, emergency role, credential or production access is authorized. |

## 1. Purpose and module ownership

This contract completes the IdentityAccess-side technical boundary for Emergency Access. IdentityAccess is authoritative for Account, Credential, Session, Role, Permission, authenticated principal, emergency request/approval records, emergency execution authorization and emergency lifecycle state. It does not own Branch/Warehouse business data, Inventory/Finance mutations, Backup content, or canonical AuditSecurity storage.

IdentityAccess MUST NOT delegate its own account/session/credential controls to an emergency role. Emergency Access is a time-bounded, operation-specific authorization overlay; it is never a permanent role, Super Admin expansion, impersonation facility, or alternate login.

This document is the IdentityAccess technical owner proposal. The repository author cannot manufacture a formal attributable module-owner acceptance. Publication requires a recorded review by the designated IdentityAccess module owner against the exact commit containing this document. Until then, all proposed semantics are non-operative.

## 2. Binding policy constraints

The following are inherited constraints, not reopened design choices:

1. Ordinary Emergency Access activation has exactly one independent approver. The approver MUST differ from both requester and executing operator.
2. Initial grant duration is at most 60 minutes. Extension is a new request with a new reason and fresh approval; no in-place extension.
3. Authorization expires synchronously at the stored expiry instant. Background cleanup is housekeeping only and is never the security boundary.
4. The only allowed operation families are:
   - unlock a locked account;
   - revoke an invalid session;
   - credential reset, subject to the normal independent controls;
   - isolated Backup restore, which always requires two independent approvers.
5. Emergency Access MUST deny sales, purchases, Inventory mutation, Journal/Posting, Product Owner binding changes, WORM retention changes, Audit/Movement Ledger deletion, permanent administrative elevation, arbitrary SQL/DBA actions, impersonation, and all operations not explicitly allow-listed.
6. The Product Owner may approve only when not the requester and not the executing operator. The narrow Product Owner Branch/Warehouse assignment exception does not apply.
7. Isolated Backup restore requires two approvers distinct from requester and operator, and remains subject to O-31 two-part recovery where applicable.
8. Emergency actions must be attributable, audited, revocable, idempotent and fail-closed. At termination, emergency authorization is revoked and the operator session is closed.

## 3. Identity and trust model

### 3.1 Authenticated principal

Every request, approval, revocation and execution command MUST derive the actor from the validated server-side IdentityAccess principal. The caller cannot supply or override AccountId, SessionId, role, permission, owner status, requester, approver, operator or session state.

The minimum actor binding is:
- AccountId (opaque stable account identifier);
- SessionId (opaque non-secret session identifier);
- credential/principal version;
- authenticated-at timestamp;
- current account enabled state;
- current session non-revoked state and valid lease;
- current permission/eligibility decision.

A missing, disabled, revoked, expired, stale, ambiguous or unavailable principal yields Denied/Unavailable. There is no fallback to username, email, role name, client claims, Product Owner labels or service-supplied actor IDs.

### 3.2 Requester, approver and operator

The request stores immutable requester AccountId and SessionId. Approval stores immutable approver AccountId and SessionId. Execution stores immutable operator AccountId and SessionId. These identities are resolved independently at each transition.

For ordinary Emergency Access:
- approver AccountId MUST differ from requester AccountId;
- approver AccountId MUST differ from operator AccountId;
- approver SessionId MUST be live at approval;
- the approval cannot be self-approved through another session of the same account;
- the operator must be authenticated and eligible for the exact operation;
- an approver cannot silently become the operator.

For Backup restore, two distinct eligible approver accounts are required, each distinct from requester and operator. Duplicate sessions of one account do not satisfy person-count requirements.

The requestor and operator MAY be the same AccountId only if the operation-specific eligibility policy explicitly permits it; this does not relax approver separation. If no such policy exists, deny.

### 3.3 Product Owner binding

Product Owner authority is resolved only through the protected IdentityAccess Product Owner binding contract. It is not inferred from `super_admin`, username, email, GitHub identity, client flag or role membership. Until immutable installation-scoped binding and same-AccountId recovery are implemented and accepted, Product Owner eligibility is Unavailable and no Product Owner-specific approval path can be used.

## 4. Emergency operation allow-list

The allow-list is closed and versioned. Every operation has an exact operation key, target type, target identifier, allowed state transition, preconditions and audit class. Wildcards, caller-defined operation names, arbitrary resource scopes and generic "admin" capabilities are forbidden.

| Operation key (candidate) | Target | Permitted effect | Mandatory restrictions |
|---|---|---|---|
| `identity.emergency.account.unlock` | AccountId | Clear an active lockout | Account must otherwise be enabled; no account enablement, role or permission changes |
| `identity.emergency.session.revoke` | SessionId | Revoke the named invalid/compromised session | Cannot create/extend a session or revoke all sessions by wildcard |
| `identity.emergency.credential.reset` | AccountId | Initiate the existing controlled credential-reset flow | Must preserve reset policy, one-time challenge, expiry, rate limits and credential-version/session revocation semantics; emergency approval does not reveal or set plaintext credentials |
| `backup.emergency.restore` | Pre-registered isolated restore target | Start an isolated restore workflow | Two approvers, isolated destination, separate Recovery identity, explicit restore manifest and O-31 controls; never restore over live production in this contract |

These strings are proposed exact operation identifiers, not approved PermissionCatalog entries. They MUST NOT be seeded or granted until IdentityAccess owner acceptance, permission catalog reconciliation and the applicable security review. The allow-list version is stored with each request and execution.

No other operation can be added by configuration alone. Adding or changing an operation requires a new contract version, threat-model review, owner approval, test vectors and exact-head CI.

## 5. Lifecycle state machine

A request is immutable in identity, operation, target and scope after submission. Any material change requires cancellation and a new request.

States:
- `PENDING_APPROVAL`
- `APPROVED`
- `ACTIVE`
- `REVOKED`
- `EXPIRED`
- `CANCELLED`
- `DENIED`
- `FAILED_TERMINAL`

Allowed transitions:
1. Create: authenticated eligible requester creates `PENDING_APPROVAL`.
2. Deny: eligible approver denies `PENDING_APPROVAL` → `DENIED`.
3. Approve: one eligible independent approver (two for Backup restore) approves → `APPROVED`.
4. Activate: IdentityAccess atomically verifies all approvals, live identities, request expiry, allow-list version and exact operation binding → `ACTIVE`.
5. Execute: operator submits one operation-bound command; IdentityAccess revalidates actor/session, request state, expiry, operation, target, idempotency key and approval separation.
6. Complete: successful single-use operation → `REVOKED` (consumed) with terminal result.
7. Revoke: requester, eligible approver or authorized security operator may revoke before expiry, subject to policy → `REVOKED`.
8. Expire: any authorization check at or after expiry treats it as `EXPIRED`, irrespective of cleanup job state.
9. Cancel: requester may cancel while pending; an approved/active request requires revocation, not mutation.
10. Unavailable/ambiguous transition: deny and leave no active authorization. A retry must use the same idempotency key and return the recorded outcome.

No transition may move a terminal state back to pending/approved/active. Re-approval or extension creates a new EmergencyAccessId.

## 6. Request and approval data contract

The following is the logical minimum. Physical table names and migration are not authorized by this document.

**EmergencyAccessRequest**
- EmergencyAccessId: UUIDv7 generated under the accepted Platform UUIDv7 contract;
- RequestVersion and AllowListVersion;
- RequesterAccountId, RequesterSessionId;
- OperationKey, TargetType, TargetId, exact ResourceScope;
- IncidentReference, ReasonCode, bounded human-readable justification;
- RequestedAtUtc, NotAfterUtc;
- State, StateVersion;
- CorrelationId and RequestId/IdempotencyKey;
- immutable request payload digest.

**EmergencyAccessApproval**
- ApprovalId (UUIDv7);
- EmergencyAccessId and RequestVersion;
- ApproverAccountId, ApproverSessionId;
- Decision (Approve/Reject);
- DecisionAtUtc;
- reason;
- policy/eligibility version;
- unique constraint preventing more than one decision by the same AccountId for a request.

**EmergencyAccessExecution**
- ExecutionId (UUIDv7);
- EmergencyAccessId and approval-set digest;
- OperatorAccountId, OperatorSessionId;
- OperationKey, TargetType, TargetId, exact scope;
- IdempotencyKey;
- StartedAtUtc, CompletedAtUtc;
- outcome/result code and affected-object reference;
- transaction correlation and audit event reference.

No credential, password, OTP, bearer token, token digest, recovery secret, raw authorization header or unnecessary sensitive payload may be stored in these records.

## 7. Authorization decision contract

The IdentityAccess boundary exposes typed decisions, not raw role/grant lists:

- `EvaluateEmergencyRequestAsync(request)`
- `EvaluateEmergencyApprovalAsync(requestId)`
- `ActivateEmergencyAccessAsync(requestId)`
- `EvaluateEmergencyExecutionAsync(requestId, operationKey, target, idempotencyKey)`
- `RevokeEmergencyAccessAsync(requestId, reason)`

These names describe semantic operations, not approved C# signatures. Every result is one of Allowed, Denied or Unavailable and carries contract version, request version, policy/allow-list version, evaluated-at UTC and a stable reason code. Unavailable is never converted to Allow.

The consumer receives only a narrow operation-scoped decision for the exact request, operation, target, scope and expiry. It does not receive a generic elevated principal, role, permission set or reusable bearer credential.

## 8. Transaction, concurrency and idempotency

1. Request creation, approval, activation, revocation and execution authorization are serialized by a durable request version and state transition guard.
2. Every state-changing command uses a caller-owned PostgreSQL transaction and the same connection for IdentityAccess guard plus protected local mutation where the consumer owns a local mutation.
3. A consumer MUST NOT claim cross-module atomicity unless the Platform global lock order and all affected module owners have accepted the order. The existing IdentityAccess v1.2 transaction guard is not a global lock-order approval.
4. At commit-time, the transaction MUST revalidate the exact current Account, Session, credential version, request state/version, approval set, expiry, allow-list version and operation/target binding. The relevant rows remain locked until commit/rollback.
5. Expiry check uses authoritative PostgreSQL UTC time, not client clock. The authorization interval is half-open: `[ActivatedAtUtc, ExpiresAtUtc)`; at exact expiry it is denied.
6. Revoke, expiry, account disable, session revoke and credential-version change must prevent any later protected command from being authorized after the documented serialization point.
7. Each execution has a unique idempotency key scoped to EmergencyAccessId + operation + target. Replays return the recorded outcome; they do not repeat the side effect.
8. Unknown commit outcome is resolved by reading the durable execution/idempotency record; do not blindly repeat a non-idempotent effect.
9. Deadlock, timeout, connection loss, serialization failure or missing audit dependency returns Denied/Unavailable and creates no authorization bypass.
10. The global lock order, transaction time bounds and retry policy remain subject to Platform and affected module-owner acceptance.

## 9. Expiry, revocation and session termination

- Maximum ordinary grant: 3600 seconds; no rounding up or grace interval.
- Activation time and expiry are persisted by the authoritative database clock.
- Every execution rechecks expiry synchronously. A cleanup worker may mark records expired later but cannot grant access.
- Revocation takes effect at the transaction serialization point and is checked on every subsequent command.
- At expiry/revocation/consumption, the emergency authorization is unusable immediately and the operator session is revoked/closed according to the accepted session contract.
- A new emergency action requires a new request and approval. No renewal token or reusable elevation survives.
- A normal application session remaining open does not preserve Emergency Access.

## 10. Audit and evidence contract

Every create, approve, reject, activate, execute, deny, fail, revoke, expire and cancel transition emits a stable AuditIntent in the same local transaction as the state change. Required fields:
- EventId, EmergencyAccessId, RequestVersion, transition;
- requester, approver(s), operator AccountId and SessionId;
- operation, target type/id and exact scope;
- incident reference, reason code and justification digest;
- approval-set digest, allow-list/policy version;
- event timestamp, correlation/idempotency IDs;
- outcome and stable failure code.

Audit MUST NOT contain secrets, passwords, OTPs, access tokens, token hashes or raw credentials. Local AuditIntent is not canonical AuditSecurity acceptance. A durable canonical AuditSecurity ACK, external anchor delivery and operational reconciliation are separate gates; a missing required audit dependency must fail closed for protected activation/execution.

## 11. API boundary and route policy

No route names or HTTP methods are authorized by this design. Before implementation, IdentityAccess must publish the exact versioned API contract and ensure every route is covered by SessionAuthenticationMiddleware and explicit authorization metadata.

All routes require:
- validated session principal;
- anti-CSRF protection where cookie-based transport is used;
- request size/schema validation;
- rate limits per principal and target;
- generic denial responses that do not disclose account/resource existence;
- correlation and idempotency enforcement;
- server-side operation allow-list lookup;
- no client-supplied actor, approval, role, scope or expiry override.

Health/readiness routes remain unauthenticated only if they disclose no protected data or operations.

## 12. Failure semantics

| Condition | Result |
|---|---|
| Missing/invalid/revoked/expired session | 401 / Denied |
| Account disabled or credential version mismatch | 401 / Denied |
| Requester/approver/operator separation violated | 403 / Denied |
| Unknown operation key, target type or allow-list version | 403 / Denied |
| Missing/insufficient approval count | 403 / Denied |
| Expiry reached or request terminal | 403 / Denied |
| Resource missing/out of scope | Non-disclosing Denied |
| Database, AuditSecurity or authoritative dependency unavailable | 503 / Unavailable; no execution |
| Duplicate idempotency key, same payload | Return recorded outcome |
| Duplicate idempotency key, different payload | Conflict / Denied |
| Unexpected internal error | 500 generic; no sensitive detail |

## 13. Acceptance test matrix (required before publication)

### IA-01 — trusted principal binding
- Forged AccountId, SessionId, requester, approver, operator, role and owner flags are ignored.
- Actor is derived from a live validated server session only.
- Disabled account, revoked session, expired lease and credential-version mismatch deny.
- Multiple sessions for one AccountId cannot satisfy independent-person requirements.
- Product Owner authority unavailable until protected binding lifecycle is accepted; role/username/client claim cannot substitute.
- Principal changes between request and approval are reflected by current server-side resolution.

### IA-02 — independent approval
- Requester cannot approve their own request from same or another session.
- Approver cannot execute the approved request.
- One eligible independent approver activates ordinary operation; zero or two ordinary approvals are rejected unless policy explicitly requires two.
- Backup restore requires two distinct approvers, both different from requester and operator.
- Approver eligibility is rechecked at approval and activation.
- Approval is bound to exact request version, operation, target, scope, expiry and allow-list version; any change invalidates approval.
- Self-approval and approval replay fail under concurrent requests.

### IA-03 — session-bound execution and lifecycle
- Execution requires live operator session and exact request-bound operation.
- Expiry is denied at exact database timestamp boundary.
- Revocation racing with execution has a single documented serialization outcome.
- Account disable, session revoke and credential reset revoke/deny the emergency execution path.
- Operation is one-use; replay with same idempotency key returns prior result without repeating effect.
- Different payload with same key conflicts.
- Session closure occurs on expiry/revoke/consumption; ordinary session liveness does not preserve emergency grant.
- Audit intent is atomic with local state; missing durable audit dependency denies activation/execution.
- Database outage, deadlock, timeout, unknown commit and crash/retry fail closed.

### Cross-boundary and abuse tests
- No emergency authorization can mutate Sales, Purchasing, Inventory, Finance, Journal, Product Owner binding, WORM retention or Audit history.
- No endpoint or command can create permanent Super Admin or wildcard permission.
- Organization/Inventory consumers use only published Contracts APIs; no private IdentityAccess table access.
- Consumer cannot bypass accepted global lock order or claim distributed atomicity.
- Every denial and successful state transition is attributable without secret leakage.

## 14. Publication and implementation gates

The contract may be published only after:
1. IdentityAccess module owner records attributable acceptance of this exact version and SHA.
2. IdentityAccess confirms the operation keys and eligibility policy are reconciled with the current permission catalog and owner policy; no seed/grant is implied.
3. Organization/Backup/AuditSecurity consumers accept their respective narrow boundaries.
4. Platform and affected module owners accept global lock order, transaction bounds and retry semantics.
5. Independent Security review closes all Critical/High findings.
6. Disposable PostgreSQL tests execute the complete IA-01..IA-03 concurrency, expiry, revocation, replay, rollback, crash and failure matrix.
7. Exact-head CI, checksum and documentation gates pass.

After publication, implementation still requires a separate explicit G1/implementation authorization. This contract itself does not authorize runtime code, endpoints, schema, migrations, seeds, grants, role mapping, credentials, LOGINs, production access, Organization G1 or Go-Live.

## 15. Disposition

**Technical design content: COMPLETED as an IdentityAccess module-owner proposal.**  
**Formal IdentityAccess module-owner acceptance: OPEN.**  
**Published runtime contract for Emergency Access: NOT YET PUBLISHED.**  
**IA-01..IA-03 implementation and PostgreSQL acceptance evidence: NOT RUN / OPEN.**  
**Emergency Access production capability: DISABLED / NOT AUTHORIZED.**

**END — MDG-IDENTITYACCESS-EMERGENCY-ACCESS-CONTRACT-001 v0.1**
