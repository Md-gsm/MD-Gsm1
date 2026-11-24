# O-46 — Restart-Safe Idempotency and Reconciliation Decision Addendum

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-RESTART-SAFE-IDEMPOTENCY-DECISION-ADDENDUM-001 |
| Version / Date | 0.2 / 2026-11-24 |
| Status | **OWNER-APPROVED ARCHITECTURE TARGET WITH EXPLICIT OPEN SECURITY/PHYSICAL GATES — DESIGN ONLY; NO IMPLEMENTATION AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | Restart-Safe Idempotency Design Proposal v0.1 for the decisions and corrections below |

## 1. Controlling owner decisions

The Product Owner has approved the following as the target architecture:

- **JRN-SEC-01:** Windows CNG with a TPM-backed key through Microsoft Platform Crypto Provider is the preferred cryptographic protection architecture for the O-46 local Bootstrap Operation Journal. DPAPI is not the primary Journal root of trust; it may be used only for separately approved ancillary local-secret protection.
- **JRN-REC-01:** Reconciliation is to use a dedicated read-only PostgreSQL View, a logically separate `JOURNAL_RECONCILIATION_READER` identity, and Public-Key verification. It must not create a third SECURITY DEFINER routine and must not expand `mdgsm_bootstrap_executor` privileges.
- Initial algorithm target: ECDSA P-256 / SHA-256 for checkpoint signatures, conditional on a real TPM/provider PoC. No silent algorithm or provider fallback is allowed.
- Private key material is not copied for ordinary backup/recovery. Public keys and key identifiers are retained so historical signatures remain verifiable. TPM replacement/loss requires a newly enrolled key, explicit owner approval and a separately trusted key-registry/anchor update.

These are architecture-target approvals, not implementation authorization. The CNG provider, exact key usages, encryption envelope, reader database identity, View owner/ACL, serialization proof, external-anchor mechanism and recovery procedure remain subject to the gates below.

## 2. Critical cryptographic distinction: signing is not encryption

The approved ECDSA signing key provides authenticity/integrity evidence for a checkpoint. **It does not encrypt the Journal and cannot protect confidentiality of the stored encoded password hash.** A signature, hash chain or public key does not make the Journal contents confidential.

The O-46 Journal contains the exact encoded IdentityAccess password hash and fingerprint needed to preserve claim idempotency. The encoded password hash is credential-verifier material and must be confidential at rest as well as integrity-protected.

Therefore the target has two distinct cryptographic functions:

1. **Confidentiality:** encrypt the Journal payload with an authenticated-encryption scheme, with a random per-journal or per-record data-encryption key (DEK). AES-256-GCM through the Windows CNG BCrypt interface is the initial candidate. Protect/wrap the DEK with a separate TPM-backed CNG key whose approved provider supports the selected wrapping/key-agreement operation.
2. **Integrity/authenticity:** hash-chain canonical Journal records and sign versioned checkpoints with a separate TPM-backed asymmetric signing key, initially ECDSA P-256 / SHA-256.

The signing key must not be misused as an encryption key. The TPM-backed wrapping/key-agreement key and signing key have distinct key identifiers, usages, ACLs and rotation lifecycles. The exact key-wrapping construction and TPM provider capability must be proven in PoC. If the selected Microsoft Platform Crypto Provider/TPM does not support the exact required operation, stop and submit an explicit ADR/owner decision; do not silently fall back to DPAPI, a software KSP, exportable key material or another algorithm.

The local service identity receives only the minimum CNG key-use rights. TPM non-exportability protects against ordinary key extraction; it does not by itself prevent a sufficiently privileged local administrator or compromised service process from asking the TPM to use the key. The Windows threat model must explicitly state this limit and prove service-SID ACLs, process isolation and key-use auditing.

## 3. Journal scope correction

In this O-46 context, **Journal means the local Bootstrap Operation Journal** that preserves the exact Owner-claim payload across Setup Service restart. It is not the Accounting/General-Ledger journal.

Accordingly, Accounting fields such as Debit, Credit, CurrencyCode, SourceDocumentReference and PostingStatus are outside this design and must not be added to the O-46 View or local Journal merely because the word “Journal” is shared.

The protected bootstrap record may contain only the minimum operation data required for exact retry and reconciliation, including:
- format/version and monotonic local record sequence;
- InstallationId, SetupOperationId and AccountId;
- exact validated username-normalized value, display_name and email;
- password-hash version and exact encoded password-hash output;
- fingerprint version and exact 32-byte fingerprint;
- key identifiers, record/checkpoint hashes, signature and non-secret state metadata.

