# O-46 Local IPC and Bootstrap Trust Threat Review

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-LOCAL-IPC-THREAT-REVIEW-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **TECHNICAL DESIGN REVIEW — NON-AUTHORIZING — IMPLEMENTATION AND INDEPENDENT REVIEW OPEN** |
| Scope | Windows server-local Wizard IPC, operator authentication, server authentication and first-owner challenge transport |
| Decision authority | Existing Product Owner / IdentityAccess Module Owner |
| Independent reviewer | Existing Security/Architecture Reviewer |
| Owner policy source | O-46 Bootstrap Owner Decision v0.1; CHG-101 |
| Governing successor target | MDG-ARCH-MASTER-001 v3.0.4 |

## 1. Purpose and current status

This review turns the owner-accepted local Wizard and Named Pipe constraints into a testable Windows security design. It is not an independent security certification, a runtime implementation, or an authorization to enable bootstrap.

The accepted owner policy remains controlling:

- Only the initial Setup Wizard running locally on the designated central Windows server may create the first Product Owner.
- There is no alternate Product Owner creation, replacement, transfer or re-enrollment path.
- The Windows OS Administrator/SYSTEM and PostgreSQL deployment/database-owner are explicit roots of trust.
- The owner-selected final username, password and profile are completed in the Wizard. The initial Product Owner is not forced to change them after first login.
- The one-time challenge has at least 256 bits of CSPRNG entropy, a maximum 30-minute lifetime, verifier-only persistence and binding to InstallationId, SetupOperationId, purpose and the authenticated local operator SID.
- The central InstallationId is UUIDv7 and must survive reinstall, Repair, update and restore.

All runtime, process-boundary, DACL, impersonation, database-provisioning and concurrency proofs below are **NOT RUN / NOT VERIFIED**.

## 2. Verified Windows/.NET API constraints

The following constraints were confirmed against current Microsoft documentation for .NET 10 and Windows:

1. .NET 10 `PipeOptions` exposes `CurrentUserOnly`, `Asynchronous`, `FirstPipeInstance` and `WriteThrough`; it does not expose a `PIPE_REJECT_REMOTE_CLIENTS` option. The native Windows `CreateNamedPipeW` API exposes `PIPE_REJECT_REMOTE_CLIENTS` (0x00000008), which automatically rejects remote clients.
2. `NamedPipeServerStreamAcl.Create` accepts an explicit `PipeSecurity`. However, when `PipeOptions.CurrentUserOnly` is specified, the .NET API ignores the supplied `PipeSecurity` and constructs a security descriptor for the current Windows user as the only owner. That behavior is incompatible with this design's need to grant access to both a specific operator SID and a distinct server-service SID.
3. If a native named pipe is created with a null security descriptor, Windows applies a default descriptor. Microsoft documents default read access for Everyone and Anonymous. A null/default descriptor is therefore prohibited for this setup channel.
4. `NamedPipeServerStream` provides a constructor that wraps a native `SafePipeHandle`, making a native-handle creation path available to a .NET service while retaining stream-based managed I/O.
5. `NamedPipeServerStream.RunAsClient` executes a callback while impersonating the connected pipe client. This permits the server to inspect the OS-authenticated client identity; the callback must be limited to identity extraction and must not execute privileged setup/database operations.

These are API facts, not proof that the eventual implementation is correct. Exact target framework, package version, interop signatures, safe-handle ownership and Windows Server support matrix must be built and tested in the actual repository toolchain.

## 3. Recommended pipe creation strategy

### 3.1 Native endpoint creation

Candidate implementation:

