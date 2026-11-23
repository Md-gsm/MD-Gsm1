# O-46 — Initial Product Owner Challenge Issuance Protocol

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-CHALLENGE-ISSUANCE-PROTOCOL-PROPOSAL-001 |
| Version / Date | 0.3 / 2026-11-23 |
| Status | **OWNER-APPROVED TWO-ROUTINE DESIGN CONSTRAINT — IMPLEMENTATION / EXECUTOR ACTIVATION NOT AUTHORIZED** |
| Owner | IdentityAccess |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | v0.2 where reconciled below |

## 1. Purpose and controlling approval

This successor preserves the Product Owner's bounded approval recorded in PR #92 comment 7110645790. Exactly two fixed-signature SECURITY DEFINER routines are approved as a design constraint:

1. `identity_bootstrap.issue_initial_owner_challenge_v1(...)`
2. `identity_bootstrap.claim_initial_product_owner_v1(...)`

Both are owned by the existing NOLOGIN `mdgsm_identity_bootstrap_fn_owner` and executable only by the existing `mdgsm_bootstrap_executor`, after separate activation authorization. No third principal, generic dispatcher, operation/phase parameter, direct executor table/sequence privilege or implementation authorization is implied.

IdentityAccess owns challenge issuance, persistence, expiry, revocation, single-use consumption and Owner claim. Platform supplies stable InstallationId/SetupOperationId and coordinates setup only; it does not own or write challenge state.

## 2. Separation from privileged maintenance

Challenge issuance is an application-level Setup operation, distinct from database provisioning:

- The **maintenance operation** is explicitly invoked locally on the designated central Windows Server by the existing approved PostgreSQL cluster-superuser root. It provisions the two principals and installs/verifies the routine pair. It does not use a Named Pipe, Setup Service, Wizard, API, scheduled task or background service.
- The **Setup channel** is the authenticated local Windows Named Pipe between the Wizard and Setup Service. It is used to authenticate/attest the local operator SID and deliver the raw challenge once. It never carries the cluster-superuser credential and cannot perform DDL, role creation or routine installation.
- The Setup Service must not be treated as a DBA or maintenance actor. It may invoke the routines only through the separately gated executor connection, after the executor's secret custody, loopback-only PostgreSQL access and effective HBA rules are proven and LOGIN is separately authorized.

This distinction resolves the apparent conflict between the owner-approved local Named Pipe setup channel and the owner-approved no-Named-Pipe maintenance/provisioning boundary: they serve different trust purposes.

## 3. Challenge generation and one-time delivery

The Setup Service generates exactly 32 random bytes using the Windows OS CSPRNG. It computes SHA-256 over the 32 bytes. Raw challenge remains only in Setup Service memory until one delivery to the authenticated local Wizard over the Setup Named Pipe. It is never persisted, logged, placed in arguments/environment, telemetry, traces, crash artifacts, CI evidence or PostgreSQL.

Only the 32-byte digest is sent to the issuer routine and stored as `identity.bootstrap_challenges.verifier_digest`. The service fails closed on CSPRNG failure, clears mutable buffers where practical, and documents managed-memory zeroization limitations without claiming guaranteed process-memory erasure.

## 4. Issuer contract

The issuer's typed inputs are:

- stable InstallationId UUID;
- stable SetupOperationId UUID;
- OS-attested operator SID (not caller-supplied Wizard text);
- exactly 32-byte SHA-256 digest;
- challenge_request_id UUID as issuer idempotency key.

Purpose is a fixed routine-owned constant for InitialOwnerClaim. Lifetime is fixed by the routine and no greater than 30 minutes. PostgreSQL time governs created/expiry timestamps and expiry checks. The issuer returns only challenge_id and expiry, never the raw challenge or digest.

The Setup Service obtains the SID from the authenticated Named Pipe client token, impersonates only for identity inspection, reverts promptly, and re-attests the same SID for claim. It rejects any SID supplied in Wizard payload or API input.

