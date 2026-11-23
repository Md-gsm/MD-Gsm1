# O-46 Bootstrap Trust-Boundary and First-Owner Provisioning Proposal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-TRUST-BOUNDARY-PROPOSAL-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **TECHNICAL PROPOSAL — OWNER DECISION REQUIRED — NON-AUTHORIZING** |
| Scope | O46-G01 bootstrap trust boundary and the first IdentityAccess owner principal only |
| Related decisions | ADR-0012, ADR-0019, IdentityAccess Module Baseline v1.0, Product Owner Binding v0.2, O-46 Audit v0.4 |
| Human governance | Existing Product Owner/module owner `Md-gsm`; existing independent reviewer `mrdeljoe4`. No additional project participant is proposed. |

## 1. Purpose and boundaries

This proposal resolves the technical shape of the first-owner bootstrap path without enabling it. It is intentionally limited to the initial trust boundary and the creation of the first IdentityAccess owner principal. It does not design or activate email Recovery, SMTP, permission seeds, Emergency Access, a production listener, a production database migration, or G1.

The following constraints are already binding:

- The application is a central client/server system: one designated server host owns the API-to-PostgreSQL connection; Windows desktop clients are clients and must never provision the first owner.
- ADR-0019 requires that the first-run Wizard is available only for a genuinely new central installation. Reinstall, Repair, update, restore and additional desktop-client installation must preserve the same database and must not repeat owner/workshop/fiscal-period creation.
- IdentityAccess owns the Account and credential lifecycle. Product Owner authority is a separate immutable binding to the exact original AccountId and installation identity.
- Fixed, guessable, shared, embedded or reusable bootstrap credentials are prohibited.
- O-46 remains OPEN. This proposal is not an approval of runtime implementation.

## 2. Recommended trust model

### 2.1 Server-host-only local setup channel

The initial Setup Wizard should execute only in an interactive local session on the designated server host. It must not be callable from a Windows desktop client or from another machine on the LAN.

The Wizard communicates with the server-side setup component through an operating-system local IPC channel, not through an unauthenticated HTTP endpoint:

- On a Windows server host, the candidate transport is a Windows Named Pipe.
- The pipe must reject remote clients and use a restrictive security descriptor. Access is limited to the exact Windows SID enrolled as the initial local setup operator, the service's own SID and the minimum operating-system service principals required to operate the pipe. It must not grant access to all authenticated users.
- The server verifies the connecting client's OS principal at the pipe boundary. It does not accept a caller-supplied SID, username, role, Product Owner flag or AccountId as proof.
- No TCP setup endpoint, LAN listener, loopback HTTP bootstrap route, browser-based unauthenticated setup page, command-line secret, environment-variable secret or file-drop secret is part of this proposal.
- A future non-Windows server-host target would require its own peer-credential-verified local IPC adapter and threat review. Do not silently substitute a TCP endpoint.

The Windows SID is only a temporary local-operator authorization fact. It is **not** the Product Owner identity and must never be mapped to Product Owner authority. Product Owner authority is created only by IdentityAccess binding the installation to the AccountId that the owner establishes in the Wizard.

### 2.2 Explicit root-of-trust assumption

The server host's operating system administrator/SYSTEM boundary and the PostgreSQL deployment/database-owner boundary are trusted roots for this bootstrap operation. A local administrator or database owner can ultimately replace binaries, alter local ACLs or directly modify the database; this proposal does not claim to defend against a malicious fully privileged server/DB administrator.

The exact enrolled local SID limits routine invocation by other local users and remote clients, but it does not prove a person's legal identity. If the Product Owner requires protection against a malicious local administrator or database owner, local IPC alone is insufficient; a separate external identity-proofing or hardware-backed trust design would be required and is outside this proposal.

### 2.3 Installation identity continuity

A genuinely new central installation receives one unique installation identifier. The recommended representation is UUIDv7, consistent with the accepted platform identifier policy. The identifier is created once and persisted in both:
- Platform-owned central installation metadata in the authoritative PostgreSQL database; and
- protected server-host configuration needed to reconnect to that same installation.

The identifier is not derived from machine name, Windows SID, database name, username, email, application version or file path. It is never regenerated by reinstall, Repair or update. A database restore must preserve the same installation identifier and the same Product Owner AccountId.

If the database and protected local installation metadata disagree, the host must fail closed and enter a controlled restore/incident procedure. It must not choose either value, create a new installation identity or offer a new-owner Wizard automatically.

The exact protected configuration mechanism and metadata schema are implementation design items. No schema or migration is authorized here.

## 3. One-time bootstrap challenge

The recommended bootstrap exchange has these properties:

