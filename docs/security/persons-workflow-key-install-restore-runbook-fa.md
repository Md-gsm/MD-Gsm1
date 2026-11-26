# Persons Temporary Workflow Key: Installation and Restore Runbook

**Document ID:** MDG-OPS-PERSONS-WORKFLOW-KEY-001  
**Version:** 0.1  
**Status:** Operational procedure for independent review; no production key provisioned by this document  
**Applies to:** MDGsmApi service and Persons temporary-workflow AES-256-GCM key only

## 1. Approved installation boundary

The approved installer provisions one random 256-bit key per installation. The key is protected with Windows DPAPI LocalMachine and stored only as a protected blob at:

`%ProgramData%\MD-Gsm\Keys\Persons\MDGsmApi\workflow-key.dpapi`

Application binaries remain under `%ProgramFiles%\MD-Gsm\`; ProgramData is not an application installation directory.

The final service identity is `NT AUTHORITY\LocalService` with service SID `NT SERVICE\MDGsmApi`. The service SID is the only runtime reader. LocalSystem is the maintenance principal. The interactive Setup identity, Administrators group, LocalService group, ordinary users, Authenticated Users, Everyone and network clients must not receive key-file read access.

The Setup path must create/configure the service and enable its service SID before one-time key provisioning. It must provision for the service SID explicitly, validate the resulting ACL and start the service. Setup must not read raw key bytes. API startup must validate key access and DPAPI unprotection under the actual service token.

## 2. First installation / provisioning

1. Run the signed/approved system-wide installer elevated on the intended Windows server machine.
2. Confirm the application path is under `C:\Program Files\MD-Gsm\`.
3. Confirm the key path is under `C:\ProgramData\MD-Gsm\Keys\Persons\MDGsmApi\`.
4. Confirm service name `MDGsmApi`, service account `NT AUTHORITY\LocalService`, and service SID type `unrestricted`.
5. Confirm the installer reports one-time provisioning only on a clean installation; the API starts and validates DPAPI unprotection as the service SID.
6. Confirm the key-provisioned marker is present, and verify DACLs with the approved Windows service installer test.
7. Do not copy, display, export, log, or include the raw key in any backup, artifact, issue, or support ticket.

A missing key on an already provisioned installation is a failure, not a request to create a replacement. Repair must verify and preserve the existing key. It must not silently overwrite, rotate, or regenerate it.

## 3. Repair, uninstall and reinstall

- Repair must preserve the protected key bytes and verify the existing DACL.
- Uninstall removes the service and application files under Program Files but preserves the ProgramData key directory.
- Reinstall on the same Windows installation must reuse the preserved key and must not run provisioning again.
- If the key file exists but has invalid ACL, malformed contents, or cannot be unprotected, stop and investigate. Do not rewrite it.

## 4. Backup and restore boundary

### Supported same-machine recovery

A backup may preserve the DPAPI-protected blob as opaque data only when the backup system is explicitly approved for protected application data. The blob is not a plaintext key export. Restore is supported only to the same Windows installation with its LocalMachine DPAPI protection context intact:

1. Stop MDGsmApi.
2. Restore the exact protected blob and its approved directory/file ACLs to the original ProgramData path.
3. Do not restore or change the service identity.
4. Start MDGsmApi and verify startup key validation and liveness.
5. Verify an existing encrypted workflow can be decrypted only through the service; do not expose plaintext in diagnostics.
6. Record the restore evidence without recording key bytes.

A Windows reinstall, different host, different DPAPI LocalMachine context, or missing machine DPAPI state is not a supported restore target under the current decision.

### Unsupported cross-machine / lost-key recovery

Cross-machine DPAPI transfer, raw key export, and plaintext key backup are not authorized. If the machine or DPAPI context is lost, or the protected key is irrecoverable:

1. Keep the affected ciphertext and database evidence intact; do not delete or overwrite them automatically.
2. Keep Promotion disabled and fail closed.
3. Record only non-secret failure codes and affected workflow identifiers in restricted operational records.
4. Escalate to the Product Owner for a separately approved key-recovery/escrow decision before attempting any recovery or data-discard action.

Creating a replacement key does not recover old ciphertext. No automatic re-key, ciphertext purge, or silent loss of temporary workflow data is permitted.

## 5. CI / installer verification evidence required

The Windows installer integration gate must prove, on a disposable clean Windows runner:

- one-time provisioning generates a key and marker;
- the service SID can read and DPAPI-unprotect it;
- the non-service test identity cannot read the key file;
- repair and reinstall preserve the existing key without reprovisioning;
- uninstall removes Program Files/service but preserves ProgramData key material;
- missing, corrupt, wrong-version or ACL-denied key fails closed;
- no key bytes appear in logs or artifacts.

CI evidence proves the installer behavior on the tested runner only. It does not provision a customer installation, authorize production deployment, or enable Promotion.

## 6. Release and review gates

This runbook and the corresponding implementation require independent security review of the service SID, ACL, DPAPI, provisioning, repair/uninstall, same-machine restore and unsupported cross-machine recovery boundary. Promotion remains disabled until its separate AC-37 acceptance, independent review and owner release gates are satisfied. No production key or database migration is provisioned/applied by this procedure.
