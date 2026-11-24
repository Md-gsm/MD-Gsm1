# Platform First-Run Setup Orchestration Contract — Proposal

| Field | Value |
|---|---|
| Document ID | MDG-PLATFORM-FIRST-RUN-SETUP-ORCHESTRATION-CONTRACT-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **CROSS-MODULE TECHNICAL PROPOSAL — NON-AUTHORIZING — MODULE CONTRACT ACCEPTANCE OPEN** |
| Scope | One-time first-run coordination on the designated central server |
| Decision sources | ADR-0019; O-46 Owner Decision v0.1; IdentityAccess Baseline v1.1; CHG-101 |
| Related modules | IdentityAccess, Organization, Accounting, Platform Operations |

## 1. Purpose

This proposal defines how the already accepted one-time Setup Wizard can coordinate the initial Product Owner account, workshop identity and initial FiscalPeriod without taking ownership of any of those module data sets and without creating a distributed transaction.

It does not create module endpoints, tables, migrations, runtime permissions, roles, grants or an installer. Organization and Accounting must accept the command contracts within their own boundaries before implementation. The Product Owner and existing independent reviewer remain the only human project participants; no additional person or team is appointed.

## 2. Invariants

1. The Wizard is available only on the designated central Windows server host through the accepted local IPC trust boundary. Desktop clients never invoke it.
2. The initial Product Owner is created only once through the Wizard. No alternate creation, replacement, transfer, successor or re-enrollment route exists.
3. InstallationId is stable UUIDv7. Reinstall, Repair, update and restore do not assign a new InstallationId or cause a new setup.
4. Reinstall/update, DB unavailability, authentication failure, schema mismatch, migration failure or missing configuration cannot be interpreted as a fresh installation.
5. Setup is permitted only when the server has an explicitly provisioned central database, matching protected InstallationId metadata, a supported schema and a durable authoritative `NotStarted` state.
6. IdentityAccess owns Account, credential, Product Owner binding and owner-claim result. Organization owns workshop/business identity. Accounting owns FiscalPeriod. Platform owns only technical orchestration state, stable operation identifiers, step state and references to module receipts.
7. Each module commits only its own local transaction. There is no single distributed transaction across modules and no destructive compensating deletion of a previously committed identity or business record.
8. Every mutating setup command is idempotent under the same InstallationId + SetupOperationId + StepKey. A different payload for an already committed step is a conflict, not an update.
9. The initial Product Owner's final username/password/profile are completed in the Wizard. No forced credential/profile change occurs after the first owner login. Ordinary subsequent password-change behavior remains intact.
10. A successful SetupCompleted state does not itself authorize G1, production listener activation, module runtime, production migrations or Go-Live. Those remain distinct release gates.

## 3. Canonical setup lifecycle

The recommended monotonic state machine is:

`NotStarted → OwnerBound → OrganizationCreated → InitialFiscalPeriodCreated → Completed`

`RecoveryRequired` is an exceptional hold state, not a normal progression step and never a route back to `NotStarted`.

| State | Evidence required to enter | What it means |
|---|---|---|
| `NotStarted` | Server provisioning confirms exact central database target, InstallationId match and supported schema; no owner claim has ever committed | The one-time Wizard may begin |
| `OwnerBound` | IdentityAccess durable receipt for one AccountId + immutable Product Owner binding + InstallationId + consumed bootstrap challenge under one SetupOperationId | First owner is established; no second owner may be created |
| `OrganizationCreated` | Organization durable receipt for the initial workshop/business identity under the same operation | Workshop identity exists exactly once |
| `InitialFiscalPeriodCreated` | Accounting durable receipt for exactly one initial FiscalPeriod under the same operation | First period exists; no opening balances are implied |
| `Completed` | All required step receipts are re-read from their owning modules and match the current InstallationId and SetupOperationId | First-run setup is durably complete; the initial Wizard is permanently disabled |
| `RecoveryRequired` | Conflicting, missing, rolled-back, unverifiable or ambiguous durable evidence that cannot be reconciled safely | Stop setup and privileged activation; require controlled recovery, never re-enroll owner |

State transitions are monotonic. A committed state is never reset by reinstall, repair, update, application restart, current calendar year, a missing settings file or a DB connection error. The transition from `Completed` to any other state is prohibited by ordinary setup commands.