1. Only after a database target has been explicitly provisioned and verified as a new installation in a disposable/pre-production rehearsal or an independently approved production provisioning process may the setup component create a bootstrap challenge.
2. The challenge contains at least 256 bits of cryptographically secure random material. It is bound to the installation identifier, a stable SetupOperationId, purpose `InitialOwnerClaim`, the enrolled local-operator SID and an expiry.
3. The raw challenge is delivered exactly once to the authenticated local Wizard process over the protected IPC channel and exists only in the necessary process memory. It is not written to a file, registry value, installer property, command-line argument, environment variable, database column, log, trace, telemetry, crash dump or CI artifact.
4. PostgreSQL stores only a verifier/digest and non-secret metadata. Because the challenge has high entropy, SHA-256 of the raw random challenge is the proposed verifier; compare in constant time. The raw challenge must never be recoverable from the database.
5. The proposed maximum challenge lifetime is **30 minutes** from issuance. This is a recommendation, not an accepted policy value. The challenge is one-use; expiry, successful claim, replacement or explicit abort invalidates it. A replacement may be issued only from the same trusted local channel while setup remains unclaimed and `NotStarted`.
6. Challenge verification and consumption are serialized in the authoritative database. Concurrent claim attempts must yield exactly one success. Replays, wrong installation IDs, wrong SetupOperationIds, wrong local SIDs, expired challenges and unknown state all fail closed.
7. Error responses are non-secret and do not reveal account existence or credential material. No challenge is returned by a later status/read operation.

The challenge is a one-time setup capability, not a login password, not a recovery mechanism and not proof of the human operator's real-world identity.

## 4. Initial owner claim transaction

The proposed first-owner claim is one IdentityAccess-owned local transaction. Its inputs are the verified local setup channel, valid one-time challenge, stable installation ID and SetupOperationId, and the owner-entered final account/profile/credential fields.

On success, the transaction must:

- validate and normalize the owner-selected username under the accepted IdentityAccess username policy;
- validate the owner-selected password using the centralized password policy and store only a supported versioned password hash;
- create exactly one AccountId;
- create the immutable Product Owner binding to that exact AccountId and installation ID, using the protected IdentityAccess lifecycle;
- consume the challenge and persist the stable operation result;
- write secret-free audit intent; and
- make the outcome queryable by SetupOperationId so an unknown client response or ambiguous commit can be reconciled without issuing another account or another owner binding.

The account, credential and Product Owner binding must either commit together or remain unclaimed. No half-created owner, alternate AccountId, transfer, successor or automatic owner re-enrollment is allowed.

This first-owner claim does **not** create role definitions, permission grants, a SuperAdmin expansion, or a database login. Exact bootstrap role/permission seed and deployment-principal boundaries remain O46-G04 and must be reviewed separately. Until all other setup and release gates are closed, the new account does not enable ordinary business routes.

## 5. First-login policy reconciliation required

There is one material policy inconsistency to resolve before implementation:

- ADR-0019's accepted Wizard flow collects the owner's final username, password and profile locally before normal application access.
- IdentityAccess Module Baseline v1.0, section 5.5, currently says the first login must force a change of username, password and profile.

Requiring the owner to change the same final credentials immediately after entering them in the Wizard is contradictory. This proposal recommends treating successful completion of the trusted local Wizard as the initial credential/profile completion step that the forced-first-use rule was intended to enforce. Under that resolution, the owner chooses final credentials/profile before the first ordinary authenticated session; there is no temporary/default password and no second forced password change immediately afterward.

This recommendation requires explicit Product Owner acceptance and a controlled reconciliation of the IdentityAccess baseline. Until accepted, implementation must not choose either interpretation silently. The alternative is to stop collecting final credentials in the Wizard and implement a separate one-time bootstrap credential followed by a forced first-login change; that is less consistent with the approved Wizard fields and creates another secret-handling flow.

## 6. Setup lifecycle and recovery from interruption

The platform-level setup coordinator owns only technical workflow state, stable operation IDs and step receipts. It does not own or duplicate Account, Organization or FiscalPeriod data.

Candidate state progression:

`NotStarted → OwnerBound → OrganizationCreated → InitialFiscalPeriodCreated → Completed`

A separate `RecoveryRequired`/blocked condition records an unreconciled or inconsistent state; it is not a path to reset or re-enroll an owner.

- IdentityAccess owns the atomic owner-account and Product Owner-binding step.
- Organization owns the first workshop/business-identity step.
- Accounting owns the first FiscalPeriod step.
- Platform coordinates the steps with a stable SetupOperationId and idempotent module commands. Each module commits only its own local transaction and exposes a durable outcome for reconciliation.
- A failed later step does not delete an already-created owner, Organization identity or fiscal period. The coordinator resumes the same SetupOperationId and reconciles prior results; it does not rerun a successful step.
- No distributed database transaction is assumed. No compensating deletion of business/security identities is used as a generic rollback.
- Each module's setup command and its authorization contract require acceptance by that module's data owner before implementation.

The coordinator's persistence schema and owner must be established under the Platform data boundary and migration registry before DDL. Do not place Platform setup metadata in IdentityAccess private tables merely because IdentityAccess is the first module used.

## 7. Listener and application gating

Before setup reaches a verified `Completed` state:

