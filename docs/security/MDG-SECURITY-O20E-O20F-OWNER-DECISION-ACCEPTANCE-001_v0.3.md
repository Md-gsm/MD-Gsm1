# O20-E / O20-F — Owner Decision Acceptance and Reconciliation Record

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 |
| Version / Date | 0.3 / 2026-11-23 |
| Supersedes | v0.2 only for OD-04 Emergency Access activation policy |
| Status | OWNER POLICY DECISIONS ACCEPTED; TECHNICAL IMPLEMENTATION / G1 / PRODUCTION ACCESS NOT AUTHORIZED |
| Governing owner decision | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6 |
| Owner decision record | PR #92 comment 7094120301 |
| PR | #92 Draft / unmerged |

## 1. Purpose and precedence

This successor preserves OD-01, OD-02, OD-03 and OD-05 as already accepted in v0.2. It amends OD-04 Emergency Access only. No historic version is rewritten. Where v0.2 says every Emergency Access activation needs two approvers or has a 30-minute maximum, this v0.3 amendment controls.

## 2. OD-04 — Emergency Access activation (AMENDED / OWNER-APPROVED)

- Every request identifies requester, incident, reason, exact operation, resource and scope.
- Ordinary allow-listed Emergency Access activation requires one approval from the Product Owner or an authorized independent approver.
- The approver must be distinct from both requester and emergency operator. A System Manager cannot grant Emergency Access to itself. The Product Owner's narrow Organization Branch/Warehouse assignment exception does not apply to Emergency Access self-approval.
- One ordinary Emergency Access grant lasts no more than **60 minutes** measured using trusted database/server UTC. A shorter term may be requested. Extension requires a new reason and fresh approval.
- At `now >= expires_at`, authorization is denied synchronously, even if the session is open. Cleanup is housekeeping only.
- The allow-list is limited to locked-account unlock, invalid-session revocation, credential reset with independent controls, and isolated Backup restore with two-person approval.
- Sales, purchases, inventory mutation, Journal/Posting, Product Owner binding mutation, WORM retention change, Audit/Movement Ledger deletion and permanent administrative elevation are denied.
- Emergency actions are authenticated, incident-bound, reasoned, exactly scoped, time-limited and fully audited. At termination emergency token/credential is revoked and the session is closed.
- **Isolated Backup restore retains two distinct independent approvers.** The previously accepted O-31 offline two-part recovery package for emergency recovery when both System Managers are unavailable remains unchanged and requires both trusted persons' presence/cooperation.

This is one independent approval for ordinary Emergency Access, not a second-person approval requirement for every ordinary activation. The two-person condition is retained specifically for isolated Backup restore and the separate O-31 recovery package.

## 3. Other accepted decisions carried forward

- OD-01: accepted BranchCode/WarehouseCode canonicalization policy remains unchanged.
- OD-02: Warehouse assignment remains attached to immutable WarehouseId across reassignment; no implicit grant/revocation or Branch assignment inference.
- OD-03: ARCHIVED is terminal; no physical deletion.
- OD-05: Organization assignment/relationship history is retained by default; no automatic disposal; any future disposal requires an applicable approved schedule, no active Legal Hold and documented authorized approval. The duration/legal classification and operational Hold/disposal mechanism remain subject to the newer Product Owner policy in decision packet v0.6 and the current records-retention successor.

## 4. Required technical verification

- trusted IdentityAccess resolution for requester, approver, Product Owner and emergency operator;
- proof of distinct requester/approver/operator identities and trusted approver eligibility;
- positive/negative allow-list tests for each exact operation and resource;
- 60-minute boundary acceptance and 3601-second rejection;
- synchronous expiry and revocation while sessions remain open;
- two independent approvals for isolated Backup restore and O-31 two-part recovery;
- atomic local audit-intent persistence and canonical AuditSecurity durable ACK, retry/quarantine and operational reconciliation;
- exact-head Linux/Windows PostgreSQL tests, module-owner acceptance and independent Security review.

## 5. Non-authorization and disposition

This record is owner policy only. It does not authorize an emergency LOGIN, credential, secret, role, membership, database/API capability, production grant, route, migration, Backup restore, production Audit delivery or G1.

**OD-01 / OD-02 / OD-03 / OD-05 remain OWNER-ACCEPTED. OD-04 is AMENDED and OWNER-APPROVED as stated above. O20-E remains OPEN. O20-F remains OPEN pending trusted production identity/capability/audit evidence. Overall O-20 remains OPEN. Organization G1 NOT GRANTED.**

**END — MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.3**
