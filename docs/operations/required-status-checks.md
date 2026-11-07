# Required Status Checks — MD-Gsm1 main governance

**Repository:** `Md-gsm/MD-Gsm1`  
**Default branch:** `main`  
**Current main commit at reconciliation start:** `fb46da7a00a977a441c4b1add70a6ba47a575f26`  
**Last documentation synchronization:** 2026-11-07

## Required checks

The Persons Module Gates workflow contains these 13 jobs:

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

The governance audit also recorded `project-charter-owner-gate` as a required control on main. The Windows Preview Installer workflow runs on pull requests and main, but is not treated here as a required check unless the Project Owner changes that policy.

## Current evidence and readback limitation

The latest repository audit recorded main as protected, strict required checks configured, and the Project Charter Owner Gate included. The audit recorded 14 required controls (the 13 Persons Module Gates jobs plus `project-charter-owner-gate`). The exact SCM configuration remains authoritative; workflow YAML or a green workflow run alone does not prove that a check is required by branch protection.

During the 2026-11-07 synchronization, a fresh request to the dedicated GitHub Branch Protection details endpoint returned HTTP 403 (`Resource not accessible by integration`). Therefore this document preserves the last recorded owner/repository audit rather than claiming a new direct readback in this run. A repository Admin should attach a fresh API export or screenshot when the integration can no longer read the endpoint. Do not silently downgrade or upgrade the recorded SCM state based only on this 403 response.

The current manifest records `requiredStatusChecksConfiguredInScm=true` based on the latest available governance evidence. The evidence should be refreshed by an Admin if the protection configuration changes.

## Historical CI evidence — pre-PR #40 main

These runs belong to the historical pre-PR-40 main commit `194720455d01a5f88b1996ed9b100b7bc7ec363b`; they are not evidence for current main `fb46da7a00a977a441c4b1add70a6ba47a575f26` or the reconciled PR #45 head:

| Workflow/control | Run ID | Result |
|---|---:|---|
| Persons Module Gates | `42381030785` | SUCCESS — 13/13 jobs |
| CodeQL | `42381030784` | SUCCESS |
| Windows Preview Installer | `42381030763` | SUCCESS |
| Project Charter Owner Gate | `42381030735` | SUCCESS |

The Persons Module Gates run includes disposable PostgreSQL 18 Linux and Windows migration validation. It does not migrate a production or real-data database.

## Protection policy recorded by the latest governance audit

The last available protection audit recorded:

- `main` protected and strict required checks enabled.
- At least one approving review required; latest-push approval and stale-review dismissal enabled.
- Code-owner review was not globally required.
- Force pushes and branch deletion were restricted.
- The Project Charter Owner Gate was required.

Do not infer the current commit-signature requirement from old snapshots. The Project Owner temporarily disabled the signed-commit requirement during troubleshooting; its current setting must be read back directly before it is described as enabled.

## Negative enforcement evidence

PR #6 deliberately introduced a forbidden `MDGsm.Domain -> MDGsm.Infrastructure` project reference. The architecture boundary check failed and the PR was blocked/closed without merge. This is historical negative-test evidence; it does not replace periodic verification of live branch protection.

## PostgreSQL and migration runner

The application-owned `tools/MDGsm.DbMigrator` is exercised by the Linux and Windows PostgreSQL 18 CI jobs. The historical pre-PR-40 main run `42381030785` passed all 13 jobs; it is not evidence for the current main commit. These tests use disposable databases and validate migration behavior/idempotence; they are not permission to run migrations against production data.

## Admin readback procedure

A repository Admin should periodically:

1. Read `GET /repos/Md-gsm/MD-Gsm1/branches/main/protection` and export the result with timestamp and commit context.
2. Confirm the 13 job contexts and `project-charter-owner-gate` are required and strict.
3. Confirm approval, latest-push approval, stale-review dismissal, administrator enforcement, no-bypass policy, force-push and deletion restrictions.
4. Record the signed-commit requirement separately; do not assume it is enabled.
5. Attach the evidence reference to the governance/evidence record.

The repository helper `scripts/Configure-MainBranchProtection.ps1` can be used by a repository Admin for a reviewed dry run and explicit configuration. It must not be run automatically by CI or used to overwrite unrelated protection settings without owner approval.


## Current-main validation status after PR #45

The current main merge commit after PR #45 is `fb46da7a00a977a441c4b1add70a6ba47a575f26`. No CI run IDs are claimed here for that commit. PR #45 is merged; its successful PR-head runs are not evidence for this merge commit. Fresh Persons Module Gates, CodeQL, Windows Preview Installer and Project Charter Owner Gate runs bound to this exact main SHA remain pending.