- No production LAN/API listener is bound.
- No ordinary business endpoint, login endpoint, Recovery endpoint, module route, background business worker or client registration is enabled.
- Only the local setup IPC channel and the minimum local health/diagnostic mechanism needed to operate the setup process may run.
- Migration execution remains explicit and uses the separately provisioned deployment identity; API startup never applies migrations.
- Missing database, unknown schema version, migration drift, installation-ID mismatch, challenge failure, partial/ambiguous setup state or any module dependency failure stops setup and stays fail-closed.

After `Completed`, the host may proceed only through the separate accepted deployment/TLS/firewall and release gates. This proposal does not authorize opening the production listener.

## 8. Secret and data-preservation invariants

Never expose the bootstrap challenge, raw password, password confirmation, bearer token, connection string, database secret or key material in logs, telemetry, crash artifacts, support bundles or CI artifacts. The challenge is invalidated after successful claim or expiry. The password is passed only over the authenticated local IPC channel and converted to a supported password hash inside IdentityAccess.

The bootstrap path must never:
- create a replacement database because the configured database cannot be reached;
- classify an existing database as new based on connectivity failure, empty tables, missing config file or an authentication error;
- reset migration history, drop/truncate schemas, clear existing data or seed over an existing installation;
- regenerate the installation ID or Product Owner binding during reinstall/update/restore;
- create another owner because a previous owner cannot be recovered; or
- create any fiscal period. FiscalPeriod creation belongs to the later Accounting setup step and happens once, only in a genuinely new setup operation.

## 9. Required proof catalogue — proposed; NOT RUN

| ID | Proof | Required result |
|---|---|---|
| O46-BT01 | Remote pipe/client rejection | Remote and LAN clients cannot open or invoke the bootstrap channel. |
| O46-BT02 | Local OS principal | Only the exact enrolled local SID and required service identities can connect; caller-supplied identity claims are ignored. |
| O46-BT03 | Privileged-root threat boundary | Documentation and tests distinguish local user isolation from the explicitly trusted OS/DB administrator root. |
| O46-BT04 | Challenge generation/storage | At least 256-bit CSPRNG; verifier-only persistence; no raw challenge in database or artifacts. |
| O46-BT05 | Challenge one-time delivery | Challenge is delivered once to the authorized Wizard process and cannot be read back or replayed. |
| O46-BT06 | Challenge binding/expiry | Wrong InstallationId, SetupOperationId, operator SID, purpose or expired challenge is denied. |
| O46-BT07 | Concurrent claim | Exactly one concurrent claimant succeeds. |
| O46-BT08 | Atomic IdentityAccess claim | Account, credential, Product Owner binding, challenge consumption and audit intent commit atomically. |
| O46-BT09 | Unknown commit | Stable operation ID reconciles committed/not-committed outcome without duplicate account or binding. |
| O46-BT10 | Installer/update preservation | Update, Repair and reinstall keep the same DB target, installation ID, AccountId and binding. |
| O46-BT11 | Restore continuity | Restore preserves installation ID, owner AccountId and binding; mismatch blocks privileged operation. |
| O46-BT12 | Listener gate | No production network listener or business route before setup Completed. |
| O46-BT13 | Setup resume | Failure after any module step resumes the same operation without duplicate records or destructive compensation. |
| O46-BT14 | Secret-sentinel scan | Zero challenge/password/token/connection secret in logs, traces, crash dumps, telemetry and CI artifacts. |
| O46-BT15 | Schema/migration guard | No API-startup migration; no unreserved migration; drift/unknown version fails closed. |

All proofs are specifications only. No test is marked PASS and no runtime bootstrap behavior is claimed.

## 10. Owner decisions requested to close O46-G01

The recommended decision package is:

1. Accept a server-host-only local setup channel (Windows Named Pipe on Windows hosts), remote-client rejection and exact-SID ACL, with the stated OS administrator/SYSTEM and PostgreSQL deployment/database-owner trust assumption.
2. Accept the proposed 30-minute one-time challenge lifetime, 256-bit CSPRNG and verifier-only storage.
3. Accept the installation-identity continuity model and UUIDv7 representation.
4. Accept the recommendation that final owner credentials/profile entered in the trusted Wizard satisfy the initial first-use completion requirement, and direct that IdentityAccess Module Baseline v1.0 §5.5 be reconciled accordingly.

Items 1–4 are a proposal, not accepted decisions. If the trust assumption in item 1 is not acceptable, pause implementation and choose a stronger external proof mechanism rather than weakening the local channel or adding an unauthenticated endpoint.

## 11. Non-authorization and next gates

This proposal performs no bootstrap provisioning and authorizes no runtime route, TCP listener, account, role, permission, credential, seed, migration, database grant, secret-provider integration, SMTP, Recovery or production activation. It does not close O46-G01 until the Product Owner explicitly accepts the trust boundary and policy reconciliation. O46-G04 through O46-G07 remain separate gates. Independent review of the accepted design and exact implementation SHA is still required before any feature activation.

**END — MDG-SECURITY-O46-BOOTSTRAP-TRUST-BOUNDARY-PROPOSAL-001 v0.1**
