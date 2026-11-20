# MDG-ARCH-BASELINE-REGISTRATION-001
## Architecture Master Acceptance and Repository Registration Record — Version 1.0

| Field | Value |
|---|---|
| Document ID | MDG-ARCH-BASELINE-REGISTRATION-001 |
| Version | 1.0 |
| Decision / record date | 2026-11-21 |
| Owner | Product Owner — @Md-gsm |
| Status | **OWNER-DIRECTED CURRENT BASELINE — v3.0.3 SOLE AUTHORITATIVE MASTER** |
| Governing artifact | `docs/architecture/MDG-ARCH-MASTER-001_v3.0.3.txt` |
| Main registration | PR #71 merge commit `8f89653e25e6a599387128e0209f5009d9dd59cd`; artifact is present in `main` |
| Latest main at this record | `538eebd2dfa5644e97902cc8abaecbe976e6a046` (PR #82 merge) |
| Supersedes | Any repository/document-register statement that v2.5 is the current/last accepted baseline or v3.0.3 remains only a candidate |

## 1. Explicit owner direction

The Product Owner explicitly directs and confirms:

1. **MDG-ARCH-MASTER-001 v3.0.3 is the sole current and governing architecture/accounting Master for all current project work.**
2. **MDG-ARCH-MASTER-001 v2.5 is archived historical material only.** It is not the current baseline and must not be cited as the governing or last accepted baseline.
3. v2.6, v3.0, v3.0.1 and earlier versions are historical/superseded references only. They remain available for traceability and must not be deleted or silently rewritten.
4. All current module designs, workpapers, owner decisions, contracts, test specifications, registers, implementation planning and future changes must be reconciled against v3.0.3. Where a subordinate document has an older baseline reference, its content remains historical until a controlled successor or explicit alignment addendum is registered.
5. This decision changes the **document authority hierarchy only**. It does not imply that every design is complete, implemented, tested, evidenced, legally cleared, released or operational.

## 2. Acceptance and registration facts

- The v3.0.3 Master control page records explicit owner acceptance on 2026-11-20.
- The v3.0.3 text rendition is present in the repository at `docs/architecture/MDG-ARCH-MASTER-001_v3.0.3.txt`; it was added through PR #71, merged on 2026-11-20 at `8f89653e25e6a599387128e0209f5009d9dd59cd`.
- PR #72 was closed as superseded and was **not** merged. Its former “registration pending” statement is obsolete because the v3.0.3 artifact had already entered `main` through PR #71.
- PR #82 was merged on 2026-11-20 at `538eebd2dfa5644e97902cc8abaecbe976e6a046`; it registers the Inventory owner-decision and cross-module successor package. It does not replace or downgrade v3.0.3.
- The owner-supplied registration archive SHA-256 recorded in PR #82 is `8ffbaed52f3ae72dc5c4ad237217e059fc556453877b7bcd1f159134e89a5763`. Its internal 11-payload manifest was verified at the time of intake. The archive's original Markdown/DOCX/PDF hashes remain provenance identifiers for that supplied package; they are not hashes of the repository text rendition.

## 3. Supersession and correction of stale control text

This record is the current repository-level authority for baseline status and supersedes only the stale **status/registration assertions** in older registers and crosswalks, including:

- Document Register v1.8 statements that v3.0.3 is a candidate and v2.5 is last accepted;
- Open-Issue Register v1.3 statements with the same obsolete baseline status;
- CR-ARCH-INV-OWNER-FOLLOWUP-001 v0.2 statements that formal Master acceptance and registration remain pending;
- the v3.0 Master file's former candidate status, which is now historical;
- the v3.0.3 text rendition's item 3 “Repository Registration — PENDING”, which predates the PR #71 merge.

The v3.0.3 text rendition is preserved as a versioned artifact; this registration record corrects its time-dependent repository-registration statement without changing the accepted architecture requirements or the artifact's source content/hash.

## 4. Current authority hierarchy

1. Product Owner decisions and accepted accounting policies, within their explicit scope.
2. **MDG-ARCH-MASTER-001 v3.0.3 — sole current Master baseline.**
3. Accepted ADRs and owner decisions consistent with v3.0.3.
4. Current data-ownership registry, module contracts, ERDs, physical designs and controlled workpapers reconciled to v3.0.3.
5. Test specifications, implementation evidence, audit reports and release records, each with its own independent status.

Older documents remain historical records. A subordinate document does not become current merely because it mentions v3.0.3; its version, status, owner acceptance, implementation state and evidence must still be checked.

## 5. Independent status boundaries — unchanged

- Independent technical review of the exact current Master artifact/commit: **PENDING unless separately evidenced**.
- Module-level design review and cross-owner acceptance: remain item-specific.
- PostgreSQL runtime tests, migration tests, concurrency/race/replay/tie-out tests and restore tests: retain their actual recorded statuses; no NOT_RUN or NOT_VERIFIED item is promoted by this record.
- O-35, O-36 and O-64 remain OPEN pending their required implementation and evidence.
- FE-08 remains BLOCKED pending Finance/Accounting acceptance of the WAC pool key.
- D-15 policy decision remains CLOSED/APPROVED; its two numeric budgets remain NOT_SET and its operational cutover gate remains unsatisfied.
- Migration 0009 remains RESERVED. No SQL/DDL, EF mapping, migration, GRANT/REVOKE, role creation, Inventory activation, negative-stock feature activation, positive Posting Rule, Posting Release, production migration or Go-Live is authorized by this record.

## 6. Required repository reconciliation

The current controlled successors are:

| Artifact | Current successor |
|---|---|
| Architecture baseline authority | This record, v1.0, together with Master v3.0.3 |
| Finance Document Register | MDG-WP06-DOCUMENT-REGISTER-001 v1.9 |
| Canonical Open-Issue Register | MDG-OPEN-ISSUE-REGISTER-001 v1.4 |
| Inventory owner follow-up crosswalk | CR-ARCH-INV-OWNER-FOLLOWUP-001 v0.3 |
| Inventory policy index | OWN-INV-018 v1.1 |
| Prior Master v3.0 file | Retained as ARCHIVED / SUPERSEDED historical record |

This is a controlled documentation and authority correction only. No application code, database schema, migration or runtime behavior is changed.

## 7. Revision history

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-11-21 | Records the owner's explicit direction that v3.0.3 is the sole current Master and v2.5 is archived; reconciles repository registration through PR #71 and PR #82 history; preserves all independent implementation/test/release gates. |
