# Required Status Checks — MD-Gsm G0/G1 control

Workflow: `.github/workflows/persons-module-gates.yml`

The complete required check set contains **13 job names**:

- `foundation-build-test`
- `persons-spec-lint`
- `persons-architecture-boundary`
- `persons-consumer-registry`
- `persons-api-contract`
- `persons-temporary-data-retention`
- `persons-merge-contract`
- `persons-postgres-migrations`
- `persons-postgres-windows-migrations`
- `persons-security-tests`
- `persons-e2e-disposable`
- `persons-ci-enforcement-test`
- `persons-ci-evidence`

## Current remote read-back

A fresh read of GitHub's public branch metadata for `main` on 2026-11-01 returned `protected=true`, `protection.enabled=true`, and `required_status_checks.enforcement_level=everyone`, but exactly **12 required contexts** are listed. They match the list above except `persons-postgres-windows-migrations`.

Therefore the remote rule is incomplete: the Windows PostgreSQL migration job is not currently a required context. This is material because Windows is a supported V1 platform. `requiredStatusChecksConfiguredInScm=false` until all 13 contexts are read back from GitHub.

The detailed `GET /branches/main/protection` endpoint returned HTTP 403 to the connected integration, so this read-back does not independently establish the current approval count, signature setting, or every other detailed protection property. An older owner-reported record says the approval count was zero. Do not treat that historical value as a current verified configuration.

## Required remote policy

The repository Admin must configure and read back all of the following on `main`:

1. Require all 13 check contexts above, including `persons-postgres-windows-migrations`.
2. Require the branch to be up to date before merge (`strict=true`).
3. Enforce the rule for administrators; no bypass allowances.
4. Require pull requests and at least **one approving review** from someone other than the last pusher.
5. Dismiss stale approvals and require approval of the latest push.
6. Preserve signed-commit enforcement, linear history and conversation resolution.
7. Disallow force pushes and branch deletion.

The repository helper `scripts/Configure-MainBranchProtection.ps1` has been corrected to request these 13 checks and one independent approval. It performs a preflight against the current `main` HEAD, applies the rule only with the explicit `-Apply` switch, then reads the detailed settings back and fails unless the 13 names and review policy match. The current connector exposes no Branch Protection write action; the owner must run this helper locally using GitHub CLI authenticated as a repository Admin.

## Negative enforcement evidence

The earlier PR #6 deliberately introduced a forbidden project reference and caused the architecture check to fail, but it predates the complete 13-check rule. It is not evidence that the corrected remote rule blocks merge. After the Admin read-back succeeds, a new disposable negative PR must demonstrate that a failing required check blocks merge. Keep that test PR unmerged and close it after recording the result.

## Signed commits and merge method

The repository evidence records signed-commit enforcement on `main`; the current `main` tip is also reported by GitHub as signature-verified. The feature-branch commits in PR #12 are unsigned, so do not use rebase or a merge method that would place those unsigned commits directly on `main`. Use **Squash and merge from the GitHub web interface** after every gate and approval is satisfied, then verify that the resulting commit on `main` is marked **Verified**. This is a merge instruction, not authorization to merge PR #12.

## Historical observations

On 2026-10-31 an earlier branch metadata observation returned an empty required-context list. The 2026-11-01 read-back now shows 12 contexts, but still omits the Windows PostgreSQL job. Keep these observations distinct; neither proves the full 13-context policy is active.

## PostgreSQL and application migration runner

The application-owned `tools/MDGsm.DbMigrator` and PostgreSQL 18.6 Linux/Windows integration were revalidated on the G0 head in run `41185491539` (13/13 workflow jobs passed). First run applied one migration and second run applied zero migrations in each disposable test database. Earlier detailed migration evidence remains at `docs/operations/evidence/postgresql18-migration-validation-2026-10-31.json`; the complete G0 CI record is `docs/operations/evidence/g0-ci-validation-2026-10-31.json`.