### 3.1 SetupOperationId

- The server creates one SetupOperationId for the one initial setup operation after verifying `NotStarted`.
- The identifier is stable across Wizard restart, process crash, timeout, client response loss and retry. It is not regenerated to make a failed request appear new.
- The recommended identifier representation is UUIDv7, subject to the already accepted platform identifier policy and actual module contract.
- Platform persists the operation identity and lifecycle state in its own persistence boundary. The exact record schema and migration remain unapproved and must be reserved through WG-0 before DDL.
- Each module receives the same InstallationId and SetupOperationId plus its own stable StepKey, such as `identity.owner.v1`, `organization.initial-workshop.v1` and `accounting.initial-fiscal-period.v1`.
- A second concurrent Wizard instance cannot create a second SetupOperationId for the same InstallationId. Platform serializes lifecycle transitions and uses optimistic version/state checks or an equivalent proven database serialization mechanism.

### 3.2 Durable receipts and idempotent module commands

Each module owns its own durable operation outcome, committed atomically with its own data:

| Step | Data owner | Required durable outcome |
|---|---|---|
| Initial owner | IdentityAccess | Original AccountId, credential completion status, immutable Product Owner binding, challenge consumed and owner-claim operation result |
| Workshop | Organization | Organization-owned workshop/business identity reference and step operation result |
| Initial fiscal period | Accounting | Accounting-owned PeriodId, validated date range/status and step operation result |

Platform stores only the technical receipt reference and state metadata required to coordinate. It does not duplicate username, password, owner profile, workshop name/contact data, fiscal period dates, accounting balances or other business payloads.

If a module commits but the caller loses the response before Platform records the receipt, the next attempt queries that same module using the same SetupOperationId/StepKey. The module returns the prior committed result if the payload fingerprint and installation binding match. If a different payload is supplied for an already committed step, the module returns a typed conflict and no mutation occurs.

Suggested typed outcomes (names illustrative, contract acceptance pending):

- `Applied`
- `AlreadyAppliedSameOperation`
- `ConflictDifferentOperationOrPayload`
- `RejectedInvalidState`
- `Unavailable`

No caller-supplied ActorId, AccountId, Product Owner flag, role or service identity is accepted as authority. The server-side setup boundary derives the authorized local operator and installation context from the trusted host/IPC boundary.

## 4. Step execution protocol

### 4.1 Preflight

Before presenting data-entry steps or issuing a bootstrap challenge, the central host must:

1. verify it is the configured server installation, not a Windows desktop client;
2. connect to the exact configured central PostgreSQL database;
3. compare protected local InstallationId metadata with the database's authoritative InstallationId;
4. check the supported schema version and migration history without applying migrations implicitly;
5. read the authoritative Platform setup state and check that it is truly `NotStarted`;
6. verify that no Product Owner binding/claim already exists and no contradictory owner-claim evidence is present; and
7. verify that the accepted local IPC endpoint and service identity are available.

Any unavailable, mismatched, partial, unknown or conflicting condition fails closed. No empty database is created and the Wizard is not used as a repair or recovery mechanism.

### 4.2 IdentityAccess owner step

The Wizard obtains the final owner username, password and required profile fields. The local IPC flow is bound to the accepted one-time challenge and the stable SetupOperationId.

IdentityAccess is the only module allowed to validate and normalize the username, apply PasswordPolicy, hash the final password and commit the original AccountId + Product Owner binding. The Account, credential, binding, challenge consumption, stable owner-claim result and IdentityAccess audit intent must commit or roll back together in one IdentityAccess-owned local transaction.

The initial owner's final password is not temporary and must not be marked for forced change after the first login solely because the Account is new. This step does not implicitly create roles, permission grants, a SuperAdmin expansion or a runtime database login. Those remain O46-G04 and separate authorization gates.

After a durable `OwnerBound` result, failure of a later step must never return the lifecycle to `NotStarted` and must never offer an alternative Product Owner creation route.

### 4.3 Organization workshop step

Organization receives only the workshop/business-identity fields it owns, plus InstallationId and SetupOperationId/StepKey for idempotency. It validates and commits its own record in its own transaction and returns its own durable ID/receipt.

The Wizard cannot write Organization-owned tables directly. IdentityAccess does not store or own workshop business data. Exact Organization entity shape, required fields, validation, first-organization uniqueness and its public/internal command contract remain subject to the Organization design and data-owner acceptance.

