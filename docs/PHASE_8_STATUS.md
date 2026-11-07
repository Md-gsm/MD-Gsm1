# Phase 8 — Architecture baseline and menu inventory

## Delivered
- Explicit architecture laws for the clean MD-Gsm codebase.
- Data ownership registry for shared concepts and canonical sources of truth (`data-ownership.md` plus machine-readable `data-ownership-registry.json`).
- Domain dependency directions and implementation order.
- Rules prohibiting parallel concept/model/service paths.
- Database migration discipline and disposable-database test rules.
- Server-side authorization and role-scoped dashboard/report access policy, including technician self-scope.
- Accepted ADR for a single central operational database with independent fiscal periods and complete historical continuity.
- Persian menu catalog in Markdown and UTF-8 CSV; 196 rows with stable identifiers, ownership domain, source evidence, status, and authorization scope.
- Navigation behavior contract for hover, click, keyboard, level-three menus, unavailable modules, and authorization.

## Evidence reviewed
- All 16 image files in the available `Screenshots(2).zip` archive were scanned for relevance. A subset contains open menu panels; one is the initial screen with no open menu.
- `MD-Gsm.html` was reviewed only as a visual/naming reference. Its HTML/CSS/JavaScript was not copied into the application source.
- Menu entries absent from readable screenshots are explicitly marked as HTML-only, proposed, or unknown; no unseen third-level items are presented as facts.

## Not implemented in this phase
- No shell/UI implementation.
- No business tables, business migrations, authentication, financial posting, inventory movement, customer/repair workflows, or production installation.
- No source from another application has been included.

## Acceptance gate for Phase 9 (shell)
1. Review the menu catalog and owner mapping.
2. Build the C# desktop shell and design system from scratch in MD-Gsm.
3. Render all approved top-level and submenu nodes with clear not-implemented states where applicable.
4. Ensure menus open by hover and remain open while moving through submenu levels; keyboard/click alternatives work.
5. Ensure navigation visibility never bypasses server-side authorization.