- Create the endpoint using the native Windows `CreateNamedPipeW` API through a narrowly scoped, reviewed P/Invoke wrapper.
- Set the pipe name in the local namespace (`\\.\pipe\...`); its name is an identifier, not a secret or an authorization mechanism.
- Set `PIPE_REJECT_REMOTE_CLIENTS` in the native pipe mode.
- Set `FILE_FLAG_FIRST_PIPE_INSTANCE` on initial server creation so an already-created pipe instance causes the service to fail closed rather than silently attach to an existing endpoint.
- Use a single server instance for the one-time setup channel; any additional instance or parallel claim must be explicitly rejected or handled by the same database serialization rule.
- Supply an explicit, protected security descriptor. Do not pass a null descriptor and do not inherit broad default ACLs.
- Wrap the returned native handle in `SafePipeHandle` and then in `NamedPipeServerStream`. If overlapped I/O is selected, the wrapper's asynchronous flag must match the native handle flags.
- Keep the native interop surface small, with SafeHandle disposal and error handling reviewed separately. Invalid handles, unsupported flags, descriptor failure, pipe-name conflict or unexpected API error must abort setup; there is no weaker fallback.

### 3.2 DACL requirements

The DACL must be explicit, protected against unintended inheritance, and allow only:

- the exact enrolled local operator SID for the minimum client read/write rights needed by the request protocol; and
- the dedicated service SID for the server rights required to create, accept, read, write and dispose the pipe.

The operator ACE must not grant `FILE_CREATE_PIPE_INSTANCE`, `WRITE_DAC`, `WRITE_OWNER`, delete rights or the ability to alter the pipe's ACL. Avoid generic write grants where they implicitly include pipe-instance creation rights; derive and test the exact access masks for the selected client API.

Do not add broad allow ACEs for Everyone, Authenticated Users, Users, Interactive or Anonymous. Do not silently substitute the local Administrators group for the exact operator SID. OS Administrator/SYSTEM remain the accepted trusted root and can ultimately alter local security state; that is an explicit residual assumption, not an ordinary Wizard allow rule.

The service identity should be a separately identifiable Windows service SID or another dedicated least-privilege service identity. Choosing the concrete service account, service SID configuration and its privileges remains part of Platform deployment design; do not default to LocalSystem solely for convenience.

### 3.3 Do not combine CurrentUserOnly with the multi-SID DACL

Do not use `PipeOptions.CurrentUserOnly` for this endpoint. It is not a substitute for the accepted operator/service DACL. On .NET 10, the ACL helper documents that `CurrentUserOnly` ignores the caller-supplied `PipeSecurity` and assigns the current Windows user as sole owner. It also does not provide the required explicit native remote-rejection flag.

### 3.4 Local client and server authentication

Both ends must authenticate their peer; access to a pipe name alone is not sufficient.

**Server authenticates client:**

1. Windows performs the DACL access check before granting the client handle.
2. The client must connect with an impersonation level sufficient for the server to obtain the authenticated client token.
3. After a complete protocol handshake, the server calls `RunAsClient` only to inspect the effective Windows identity and its user SID.
4. The server compares that SID with the exact enrolled operator SID and, where the approved session policy requires it, the logon SID/session identifier. Missing identity, null SID, lower/unavailable impersonation, impersonation failure or mismatch is a hard denial.
5. Caller-supplied SID, username, group name, AccountId, owner flag or role is never trusted.
6. The server leaves the impersonation callback and reverts to its own service identity before executing any setup operation. It must not perform privileged database, file-system or secret-store work under a caller-controlled impersonation context.

**Client authenticates server:**

- Before sending any password, profile data or challenge response, the Wizard verifies that the connected pipe server is the expected MD-Gsm server service process.
- The candidate proof is to obtain the connected server PID using Windows `GetNamedPipeServerProcessId`, open the process/token with minimum query rights and verify the expected service SID and protected installation/service identity. A PID or process name alone is not sufficient.
- The server must create the first pipe instance itself. If another process created the name first, service initialization fails closed. The Wizard must not send any secret to an unverified endpoint.
- If the service identity cannot be verified or the connection is ambiguous, the Wizard displays a controlled server-authentication error and terminates the exchange.

The exact API sequence, process-handle race handling, token-query rights and protection against pipe squatting must be proven in disposable Windows tests before this mechanism is accepted.

## 4. Operator SID enrollment — unresolved technical sub-gate

The policy requires an exact operator SID; it does not authorize trusting whichever process connects first.

The implementation design must define how the first operator SID is explicitly enrolled on the server host and how the protected SID metadata is stored and changed. Candidate constraints:

