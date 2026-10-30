# Phase 9 — C# desktop shell and menu navigation

## Current UI state (v0.2.10)

- Uses the Stage 8 menu catalog as the runtime navigation source (`Assets/menu-catalog-fa.json`).
- Shows all 11 main menu roots; catalog groups explicitly render as direct rows, true left-cascading submenus, or hidden future items. The root menus remain hover-only.
- Top-level menus open only on `MouseEnter`; there is no click-toggle handler. The dropdown appears after the brand/quick-action bar, like the HTML reference. The whole header remains the hover boundary so crossing the brand bar does not close it; leaving the whole header closes it after a short delay, and Escape closes it immediately.
- Top-level menus never create workspace tabs/pages. Not-yet-implemented destinations produce a status message while leaving the current screen in place. `BASE-P-06` opens the separate Persons UI preview.
- The dashboard opens maximized but content keeps responsive 10% insets. Typography is enlarged for readability; the reception grid derives its height from the filtered row count and has no internal scrollbar. The overall dashboard may scroll vertically to avoid clipping lower reports. The five-tab reception worklist selects delivery today by default. Right-edge markers are 3.3 px wide: vivid red `#E00000` for urgent, purple for partner, orange for due today, and green for tomorrow/day-after; the marker is independent of repair status. Product identity is on the physical right of the brand row, quick actions on the left, and the top navigation frame is 42 px high. All shown business-like numbers and rows are demo fixtures, not actual shop data.
- Persons search/add remains in-memory-only. No API calls for Persons and no business table or migration were added.
- The preview does not call or require the API to display the UI. The footer is styled like the HTML reference and shows demo-only user/company/year status.
- UI was rebuilt in native WPF XAML/C# based on the visual reference. The reference HTML, its JavaScript, CDNs and assets were not copied into the executable source or added as runtime dependencies.

## Explicitly not implemented

- No business data operations, financial posting, stock posting, actual reception/invoice workflow, login/authentication, production authorization service, fiscal-year write operations, or Persons business database schema.
- Dashboard values and rows are synthetic; no real business data is read or written.
- Menu visibility is not security enforcement. Server-side authorization remains mandatory for future real endpoints.
- User-specific dashboard widgets, drag/resize persistence and production charts remain future work.

## Validation evidence (updated 2026-10-30)

The following checks have now been performed on GitHub-hosted Windows runners; this supersedes the earlier statement that Windows build/test and installer compilation were not yet performed.

- Persons Module Gates run `41081078694`: all 12 jobs succeeded.
- Windows .NET 10 Release build: succeeded with 0 warnings and 0 errors.
- .NET tests: 16 passed, 0 failed, 0 skipped.
- Python CI policy suite: 30 tests passed, including the architecture negative tests, CI evidence environment guard, and Windows installer pull-request trigger regression test.
- PostgreSQL 18 disposable migration gate succeeded.
- Windows Preview Installer run `41081078775`: succeeded. Inno Setup 6 compiled the self-contained Windows x64 preview installer and uploaded artifact `MD-Gsm-Persons-Preview-Installer-v0.2.10` (artifact ID `12990690282`). The artifact archive SHA-256 is `9f9e7a3c66af1a83f1ab4f0c51253850953c74a85b4d9b1d3a4880734142622e`.

The CI evidence verifies compilation, automated tests, packaging, and artifact upload. It does **not** verify the visual appearance or interactive behavior of the installed WPF window.

## User-reported manual acceptance — 2026-10-30

The user installed and launched the v0.2.10 preview package and reported that installation, application startup, and the application itself were working correctly. The only reported issue was the Windows **Unknown Publisher** message shown during installation.

This is consistent with the unsigned Authenticode state of v0.2.10. The Inno Setup `AppPublisher` metadata does not digitally sign the installer. Version v0.2.11 adds an optional signing pipeline for both the application EXE and installer EXE, with SHA-256 signing, RFC 3161 timestamping, and SignTool verification. A trusted code-signing PFX must first be configured in GitHub Actions secrets; without it, the workflow deliberately labels the output `UNSIGNED` and does not claim to have removed the publisher warning.

## Acceptance criterion

Accept the UI restoration only after the Windows Release build and .NET tests pass, the window is launched and visually checked, the top menus are verified as hover-only dropdowns, and the Windows installer is successfully produced. This does not authorize G1 or implement the business Persons module.


## v0.2.5 readability and due-priority markers

- Dashboard remains maximized but the content has standard inner spacing; it no longer touches the display edges. Main navigation labels are 11 pt, submenu/quick actions 12 pt, tabs 14 pt and grid headers/cells 13 pt.
- `تحویل امروز` is selected at startup and lists all not-yet-delivered records whose due date is today. The counter follows the same rule.
- The physical right edge of each worklist row uses `MarkerBrush`: strong red for urgent, purple for partner, green for tomorrow/day-after, orange for today, neutral gray otherwise. This marker no longer incorrectly follows the repair `Status`.
- The C# architecture tests now cover margins, default tab, filtering and marker-color mapping. WPF build, .NET tests and visual execution must still be confirmed on Windows; no success is claimed here. The preview remains synthetic and G1 remains locked.


## v0.2.3 dashboard/menu restoration (historical)

