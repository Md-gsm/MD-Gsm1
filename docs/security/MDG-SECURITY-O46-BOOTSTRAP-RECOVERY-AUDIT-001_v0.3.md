# O-46 IdentityAccess Bootstrap and Recovery Audit — Capture-Once, Invalidate, Secret Hygiene

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001 |
| Version / Date | 0.3 / 2026-11-21 |
| Baseline | ADR-0013; IdentityAccess owner decisions 2026-11-06/07; Stage 08; N.3/N.6 |
| Status | CONTROLLED IMPLEMENTATION-BLOCKER AUDIT — EMAIL-ONLY LEAST-PRIVILEGE CLEANUP RECORDED; TRUST BOUNDARIES OPEN; NO RUNTIME IMPLEMENTATION |
| Execution | Documentation/source audit and implementation-boundary review only. No bootstrap/recovery code, secret, credential, migration, database grant, SMTP, endpoint or runtime operation performed. |

## 1. Current source findings

The current main source has IdentityAccess account/session/recovery persistence primitives, but O-46 is not implemented or verified.

| Area | Current state | Required closure |
|---|---|---|
| Initial administrator creation | No installer/bootstrap provisioning workflow or one-time bootstrap handler was found. Migration 0003 has an `is_bootstrap_account` flag, unique partial index and `bootstrap_completed_at_utc` setting, but no operational state machine uses them. | Secure one-time provisioning, first-login forced change and fail-closed startup/listener boundary. |
| Static credential | ADR-0013 contains historical `admin/admin` wording; the owner decisions and `BootstrapInvariantTests.FixedPairIsAbsent` prohibit a fixed/guessable credential in source, installer, config, migration, logs or telemetry. | Do not implement or ship the static pair. Resolve the secure one-time delivery method. |
| Capture-once bootstrap secret | No one-time secret generation/delivery/claim workflow was found. | Generate high-entropy one-time material, show/deliver only through a trusted approved channel, persist only a verifier/digest, and invalidate atomically on claim or expiry. |
| First-login setup | Product decision requires username/password/profile change on first login; no complete bootstrap state machine or fail-closed gate was found. | Require setup completion before network listener and ordinary business operations are enabled. |
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

## 3. Secure bootstrap target contract

The product decision is to create the initial administrator during installation and force a first-login change of username, password and profile. It does not approve a static password or specify the trusted delivery channel.

Proposed safe lifecycle, pending approval of the local trust boundary:
1. Installer/provisioning service creates a one-time bootstrap challenge using a CSPRNG with at least 256 bits of entropy.
2. The bootstrap secret is delivered once through an explicitly approved trusted local channel. It must not be embedded in source, installer, appsettings, migration, environment sample, logs, telemetry, crash reports or CI artifacts.
3. PostgreSQL stores only a verifier/digest of the one-time secret, bound to the single bootstrap account, purpose, expiry and installation identity. Raw secret is never stored or recoverable.
4. Bootstrap claim is single-use and transactionally serialized. The successful claim consumes/invalidate the challenge, creates or finalizes the administrator credential, assigns only the approved bootstrap role, sets `must_change_password=true`, and records secret-free audit evidence.
5. First login permits only the required username/password/profile completion workflow. The account remains restricted until all required setup fields are committed.
6. Listener/network binding and ordinary application/business routes remain disabled until bootstrap completion is durably recorded and verified from PostgreSQL.
7. Replays, expired challenges, unknown installation IDs, duplicate claims, DB/provider errors and ambiguous commit outcomes fail closed. An unknown commit outcome is reconciled using a stable operation ID; it never issues a second bootstrap secret/account.
8. After successful claim, the one-time bootstrap verifier is unusable. No reusable recovery credential or static default remains.

The trusted channel (installer UI, local administrator console or another approved method), expiry period, OS trust assumptions, installation identity and operator proof are not owner-approved yet. Do not select them by inference.

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

The new source and effective-privilege assertions have **not yet passed a fresh exact-head PostgreSQL 18 CI run**. Until that run succeeds, the change is SOURCE UPDATED / CI PENDING rather than an executed PASS. No production grant script or database was modified.

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
| O46-T06 | First-login forced change | Username/password/profile completion is mandatory |
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