### 4.4 Accounting initial FiscalPeriod step

Accounting receives the user-selected initial period values and the same InstallationId and SetupOperationId/StepKey. It validates dates and creates exactly one Accounting-owned FiscalPeriod in its own transaction.

The current FiscalPeriod domain is a dated entity with StartsOn, EndsOn and Open/Closed status; it permits posting only for dates inside an open period. The setup contract must preserve Accounting's ownership of date-range validation and status transitions.

The Wizard must not silently select start/end dates, assume a default fiscal year from the machine clock, or create a new period during reinstall, update or a new calendar year. Persian calendar display/input can be provided by the UI, but the canonical range and calendar conversion/validation must follow the accepted Accounting contract.

Creation of the initial FiscalPeriod is not an opening-balance posting. It creates no ledger entries, chart-of-accounts seed, cash/bank balances, Party subledger balance, inventory quantity/value, tax balance or opening journal. Those require their separate Accounting/Finance/Inventory designs and acceptance.

### 4.5 Completion

Platform marks the setup `Completed` only after it re-reads and verifies all three owner-module receipts for the same InstallationId + SetupOperationId and confirms that each required owner-module record still exists and matches its receipt.

Completion must be idempotent. Replaying the same completion request cannot create another account, workshop or period. The local Wizard's owner-creation action is then permanently disabled.

`SetupCompleted` and `RuntimeActivationAuthorized` are separate facts. Ordinary sign-in, network listener, route activation and business workers remain subject to the separately accepted bootstrap role/permission, TLS, deployment, security-review, CI and release gates.

## 5. Failure, restart and reconciliation

- **Failure before a module transaction commits:** the current step remains incomplete and may be retried with the same operation/step identity. The user may re-enter unsaved form values; no secret or business payload is stored in Platform merely to reconstruct a discarded UI session.
- **Commit succeeded, response lost:** query the owning module by the same SetupOperationId/StepKey and reconcile the durable outcome. Never issue a new owner claim or new operation ID.
- **Owner step committed, Organization step failed:** remain at `OwnerBound`; resume only the Organization step under the same operation. Do not delete or recreate the owner.
- **Organization step committed, Accounting step failed:** remain at `OrganizationCreated`; resume only the Accounting step. Do not delete or duplicate the workshop.
- **Accounting step committed, Platform completion update failed:** query Accounting's existing receipt and revalidate all prior receipts; then mark Completed once.
- **DB unavailable or schema mismatch:** stop; no fallback database, no Wizard re-enrollment, no schema reset or implicit migration.
- **Unknown or contradictory step result:** enter `RecoveryRequired`; preserve records and evidence; require controlled investigation and exact same-ID reconciliation.
- **Cancellation or power loss:** resume the same SetupOperationId and latest durable state. Do not roll back a successfully committed module step by deleting business/security data.

A failure after `OwnerBound` does not entitle an operator to start another Owner wizard. The only allowed continuation is the remaining steps of that same SetupOperationId. If the original AccountId/binding is lost or cannot be proven, the privileged path fails closed; no replacement owner is enrolled.

## 6. Restore and rollback protection

A restore must not accidentally turn an already-claimed installation back into `NotStarted`.

- The restore procedure must validate InstallationId, original AccountId, Product Owner binding, setup state, all module step receipts and restore-point provenance before the server can reopen first-run operations.
- If a restored database or local configuration indicates that owner claim/setup had previously advanced but the restored data lacks the corresponding owner/binding/receipt, enter `RecoveryRequired`. Never infer `NotStarted` from missing rows.
- A restore point from before the initial owner claim must not automatically re-enable Product Owner enrollment on an installation whose owner was previously created. The evidence/provenance mechanism that prevents correlated rollback of both database and local state remains an open technical requirement; it must be addressed in the backup/restore threat model and verified before runtime implementation.
- Restore may recover only the exact original AccountId and InstallationId. If the exact AccountId cannot be recovered, no new owner is created.
- Reinstall/Repair/update preserve database target, local installation identity, key dependencies and business files. A client-machine reinstall has no authority to change server setup state.

## 7. Platform setup persistence boundary

The future Platform-owned setup record may contain only technical orchestration facts, such as:

