# Architecture Reconciliation — N.3 Session, Token, Device and Seats vs ADR-0013

| Field | Value |
|---|---|
| Document ID | MDG-ARCH-N3-ADR-0013-RECONCILIATION-001 |
| Version / Date | 0.3 / 2026-11-21 |
| Baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner decision source | ADR-0013, including the explicit owner session-policy correction dated 2026-11-07, as recorded in `docs/security/identityaccess-owner-decisions-fa.md` |
| Status | OWNER DECISION D RECORDED; O-25 ROLE MODEL RECONCILED; SYSTEM-MANAGER ROLE BINDING BLOCKED PENDING CONTROLLED MAPPING |
| Implementation authority | NONE |

## 1. Purpose and authority

This document resolves only the documented cross-reference conflict between v3.0.3 Appendix N.3 and the later owner session-policy correction recorded by ADR-0013. It does not silently rewrite the accepted v3.0.3 master, grant implementation authority, or close any security gate.

Where a later explicit owner decision supersedes an earlier N.3 rule, the later decision is authoritative. The owner explicitly resolved both System Manager limits on 2026-11-21: no cap on provisioned System Manager accounts, and at most one active System Manager session globally. These are separate constraints; the global session cap is not a per-account cap.

## 2. Controlled crosswalk

| v3.0.3 N.3 item | Later owner decision / evidence | Reconciliation disposition |
|---|---|---|
| Operational seats: maximum five users/computers at a time | Initial system cap is five distinct active accounts; multiple sessions for an already-active account do not consume another distinct-account slot. | REPLACE the ambiguous seat wording with **five distinct active accounts system-wide**. Enforce transactionally in PostgreSQL. |
| System Manager accounts: maximum two accounts | Explicit owner decision on 2026-11-21: **D — no dedicated cap on provisioned System Manager accounts**. | **SUPERSEDED.** No separate maximum number of System Manager accounts is imposed by N.3. All accounts remain subject to the general account-provisioning authorization and audit controls. |
| Active System Manager sessions: maximum one | Explicit owner decision on 2026-11-21: **D — retain one active System Manager session globally**, across all System Manager accounts. | **RETAIN AS A ROLE-WIDE GLOBAL CAP.** At most one live System Manager session may exist across the system. This does not reintroduce a per-account session cap for ordinary accounts; serialize/lock the global manager-session slot transactionally in PostgreSQL. |
| Active session per account: maximum one | The later owner decision states that no per-account concurrent-session cap is approved. | **SUPERSEDED.** Do not implement the historical one-session-per-account limit. |
| Sixth Emergency Session | N.4 restricts emergency mode to an allow-listed technical/recovery path outside the commercial seat and prohibits commercial operations. No later decision in the reviewed record supersedes that boundary. | **RETAIN N.4 boundary.** It is not an ordinary sixth business seat and must not be used to bypass the five-account cap for commercial work. |
| Fixed session timeout | No fixed absolute TTL is approved. | **NO FIXED ABSOLUTE TTL.** |
| Session validity while program is open and heartbeat/connection is valid | Heartbeat target 30 seconds; lease 180 seconds; PostgreSQL is authoritative. | **CLARIFY** as an active session requiring enabled account, non-revoked session, unexpired 180-second lease and matching credential version; heartbeat target 30 seconds. |
| Token lifecycle unspecified | Stage 08 specifies opaque CSPRNG bearer token with at least 256 bits entropy, digest-only persistence, rotation/revocation and no raw-token logging. | Use Stage 08 token contract as the proposed technical acceptance baseline; implementation remains gated by O-45 and exact-SHA review. |

## 2.1 Reconciliation with owner decision O-25 — 2026-11-19

Master v3.0.3 §16.5 and the accepted O-25 supplement establish that the owner has one user account which simultaneously carries the conceptual roles `OWNER`, `GENERAL_MANAGER` and `SYSTEM_MANAGER`. The three titles do not create three separate owner identities. O-25 also states that `SYSTEM_MANAGER` by itself does not confer commercial or financial authority; role/permission assignments and domain approvals remain separate.