1. Trusted bootstrap delivery channel and local trust boundary; installer UI vs local administrator console vs another method.
2. Bootstrap challenge expiry and operator identity/authorization; installation identity binding.
3. How the initial super_admin email is captured and verified before Recovery is available.
4. Recovery entrypoint: network endpoint or trusted local console, including its threat boundary.
5. SMTP relay/provider selection and secret delivery mechanism.
6. Dedicated recovery HMAC key provider, versioning, rotation and recovery ownership.
7. WG-0 reservation and approval of the exact migration/schema delta.
8. Independent reviewer for mailbox compromise, bootstrap trust boundary and secret hygiene.

## 8.1 Execution decision for this work package

Bootstrap and Recovery are deliberately **not implemented** in PR #86 at this point. This is not a skipped coding task: required security trust boundaries and operational dependencies have not been approved, and implementing them by inference would create a new authentication/recovery path without an accepted threat boundary.

Blocking dependencies that must be resolved before the first production-capable implementation:

1. **Bootstrap delivery and local trust:** the installer/local administrator channel, operator identity, installation binding, protection against local process/user inspection, secret capture-once display and expiry are not selected. No static password, file drop, command-line argument, environment variable, installer log or hidden default is an acceptable substitute.
2. **Recovery entrypoint and account proof:** no accepted decision exists between a network endpoint and a trusted local console. The initial `super_admin` email needs authoritative enrollment and proof before any email-only reset can be trusted. The owner-approved email-only channel policy does not itself authorize a route or prove mailbox ownership.
3. **Operational services:** SMTP relay, TLS/authentication, provider delivery semantics, rate-limit enforcement, secret manager and dedicated versioned HMAC key custody/rotation/recovery have not been provisioned or selected.
4. **Schema and registry:** migration 0003 lacks the complete attempt/send quota/lockout/key-version/delivery/operation-id state required by the approved Recovery policy. Migration 0009 remains reserved and must not be consumed. Exact schema must be reviewed and reserved by WG-0 before DDL is authored/applied.
5. **Authorization and bootstrap seed:** no accepted end-to-end bootstrap account/role/permission seed design proves that the first account is created under a separate deployment principal while the API runtime remains denied account/role INSERT. Do not weaken runtime grants to make bootstrap appear to work.
6. **Atomicity and operational evidence:** no concurrency/idempotency proof exists for claim, quota, challenge replacement, delivery ambiguity, credential change, session revocation and audit outbox as one transaction. Secret-sentinel scans and crash/trace/telemetry proof have not run.

Safe work permitted before those decisions is limited to domain-neutral contract refinement, test specifications, threat modeling and disposable-only test harness preparation. Do not add a live route, enable the feature flag, provision a credential, generate real OTP/bootstrap material, run SMTP or apply migration/DB grants.

### Release-gate sequence

| Gate | Required evidence | Current status |
|---|---|---|
| O46-G01 | Owner records trusted bootstrap channel, operator authorization, installation binding and expiry | BLOCKED — DECISION REQUIRED |
| O46-G02 | Owner records Recovery entrypoint/trust boundary and verified-email enrollment/proof contract | BLOCKED — DECISION REQUIRED |
| O46-G03 | Platform provisions approved SMTP, separate HMAC key and secret-manager operations (without exposing secrets to repo/CI) | BLOCKED — PLATFORM DEPENDENCY |
| O46-G04 | WG-0 reserves reviewed schema/migration; account/role seed and bootstrap principal boundary reviewed | BLOCKED — NOT RESERVED / NOT APPROVED |
| O46-G05 | Disposable PostgreSQL concurrency, quotas, replay, unknown-commit, key-rotation and recovery tests all PASS | NOT STARTED |
| O46-G06 | Secret-sentinel scans across logs/traces/crash/CI plus independent threat/security review PASS | NOT STARTED |
| O46-G07 | Explicit owner release authorization; feature remains disabled until this gate | NOT AUTHORIZED |
## 9. Status and non-authorization

O-46 remains **OPEN / NOT IMPLEMENTED / NOT VERIFIED**. PR #86 adds no Bootstrap or Recovery runtime path. The email-only OTP values and exclusions remain the existing owner-approved policy, but they do not resolve the entrypoint, email ownership, SMTP, key custody, schema reservation or independent review gates. Existing database columns and storage primitives do not constitute bootstrap or Recovery. The older ten-offline-code wording is superseded for this phase by the later owner-approved email-only policy. No bootstrap credential, OTP, recovery code, SMTP secret, HMAC key, schema, migration, production endpoint, listener, feature activation, G1 activation, Posting Release or Go-Live is authorized by this audit.

**END — MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001 v0.3**