- InstallationId;
- stable SetupOperationId;
- current monotonic lifecycle state and optimistic concurrency version;
- step key/status and owner-module receipt references;
- timestamps, correlation IDs and sanitized error/reconciliation codes;
- non-secret operation fingerprints needed to reject different-payload replay.

It must not store raw owner passwords, bootstrap challenge, bearer tokens, recovery OTPs, SMTP credentials, database connection secrets, duplicated account/profile data, workshop content, fiscal period business data or accounting balances.

The actual schema, database ownership, ACL, DDL and migration reservation remain open. This proposal consumes no migration number, including reserved 0009.

## 8. Required acceptance tests — NOT RUN

| ID | Scenario | Pass condition |
|---|---|---|
| SETUP-ORCH-01 | Fresh server setup | One SetupOperationId and the monotonic state sequence are created once |
| SETUP-ORCH-02 | Concurrent Wizard launch | Exactly one operation is active for an InstallationId |
| SETUP-ORCH-03 | Same-step retry | Same OperationId/StepKey returns the existing result without duplicate records |
| SETUP-ORCH-04 | Different-payload replay | Different payload for a committed step is rejected without mutation |
| SETUP-ORCH-05 | Unknown IdentityAccess commit | Exact original AccountId/binding result is reconciled; no second owner |
| SETUP-ORCH-06 | Failure after OwnerBound | Wizard resumes Organization step only; no owner re-enrollment |
| SETUP-ORCH-07 | Failure after OrganizationCreated | Wizard resumes Accounting step only; no workshop duplication |
| SETUP-ORCH-08 | Failure after Accounting commit | Platform recovers the same PeriodId and marks Completed once |
| SETUP-ORCH-09 | No distributed transaction | Module local transactions/receipts reconcile across process restarts |
| SETUP-ORCH-10 | FiscalPeriod validation | Invalid range and invalid status are rejected by Accounting; no UI/client bypass |
| SETUP-ORCH-11 | No opening balances | Setup creates no journal, cash/bank/party/inventory balances or opening postings |
| SETUP-ORCH-12 | Reinstall/Repair/update | Same DB target, InstallationId, AccountId, workshop and FiscalPeriod survive; Wizard not repeated |
| SETUP-ORCH-13 | DB unavailable/schema mismatch | Fail-closed; no empty DB, migration, reset or fresh Wizard |
| SETUP-ORCH-14 | Restore before-owner point | Previously claimed installation never returns to NotStarted; exact original AccountId recovery only |
| SETUP-ORCH-15 | Restore same valid point | All module IDs/receipts and Completed state reconcile without duplicates |
| SETUP-ORCH-16 | Completed setup replay | Owner/workshop/period creation stays permanently disabled |
| SETUP-ORCH-17 | Runtime activation separation | SetupCompleted alone does not open listener or set G1/production flags |

All tests remain NOT RUN / NOT VERIFIED.

## 9. Decisions and module acceptance still required

1. IdentityAccess owner must accept the exact public/private claim command, operation-result semantics and transaction boundary.
2. Organization contract must accept its initial workshop command, data schema/validation, uniqueness and idempotency receipt.
3. Accounting contract must accept the initial-period command, exact required input fields, date/calendar conversion and initial status policy.
4. Platform must accept ownership and persistence schema for SetupOperationId/state/step receipts; WG-0 must reserve the migration before DDL.
5. O46-G04 must close the separate bootstrap deployment/provisioning principal and role/permission seed boundary without broadening runtime grants.
6. Restore provenance and anti-rollback evidence must prevent a previously claimed installation from returning to `NotStarted`.
7. Independent Security/Architecture review must assess this cross-module design and the exact implementation SHA.

No additional human module owner or project participant is appointed by this proposal. The logical data owners remain the modules; existing Project Owner acceptance and independent reviewer duties remain unchanged.

## 10. Non-authorization

This proposal does not implement Setup Orchestrator, IPC, IdentityAccess bootstrap, Organization setup, Accounting setup, schema, migration, role/permission seed, credential, grant, listener, Recovery, G1 or Go-Live. It does not assign dates, opening balances, role grants or fiscal calendar defaults. No new owner can be created by any path other than the owner-accepted initial Wizard, and this document adds no alternate route.

**END — MDG-PLATFORM-FIRST-RUN-SETUP-ORCHESTRATION-CONTRACT-001 v0.1**
