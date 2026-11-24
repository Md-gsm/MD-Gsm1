# O-46 IdentityAccess Bootstrap and Recovery Audit — Capture-Once, Invalidate, Secret Hygiene

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001 |
| Version / Date | 0.6 / 2026-11-24 |
| Baseline | ADR-0012; ADR-0019; IdentityAccess Module Baseline v1.1; O-46 Owner Decision v0.1; Stage 08; N.3/N.6 |
| Status | CONTROLLED SUCCESSOR AUDIT — O-46-G01 OWNER DECISION RECORDED; TECHNICAL THREAT REVIEW, IMPLEMENTATION AND RELEASE GATES OPEN |
| Execution | Documentation/source audit and implementation-boundary review only. No bootstrap/recovery code, secret, credential, migration, database grant, SMTP, endpoint or runtime operation performed. |

## 1. Current source findings

The current main source has IdentityAccess account/session/recovery persistence primitives, but O-46 is not implemented or verified.

| Area | Current state | Required closure |
|---|---|---|
| Initial Product Owner creation | No bootstrap provisioning workflow or one-time handler exists. Existing migration metadata remains non-operational. | Implement the owner-accepted exclusive server-local Wizard path, atomic Account + binding, and fail-closed setup/listener boundary; no alternate owner-creation route. |
| Static credential | Historical `admin/admin` wording is superseded; the owner decision and `BootstrapInvariantTests.FixedPairIsAbsent` prohibit fixed/guessable credentials in source, installer, config, migration, logs or telemetry. | Do not implement or ship any static/default pair. |
| Capture-once bootstrap challenge | No one-time challenge generation/delivery/claim workflow was found. | Implement the accepted server-local Windows Named Pipe channel, exact-SID ACL, at least 256-bit CSPRNG challenge, maximum 30-minute lifetime, verifier-only persistence and atomic single-use claim. |
| First-owner credential/profile completion | Product Owner decision now requires final username/password/profile entry in the trusted local Wizard; no runtime state machine exists. Forced change after the first Product Owner login is explicitly removed. | Complete Wizard credential validation/hash/binding and durable fail-closed setup gating. |
| Recovery orchestration | Only storage primitives exist: `IRecoveryStore`, PostgreSQL challenge creation/consumption and recovery-code consumption. No endpoint, orchestration handler or email delivery adapter was found. | Implement only after entrypoint, verified email and operational dependencies are approved. |
| Recovery policy | Email-only OTP is owner-approved for this phase; it is not MFA. SMS and offline recovery-code issuance are out of scope. | Preserve the policy; explicitly supersede old three-channel/ten-offline-code assumptions. |
| Recovery schema | `identity.recovery_challenges` stores digest/channel/created/expiry/consumed/revoked; it lacks attempt counters, rolling send quotas, delivery state, key version and operation idempotency. | Reserve a migration number through WG-0, then implement the approved schema delta with tests. |
| Email ownership proof | Account has an email column, but no authoritative verified-email state or proof workflow was found. | Define how the initial super_admin email is registered and verified. |
| SMTP and keys | No production SMTP relay or dedicated recovery-challenge HMAC key is configured in source. | Platform selects and provisions secret manager, relay and separate versioned HMAC key outside PostgreSQL. |
| Secret logging | Existing API exception logging records exception type/request ID rather than request body; however end-to-end bootstrap/recovery log/trace/crash-artifact proof is absent. | Add redaction tests and scan all outputs/artifacts using sentinel secrets. |

## 2. Policy precedence: ten recovery codes vs email-only decision

The older O-46 summary in the architecture/open-issue register mentions “ten recovery codes.” The later owner decision for this phase explicitly accepts email-only recovery, states that it is not MFA, and excludes SMS and offline recovery-code issuance. Stage 08 likewise places SMS/offline codes outside this phase.

