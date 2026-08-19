## 2026-08-19T15:16:03Z

You are worker_m3_1 (teamwork_preview_worker).
Your working directory is C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m3_1.

## MANDATORY: Read ORIGINAL_REQUEST.md first
Read C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md before starting work.

## Mission
Implement Milestone 3: App Shell Decomposition in `frontend/libs/app-shell/` according to Requirement R2, R5, `PROJECT.md`, and the blueprints in `C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_m3_1\analysis.md`.

## Exclusive Write Ownership
You own ONLY the files in:
`frontend/libs/app-shell/*`
(e.g., `src/lib/nav/*`, `src/lib/topbar/*`, `src/lib/breadcrumb/*`, `src/lib/shell/*`, `src/index.ts`, and test files).

## Key Requirements to Implement
1. **4 Standalone Components**:
   - `ShellComponent` (`selector: 'bb-shell'`): Root CSS Grid orchestrator (`grid-template-columns: 56px 1fr; grid-template-rows: 46px auto 1fr; height: 100dvh; overflow: hidden;`).
   - `ShellNavComponent` (`selector: 'bb-shell-nav'`): 56px fixed left rail (`z-index: 5`), ink ground (`--color-ink`), active cutout indicator with left accent rule, user profile menu, responsive mobile tab bar (<860px).
   - `ShellTopbarComponent` (`selector: 'bb-shell-topbar'`): 46px sticky bar (`z-index: 6`), searchable organization switcher dropdown, display-only FY tag, action group buttons (`New`, `Favourites`, `Help`, `Sign out`).
   - `ShellBreadcrumbComponent` (`selector: 'bb-shell-breadcrumb'`): Sticky breadcrumbs (`z-index: 4`) replacing `<h1>` headers, dynamic route path resolution, action projection host (`<ng-content select="[bbShellActions]" />`).
2. **Strict UI Label Rule**:
   - The UI label for the accounting module is strictly **Accounts** ("Accounting" must NEVER appear anywhere in the UI).
3. **Public API Exports**:
   - Export `ShellComponent`, `ShellNavComponent`, `ShellTopbarComponent`, `ShellBreadcrumbComponent` from `frontend/libs/app-shell/src/index.ts`.
4. **Z-Index Layering & Stacking**:
   - Topbar: `z-index: 6`
   - Rail: `z-index: 5`
   - Breadcrumb: `z-index: 4`
   - Data Table Header: `z-index: 3`
   - Content: `z-index: 1`
5. **No JS animation/hover**: Use pure CSS transitions (120ms).
