# Required Status Checks — MD-Gsm G0/G1 control

Workflow: `.github/workflows/persons-module-gates.yml`

The following 13 job names are the required check set that must be configured on the remote default branch:

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

## Remote configuration evidence — 2026-10-31

The GitHub branch metadata endpoint for `main` returned:

- `protected=true`
- `protection.enabled=true`
- `protection.required_status_checks.enforcement_level=non_admins`
- `protection.required_status_checks.contexts=[]`
- `protection.required_status_checks.checks=[]`

The rulesets endpoint returned an empty array. The dedicated Branch Protection details endpoint returned HTTP 403 (`Resource not accessible by integration`). **The current branch metadata therefore does not list any required status checks.** A protected branch is not enough to close this condition.

Recommended Admin configuration:
1. Add all 13 check names listed above as required checks.
2. Require branches to be up to date before merging.
3. Prevent bypass for administrators and maintainers for these checks, unless a documented emergency exception is explicitly approved by the Project Owner.
4. Keep required checks attached to the default branch rules, not just workflow YAML.

The connected GitHub integration currently exposes no Branch Protection/Rulesets write action. It cannot change these settings even though the repository account has Admin permission. A repository Admin must configure them in GitHub Settings and provide an exported/screenshot/API evidence reference. Until this is verified, `requiredStatusChecksConfiguredInScm=false`.

## Negative enforcement test

PR #6 deliberately introduced a forbidden `MDGsm.Domain -> MDGsm.Infrastructure` project reference. The `persons-architecture-boundary` check failed; GitHub reported the PR as blocked and it was closed without merge. This is recorded as evidence that the negative architecture test was exercised. It does not replace the requirement to configure the complete 13-check set on `main`.

CI unit tests exercise the fail-closed validator; they are not a substitute for remote Branch Protection enforcement.

## PostgreSQL and application migration runner

The application-owned `tools/MDGsm.DbMigrator` and PostgreSQL 18.6 Linux/Windows integration were revalidated on the post-relocation PR head in run `41185491539` (13/13 jobs passed). First run applied one migration and second run applied zero migrations in each disposable test database. Earlier detailed migration evidence remains at `docs/operations/evidence/postgresql18-migration-validation-2026-10-31.json`; the complete current-head CI record is `docs/operations/evidence/g0-ci-validation-2026-10-31.json`.

The Linux PostgreSQL service image is pinned to `postgres:18.6-bookworm@sha256:5c4783a8cc3f0074912164f94dfa1be4e043438f0f707cf968d7af9a0d31a2c8`.


## Admin one-command helper

A PowerShell helper is available at `scripts/Configure-MainBranchProtection.ps1`.

1. Install GitHub CLI and authenticate as the repository Admin with the **Administration: write** permission. No token is written to this repository.
2. From the project root, run a dry run:
   ```powershell
   .\scripts\Configure-MainBranchProtection.ps1
   ```
3. Review the displayed check names and settings. To apply, run:
   ```powershell
   .\scripts\Configure-MainBranchProtection.ps1 -Apply
   ```
   The script requires typing the exact repository/branch name, preserves existing unrelated restrictions, configures strict required checks, admin enforcement, at least one approval, latest-push approval, no bypass allowances, no force pushes and no deletion, then reads the result back and verifies it.
4. After successful configuration, perform a **new** negative PR test. The earlier PR #6 was before these required checks were configured and cannot replace that post-configuration test.

The script intentionally does not run automatically in CI: changing remote branch protection is an explicit repository-Admin operation. GitHub's official REST documentation lists the required fields, strict option and Administration-write permission for this endpoint: https://docs.github.com/en/rest/branches/branch-protection.