It must never contain plaintext password, raw challenge, challenge verifier digest, cluster-superuser credential, executor secret, private key or database connection string. The Journal is recovery material, not the authoritative source of whether the Owner claim committed.

## 4. Hash chain, checkpoint and external-anchor limits

The Journal may use canonical, versioned record serialization, a SHA-256 hash chain and signed checkpoints. Each checkpoint records the chain head, sequence range, format version, signing KeyId and signature. Verification must pin the expected public key through a separately trusted registry.

A hash chain and signature detect unauthorized modification only relative to a trusted chain head/public key. They do **not** independently prevent deletion, truncation, replay of an older valid Journal, rollback to an earlier signed checkpoint, or replacement of the public-key registry.

An append-only/WORM External Anchor can provide rollback/truncation evidence only if its trust, availability, retention, monotonic sequence semantics, recovery and write authorization are independently specified. No external WORM service or cloud dependency is currently approved or assumed. Until such an anchor exists, the Journal must not claim rollback-proof or non-repudiation guarantees. PostgreSQL's immutable binding and receipt remain authoritative for Owner-claim outcome.

Key rotation:
- keep all historical public keys and their KeyIds;
- never export/copy the old TPM private key as ordinary backup;
- create a new TPM-backed key after replacement/loss;
- require explicit Owner authorization and trusted registry/anchor enrollment of the new public-key fingerprint;
- verify old checkpoints using their original public keys;
- do not retroactively re-sign old checkpoints and present them as if signed by the old key.

A restored old Journal is always reconciled against current PostgreSQL state before any action. A valid old signature is not proof that its state is current.

## 5. Read-only reconciliation contract

The target is a dedicated, non-updatable View over only the minimum IdentityAccess bootstrap state needed to classify one operation. Candidate output fields are limited to:
- InstallationId;
- SetupOperationId;
- AccountId;
- ChallengeId;
- payload_fingerprint_version and payload_fingerprint;
- completed_at_utc;
- the minimum challenge consumed/revoked/expiry metadata required to distinguish a terminal claim from an unconsumed/revoked challenge.

The View must not expose password hashes, plaintext or verifier secrets, raw challenges, email/display-name data, executor credentials or unrelated account attributes. It is not an Accounting ledger View.

The View must have a pinned, non-login owner with only the required underlying SELECT columns; the Reader has SELECT only on the View and no direct table/sequence privileges. The View is explicitly read-only: no INSERT/UPDATE/DELETE/TRUNCATE grants, no INSTEAD OF trigger, no mutating rule, and a definition that is not automatically updatable. Use `security_barrier` where appropriate to constrain predicate pushdown/leaky-function behavior. `security_barrier` is not a substitute for row authorization or RLS. The owner, RLS behavior, view security mode, installation scoping and effective privileges require PostgreSQL 18 catalog and adversarial proof.

### 5.1 Important principal-count conflict — unresolved

The previously owner-approved PostgreSQL boundary permits exactly two new principals:
- `mdgsm_identity_bootstrap_fn_owner` (NOLOGIN);
- `mdgsm_bootstrap_executor` (initially NOLOGIN).

The new logical identity `JOURNAL_RECONCILIATION_READER` is now approved as an architectural responsibility, but its database realization is **not yet reconciled** with the exact-two-principal rule. If it is a new PostgreSQL role/login, that is a third principal and explicitly supersedes the earlier count; it requires a precise owner decision and full ACL/credential/HBA review. If it is only a Windows service SID, it still needs a defined mapping to an already-approved database identity and a narrowly granted View SELECT path. Reusing the executor is not permitted because its privileges must not be expanded. No new PostgreSQL role, login, membership or grant is created by this addendum.

### 5.2 Negative-result linearization

A View SELECT by itself cannot prove that no claim transaction is still in flight or capable of committing. A normal MVCC snapshot can miss an uncommitted transaction. Therefore the reconciliation protocol must share the same InstallationId-scoped serialization primitive as the claim routine.

Proposed read sequence: begin a READ COMMITTED transaction; acquire a shared transaction-level lock for the exact same deterministic InstallationId lock key used exclusively by claim; after the shared lock is acquired, issue a separate SELECT from the reconciliation View; hold the lock until transaction end. This is data-read-only and creates no third SECURITY DEFINER routine, but it does acquire a transaction lock. It is valid only if:
- every claim path takes the matching exclusive transaction-level lock before any binding/receipt mutation;
- the key derivation, namespace and collision behavior are identical and reviewed;
- the shared lock cannot be bypassed by any alternative claim path;
- the SELECT runs after lock acquisition under READ COMMITTED with a fresh statement snapshot;
- PostgreSQL 18 concurrency/crash tests prove that a negative result means no earlier claim transaction remains capable of committing.