Therefore:
- **Do not generate or issue ten offline recovery codes in this phase.**
- Do not add recovery-code delivery, export, printable sheets, support bypass or fallback.
- The existing `identity.recovery_codes` table is a dormant persistence primitive, not authority to provision codes.
- Any future offline-code feature requires a new explicit owner decision, threat model and architecture change.

The “ten codes” wording must be marked superseded in the controlled issue register/master successor; it must not be treated as a current acceptance requirement.

## 3. Accepted first-owner bootstrap contract — policy approved; runtime absent

The Product Owner has accepted the owner decision recorded in `MDG-SECURITY-O46-BOOTSTRAP-OWNER-DECISION-001_v0.1.md`. This replaces the earlier proposal to force a username/password/profile change at the first owner login.

1. Only the interactive local Setup Wizard on the designated central Windows server host may create the initial Product Owner. No alternate API, client, admin UI, CLI, script, seed, migration, support path or routine direct-DB operation may create/re-enroll/replace Product Owner authority.
2. The Wizard communicates with the server through Windows Named Pipe local IPC. Remote clients are rejected. The ACL is restricted to the exact OS-authenticated local operator SID and required service identities. SID authorizes only local Wizard invocation; it is not Product Owner identity.
3. The accepted root-of-trust assumption includes Windows OS Administrator/SYSTEM and PostgreSQL deployment/database-owner boundaries. The design does not claim to resist malicious full privilege over these roots.
4. Challenge uses at least 256 bits of CSPRNG entropy, is delivered once through the authenticated local channel, has a maximum 30-minute lifetime, and is bound to InstallationId, SetupOperationId, purpose and local SID. PostgreSQL stores only a verifier/digest.
5. Claim is serialized and single-use. Replay, expiry, mismatch, database failure and unknown commit outcome fail closed; the same SetupOperationId reconciles ambiguous outcomes without creating another Account or owner binding.
6. InstallationId is UUIDv7, persisted across reinstall, Repair, update and restore. Product Owner binding is immutable to the original AccountId + InstallationId. No transfer, second owner, successor or re-enrollment exists.
7. Wizard collects the owner's final username, password and profile. PasswordPolicy applies and a supported versioned password hasher stores only the hash. **The initial Product Owner is not forced to change username, password or profile after first login.** The owner may still change their password later under the ordinary password-change policy.
8. No temporary/default/shared credential is created. Owner recovery can restore only the same AccountId; an unrecoverable original owner means privileged owner operations stay fail-closed.
9. IdentityAccess owns the Account, credential and owner binding; Platform owns only setup orchestration state; Organization owns workshop identity; Accounting owns initial FiscalPeriod. Local transactions are idempotent and reconciled by stable SetupOperationId. No distributed transaction or destructive compensation is assumed.
10. Setup remains non-operational until the entire setup state is durably verified as Completed. Production listener/business routes remain separately gated.

The accepted policy does not provide runtime proof. Named Pipe/ACL threat review, deployment-principal separation, schema reservation, implementation, PostgreSQL concurrency/idempotency tests, secret-sentinel scans and independent review remain open.

## 4. Email-only recovery target contract

Owner-approved policy for this phase:
- Email OTP only; this is not MFA or two independent factors.
- OTP TTL 600 seconds; CSPRNG generation; only a dedicated HMAC-SHA-256 digest is persisted.
- Dedicated recovery-challenge HMAC key, versioned and held outside PostgreSQL; separate from bearer-token SHA-256 digest and the IdentityAccess privacy-digest key.
- Maximum five failed attempts per challenge; then 24-hour Recovery lock.
- Resend cooldown at least one hour; at most one send per rolling hour and five sends per rolling 24 hours; exceeding limits locks Recovery/sending for 24 hours.
- A replacement challenge revokes the previous open challenge for that account.
- Expiry boundary: `now_utc >= expires_at_utc` is expired.
- One-time consume, constant-time digest comparison, concurrency-safe counters/quotas, generic account-enumeration-resistant responses and secret-free audit.
- On successful proof, one transaction changes password hash, increments credential_version, revokes all sessions, sets `must_change_password=true`, consumes/revokes the challenge and records the audit intent.