That policy is distinct from the older default role matrix and must be preserved when N.3 is implemented:
- `GENERAL_MANAGER` is the operational manager concept. It must not be conflated with `SYSTEM_MANAGER`.
- Owner option D (2026-11-21) removes the N.3-specific numeric cap on provisioned System Manager accounts and retains at most one active System Manager session globally.
- The owner account's three-role combination under O-25 remains a single account identity; additional System Manager accounts, if provisioned under the later option D, are still subject to the same single global active-session slot.

## 2.2 Source-to-policy role mapping gap — blocks implementation

Current IdentityAccess source does not yet expose a canonical role key named `SYSTEM_MANAGER`, `OWNER` or `GENERAL_MANAGER`:
- `RoleCatalog` currently defines `super_admin`, `manager` and eight operational roles; no dedicated `system_manager` role key is present.
- `PermissionCatalog` grants `super_admin` the full initial permission set, while `manager` receives ordinary operational permissions and is explicitly denied identity administration and `system.settings.manage`.
- Neither the accepted O-25 text nor the current code explicitly declares `super_admin == SYSTEM_MANAGER`. Treating them as equivalent by inference is unsafe: O-25 says SYSTEM_MANAGER alone does not grant commercial/financial authority, whereas the current `super_admin` permission template includes commercial/financial operations.
- No OWNER/GENERAL_MANAGER/SYSTEM_MANAGER multi-role owner-account seed or mapping implementation is present in the reviewed IdentityAccess source.

Therefore the **global one-active-System-Manager-session invariant is not implemented**. The existing login handler enforces the five-distinct-active-account limit and has no System Manager role/session-slot check. Do not attach the N.3 guard to `manager` or `super_admin` until the role/permission mapping is explicitly versioned and reviewed.

Required controlled follow-up before implementation:
1. Register the canonical server-side identity for `SYSTEM_MANAGER` in a versioned IdentityAccess role/permission contract, including its relationship to `OWNER`, `GENERAL_MANAGER` and the current `super_admin` role.
2. Ensure SYSTEM_MANAGER alone does not accidentally inherit operational/commercial permissions; preserve O-25's separation of title, permissions and domain authority.
3. Define owner/deputy account provisioning and explicit additional-System-Manager assignment/revocation authority, all audited.
4. Implement the global claim/release inside the existing authoritative PostgreSQL transaction/lock boundary (or a separately reserved schema object if needed); second concurrent claim must be denied across API instances without evicting the first session.
5. Test two distinct System Manager accounts, concurrent API-instance login, ordinary-account multiple sessions, global slot reuse after logout/revocation/lease expiry, owner account multi-role behavior, and account disable/password reset.

Until these five steps are approved and tested, N.3 policy D is recorded but its global-session enforcement remains OPEN. This addendum does not authorize production session-policy activation.
## 3. Proposed replacement text for N.3

The following text is the owner-confirmed reconciliation for the next controlled architecture-master revision. The accepted v3.0.3 master remains unchanged until a separately controlled revision is produced and accepted:

- **System-wide active-account capacity:** initially five distinct active accounts. Multiple sessions belonging to an account already counted as active do not consume additional distinct-account slots. Capacity is enforced transactionally in PostgreSQL across API instances. A full system rejects a new distinct account; it does not silently evict an existing account.
- **Session lifetime:** no fixed absolute TTL is approved. A session remains valid only while the program maintains its liveness contract and the authoritative server-side state remains valid.
- **Liveness:** heartbeat target 30 seconds; lease 180 seconds from the last accepted heartbeat. PostgreSQL time is authoritative.
- **Session validity:** account enabled, session not revoked, lease not expired, and credential version at issue equal to the account's current credential version.
- **Per-account concurrent sessions:** no approved cap. The historical one-session-per-account restriction is superseded.
- **Revocation:** logout revokes the current session. Password change/reset and account disablement revoke all sessions for that account atomically; the next protected request must fail after authoritative state is re-read.
- **Emergency mode:** remains limited to the N.4 allow-list and cannot perform commercial or posting operations or bypass commercial seat capacity.
- **System Manager account count:** no N.3-specific cap on the number of provisioned System Manager accounts.
- **System Manager sessions:** at most one active System Manager session globally across all such accounts. The session claim/release must be atomic and PostgreSQL-authoritative across API instances; an occupied slot rejects a second manager session without silently revoking the first.
- **Role binding:** the implementation must use the canonical, owner-approved System Manager role/permission identity. It must not infer that every ordinary `manager` role is a System Manager unless the role catalog explicitly defines that equivalence.

