# O-46 — Physical Schema Decision Register for WG-0

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **ENGINEERING DISPOSITION — OWNER DECISIONS / IMPLEMENTATION GATES REMAIN OPEN** |
| Related proposal | MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001 v0.1 |
| Branch / PR | security/o20-o21-final-closure / PR #92 Draft |
| Scope | Resolve the seven pre-WG-0 questions to the extent supported by existing accepted decisions; identify what cannot be closed technically |

## 1. Disposition summary

This register does not convert a technical recommendation into Product Owner approval. It separates decisions already constrained by accepted policy, engineering recommendations ready for owner disposition, and proof obligations that belong after schema reservation and implementation.

| # | Question | Disposition | Rationale / boundary |
|---:|---|---|---|
| 1 | Canonical tables, columns and payload fingerprint | **RECOMMENDED BASELINE; exact fingerprint remains OPEN** | Freeze the three IdentityAccess-owned tables named in the physical schema proposal. Keep the receipt append-only. Use a versioned canonical payload envelope; do not include raw password, raw challenge or secret. Fingerprint must bind all material account/profile inputs and the password-hash output. A keyed HMAC-SHA-256 is preferred if a separately managed key can be provided without expanding the approved principal boundary; otherwise the fingerprint format needs an explicit threat review before schema DDL. No unkeyed fingerprint of a low-entropy profile/password-only payload is accepted as a final design. |
| 2 | Challenge issuance ownership and persistence | **IDENTITYACCESS OWNS CHALLENGE STATE; ISSUANCE MECHANISM OPEN** | IdentityAccess owns challenge binding, expiry, single-use consumption and owner claim. Platform may carry SetupOperationId and orchestration state only. The current approved executor contract exposes only the exact claim capability; it does not yet define a separately authorized challenge-issuance caller. Do not silently add a second database principal, widen executor to a generic dispatcher, or let Platform own the authoritative challenge. Resolve the issuer path as a separate narrowly typed capability before function/ACL implementation. |
| 3 | Windows operator SID attestation | **RECOMMENDED BASELINE** | Setup Service obtains the client token by Windows pipe impersonation only for identity inspection, extracts the OS-authenticated user SID (and logon/session identity where required), reverts impersonation, then supplies the value over its authenticated database channel. The database trusts this assertion only because the executor credential and local pipe are exclusive to the dedicated Setup Service; it must not accept SID from Wizard payload. Challenge and claim compare the same SID. Pipe/DACL/process-authentication proof remains NOT RUN. |
| 4 | Owner profile fields | **OWNER INPUT REQUIRED; NOT INFERRED** | Existing accounts schema has nullable email and phone but no canonical display/profile-name columns. The accepted policy says Wizard collects final profile but does not define the field set or requiredness. Do not silently treat email/phone as the complete profile or invent fields. Until owner specifies the profile contract, reserve only schema-independent bootstrap state if WG-0 permits, or defer the Account profile portion of the migration. |
| 5 | UUIDv7 generation and audit vocabulary | **RECOMMENDED BASELINE** | Platform generates stable InstallationId and SetupOperationId; IdentityAccess generates AccountId and audit EventId using the approved UUIDv7 provider at its trusted server boundary. The DB function accepts typed UUIDs and validates binding/idempotency; it does not invent identity from username, SID or machine name. Proposed audit event type: `identity.product_owner.bootstrap_claimed`; outcome: `success`. Audit is an in-transaction intent event, not proof of external audit delivery. UUIDv7 generator compatibility and event vocabulary require code-level tests. |
| 6 | Meaning of `bootstrap_completed_at_utc` | **RECOMMENDED BASELINE** | Immutable `identity.product_owner_bindings` existence is the authoritative owner-claim predicate. Keep `system_settings.bootstrap_completed_at_utc` only as a compatibility/diagnostic marker, set atomically in the same transaction and never use it alone to authorize a new claim. Any mismatch between marker, binding and receipt is an integrity failure and fail-closed. |
| 7 | PostgreSQL 18 proof | **POST-RESERVATION ACCEPTANCE EVIDENCE** | Exact ACL, column-level grants, FK/index/trigger/RLS behavior, security-definer semantics, lock serialization, concurrent claims, idempotent retry, unknown commit, challenge expiry/replay, rollback and no-second-owner require a disposable PostgreSQL 18 fixture. These are not preconditions to request a number, but are mandatory before implementation acceptance or any runtime activation. All remain NOT RUN / NOT VERIFIED. |