Recovery remains disabled until the trusted entrypoint, authoritative verified-email state, SMTP relay, key provisioning/rotation, abuse controls, concurrency/idempotency tests, mailbox-compromise threat review, independent security review and owner release gate are all complete.

## 4.1 Runtime boundary correction — offline recovery codes remain non-operational

The owner-approved policy for this phase is email-only OTP recovery. SMS and offline recovery-code issuance are explicitly out of scope; the historical “ten recovery codes” wording is superseded.

PR #86 now removes the unused `IRecoveryStore.ConsumeRecoveryCodeAsync` application port and its PostgreSQL store implementation. The runtime grant template no longer grants `SELECT` or `UPDATE` on `identity.recovery_codes`; it explicitly executes `REVOKE ALL PRIVILEGES ON identity.recovery_codes FROM mdgsm_runtime` so reapplying the checked-in grant script also removes any earlier legacy grants. The disposable runtime_app integration test now asserts both table privileges are absent and direct SELECT/UPDATE attempts are denied.

`identity.recovery_codes` remains as a dormant table in migration 0003 for schema-history compatibility only. Its presence does not authorize provisioning, display, print, export, consumption or fallback recovery-code use. No migration/DDL was performed to drop the table, because migration 0009 is reserved and WG-0 has not approved a schema change.

The new source and effective-privilege assertions passed on exact SHA `284ac9dbb94fbbd05d19de18146ccc27d35d8cbf` in workflow `44914448417`, PostgreSQL job `136876940898`: **37 passed / 0 failed / 0 skipped**. The `persons-spec-lint` checksum job `136876940885` also passed (459 tracked files). This PASS only proves the disposable runtime_app denial behavior; it does not implement email OTP delivery, bootstrap provisioning or production Recovery. No production grant script or database was modified.

## 4.2 Exact-head evidence for offline-code runtime denial

The updated Recovery boundary test passed in the complete PostgreSQL 18 integration suite on SHA `284ac9dbb94fbbd05d19de18146ccc27d35d8cbf`, workflow `44914448417`, job `136876940898` (37/37). The test confirms the disposable `runtime_app` LOGIN cannot SELECT or UPDATE the dormant `identity.recovery_codes` table after the updated grants are applied, including the explicit REVOKE ALL statement.

The same suite proves only that the offline-code capability is denied to the disposable runtime identity. It does not provide a Bootstrap credential-delivery workflow, email OTP issuance/verification, verified mailbox enrollment, SMTP relay, HMAC key custody, recovery orchestration, or a production endpoint. O-46 remains OPEN / NOT IMPLEMENTED / NOT VERIFIED at the feature level.

## 5. Required schema work — not authorized or reserved

Current migration 0003 does not persist enough state for the accepted recovery quotas and safe operation reconciliation. A future migration will likely need:
- challenge key version;
- attempt count and lockout state;
- account-level recovery lock state;
- atomic rolling send quota and cooldown state;
- provider delivery attempt/outcome metadata without OTP or raw email payload;
- stable operation ID and durable result for unknown-commit reconciliation;
- verified-email status/proof metadata, if the approved email ownership model requires it;
- bootstrap challenge state, expiry, verifier and completion metadata if current columns are insufficient.

Exact schema is subject to domain design and data ownership. Migration number 0009 is not to be consumed until WG-0 formally reserves it. No SQL or DDL is included in this document.

## 6. Required audit/logging invariants

Allowed audit metadata: event type, internal actor/subject IDs, UTC timestamp, non-sensitive outcome/reason code, correlation ID and stable operation ID.

Never log, trace, persist in diagnostics or include in crash/CI artifacts:
- bootstrap secret or raw bootstrap challenge;
- raw email OTP or recovery code;
- raw bearer token;
- password or temporary password;
- recovery HMAC/private keys or key material;
- SMTP credentials, connection strings or provider authorization headers;
- full request bodies containing contact data or secrets.

