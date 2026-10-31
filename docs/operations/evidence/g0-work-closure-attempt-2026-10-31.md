# G0 technical closure record — 2026-10-31

**Repository:** `Md-gsm/MD-Gsm1`  
**PR:** [#8](https://github.com/Md-gsm/MD-Gsm1/pull/8)  
**Recorded state:** technical remediation committed; final CI on the latest HEAD and external governance evidence are still required. This record does not declare G0 fully closed and does not authorize G1.

## Completed in the current PR branch

1. Relocated the four shared technical projects into `src/BuildingBlocks/*` and the API/Desktop projects into `src/Host/*`; removed the former flat source roots.
2. Updated `MDGsm.sln`, project references, API composition references, the migration resource path, Desktop/preview/smoke scripts, and Windows preview CI path filters.
3. Updated the C# architecture tests and Python architecture gate to validate the exact modular project inventory, dependency graph, isolated Contracts projects, and absence of legacy flat roots.
4. Preserved four compiled, versioned Contracts assemblies for Persons, IdentityAccess, RepairOperations and Finance. They expose version markers only; they do not implement business use cases.
5. Recorded Project Owner acceptance of the temporary-data policy and merge-design checklist.
6. Assigned CI job ownership to the Group of Software Advisers, with membership appointed by the Project Owner for each stage. The Project Owner remains accountable for security/privacy.
7. Retained the negative architecture enforcement evidence: PR #6 deliberately introduced a forbidden Domain-to-Infrastructure reference; the architecture job failed and the PR was blocked/closed without merge.
8. Retained successful PostgreSQL 18.6 Linux/Windows and application-owned Migration Runner evidence (run 41181075233, 13/13 jobs). The migration runner applies only the infrastructure version-1 migration in a disposable database.
9. Pinned the Linux PostgreSQL service image to an immutable digest.

## Final technical validation still required

- GitHub Actions must finish green on the current PR HEAD after the physical relocation and all path changes. Earlier 13/13 success on commit `bfe482aff31e50811bfb2293a21c21b2eb11d150` does not validate subsequent relocation commits.
- The final architecture gate and C# build/tests must pass on the same current commit.
- The latest CI evidence artifact and manifest should reference that same tested SHA.

## External governance evidence still required

- The connected GitHub integration reports `main protected=true`, but the branch-protection details endpoint returned HTTP 403 and the Rulesets endpoint returned an empty list. Exact Required Status Checks, strict/up-to-date and bypass settings are not verified. There is no Branch Protection/Ruleset write action exposed in the connected integration; a repository Admin must inspect/export the actual settings.
- An independent architecture review by the Group of Software Advisers must be recorded against a specific commit. The group, not an invented individual name, is the review body.
- Security/privacy accountability belongs to the Project Owner only. The Project Owner's policy acceptance is recorded; a distinct security/privacy review record for the G1 entry package is still required from that owner.

## Items intentionally not implemented in G0

- AC-31..AC-45 remain `UNMAPPED`. The verification plan defines scenarios but no real Persons temporary-data runtime, party merge execution, or executable acceptance tests exist. Creating static/fake tests to mark them implemented would be misleading. Those are G1 implementation/acceptance tasks.
- No Persons business entity, business API, DbSet, Persons SQL table/migration, finance posting, or real-data access was introduced.
- `g1Authorized` remains `false`.

## Closure decision

G0 can be declared technically complete only after final-head CI is green and the architecture evidence is attached. Formal repository governance and independent review remain outside the capabilities of the connected GitHub integration and cannot be closed by fabricated configuration or approvals. G1 remains unauthorized until all required entry evidence is genuinely recorded.
