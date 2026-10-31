# MD-Gsm — Clean Foundation

A new, independently authored foundation for the MD-Gsm accounting application. No implementation artifacts from an earlier application are part of this project.

## Current scope

1. **Architecture baseline:** preparation shell with explicit layer dependency rules and an automated project-reference boundary test; the approved target is modular and documented in docs/architecture/modular-target.md.
2. **Configuration and logging:** non-secret API defaults in `appsettings.json`, console logging, and operating guidance for secrets and sensitive log data.
3. **Security design baseline:** trust boundaries and requirements for future identity, roles, authorization, and transport security.
4. **API contracts:** typed success envelope, versioned technical route, Problem Details errors, request IDs, and contract tests.
5. **Desktop-to-API connection:** typed HTTP client, configurable local API URL and timeout, UI connection check, and client error handling tests.
6. **Persistence foundation:** PostgreSQL + EF Core/Npgsql decision, conditional DbContext registration, a database-aware readiness endpoint, and migration/secret-handling rules.

These are foundational controls and design constraints, not a claim that production authentication or a complete hardened deployment already exists.

## Not implemented yet

No business tables or migrations, accounting workflows, financial posting, authentication/authorization, token issuance, production installer, or production deployment pipeline. The PostgreSQL persistence foundation is present, but it does not create or modify any database.

## Requirements

- .NET 10 SDK
- Python 3.12+ (or `py -3`) is used only by the optional/local source-preparation static gates. It is **not** a runtime dependency of the MD-Gsm desktop app or the Windows installer.
- Windows and .NET 10 SDK for building/running the WPF desktop client
- Inno Setup 6 only when compiling the Windows installer; the installed preview is self-contained and does not require the .NET runtime or Python to be separately installed

## Build and test

```powershell
dotnet restore .\MDGsm.sln
dotnet build .\MDGsm.sln --no-restore
dotnet test .\MDGsm.sln --no-build
```

Run the API with `dotnet run --project .\src\MDGsm.Api\MDGsm.Api.csproj`. The `/health/live` endpoint means process liveness only.

## Documents

- `docs/architecture/overview.md` — current shell dependency direction.
- `docs/architecture/modular-target.md` — authoritative modular target topology and module ownership.
- ADR-0004/0005/0006 — PartyId, shared PostgreSQL transaction, and opaque access-token constraints.
- `docs/operations/configuration-and-logging.md` — configuration and logging rules.
- `docs/security/security-model.md` — security design constraints and implementation status.
- `docs/architecture/api-contracts.md` — success/error contracts, route versioning, and request IDs.
- `docs/PHASE_4_STATUS.md` — implementation and validation status for stage 4.
- `docs/PHASE_5_STATUS.md` — implementation and local run instructions for stage 5.
- `docs/PHASE_6_STATUS.md` — scope and validation status for stage 6.
- `docs/data/persistence-and-migrations.md` — persistence and migration operating rules.
- `docs/decisions/0002-postgresql-and-efcore.md` — database technology decision.
- `docs/decisions/0001-clean-start.md` — clean-start decision.

## Stage 7: one-command local PostgreSQL + Desktop smoke test

After PostgreSQL 18 is installed and running locally, open PowerShell in this folder and run:

```powershell
.\scripts\Start-MDGsm-DisposableTest.ps1
```

The script prompts for the local PostgreSQL admin password without echoing it, creates a new disposable database, applies the project's versioned SQL migration, starts the API, checks readiness, and launches Desktop. Click **بررسی اتصال به API** in the Desktop window. Full instructions and safety limitations are in `docs/operations/stage-7-smoke-test.md`.


## Stage 8: architecture laws and menu catalog

Stage 8 adds architecture/data ownership/dependency/migration/security rules, the accepted single-database fiscal-period decision, and a Persian hierarchical menu catalog (Markdown + CSV) based on available reference screenshots and the naming-only HTML reference. No business module or application shell is implemented in this stage. See `docs/PHASE_8_STATUS.md`.


## Stage 9: C# shell and menu navigation

The desktop has a WPF dashboard shell driven by the screenshot-derived menu catalog. The current v0.2.10 UI pass keeps responsive 10% insets, uses the requested `#F1F5F9` dashboard canvas, places product identity at the physical right and quick actions at the physical left, and standardizes DataGrid header/cell alignment. The delivery-priority marker is 3.3 px wide, with vivid urgent red `#E00000`. Reception and Persons grids expand to all filtered rows without internal scrollbars; the overall dashboard page may scroll vertically when needed to avoid clipping lower reports on laptop displays. `تحویل امروز` remains the default tab. Top-level menus remain hover-only; no business writes are implemented. A Windows build and visual runtime check remain required before acceptance. See `docs/PHASE_9_STATUS.md`.