Audit must record issuance attempt, delivery outcome, claim/consume, expiry, lockout, revocation, completion and failure without revealing the secret or confirming account existence to unauthenticated callers.

## 7. Required test catalogue — NOT RUN

| ID | Test | Pass condition |
|---|---|---|
| O46-T01 | Unique bootstrap generation | Independent installations receive non-guessable distinct secrets |
| O46-T02 | Capture once | Raw bootstrap secret is displayed/delivered once and cannot be retrieved later |
| O46-T03 | Bootstrap digest storage | Only approved verifier/digest is stored; no raw secret |
| O46-T04 | Replay/expiry | Second claim, expired claim and wrong-installation claim fail closed |
| O46-T05 | Concurrent claim | Exactly one claimant succeeds |
| O46-T06 | Initial owner credential/profile completion | Wizard stores final owner-selected username/password/profile; first owner login has no forced change |
| O46-T07 | Listener fail-closed | Network/business operations remain disabled before bootstrap completion |
| O46-T08 | Static credential scan | No fixed/guessable default pair in source, installer, config, migration, logs or artifacts |
| O46-T09 | Email proof | Recovery can target only an authoritative verified email |
| O46-T10 | OTP lifecycle | 600s, digest-only, constant-time compare, one-time use, equality-expired |
| O46-T11 | Attempt/send quotas | Five attempts, 24h lock, cooldown and rolling quotas are atomic under concurrency |
| O46-T12 | Challenge replacement | Prior open challenge is invalidated atomically |
| O46-T13 | Recovery transaction | Password, credential version, session revoke, must-change, challenge and audit intent commit atomically |
| O46-T14 | SMTP/provider failure | Ambiguous delivery never counts as proof or successful recovery |
| O46-T15 | Unknown commit outcome | Stable operation ID resolves outcome; no second reset/bootstrap |
| O46-T16 | Mailbox threat review | Compromise/phishing/forwarding residual risks reviewed and accepted or blocked |
| O46-T17 | Secret-log sentinel scan | Zero raw bootstrap/OTP/token/password/key/SMTP secret in logs, traces, crash and CI artifacts |
| O46-T18 | DB unavailable | All bootstrap/recovery operations fail closed |
| O46-T19 | Recovery code exclusion | No offline recovery-code issuance or SMS fallback in this phase |
| O46-T20 | Restore/key rotation | Live challenge/key-version verification remains safe across approved key rotation and restore |

All tests above are specifications only and NOT RUN / NOT PASS.

## 8. Owner/platform decisions required

1. **Accepted:** server-local Windows Named Pipe, remote-client rejection, exact OS-authenticated local SID ACL, explicit OS Administrator/SYSTEM and PostgreSQL deployment/database-owner trust boundary.
2. **Accepted:** at least 256-bit CSPRNG, maximum 30-minute one-time challenge, verifier-only persistence and binding to UUIDv7 InstallationId + SetupOperationId + purpose + local SID.
3. How the initial super_admin email is captured and verified before Recovery is available.
4. Recovery entrypoint: network endpoint or trusted local console, including its threat boundary.
5. SMTP relay/provider selection and secret delivery mechanism.
6. Dedicated recovery HMAC key provider, versioning, rotation and recovery ownership.
7. WG-0 reservation and approval of the exact migration/schema delta.
8. Independent reviewer for mailbox compromise, bootstrap trust boundary and secret hygiene.

## 8.1 Execution decision for this work package

Bootstrap and Recovery are deliberately **not implemented** in PR #86 at this point. This is not a skipped coding task: required security trust boundaries and operational dependencies have not been approved, and implementing them by inference would create a new authentication/recovery path without an accepted threat boundary.

Blocking dependencies that must be resolved before the first production-capable implementation:

