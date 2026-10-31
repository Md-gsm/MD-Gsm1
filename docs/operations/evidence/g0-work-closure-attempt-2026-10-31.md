# G0 technical closure record — 2026-10-31

**Repository:** `Md-gsm/MD-Gsm1`  
**PR:** [#8](https://github.com/Md-gsm/MD-Gsm1/pull/8)  
**Validated source/architecture commit:** `7bcc423a87b44e19291f4f28d04858e06e74515c`  
**Current-head CI:** [run 41185491539](https://github.com/Md-gsm/MD-Gsm1/actions/runs/41185491539) — SUCCESS, 13/13 jobs.

## Technical G0 work — complete

1. Relocated the Domain, Application, Infrastructure and Client projects into `src/BuildingBlocks/*`, and API/Desktop into `src/Host/*`; removed the previous flat project roots.
2. Updated `MDGsm.sln`, all project references, API composition references, embedded migration resource path, Windows build/preview/smoke scripts, test path assumptions and workflow path filters.
3. Added and compiled four isolated versioned Contracts projects for Persons, IdentityAccess, RepairOperations and Finance. They contain version markers only; no business API/DTO/runtime implementation is claimed.
4. Strengthened C# and Python architecture controls to verify exact project inventory, dependency graph, Contracts isolation and absence of old flat roots.
5. Passed the latest PR workflow on the validated commit: 13/13 jobs succeeded, including foundation Release build/test, all architecture/specification/registry/API/security/retention/merge/enforcement checks, disposable E2E, and PostgreSQL 18.6 Linux and Windows migration validation.
6. Windows preview installer build passed in run [41185066512](https://github.com/Md-gsm/MD-Gsm1/actions/runs/41185066512) on commit `de9ffc1aab96e8954abff417a32f51cd6bb3e362`. The two commits after that installer run and before the current validated commit changed documentation only; no source, project, installer or script content changed.
7. Recorded Project Owner acceptance of the temporary-data policy and merge-design checklist.
8. Assigned CI job ownership to the Group of Software Advisers; membership for each phase is appointed by the Project Owner. Security/privacy accountability remains solely with the Project Owner.
9. Retained the negative architecture enforcement evidence: PR #6 introduced a forbidden Domain-to-Infrastructure reference; architecture CI failed and GitHub reported the PR blocked; the test branch was closed without merge.
10. Pinned the Linux PostgreSQL 18.6 service image to an immutable digest.

The machine-readable evidence is in `docs/operations/evidence/g0-ci-validation-2026-10-31.json`. The architecture prerequisite is now `true`; G1 authorization remains `false`.

## Governance / merge blockers — still open

### 1. Required Status Checks are not configured

The current GitHub branch metadata for `main` reports `protected=true`, but also:
- `required_status_checks.enforcement_level=non_admins`
- `contexts=[]`
- `checks=[]`

The Rulesets endpoint returned an empty list. The detailed protection endpoint is not readable by the connected integration (HTTP 403). Therefore the 13 checks passing in Actions are **not** evidence that they are required by branch protection. A repository Admin must configure all 13 names in `docs/operations/required-status-checks.md`, require up-to-date branches, restrict bypass (including administrators unless an explicit emergency exception is approved), and save/export evidence.

The connected GitHub toolset has no Branch Protection/Rulesets write operation; this setting was not changed by this session.

### 2. Independent architecture review is not recorded

The Group of Software Advisers must review the current PR head and the architecture/ADR boundary checklist independently and record the decision and any findings in `docs/architecture/INDEPENDENT_REVIEW_CHECKLIST_FA.md`. No name, review or approval has been invented or substituted.

### 3. Project Owner security/privacy review is not recorded

The Project Owner is the sole accountable security/privacy owner. The checklist is prepared, but no review outcome has been entered. Project Owner acceptance of the policy document is not the same as recording the end-to-end security/privacy review for this G1 entry package.

## Items intentionally outside G0

- AC-31..AC-45 remain `UNMAPPED`: these need actual temporary-data lifecycle and Party merge runtime plus executable tests. They are G1 implementation/acceptance work, not defects to hide with placeholder tests.
- No Persons business entity, business API, DbSet, Persons SQL table/migration, finance posting or real-data access was introduced.
- The branch contains only G0 preparation and must not authorize or start G1.

## Decision

**Technical G0 implementation and CI validation are complete on the commit above. Formal G0/PR merge and G1 entry remain on HOLD** until the Admin branch-protection configuration and the two genuine human reviews are recorded. `g1Authorized=false` must remain unchanged.