## Preparation package maintenance

- Current preview package: v0.2.10. See `CHANGELOG_V0.2.10_FA.md` and `README_PREVIEW_FA.md` for the Persian product branding, product/date block on the right, quick actions on the left, 10% responsive insets, default today-delivery tab and self-contained Windows installer steps. The v0.2.3 report is historical UI restoration evidence; the v0.1.1 validation report remains historical foundation evidence.

## Persons module source preparation — G0 (not G1-authorized)

This package is a clean Stage9-based source preparation branch for introducing the Persons module. It intentionally adds **no** Persons entity, API endpoint, business DbSet, SQL Persons table, or Persons migration. The reserved source folders define module boundaries only.

Preparation assets:

- `docs/persons/G1_PREPARATION_AND_ENTRY_CHECKLIST_FA.md` — G1 entry conditions and remaining external approvals.
- `docs/persons/persons-module-manifest.json` — machine-readable stage and gate state; `g1Authorized` must remain `false` until evidence is complete.
- `docs/persons/acceptance-gate-map.json` — AC-31..AC-45 are UNMAPPED because executable tests do not exist; AC-46..AC-49 link to checked-in policy tests.
- `docs/persons/consumer-registry.json` — current runtime consumers (none yet) and rules for registering future consumers.
- `docs/persons/TEMPORARY_DATA_RETENTION_POLICY_FA.md` and `docs/persons/MERGE_CONTRACT_CHECKLIST_FA.md` — policy proposals/checklists requiring approval.
- `.github/workflows/persons-module-gates.yml` — 12 CI jobs, including Windows restore/build/test, a disposable PostgreSQL 18.6 Linux migration smoke test and redacted evidence enforcement. PostgreSQL-on-Windows validation and an application-owned migration runner remain outstanding.
- `scripts/ci/validate_persons_gates.py` — fail-closed static gate checks. Runtime checks report PASS-N/A only when machine checks prove that the related feature is absent.
- `scripts/ci/write_ci_evidence.py` — produces a commit-bound CI evidence manifest with redaction checks and fails closed if any required gate or test is missing, skipped, or failing.
- `scripts/Validate-Stage9-Baseline.ps1` — Windows local restore/build/test plus source gates. It validates the Python runtime first and supports `py -3`; it rejects the Microsoft Store execution alias as a missing runtime and reports that condition separately from an actual gate failure.
- `scripts/Verify-SourceChecksums.ps1` — verify the package source checksums listed in `SHA256SUMS.txt`.
- `docs/operations/required-status-checks.md` — exact required CI check names and the remote merge-blocking test requirement.

### Current validation status

Python static gates and 17 dashboard/policy regression tests run successfully in the preparation environment. The .NET SDK, Windows PowerShell and PostgreSQL are not installed in this execution environment, so no claim is made that the WPF solution has been built/tested here or that the PostgreSQL script ran. Run `scripts/Validate-Stage9-Baseline.ps1` on the Windows development machine. The GitHub workflow provides Windows and disposable PostgreSQL jobs once pushed to a remote repository.

### Hard stop before G1

`g1Authorized` is `false`. The sole active repository is Md-gsm/MD-Gsm1; no parallel md-gsm repository is part of the project. The modular target is specified but not yet implemented. AC-31..AC-45 remain UNMAPPED. Required Status Checks must still be configured on the actual remote repository, owners assigned, policy/checklist approval recorded, and a negative PR test must prove a failed required check blocks merge. A workflow file alone does not enable branch protection.

No production/real-data database was accessed or changed. The existing Stage9 infrastructure migration was not modified.


## Windows preview package v0.2.8 — grid alignment and dashboard background

The `v0.2.8` package opens maximized on the current display with responsive 10% insets on all four sides of the dashboard content. Its second header row places the MD logo badge, full Persian product name, version and live Persian date/time on the left; the `پذیرش جدید`, `پذیرش همکار`, and `ماشین‌حساب` buttons are on the right. Explicit typography sizes are increased 10% over v0.2.5; `تحویل امروز` remains the default; the reception grid sizes itself to show all filtered rows without an internal vertical scrollbar. A dedicated 3 px right-edge column displays markers: urgent dark red, partner purple, due-today orange, tomorrow/day-after green; delivered rows are neutral. The menu catalog continues to use direct action rows, real cascading submenus, screenshot-derived separators and shortcut metadata. Top-level menus open on mouse hover only. The actual order/hierarchy visible in the Parsian screenshots is reproduced where evidence exists; children of unopened screenshot groups are not invented. The `BASE-P-06` (ثبت و فهرست اشخاص) item opens the separate Persons preview. Dashboard and Persons rows are synthetic and do not call a Persons API, create business tables, execute a migration, or persist data. Closing the application discards preview additions.