1. **Bootstrap implementation and threat proof:** the owner-accepted Named Pipe, exact-SID ACL, trusted OS/DB administrator boundary, 256-bit one-time challenge, 30-minute expiry and InstallationId binding need detailed threat review and implementation proof. No static password, file drop, command-line argument, environment variable, installer log or hidden default is an acceptable substitute.
2. **Recovery entrypoint and account proof:** no accepted decision exists between a network endpoint and a trusted local console. The initial `super_admin` email needs authoritative enrollment and proof before any email-only reset can be trusted. The owner-approved email-only channel policy does not itself authorize a route or prove mailbox ownership.
3. **Operational services:** SMTP relay, TLS/authentication, provider delivery semantics, rate-limit enforcement, secret manager and dedicated versioned HMAC key custody/rotation/recovery have not been provisioned or selected.
4. **Schema and registry:** migration 0003 lacks the complete attempt/send quota/lockout/key-version/delivery/operation-id state required by the approved Recovery policy. Migration 0009 remains reserved and must not be consumed. Exact schema must be reviewed and reserved by WG-0 before DDL is authored/applied.
5. **Authorization and bootstrap seed:** no accepted end-to-end bootstrap account/role/permission seed design proves that the first account is created under a separate deployment principal while the API runtime remains denied account/role INSERT. Do not weaken runtime grants to make bootstrap appear to work.
6. **Atomicity and operational evidence:** no concurrency/idempotency proof exists for claim, quota, challenge replacement, delivery ambiguity, credential change, session revocation and audit outbox as one transaction. Secret-sentinel scans and crash/trace/telemetry proof have not run.

Safe work permitted before those decisions is limited to domain-neutral contract refinement, test specifications, threat modeling and disposable-only test harness preparation. Do not add a live route, enable the feature flag, provision a credential, generate real OTP/bootstrap material, run SMTP or apply migration/DB grants.

### Release-gate sequence

| Gate | Required evidence | Current status |
|---|---|---|
| O46-G01 | Owner records trusted bootstrap channel, operator authorization, installation binding and expiry | OWNER DECISION RECORDED — TECHNICAL THREAT REVIEW / IMPLEMENTATION OPEN |
| O46-G02 | Owner records Recovery entrypoint/trust boundary and verified-email enrollment/proof contract | BLOCKED — DECISION REQUIRED |
| O46-G03 | Platform provisions approved SMTP, separate HMAC key and secret-manager operations (without exposing secrets to repo/CI) | BLOCKED — PLATFORM DEPENDENCY |
| O46-G04 | WG-0 reserves reviewed schema/migration; account/role seed and bootstrap principal boundary reviewed | BLOCKED — NOT RESERVED / NOT APPROVED |
| O46-G05 | Disposable PostgreSQL concurrency, quotas, replay, unknown-commit, key-rotation and recovery tests all PASS | NOT STARTED |
| O46-G06 | Secret-sentinel scans across logs/traces/crash/CI plus independent threat/security review PASS | NOT STARTED |
| O46-G07 | Explicit owner release authorization; feature remains disabled until this gate | NOT AUTHORIZED |
## 9. Status and non-authorization

O-46 remains **OPEN / NOT IMPLEMENTED / NOT VERIFIED**. O46-G01's owner-decision component is now recorded in the 2026-11-23 owner decision; its threat review and implementation proof remain open. PR #86 adds no Bootstrap or Recovery runtime path. The email-only OTP values and exclusions remain the existing owner-approved policy, but they do not resolve the entrypoint, email ownership, SMTP, key custody, schema reservation or independent review gates. Existing database columns and storage primitives do not constitute bootstrap or Recovery. The older ten-offline-code wording is superseded for this phase by the later owner-approved email-only policy. No bootstrap credential, OTP, recovery code, SMTP secret, HMAC key, schema, migration, production endpoint, listener, feature activation, G1 activation, Posting Release or Go-Live is authorized by this audit.

**Prior version:** v0.4 remains historical; this v0.5 successor controls current O-46 decision status.

**END — MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001 v0.5**


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

**END — MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001_v0.6.md**