## 2. Critical design issue: challenge issuance

The accepted owner policy requires a challenge to be issued, delivered once to the local Wizard, persisted verifier-only, bound to InstallationId + SetupOperationId + purpose + authenticated operator SID, and consumed atomically by IdentityAccess.

The approved DB principal decision and package contract currently define:
- a one-purpose executor;
- one exact SECURITY DEFINER claim routine;
- no direct table access;
- no generic SQL/operation dispatcher;
- no new persistent deployment LOGIN;
- cluster-admin maintenance only for local provisioning/install/verify/revoke operations, not routine application setup.

Those constraints do not yet identify an authorized runtime caller for the initial challenge INSERT. The function-owner NOLOGIN role cannot be called directly; the executor is not approved for arbitrary or additional table operations; Platform is not the owner of IdentityAccess challenge state.

Therefore, before SQL/ACL implementation, the design must select and approve one of these safe patterns:
1. one additional narrowly typed SECURITY DEFINER challenge-issuance routine, with its own explicitly authorized caller boundary and no generic dispatcher; or
2. a revised single-purpose claim protocol in which challenge issuance and claim are both operations of one fixed, typed routine without creating an ambiguous generic command surface; or
3. another reviewed design that preserves IdentityAccess ownership and does not create a new persistent principal or expose cluster-admin credentials.

No option is selected by this register. The existing seven binding deployment restrictions remain in force. This issue is a design blocker for final function signature/ACL, but does not prevent WG-0 from considering a schema-number reservation if WG-0 accepts the proposed table-only migration scope.

## 3. Migration reservation recommendation

Request WG-0 to reserve candidate **0010** for:
- owner binding;
- setup challenge state;
- idempotent bootstrap operation receipt.

Proposed exact filename:
`database/migrations/0010_identityaccess_owner_bootstrap.sql`

Domain/schema owner: IdentityAccess.

Explicitly exclude:
- SQL function or SECURITY DEFINER routine;
- role/principal creation or alteration;
- grants, memberships, LOGIN activation or secrets;
- Account/Owner seed or credential;
- role/permission seed;
- changes to N.6 operational identities or migration_runner;
- Recovery activation;
- Organization/Accounting business records;
- production application or runtime activation.

0009 remains reserved for Accounting. Candidate 0010 is not reserved until WG-0 records and merges the reservation in the authoritative registry. If WG-0 assigns another number/path, update all documents before SQL is written.

The table-only migration can be reviewed and reserved independently of the still-open challenge issuer and Owner profile details only if WG-0 confirms that the candidate DDL scope is sufficiently stable. If WG-0 requires all seven questions closed first, do not reserve and return the request as blocked; no SQL may be drafted.

## 4. Required next gates

1. Product Owner disposition of the recommended baseline and explicit definition of the initial Owner profile fields.
2. Separate design decision for challenge issuance caller/function boundary.
3. WG-0 decision on candidate 0010 and exact migration path.
4. Independent Security/Architecture review of the final schema/ACL and challenge issuer design.
5. Only after reservation: DDL authoring under IdentityAccess ownership, disposable PostgreSQL 18 tests, catalog privilege verification and checksum/registry updates.
6. Exact-head CI and independent reviewer acceptance. PR #92 remains Draft until all required gates are complete.

## 5. Status

- Seven-question disposition: **5 recommendations / 1 owner-input item / 1 post-reservation proof item**, with challenge issuance explicitly retained as a design blocker.
- Owner profile contract: **OPEN**.
- Challenge issuer: **OPEN / BLOCKING FUNCTION ACL**.
- WG-0 reservation: **REQUESTABLE; NOT RESERVED**.
- Migration 0010: **NO SQL / NOT AUTHORIZED**.
- PostgreSQL 18 evidence: **NOT RUN**.
- O46-G04: **OPEN**.
- IdentityAccess G1 and runtime activation: **NOT GRANTED**.

**END — MDG-SECURITY-O46-PHYSICAL-SCHEMA-DECISION-REGISTER-001 v0.1**
