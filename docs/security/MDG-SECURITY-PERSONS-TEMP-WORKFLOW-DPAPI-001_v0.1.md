# Persons Temporary Workflow Payload Key Custody
**Document ID:** MDG-SECURITY-PERSONS-TEMP-WORKFLOW-DPAPI-001  
**Version:** 0.1  
**Status:** Owner-approved design; implementation and operational verification pending  
**Approval:** Product Owner approval recorded in Issue #100 on 2026-11-25

## 1. Scope and purpose
This decision applies only to the AES-256-GCM encryption key used for sensitive temporary Persons workflow payloads before promotion. It does not define custody for IdentityAccess credentials, owner recovery, AuditSecurity/Hash Chain signing, database encryption, or any other secret.

## 2. Approved custody model
- Use Windows DPAPI with `DataProtectionScope.LocalMachine`.
- Generate one independent cryptographically random 256-bit key per installation using the Windows cryptographic random-number generator.
- Protect the key with DPAPI before persistence. Persist only the DPAPI-protected blob and a non-secret format header; never persist raw key bytes in PostgreSQL, application configuration, source control, logs, installer assets, or ordinary backup manifests.
- Store the protected key below `%ProgramData%\MD-Gsm\Keys\Persons\`, in an installation-specific directory.
- Apply a protected Windows DACL to the key directory and key file. The intended API service identity receives only the access needed to read the protected key at runtime; LocalSystem retains administrative recovery/maintenance access. Ordinary interactive users and network clients receive no access.
- Provisioning is explicit and one-time. Runtime key lookup never creates, replaces, or silently rotates a missing key.
- The key purpose is isolated from all other application keys. Owner recovery codes, email OTPs, IdentityAccess secrets, and AuditSecurity signing keys must never be reused.

## 3. Cryptographic contract
- Algorithm: AES-256-GCM for payload protection.
- Key size: exactly 32 bytes.
- DPAPI scope: LocalMachine.
- DPAPI optional entropy: fixed, purpose-specific application context string; this is domain separation, not a substitute for key secrecy.
- Key version: `dpapi-localmachine-v1`.
- Ciphertext format remains nonce (12 bytes) || authentication tag (16 bytes) || ciphertext.
- Associated data remains bound to the workflow record identifier and key version.
- Decrypted key buffers are caller-owned byte arrays and must be zeroed after use. Plaintext buffers must be zeroed after parsing/use, including failure paths.

## 4. Installation identity and ACL boundary
The final API service identity must be selected by the installer/setup design before production provisioning. Provisioning must execute under that same identity, or the implementation must explicitly provision its SID as the runtime reader. This version's provisioning method binds the current Windows SID; it must therefore not be invoked under an elevated setup account that differs from the final API identity.

DPAPI LocalMachine protects the key blob at rest from ordinary offline file disclosure, but it does not protect against a local administrator, SYSTEM-level compromise, or malware executing as the API identity. This is an explicit trust boundary, not a claim of administrator-proof isolation.

## 5. Lifecycle, rotation, backup and loss
- No key rotation policy may be inferred from this decision. A versioned rotation procedure must preserve decryptability of every still-valid workflow before retiring an old key.
- Retain a prior key only while valid ciphertext still depends on it; deletion requires evidence that no such workflow remains.
- No cross-machine DPAPI key transfer or restoration is supported by this decision. A restore to another Windows installation must fail closed until a separately reviewed recovery design exists.
- Loss, corruption, ACL denial, DPAPI failure, or unknown key version makes the affected encrypted workflow unavailable. Do not generate a replacement key automatically and do not silently discard ciphertext.
- No raw key export or plaintext key backup is authorized.

## 6. Runtime activation gate
The infrastructure provider may be registered only on Windows when an explicit key path is configured and the protected key file already exists. Promotion remains additionally gated by the existing `Persons:WorkflowPromotionApiEnabled` feature flag and its existing authorization checks. This decision does not enable that flag, provision an installation key, choose the final service identity, or authorize production activation.

## 7. Required verification before AC-37 acceptance
1. Build and test on the supported Windows .NET 10 runner.
2. Verify effective directory and file DACLs under the final API service identity; confirm ordinary users and network clients cannot read the protected key file.
3. Verify separate installations generate independent keys.
4. Verify wrong key version, missing file, malformed blob, DPAPI failure, and tampering all fail closed.
5. Verify provisioning refuses overwrite and no runtime code silently provisions or rotates.
6. Verify AES-GCM key buffers and plaintext buffers are zeroed on success and failure paths; run secret-sentinel/log-redaction scans.
7. Run the full disposable PostgreSQL 18 promotion/migration integration suite against the exact same commit.
8. Wire explicit provisioning into the trusted initial Setup flow under the final API identity and test reinstall/repair behavior.
9. Complete independent security review and owner release gate.

## 8. Explicit non-decisions
This document does not select or create the Windows API service account, define a password for it, define a backup/recovery mechanism, set a retention TTL, authorize database migration execution, activate Promotion, or grant Persons G1. Those remain separate gates.
