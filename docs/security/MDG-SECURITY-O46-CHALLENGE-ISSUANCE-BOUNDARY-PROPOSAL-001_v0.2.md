# O-46 — Challenge Issuance Boundary Reconciliation

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-CHALLENGE-ISSUANCE-BOUNDARY-PROPOSAL-001 |
| Version / Date | 0.2 / 2026-11-23 |
| Status | **SUPERSEDING RECONCILIATION — TWO-ROUTINE DESIGN OWNER-APPROVED; IMPLEMENTATION NOT AUTHORIZED** |
| Owner | IdentityAccess |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | MDG-SECURITY-O46-CHALLENGE-ISSUANCE-BOUNDARY-PROPOSAL-001 v0.1 |
| Controlling protocol | MDG-SECURITY-O46-CHALLENGE-ISSUANCE-PROTOCOL-PROPOSAL-001 v0.2 |
| Owner decision | PR #92 comment 7110645790 |

## 1. Purpose

This successor reconciles the earlier v0.1 challenge-issuance boundary proposal with the Product Owner's explicit approval recorded on 2026-11-23. The earlier proposal is retained as historical design evidence only. Where v0.1 differs from this successor or Challenge Issuance Protocol Proposal v0.2, the latter documents control.

## 2. Controlling owner-approved boundary

The Product Owner approved exactly two fixed-signature SECURITY DEFINER routines, both owned by the existing NOLOGIN `mdgsm_identity_bootstrap_fn_owner` and executable only by the existing `mdgsm_bootstrap_executor`:

1. `identity_bootstrap.issue_initial_owner_challenge_v1(...)`
2. `identity_bootstrap.claim_initial_product_owner_v1(...)`

No third PostgreSQL principal, persistent LOGIN, membership, direct executor table/sequence privilege, generic dispatcher, caller-selected operation mode, dynamic SQL or cluster-administrator runtime access is approved.

The function-owner privilege delta is restricted to the exact challenge INSERT columns and conditional UPDATE of `revoked_at_utc` for prior unconsumed challenge revocation, in addition to the previously proposed claim verification and conditional `consumed_at_utc` update. No broader table/column privilege is approved.

## 3. Current protocol semantics

- Setup Service generates exactly 32 random challenge bytes with the OS CSPRNG. Raw challenge stays in memory and is delivered once to the local Wizard over the authenticated Named Pipe. PostgreSQL receives and stores only the 32-byte SHA-256 digest.
- The issuer uses typed InstallationId, SetupOperationId, OS-attested operator SID, 32-byte digest and challenge_request_id inputs. Purpose and maximum 30-minute lifetime are fixed by the routine; PostgreSQL time governs timestamps and expiry.
- Same challenge_request_id plus identical immutable inputs is idempotent. Reuse with different inputs fails closed. A new request ID may supersede a prior unconsumed challenge for the same setup operation by changing only revoked_at_utc before inserting the new challenge.
- The claim routine re-attests/binds the same SID at the Setup Service boundary, validates digest and all bindings, then atomically consumes the challenge and writes the initial Account, immutable Owner binding, operation receipt and audit intent.
- Both routines acquire the same InstallationId-scoped bootstrap serialization lock first. Exact lock primitive/key derivation and PostgreSQL 18 proof remain open.
- The two routines share one function-owner role. PostgreSQL's table/column grants are therefore aggregate, not per-function. Fixed routine bodies and adversarial tests must prove issuer/claim side-effect separation. No third role is introduced to simulate per-function ACLs.

See Challenge Issuance Protocol Proposal v0.2 and Physical Schema/ACL Proposal v0.2 for the full typed contract, ACL, lock ordering and evidence requirements.

## 4. Explicitly unresolved

- exact complete SQL signatures and routine language/body;
- exact bootstrap guard/lock primitive, key derivation and lock collision policy;
- exact table/column constraints, indexes and RLS catalog state;
- Owner profile fields and requiredness;
- canonical payload fingerprint and any required external key custody;
- WG-0 reservation for migration 0010;
- executor credential custody, SCRAM/HBA/loopback and Windows service SID proof;
- PostgreSQL 18 and Windows integration evidence;
- independent Security/Architecture review.

## 5. Non-authorization

This reconciliation records an owner-approved design constraint only. It does not authorize SQL/DDL, migration reservation/application, principal or grant creation, secret provisioning, function installation, executor LOGIN activation, Setup Wizard execution, recovery, Organization, Inventory, G1 or production runtime activation. PR #92 remains Draft and unmerged.

**END — MDG-SECURITY-O46-CHALLENGE-ISSUANCE-BOUNDARY-PROPOSAL-001 v0.2**