- Enrollment occurs only in a controlled, local, explicitly authorized server-installation step.
- The installer or setup component obtains the SID from a verified Windows access token, not from a text field or caller-supplied value.
- The operator must be explicitly confirmed by the authorized local server operator; no automatic first-connection enrollment exists.
- The SID record is protected by the service/OS ACL and tied to the same InstallationId. Reinstall, Repair, update or restore cannot silently replace it.
- An operator SID change requires a separate controlled recovery/administrative policy; it must not create a new Product Owner or rebind owner authority.

Still to determine and test: whether the enrolled SID is limited to one Windows logon session or all sessions for the same SID. A SID alone identifies the account, not a particular interactive logon session. Do not assume that RDP or another session of the same Windows account is either allowed or prohibited until this is explicitly specified.

## 5. Pipe protocol constraints

The pipe must expose a closed, versioned protocol for the single setup purpose. It must not be a generic privileged IPC or a database proxy.

- Accepted operation: the one-time initial Product Owner claim and the minimum status/reconciliation calls needed for that same setup operation.
- Every message includes a protocol version and stable SetupOperationId. The server derives InstallationId and actor SID from trusted server state and the OS token, not the request body.
- The claim is bound to `purpose=InitialOwnerClaim`, InstallationId, SetupOperationId, authenticated local operator SID and expiry.
- A challenge is generated by the server using CSPRNG with at least 256 bits of entropy; delivered once over the authenticated local channel; PostgreSQL stores only the verifier/digest and non-secret metadata. Maximum challenge lifetime is 30 minutes according to the owner decision.
- The challenge is not a login credential and is never shown as a reusable password. It is an internal one-time setup capability. No later read/status call returns the raw challenge.
- Requests have strict byte-size limits, a fixed serialization format, bounded parsing depth and strict field allow-lists. No arbitrary SQL, file path, process launch, environment mutation, database connection string or free-form command is accepted.
- Password and profile data are transmitted only over the already authenticated local pipe, held in memory only as necessary and never logged, traced, dumped in support bundles or included in CI artifacts.
- The service validates username/profile fields and PasswordPolicy server-side and uses the approved versioned password hasher. The raw password is never persisted.
- The UI must not send owner data until it has authenticated the service endpoint and the service has authenticated the operator.
- Connection timeout, protocol downgrade, malformed request, oversized message, unsupported protocol, missing/invalid SID, challenge replay/expiry and inconsistent state all fail closed.

## 6. Bootstrap transaction and database boundary

The local IPC layer is not authority to create an account by itself. The server-side IdentityAccess provisioning boundary must own the data transaction.

The eventual IdentityAccess operation must atomically:

1. lock and validate the not-yet-claimed bootstrap state for the exact InstallationId;
2. verify challenge digest, purpose, SetupOperationId, operator SID and database-clock expiry;
3. confirm there is no existing Product Owner binding and no prior committed result for a different operation;
4. create exactly one AccountId and the final password hash/profile state;
5. create the immutable Product Owner binding to that AccountId + InstallationId;
6. consume/invalidate the challenge;
7. persist an idempotent operation result under the stable SetupOperationId; and
8. persist secret-free IdentityAccess audit intent.

Unknown commit outcome is resolved by querying the same SetupOperationId, never by creating a new operation or retrying account insertion blindly.

The Platform Setup Orchestrator stores only workflow state and module-step receipts. IdentityAccess owns Account, credential, Product Owner binding and the owner-claim result; Organization owns workshop identity; Accounting owns the initial FiscalPeriod. Each module commits its own local transaction. If a later Organization/Accounting stage fails, the system reconciles and resumes the same operation and does not delete a previously committed account or owner binding as generic compensation.

**Unresolved O46-G04 boundary:** the normal IdentityAccess runtime database role deliberately has no account INSERT or role/permission write authority. Do not broaden its grants to make bootstrap work. A separately authorized provisioning/deployment principal and exact least-privilege operation must be designed and reserved before DDL or runtime implementation. No bootstrap role/permission seed, SuperAdmin expansion, database login or production grant is approved by this review.

## 7. Pipe-squatting, service lifecycle and setup gating