In one transaction the issuer uses the same InstallationId-scoped bootstrap serialization primitive as claim; rejects an existing immutable Owner binding; classifies an identical challenge_request_id/input replay as idempotent; fails closed on conflicting reuse; conditionally revokes at most the prior unconsumed challenge for the same InstallationId + SetupOperationId + purpose by changing only `revoked_at_utc`; inserts the verifier-only challenge; and commits before the raw challenge is delivered. Unknown issue commit is reconciled with the same challenge_request_id before delivery.

## 5. Claim contract

The claim routine receives typed InstallationId, SetupOperationId, challenge_id, digest, operator SID and the approved final Owner account/profile/password-hash inputs. It validates all bindings, fixed purpose, expiry against PostgreSQL time, revocation and consumption state. It consumes the challenge and atomically writes exactly one initial IdentityAccess Account, immutable Product Owner binding, append-only operation receipt and secret-free IdentityAccess audit intent.

The final Owner username/password/profile are completed in the trusted local Wizard; the Owner is not forced to change credentials/profile after first login. The plaintext password travels only over the authenticated local Setup channel to IdentityAccess, where the approved password hasher creates the hash. PostgreSQL receives only the final hash and approved typed account fields; it never receives plaintext password or raw challenge. The Account is created with `must_change_password=false`. No role/permission seed, OWNER/SUPER_ADMIN assignment, session, Recovery data, Organization/workshop, FiscalPeriod or business record is created by this claim.

A timeout or unknown commit is reconciled read-only by the same SetupOperationId, challenge_request_id and exact payload fingerprint. It must never trigger a second Account or Owner binding.

## 6. Physical-state and ACL dependency

The protocol depends on the three IdentityAccess-owned candidate tables in physical schema/ACL proposal v0.3:

- `identity.bootstrap_challenges` for verifier-only issue/revoke/consume state;
- `identity.product_owner_bindings` for immutable InstallationId-to-original-AccountId binding;
- `identity.bootstrap_operations` for append-only idempotent claim receipt.

The executor has no direct table/sequence privileges. The shared function-owner has only the exact aggregate column-level privileges required by both fixed routines. The approved issue-specific delta is INSERT on exact challenge columns and conditional UPDATE of `revoked_at_utc` only; claim separately uses conditional `consumed_at_utc` transition. No DELETE/TRUNCATE or unrelated table access is allowed.

**Aggregate-ACL residual risk:** PostgreSQL ACLs are role-wide. Both routines share the same function-owner, so the issuer body technically runs under the aggregate grant set needed by claim. Per-signature EXECUTE does not provide per-function table isolation. The issuer/claim side-effect split must be enforced by immutable, fully reviewed, non-dynamic routine bodies, signed package and separate database definition fingerprints, static review and adversarial PostgreSQL 18 tests. Independent Security/Architecture review must explicitly accept this residual trust boundary before implementation. If per-function DB-enforced isolation is required, return to Product Owner decision; do not add a third role by inference.

## 7. Serialization and required tests

The issuer and claim must acquire the same deterministic InstallationId-scoped transaction lock before reading or changing challenge/binding/receipt state. Exact lock primitive, namespace, key derivation, collision analysis and fixed table-access order remain OPEN design items. A collision may serialize unrelated installations but must never allow two operations for one installation to bypass mutual exclusion. This local bootstrap lock is not a global cross-module lock-order approval.

PostgreSQL 18 disposable tests are required for concurrent issue, issue-vs-claim, concurrent claims, duplicate request IDs, identical and conflicting retries, expiry boundary, revoked/replayed/consumed challenge, wrong installation/operation/SID/purpose/digest, unknown commit, rollback, account/binding/receipt atomicity, no second claim and secret-sentinel leakage. All are **NOT RUN / NOT VERIFIED**.

## 8. Explicit non-authorization

This protocol is an owner-approved design constraint only. It does not authorize WG-0 migration reservation/application, SQL/DDL, role/schema/routine/grant creation, secret provisioning, executor LOGIN activation, Setup Wizard execution, Recovery, Organization, Inventory, G1 or production activation.

**END — v0.3**
