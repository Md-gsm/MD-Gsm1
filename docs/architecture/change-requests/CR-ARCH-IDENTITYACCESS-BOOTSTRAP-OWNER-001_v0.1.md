# CR-ARCH-IDENTITYACCESS-BOOTSTRAP-OWNER-001 — CHG-101

| Field | Value |
|---|---|
| Change ID | CHG-101 |
| Request ID | CR-ARCH-IDENTITYACCESS-BOOTSTRAP-OWNER-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **OWNER-ACCEPTED SCOPE — INDEPENDENT REVIEW AND IMPLEMENTATION GATES OPEN** |
| Decision owner | Product Owner and IdentityAccess Module Owner (same existing account) |
| Independent reviewer | Existing Security/Architecture Reviewer |
| Target Master successor | MDG-ARCH-MASTER-001 v3.0.4 |
| Branch / PR | security/o20-o21-final-closure / PR #92 |

## 1. Reason and decision source

The Product Owner explicitly accepted the O-46 Bootstrap Trust Boundary recommendations and resolved the conflicting first-owner password-change requirement on 2026-11-23. The controlling decision is recorded in:

- MDG-SECURITY-O46-BOOTSTRAP-OWNER-DECISION-001_v0.1.md
- MDG-SECURITY-O46-BOOTSTRAP-TRUST-BOUNDARY-PROPOSAL-001_v0.2.md
- MDG-IDENTITYACCESS-MODULE-BASELINE-001_v1.1.md
- MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001_v0.5.md
- ADR-0019 addendum.

The current accepted Master v3.0.3 still contains older contradictory text about first-login owner credential change and offline recovery-code issuance. This CR reconciles those specific statements into successor v3.0.4 so one governing architecture remains coherent.

## 2. Accepted owner constraints

1. The initial Product Owner is created exactly once and only by the local Setup Wizard running on the designated central Windows server host.
2. No alternate path may create, replace, transfer or re-enroll Product Owner authority: no API/HTTP/TCP route, desktop client, admin UI, CLI, script, migration, seed, support process or routine direct DB operation.
3. Windows server Wizard uses OS-local IPC through Windows Named Pipe; remote access is rejected; ACL is limited to the exact OS-authenticated local operator SID and minimum service identities.
4. The accepted root-of-trust includes Windows OS Administrator/SYSTEM and PostgreSQL deployment/database-owner boundaries. The proposal does not claim protection against malicious full privilege over those roots.
5. One-time challenge: at least 256-bit CSPRNG entropy, maximum 30-minute lifetime, one-time local delivery, verifier/digest-only persistence, binding to UUIDv7 InstallationId, SetupOperationId, purpose and local SID.
6. InstallationId and the initial owner AccountId/binding remain unchanged through reinstall, Repair, update and restore.
7. Wizard collects final owner-selected username/password/profile; PasswordPolicy and a supported versioned password hash apply. No fixed, guessable, shared, temporary-default or embedded credential is permitted.
8. The initial Product Owner is not forced to change username/password/profile after first login. Normal later password changes remain available. Other-user administrative-reset forced-change behavior is unchanged.
9. Product Owner binding is immutable to the original AccountId + InstallationId. Recovery may restore only that exact AccountId; no successor, alternate owner or transfer.
10. Recovery remains email-only OTP for this phase, is not MFA, and excludes SMS and offline recovery-code issuance.

## 3. Architecture and data-ownership impact

| Concern | Authoritative owner | Effect |
|---|---|---|
| Account, credential, username, Product Owner binding | IdentityAccess | Atomic initial owner Account/credential/binding/challenge claim; no other owner provisioning path |
| SetupOperationId and setup orchestration receipts | Platform Operations | Coordinates only; does not own Account, workshop or FiscalPeriod data |
| Workshop/business identity | Organization | Created in its own local transaction |
| Initial FiscalPeriod | Accounting | Created once in its own local transaction |
| Windows local IPC and protected InstallationId metadata | Platform Operations | Local server-host trust and stable installation identity |
| Canonical AuditSecurity/WORM storage | Existing assigned boundary | Not moved into IdentityAccess |

No cross-module database ownership is transferred. There is no distributed transaction; each owner module has an idempotent local commit and durable operation outcome, reconciled by SetupOperationId. A later failed stage must not delete a previously committed owner, workshop or FiscalPeriod as generic compensation.

## 4. Master v3.0.4 bounded reconciliation

The successor Master updates only:
- Appendix N.1 Bootstrap;
- Appendix N.2 Recovery statements that conflict with the owner-approved email-only/no-offline-code policy;
- IAM-BOOTSTRAP and IAM-RECOVERY test registry summaries;
- O-46 open-issue description/status;
- a new Appendix W change record for CHG-101;
- the top-level document control/revision block identifying v3.0.4 as the owner-accepted successor.

All unrelated v3.0.3 architecture/accounting policies remain unchanged.

## 5. Required verification before implementation acceptance

| ID | Required proof | Status |
|---|---|---|
| CHG101-T01 | Master N.1 and IdentityAccess Baseline agree on exclusive server-local Wizard and no alternate Product Owner path | Design text updated; independent review open |
| CHG101-T02 | First-owner final credentials in Wizard; no forced first-login change; ordinary password-change remains | Design text updated; runtime proof NOT RUN |
| CHG101-T03 | Windows Named Pipe remote rejection and exact SID ACL threat model | NOT RUN |
| CHG101-T04 | Challenge generation, 256-bit entropy, 30-minute expiry, verifier-only persistence, one-time claim | NOT RUN |
| CHG101-T05 | UUIDv7 InstallationId/AccountId binding continuity across reinstall/update/restore | NOT RUN |
| CHG101-T06 | IdentityAccess Account, credential, binding and challenge consumption atomicity | NOT RUN |
| CHG101-T07 | Unknown-commit reconciliation, restart and no duplicate owner | NOT RUN |
| CHG101-T08 | SetupOperationId idempotency across IdentityAccess, Organization and Accounting | NOT RUN |
| CHG101-T09 | Secret-sentinel scan across logs, traces, crash dumps, support bundles and CI artifacts | NOT RUN |
| CHG101-T10 | Exact-head CI and independent review of the final implementation SHA | NOT RUN |

## 6. Explicit non-authorization

This CR authorizes only the documentation reconciliation of the already accepted owner constraints. It does not authorize runtime bootstrap, creation of an account or credential, role/permission seed, schema or migration, database grant, production listener, Recovery runtime, SMTP/key provisioning, Organization or Accounting setup endpoints, G1, production migration or Go-Live. WG-0 migration reservation, threat review, implementation/test evidence and independent review remain mandatory.

**END — CHG-101**