- The server service creates the first pipe instance with an explicit descriptor and native remote rejection. A pipe-name conflict is a security/availability failure; there is no fallback endpoint.
- The local Wizard verifies the pipe server's service identity before submitting any sensitive field.
- The service accepts only one setup operation in state `NotStarted`; repeated requests use the same SetupOperationId and return the reconciled result.
- The pipe is available only while central first-run setup is incomplete. After durable `Completed`, the initial owner-creation operation is permanently disabled.
- The network API listener, normal login route, Recovery route, module routes and business workers remain disabled until all setup steps are verified `Completed` and separate deployment/release gates allow activation.
- A database outage, unknown schema version, checksum drift, local/DB InstallationId mismatch, invalid operator SID, unverified pipe service, migration partial failure or unresolved setup state is not a reason to display a new-owner Wizard.
- Reinstall, Repair, update and additional Windows desktop-client installation preserve the existing DB target, InstallationId and Product Owner AccountId. The client machines never host or invoke the first-owner Wizard.

## 8. Threat register

| Threat | Principal control | Residual / required proof |
|---|---|---|
| Remote LAN client invokes setup | Native `PIPE_REJECT_REMOTE_CLIENTS`; local namespace; DACL | Remote test from a second machine; reject remote SMB pipe connections |
| Unprivileged local user connects | Explicit DACL with only enrolled operator/service SIDs; no broad allow ACE | Test multiple local accounts and Anonymous/Network contexts |
| Same account, different Windows session | Exact SID plus explicit decision on logon SID/session ID | Policy decision and RDP/multi-session tests remain open |
| Attacker pre-creates pipe name (pipe squatting) | `FILE_FLAG_FIRST_PIPE_INSTANCE`; fail-closed service startup; Wizard validates server PID/token/service SID before secrets | Race test and server-process authentication proof |
| Malicious caller supplies a forged SID/owner claim | Server obtains SID through OS token impersonation; ignores body identity fields | Token/impersonation integration tests |
| Impersonation failure causes privileged fallback | Any failure/missing identity denies; no setup work inside impersonation callback | Inject failure and prove no service-context fallback |
| Broad default pipe ACL | Explicit security descriptor; no null/default descriptor; no broad allow ACEs | Read-back effective DACL and negative access tests |
| Conflicting .NET CurrentUserOnly semantics | Do not combine CurrentUserOnly with multi-SID DACL; use native remote-reject mode | Verify exact .NET 10 runtime behavior |
| Challenge replay/concurrent claim | Digest-only challenge, database-clock expiry, atomic single-use claim and stable operation ID | PostgreSQL concurrency/replay/unknown-commit evidence |
| Duplicate owner after interruption/reinstall | Immutable AccountId binding, stable InstallationId and SetupOperationId, idempotent receipts | Crash/restart/reinstall/restore test suite |
| Secret leakage from IPC or diagnostics | Secret-free logging, sentinel scan, bounded memory handling | Logs/traces/dump/support/CI artifact scan |
| Privileged local administrator or DB owner compromise | Explicit root-of-trust assumption | Residual accepted by owner; cannot be solved by Named Pipe ACL alone |

## 9. Required test plan — NOT RUN