The exact lock primitive, role permission to call it, View ownership and Reader connection identity remain open. If this proof cannot be achieved without a third SECURITY DEFINER routine or executor privilege expansion, stop and return for an explicit Owner decision.

## 6. Reconciliation result rules

- **Committed match:** one immutable binding and one append-only receipt exist and all InstallationId, SetupOperationId, AccountId, ChallengeId and fingerprint values match the protected local Journal. Verify the relevant signed local checkpoint and historical public key. Treat as terminal; never claim again.
- **Committed conflict:** any mismatch, duplicate, partial/inconsistent receipt/binding, unexpected challenge state or signature failure. Fail closed; no repair, replacement Account or new operation.
- **Proven not committed:** the shared/exclusive serialization barrier has been acquired and the fresh View snapshot proves no receipt/binding exists and no earlier claim can still commit. Only then may the exact Journaled hash/fingerprint be retried or a separately approved abandonment procedure be used.
- **Unknown:** database unavailable, lock unavailable, stale/inconsistent View, untrusted key registry, Journal corruption, signature failure or inability to prove transaction quiescence. Preserve evidence and fail closed. Never infer rollback from timeout.

Public-Key verification establishes the authenticity of a local signed checkpoint relative to a trusted key registry; it does not replace authenticated PostgreSQL reads and does not by itself prove database freshness.

## 7. Required proof gates

Before implementation:
1. Windows 11/Server target PoC for Microsoft Platform Crypto Provider + TPM 2.0: key creation, non-exportability, service-SID key use, ECDSA P-256 signing/verification, exact DEK wrapping/unwrapping mechanism, AES-256-GCM, restart, update, repair, TPM reset/loss and replacement.
2. Explicitly separate signing and encryption key purposes; prove no software/exportable fallback.
3. Threat model for local admin, SYSTEM, service compromise, backup/restore, journal rollback/truncation, key-registry substitution and deletion limitations.
4. Public-key registry bootstrap trust, rotation enrollment and old-key retention.
5. PostgreSQL 18 proof for View owner/ACL/RLS/security barrier, non-updatability, column minimization, no base-table access, and no Executor privilege delta.
6. Owner resolution of the Reader identity's PostgreSQL principal-count conflict.
7. Shared-lock/fresh-snapshot linearization proof and crash/concurrency tests.
8. Cross-language fingerprint vectors and exact encoded-hash preservation tests.
9. Independent Security/Architecture review of one exact source/package/DB catalog SHA.

All gates are **NOT RUN / NOT VERIFIED**.

## 8. Explicit non-authorization

This addendum authorizes architecture-target documentation only. It does not authorize CNG key generation, TPM provisioning, Windows key ACLs, Journal creation, SQL/DDL, View creation, PostgreSQL role/grant/membership creation, migration reservation/application, Routine changes, Executor activation, Owner account/binding/challenge creation, Setup Wizard execution, Recovery, G1 or production activation.

**END — v0.2**


## 7. JRN-REC-01 Owner reconciliation — logical Reader and exact-two-principal rule (2026-11-24)

The Product Owner resolved the former Reader-identity conflict. Exactly two new PostgreSQL principals remain approved: `mdgsm_identity_bootstrap_fn_owner` and `mdgsm_bootstrap_executor`. `JOURNAL_RECONCILIATION_READER` is only a logical application-layer identity/capability; it is not a PostgreSQL role, LOGIN, membership or principal.

The dedicated read-only Reconciliation View may be owned only by a pre-existing controlled NOLOGIN role identified from the current PostgreSQL role inventory. SELECT may be performed only through a pre-existing execution identity whose already-effective Grant Matrix authorizes that View. No new role, LOGIN, grant, database connection identity or routine is implied. The Executor is not the reader and must not receive direct SELECT or any privilege expansion. If no pre-authorized SELECT path exists, the implementation gate remains open.

The logical Reader capability is enforced in the application and does not confer direct database access, routine execution, DML, private-key access or secret access. The View must expose only minimum reconciliation metadata and exclude password hashes, challenge verifier/raw challenge, PII and secrets. Its owner, RLS, installation scoping, non-updatability, effective SELECT route and base-table ACLs require PostgreSQL 18 proof.

The View reader and claim path must share the same InstallationId-scoped transaction-level serialization barrier; the reader obtains a fresh READ COMMITTED snapshot after acquiring the shared form, while claim uses the exclusive form. Lock derivation and all-path participation remain unproven and OPEN. No third routine is permitted.

This reconciliation supersedes any earlier wording in this addendum that left open creation of a third Reader principal or mapping a Windows SID to a new database role. It does not authorize View creation, grants, SQL, DDL, migration or activation.