To run the preview from source without Python:

```powershell
.\scripts\Run-Persons-Preview.ps1 -Build
```

To build an actual Windows installer on the Windows development machine (requires .NET 10 SDK and Inno Setup 6; **Python is not required**):

```powershell
.\scripts\Build-Windows-Installer.ps1
```

The installer is emitted at `artifacts\installer\MD-Gsm-Persons-Preview-Setup-v0.2.8.exe`. A Windows CI workflow also builds and uploads this artifact after the workflow is run on a connected repository. The installer is a preview and does not create or modify a database. G1 authorization remains false until the architecture gates and external SCM branch-protection proof are satisfied.


## UI restoration v0.2.3 (historical)

The desktop header follows the HTML reference order (top navigation, brand/quick actions, hover-only root dropdown with left-side cascading group flyouts). The dashboard includes reception metrics/table, multi-series repair chart, technician workload, and parts/repair-type statistics. The HTML file is used only as a visual reference; its executable page/script is not included in the WPF app.

## UI sizing and worklist v0.2.6 (historical baseline)

- The fixed 1075 px dashboard width remains removed. The main window starts maximized, while dashboard content has standard inner margins (responsive 10% insets on all sides). Short screens can scroll the main dashboard vertically without clipping. Menu/tab/table typography is increased for readability.
- The reception table has five tabs: delivery today, intake today, repairs with technicians, partner intake, and all intake. Delivery today is selected by default and includes due-today rows not yet handed over; intake today uses the synthetic received-today flag. Counts are calculated from the same rows.
- The reception grid height is calculated from the current filtered row count and fixed 49 px row height plus header; its vertical scrollbar is disabled so all current results are visible together. A dedicated six-pixel right-edge indicator marks urgent items in dark red, partners in purple, tomorrow/day-after delivery in green, and due-today items in orange; delivered items are neutral. Color is independent of repair status. Both 10-category bar charts distribute the categories across the chart width.
- Menu catalog order is retained from the original Parsian screenshots. 187 catalog options are retained across direct, submenu and hidden groups; 12 quick actions are listed separately. Three visible category labels were not expanded in the source screenshots, so their unknown children remain explicitly unknown rather than fabricated. Proposed future items and HTML-only options not supported by a screenshot are explicitly hidden or source-tagged.
- This is still a display-only preview. No API, business Persons database schema, migration, or persistent reception workflow has been implemented. G1 remains locked. Windows Release build and visual launch are still required to accept the package.


## v0.2.7 product identity and quick actions (historical)

The second header row swaps the previous positions of product identity and quick actions: the MD badge, full product name `نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل`, version label, and live Persian date/time are at the left; `پذیرش جدید`, `پذیرش همکار`, and `ماشین‌حساب` are at the right. The window title, installer display name, shortcuts and installer filenames use the current product branding. No business schema, Persons API, or persistent data behavior was added. Windows Release build and visual verification remain required.


## v0.2.8 grid alignment and background

- Set the dashboard canvas to `#F1F5F9`; keep the panel cards white for contrast.
- Explicitly anchor the logo/product name block on the physical left and quick actions on the physical right.
- Reduced the right-edge delivery marker from 6 px to 3 px.
- Standardized DataGrid header/cell alignment and common horizontal insets; reception and persons grids disable internal scrollbars and size to all visible rows.
- The complete dashboard page remains vertically scrollable only when necessary, so lower analytics are not clipped on laptop displays. Tables themselves have no nested scrollbars.
- Windows WPF build, visual execution and installer build remain pending until run on Windows; G1 authorization remains false.


## Windows preview package v0.2.9 — header alignment and menu sizing

- The logo/product name/version/live date block is anchored to the physical right of the brand row; `پذیرش جدید`, `پذیرش همکار` and `ماشین‌حساب` are on the physical left.
- Top navigation row height increases to 42 px and menu-button vertical padding increases to make the menu hit area taller.
- Delivery marker grows 10% from 3 px to 3.3 px. Urgent delivery is shown with saturated red `#E00000`; other category colors and priority ordering are unchanged.
- Window, assembly, installer and workflow artifact names were versioned at 0.2.9. This is UI-only; synthetic data only, no Persons API/schema/migration, and G1 remains locked.
- Windows Release build and visual runtime verification are still required before acceptance.


## Windows preview package v0.2.10 — partner row styling and brand mark

The reception list labels the person column `مشتری`, styles partner reception codes purple with a very light purple row, removes the count/disclaimer strip below the list, and slightly enlarges only the top-level menu labels. The MD mark is larger and sits above `MD-Gsm`. Submenus are unchanged. This remains a preview-only build with synthetic data; G1 remains locked.