| Test ID | Scenario | Pass condition |
|---|---|---|
| O46-IPC-01 | Pipe construction | Native API sets explicit DACL, `PIPE_REJECT_REMOTE_CLIENTS`, first-instance and non-inheritable handle |
| O46-IPC-02 | Remote rejection | Connection from another Windows machine is rejected before protocol exchange |
| O46-IPC-03 | Same-machine unauthorized SID | A different local user cannot open or use the endpoint |
| O46-IPC-04 | Operator SID allow | Exact enrolled SID can connect; caller-provided SID is ignored |
| O46-IPC-05 | Session policy | Same SID in a different logon/RDP session matches the explicitly accepted policy |
| O46-IPC-06 | Service identity allow | Only the configured service SID can create/listen on the first pipe instance |
| O46-IPC-07 | No broad ACL | Effective DACL contains no unintended Everyone/Authenticated Users/Users/Interactive/Anonymous allow |
| O46-IPC-08 | Pipe squatting | Pre-created pipe causes service startup failure; Wizard sends no secret to an unverified server process |
| O46-IPC-09 | Server authentication | Client verifies pipe server PID/token/service SID before sending credentials |
| O46-IPC-10 | Impersonation | Server extracts exact client SID through OS impersonation and reverts before privileged processing |
| O46-IPC-11 | Impersonation error | Missing token, lower level, failed impersonation or disconnected pipe is denied; no fallback to service identity |
| O46-IPC-12 | Protocol hardening | Unknown version, unknown operation, oversized message and malformed data are rejected |
| O46-IPC-13 | Challenge | 256-bit challenge, 30-minute maximum lifetime, digest-only storage, one-time claim, replay/expiry/mismatch denial |
| O46-IPC-14 | Atomic owner claim | Account, final credential, binding, challenge consume, idempotent result and audit intent commit atomically |
| O46-IPC-15 | Unknown commit | Same SetupOperationId reconciles state without duplicate AccountId/binding |
| O46-IPC-16 | Network gate | No TCP/API/login/business listener before SetupCompleted |
| O46-IPC-17 | Secret hygiene | No password/challenge/token/key material in logs, traces, crash dumps, support bundles or CI artifacts |
| O46-IPC-18 | Reinstall/update/restore | InstallationId and original AccountId/binding remain unchanged; Wizard is not re-enabled |
| O46-IPC-19 | Role and DB grant boundary | Runtime role still cannot INSERT accounts or write roles/permissions; bootstrap authority is separate and least-privilege |
| O46-IPC-20 | Post-first-login owner policy | Initial owner has no forced credential/profile change; future own-password change remains available |

Every test above is a specification. None is PASS or RUN as of this review.

## 10. Open technical questions before implementation

1. Securely enroll and persist the exact local operator SID during central server installation; never trust the first pipe client.
2. Decide whether a second Windows logon/RDP session for the same SID is allowed; SID equality alone does not distinguish sessions.
3. Select and prove the least-privilege service identity and the exact DACL access masks/owner of the pipe object.
4. Prove native CreateNamedPipeW interop, SafePipeHandle ownership, FILE_FLAG_FIRST_PIPE_INSTANCE, FILE_FLAG_OVERLAPPED (if used), message framing, timeouts and Windows Server support matrix.
5. Prove server endpoint authentication against pipe squatting through the server process token/service SID and process-handle lifecycle.
6. Complete O46-G04: bootstrap-specific deployment/provisioning principal, database owner boundary and role/permission seed policy without broadening runtime grants.
7. Reserve schema only after the data owner and WG-0 approve the exact delta. Migration 0009 remains reserved; this review consumes no migration number.
8. Reconcile the SetupOperationId receipt contract across IdentityAccess, Organization and Accounting; keep Platform workflow state in Platform ownership.
9. Independent Security/Architecture Reviewer must inspect this threat review and the exact implementation SHA before any release/activation.

## 11. Non-authorization

This document records a technical threat review and candidate construction path only. It does not create source code, Windows service, named pipe, IPC endpoint, account, credential, challenge, role, permission, grant, SMTP integration, database schema, migration, Recovery endpoint, TCP listener, G1 authorization or production activation. It does not mark O-46-G01 technical review, O46-G04 or any test as closed.

## 12. Official technical references

The following Microsoft documentation was consulted on 2026-11-23:

1. .NET 10 PipeOptions: https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0
2. .NET 10 NamedPipeServerStreamAcl.Create and its CurrentUserOnly/PipeSecurity behavior: https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstreamacl.create?view=net-10.0
3. .NET NamedPipeServerStream SafePipeHandle constructor: https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream?view=net-10.0
4. Windows CreateNamedPipeW / PIPE_REJECT_REMOTE_CLIENTS: https://learn.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-createnamedpipew
5. Windows Named Pipe Security and Access Rights: https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights
6. .NET 10 NamedPipeServerStream.RunAsClient: https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.runasclient?view=net-10.0
7. Windows GetNamedPipeServerProcessId: https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeserverprocessid

**END — MDG-SECURITY-O46-LOCAL-IPC-THREAT-REVIEW-001 v0.1**