## 4. Required acceptance evidence

The following are acceptance specifications, NOT RUN:

1. **N3-T01 — Capacity:** concurrent API instances cannot exceed the configured number of five distinct active accounts.
2. **N3-T02 — Same-account sessions:** additional sessions for an already-active account do not consume another distinct-account slot, subject to the unresolved System Manager-session policy.
3. **N3-T03 — Lease boundary:** 30-second heartbeat and 180-second lease behave correctly at before/equal/after expiry boundaries using PostgreSQL time.
4. **N3-T04 — Revocation:** logout, password change/reset and account disablement invalidate the required sessions on the next protected request.
5. **N3-T05 — No absolute TTL:** a continuously valid session is not expired solely due to elapsed absolute time.
6. **N3-T06 — Emergency boundary:** emergency mode cannot issue commercial, financial posting or inventory movement commands and cannot bypass commercial seat capacity.
7. **N3-T07 — Manager limits:** prove that account provisioning has no N.3-specific numeric cap, while all System Manager accounts collectively have no more than one active session; a second session is denied atomically across concurrent API instances, and the slot is reusable after authoritative logout/revocation/lease expiry.

## 5. Dependencies and non-authorization

- O-45 remains OPEN until token rotation, stale-session cleanup, crash/reconnect and revocation lifecycle are implemented and threat-model tested.
- O-20 remains OPEN until the actual canonical role/grant/RowScope matrix and real-credential denied-command proof are accepted.
- O-21 remains OPEN until the two-writer Hash Chain, rollback/crash/replay, external checkpoint and key custody/recovery proof are accepted.
- O-46 remains OPEN until bootstrap and recovery capture-then-invalidate, runtime controls and secret/logging proof are accepted.
- SEC-T-001..036 and the N.3-specific tests above are not presumed run or passed by this document.
- No code, SQL, DDL, migration, grant, production credential, feature activation, listener, recovery activation, G1 activation, Posting Release or Go-Live is authorized by this document.

## 6. Owner decision record — 2026-11-21

مالک گزینه **D** را صریحاً تأیید کرد:

- **تعداد حساب‌های مدیر سیستم:** بدون سقف اختصاصی N.3.
- **نشست مدیر سیستم:** حداکثر یک نشست فعال در کل سامانه، مشترک بین تمام حساب‌های مدیر سیستم.
- **سقف عمومی نشست به‌ازای حساب:** همچنان وجود ندارد؛ محدودیت مدیر سیستم یک قید سراسری وابسته به نقش است و نباید به همه حساب‌ها تعمیم داده شود.

این تصمیم فقط سیاست را تعیین می‌کند. پیاده‌سازی آن به تعریف دقیق role identity، تراکنش اتمیک PostgreSQL، تست رقابت چند API instance، بازبینی مستقل و مجوز مرحله‌ای جداگانه نیاز دارد.

**END — MDG-ARCH-N3-ADR-0013-RECONCILIATION-001 v0.3**


## Current Architecture Master authority — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. Any v3.0.3 references retained above identify the earlier source snapshot or the baseline used when this artifact was authored; they do not define current authority. Current decisions must conform to v3.0.4. This notice does not alter historical execution evidence or imply implementation, test PASS, independent acceptance, migration authorization or release.
