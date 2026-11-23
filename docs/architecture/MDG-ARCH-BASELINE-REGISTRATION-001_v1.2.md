# MDG-ARCH-BASELINE-REGISTRATION-001
## Architecture Master Acceptance and Repository Registration Record — Version 1.2

| Field | Value |
|---|---|
| Document ID | MDG-ARCH-BASELINE-REGISTRATION-001 |
| Version | 1.2 |
| Decision / record date | 2026-11-24 |
| Owner | Product Owner — @Md-gsm |
| Status | **OWNER-ACCEPTED OFFICIAL MASTER — v3.0.3 SOLE GOVERNING BASELINE** |
| Governing artifact | `docs/architecture/MDG-ARCH-MASTER-001_v3.0.3.txt` |
| Main registration | PR #71 merge commit `8f89653e25e6a599387128e0209f5009d9dd59cd`; artifact is present in `main` |
| Supersedes | v1.1's v3.0.4 Master-successor designation and any contradictory current-status statement; v1.0 remains the original registration record |

## 1. Current explicit owner direction

On 2026-11-24, the Product Owner expressly reaffirmed and directed that **MDG-ARCH-MASTER-001 v3.0.3 is the sole accepted, official and governing architecture/accounting Master** for all current MD-Gsm project work.

1. v3.0.3 is the only current Master baseline. All current module designs, contracts, workpapers, owner decisions, test specifications, registers, implementation planning and reviews must be reconciled against it.
2. v2.5, v2.6, v3.0, v3.0.1 and earlier versions are historical references only. They must not be described as the current or last accepted baseline.
3. v3.0.4 is **not** the current or official Master and does not supersede v3.0.3. The v3.0.4 file is retained as a historical working derivative for provenance and traceability.
4. Owner decisions separately recorded under CHG-101 remain valid only within their expressly accepted scope. Those bounded decisions do not constitute acceptance of v3.0.4 as a Master successor and do not amend unrelated Master requirements. Incorporation into the Master requires a separately controlled successor version and explicit acceptance.
5. Historical snapshots and old registers are preserved. Their former status statements remain historical evidence only and do not override this current registration record.

## 2. Main-branch registration facts

- The governing artifact is `docs/architecture/MDG-ARCH-MASTER-001_v3.0.3.txt`.
- It is present in `main`, registered through PR #71 merge commit `8f89653e25e6a599387128e0209f5009d9dd59cd`.
- The former statement inside the v3.0.3 text rendition that repository registration was pending on PR #72 is superseded by the actual PR #71 merge fact and is corrected in the current Master control text.
- Registration record v1.0 remains the historical record of the original 2026-11-21 registration. Registration record v1.1 is superseded as the current authority because it described v3.0.4 as an accepted Master successor. This v1.2 record controls current Master authority.

## 3. Repository-wide reconciliation rules

- Current and active documents must identify v3.0.3 as the governing Master.
- No active register, PR description, document index or control block may state that v3.0.4 is the governing/accepted Master successor or that v2.5 is the last accepted baseline.
- v3.0.4 and v1.1 are retained as historical working records; they are not deleted or silently rewritten as though the earlier record never existed.
- Older versioned snapshots that contain the former candidate/v2.5 status remain unchanged for traceability. They are historical snapshots, not current authority.
- Subordinate documents may retain their own independent versions and status. Mentioning v3.0.3 does not by itself mean that a subordinate design is accepted, implemented, tested or released.

## 4. Independent status boundaries — unchanged

Owner acceptance and official Master status do not imply independent technical review, design completion, implementation, test PASS, evidence acceptance, legal clearance, Gate C or Gate D PASS, Posting Release, production migration or Go-Live. Each retains its own criteria and status. No NOT_RUN or NOT_VERIFIED item is promoted by this record.

## 5. Explicit non-authorization

This is a document-authority and repository-control decision only. It does not authorize SQL/DDL, schema or role creation, GRANT/REVOKE, credential creation, migrations, production database changes, module activation, Posting Release, production migration or Go-Live.

## 6. Revision history

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-11-21 | Records v3.0.3 as the owner-accepted Master and its registration through PR #71. |
| 1.1 | 2026-11-23 | Historical record of the bounded v3.0.4 successor direction in PR #92; now superseded as current Master authority. |
| 1.2 | 2026-11-24 | Reaffirms v3.0.3 as the sole official accepted Master, records completed main registration, and classifies v3.0.4 as a historical working derivative only. |

**END — MDG-ARCH-BASELINE-REGISTRATION-001 v1.2**