The top-level menus are hover-only. Each root popup is aligned to the hovered root menu in RTL; group rows open a left-side cascading popup. Dashboard sections and reception/report fixtures are synthetic display data only. Windows WPF build/runtime still requires validation on Windows; G1 stays locked.


## v0.2.4 maximized responsive dashboard, expanded worklist, and screenshot-derived menu hierarchy

- `MainWindow` starts maximized and the dashboard uses a stretch layout across the available screen width; it no longer uses the fixed 1075 px content width.
- The top menu buttons were compacted to keep all eleven roots visible on typical laptop widths. The menu catalog is data-driven, hover-only, and now explicitly records each group as direct/submenu/hidden, plus separators and shortcuts.
- Added the first two worklist tabs: `تحویل امروز` (today-due rows that are ready for delivery) and `ورودی امروز` (rows marked received today). Their counts are computed from the same 12 synthetic rows. Existing technician, partner and all-intake tabs remain.
- Worklist height is clamped from viewport height; both 10-item bar charts use `UniformGrid` to distribute bars across the available width.
- The screenshots show three groups without opening their child submenu (`سامانه اقساط`, `چک‌های تضمینی`, `تولید`). Their children are intentionally not invented; the runtime shows an explanatory placeholder for those three only. The root menus `سامانه‌های مالیاتی` and `راهنما` are visible, but their contents are not opened in the Parsian screenshots, so the preview does not fabricate popups for them. A missing report row (`لیست چک‌های دریافتی عودت‌شده`) and observed shortcuts (`Ctrl+W`, `Shift+P`, `Ctrl+F13`, `Ctrl+C`) were added from screenshots. The catalog now contains 187 options plus 12 quick actions. The visual hierarchy follows the screenshots: direct sale/accounting/payroll rows are no longer incorrectly wrapped in generic category rows.
- Local static XML/source checks do not substitute for Windows Release build, .NET test and visual runtime validation; those remain required before accepting the package. No Persons business schema or migration was added and G1 remains locked.


## v0.2.6 dashboard spacing and complete delivery worklist

- Dashboard content now receives responsive 10% insets on all four sides of its scroll viewport; the window stays maximized.
- Explicit XAML and code-behind menu font sizes are 10% larger than v0.2.5.
- The reception worklist uses an explicit right-edge marker column rather than a DataGridRow border (which did not reliably render the requested marker). Urgent/due-date/partner colors are assigned deterministically.
- The DataGrid height is derived from visible filtered row count; its vertical scrollbar is disabled. The dashboard page itself may scroll to expose lower chart sections.
- Static regression tests in `scripts/ci/test_dashboard_ui_contract.py` validate these contracts. Windows WPF build and visual confirmation remain pending.


## v0.2.7 product identity and quick-action swap (historical)

- Swapped header groups: product badge, full Persian product name, preview version and live Persian date/time now sit on the left; new reception, partner reception and calculator actions now sit on the right.
- Window title and installer app/shortcut display names now use `نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل`; package and installer version is 0.2.7.
- Added a source-level regression test for the exact product name, live date control, the left/right grid columns and all three quick-action IDs.
- This is branding/layout only. No Persons API, business schema, or persistence was added. Windows Release build and visual verification are still required.


## v0.2.8 branding alignment, restrained delivery marker and grid consistency

- The product badge is explicitly laid out at the physical left, with the full product name, version and live Persian date beside it; quick actions are anchored to the physical right.
- Dashboard canvas background is `#F1F5F9`, matching the requested window background.
- The delivery-priority marker is 3 px wide (half of the previous 6 px strip). Its color semantics remain urgent red, partner purple, due-today orange, tomorrow/day-after green and neutral for delivered/other rows.
- Reception and Persons DataGrids disable their own horizontal and vertical scrollbars. Each grid sizes to its filtered visible row count; header and body text use a shared right-alignment style, with header/cell vertical alignment normalized.
- Reception controls, grid, and count footer now share a common horizontal inset.
- The whole dashboard page still has vertical scrolling when its stacked content exceeds the monitor viewport. This is intentional so lower charts/reports are not silently clipped; the data tables themselves have no nested scrollbar and expand to show all rows.
- This is still a synthetic visual preview. No business table, migration, real-data operation, or Persons API was added; G1 remains locked. Windows WPF build and screenshot verification are still required.


## v0.2.9 header alignment, stronger marker color and taller navigation

- Product logo/name/version/live date move to the physical right; quick-action buttons move to the physical left.
- Top navigation frame height increases from 36 px to 42 px and the root menu button vertical padding increases.
- Delivery marker width increases from 3 px to 3.3 px (10%); urgent marker is vivid red `#E00000`. Partner purple, future-delivery green, due-today orange and neutral delivered/other colors are unchanged.
- Package version and Windows installer workflow references were updated to 0.2.9.
- Static source checks are not a Windows WPF build or a visual acceptance test; those remain required. No business Persons API/schema or migration is added, and G1 remains locked.


## v0.2.10 reception partner styling and brand mark

- Reception column label is now `مشتری`; partner acceptances use purple code text (`#8B5CF6`) and a subtle `#FAF7FF` row tint.
- The count/disclaimer strip under the reception list was removed. The primary navigation font alone was increased to 12.5; cascading submenu font remains unchanged.
- The MD mark is larger and stacked above `MD-Gsm`.
- Static validation is not a substitute for a Windows WPF build, visual runtime check, or installer EXE build; those remain outstanding.
