# O20 Database Role / Object Privilege Matrix — Worker Identity Decision Gate

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 |
| Version / Date | 1.6 / 2026-11-22 |
| Supersedes | v1.5 |
| Status | FIVE-IDENTITY N.6 BASELINE PRESERVED; O21 OUTBOX WORKER BOUNDARY NOT YET ACCEPTED |
| Related audit | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 v1.8 |

## 1. Logical matrix retained

The N.6 five logical identity categories remain unchanged: runtime application, migration runner, backup/recovery operator, read-only observer and ephemeral emergency operator. `mdgsm_schema_owner` remains a separate NOLOGIN owner. The five-category policy is not silently expanded by this successor.

## 2. Physical runtime login binding

The O20 disposable LOGIN mapping proof demonstrates one candidate connection shape: a constrained LOGIN principal has one SET-only membership to a NOLOGIN runtime ACL role. This does not approve a production credential, login, role membership or connection-pool configuration. The N.6 audit remains the policy authority until Platform/DB owner accepts a deployment binding.

## 3. Background worker / outbox privilege boundary

A canonical outbox delivery process needs an execution context distinct from ordinary application commands. Its minimum possible database operations are: claim eligible outbox rows using a bounded lease, read immutable committed envelopes, persist attempt/delivery metadata and quarantine status. It does not need general CRUD against Branch/Warehouse/assignment aggregates, Finance, Persons or IdentityAccess private tables.

The precise database design must use one of the following owner-approved outcomes:
- a separate narrowly privileged logical service identity (preferred), which extends the current five-category baseline; or
- a provably separate role/process context under an existing identity, with enforceable non-overlap between runtime and worker actions.

A single broad runtime principal that can mutate both business aggregates and outbox delivery metadata does not satisfy the separation objective without additional enforceable controls.

## 4. Matrix entries intentionally left ungranted

Until the Product Owner, Platform/DB and Security approve the worker model, all of the following remain UNPROVISIONED:
- worker LOGIN/service principal;
- worker database role or role membership;
- outbox claim/update/quarantine grants;
- worker secret and provider/network credentials.

No worker grant is implied by the local synthetic outbox PoC. No direct base-table reporting privileges are implied by `reports.*` application permissions.

## 5. Required proof

The final proof must run separate worker and runtime principals concurrently and show:
- worker can claim/read/update only outbox rows;
- worker cannot read or mutate domain aggregates or identity/credential/session data;
- runtime cannot update delivery metadata or change outbox payload/hash;
- expired lease/retry/duplicate delivery does not duplicate canonical AuditSecurity events;
- same EventId and payload hash replay idempotently; mismatched hash quarantines;
- loss of AuditSecurity does not silently delete local intent;
- role/session reset on pooled connections prevents privilege carry-over;
- effective privileges and default privileges are inventoried on the target database.

**Disposition:** five logical identities remain the accepted baseline; background worker identity count and role/object grants are a blocking owner decision; no production role, LOGIN, grant or provider configured.

**END — MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 v1.6**
