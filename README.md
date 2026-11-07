# MD-Gsm — Clean Foundation

A new, independently authored foundation for the MD-Gsm accounting application. No implementation artifacts from an earlier application are part of this project.

## Current scope

1. **Architecture baseline:** physically separated Host and BuildingBlocks projects, four isolated versioned module Contracts projects, and automated project-inventory/dependency-boundary tests; the target topology is documented in docs/architecture/modular-target.md.
2. **Configuration and logging:** non-secret API defaults in `appsettings.json`, console logging, and operating guidance for secrets and sensitive log data.
3. **Security baseline and partial IdentityAccess runtime:** trust boundaries, server-side session/security components, and tested HTTP login/password-change paths; production activation remains gated.
4. **API contracts:** typed success envelope, versioned technical route, Problem Details errors, request IDs, and contract tests.
5. **Desktop-to-API connection:** typed HTTP client, configurable local API URL and timeout, UI connection check, and client error handling tests.
6. **Persistence foundation:** PostgreSQL + EF Core/Npgsql decision, conditional DbContext registration, a database-aware readiness endpoint, and migration/secret-handling rules.

These are foundational controls and design constraints, not a claim that production authentication or a complete hardened deployment already exists.

## Current implementation limits

The repository has moved beyond the original clean foundation. The Persons module has a protected detail-read API and PostgreSQL-backed persistence/security boundaries; Persons business migrations and the application-owned migration runner are present. IdentityAccess has runtime security/session components and an HTTP login/password-change path guarded by configuration (HTTP login remains disabled by default). These facts do not mean that the complete Persons workflow, production authentication deployment, or G1 has been accepted.

Still incomplete or not production-enabled: Persons create/search and full Contact Point lifecycle, production bootstrap and recovery, full operational Desktop write UI, temporary-data cleanup runtime, cross-module Person merge execution, and complete Finance/accounting workflows. No production/real-data database migration has been performed.

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

Run the API with `dotnet run --project .\src\Host\Api\MDGsm.Api.csproj`. The `/health/live` endpoint means process liveness only.

## Governance

- `docs/governance/PROJECT_GOVERNANCE_CHARTER_FA.md` — the binding project charter. All contributors and coding agents must follow it; only the project owner may authorize changes.
- `AGENTS.md` — mandatory startup instructions for coding agents and automated contributors.
- `.github/PULL_REQUEST_TEMPLATE.md` — required checklist for every proposed change.

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
- `docs/data/persistence-and-migrations.md` — persistence and migration operating rules, including explicit application-owned migration execution.
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

## Persons and IdentityAccess — current repository status (2026-11-07)

The source contains partial, real Persons and IdentityAccess implementation; it is not a design-only skeleton and it is not G1-authorized.

- `docs/persons/persons-module-manifest.json` is the machine-readable status source. Current state: `repositoryStage=G1_CONTRACTS`, `implementationState=PARTIAL`, `g1Authorized=false`.
- Persons includes the protected detail endpoint `GET /api/v1/persons/{partyId}`, PostgreSQL-backed repository/persistence, a business migration in the application-owned migration runner, and permission checks for sensitive fields.
- Persons create/search endpoints, complete Contact Point lifecycle, full operational Desktop forms and write wiring, temporary-data cleanup runtime, and merge execution remain incomplete.
- IdentityAccess includes server-side session/security runtime and a tested HTTP login/password-change path. HTTP login is feature-gated and defaults off; production bootstrap, final recovery protocol, production listener/TLS deployment and independent security acceptance remain open.
- The Contracts assemblies exist as separately compiled boundary projects; their presence does not imply that all four business modules are implemented.
- CI has validated disposable PostgreSQL 18 Linux/Windows migration paths. This is not a migration of a production or real-data database.
- Preview dashboard/Persons UI data remains synthetic and is not a substitute for operational API-connected UI.

### Current main validation

Main commit: `194720455d01a5f88b1996ed9b100b7bc7ec363b`.

- Persons Module Gates: run `42381030785` — 13/13 jobs successful, including PostgreSQL Linux/Windows.
- CodeQL: run `42381030784` — successful.
- Windows Preview Installer: run `42381030763` — successful.
- Project Charter Owner Gate: run `42381030735` — successful.

The run records are commit-bound. Recheck them after any subsequent code change; these results do not automatically validate a later PR head.

### G1 hard stop

`g1Authorized=false` remains mandatory. Do not interpret successful CI, the presence of migrations, or a preview installer as permission to enable Persons, apply migrations to a real database, release production authentication, or close G1. The remaining entry conditions and evidence are tracked in `docs/persons/G1_PREPARATION_AND_ENTRY_CHECKLIST_FA.md` and `docs/persons/g1-entry-evidence.json`.

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
