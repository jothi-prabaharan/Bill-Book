# Handoff Report: Milestone 3 App Shell Decomposition (`libs/app-shell`)

## 1. Observation
- **Target Library**: `frontend/libs/app-shell/`
  - Current implementation has monolithic `ShellComponent` in `libs/app-shell/src/lib/shell/shell.component.ts` (260 lines), `shell.component.html` (231 lines), and `shell.component.scss` (314 lines).
  - Existing `libs/app-shell/src/index.ts` only exports `ShellComponent`.
  - Existing test suite in `libs/app-shell`:
    - `libs/app-shell/src/lib/shell/shell.component.spec.ts` (21 tests, all passing)
    - `libs/app-shell/src/lib/integration/shell-module-integration.spec.ts` (8 tests, all passing)
  - Full frontend suite: 25 test files, 314 tests, all passing cleanly (`npm test`).
- **Design References**:
  - `PROJECT.md` §Interface Contracts & Layout Stacking:
    - Top Bar Header: `z-index: 6` (sticky, 46px)
    - Fixed Left Rail: `z-index: 5` (fixed, 56px, dark ink ground `--color-ink`)
    - Breadcrumb Strip: `z-index: 4` (sticky under topbar, replaces `<h1>` headings, hosts module actions)
    - Sticky Table Header: `z-index: 3` (sticky, `top: 0`, solid surface ground with inset bottom shadow rule)
    - Table Rows & Content: `z-index: 1`
  - Design HTML: `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\Shell.dc.html`
- **Strict Prohibited String**:
  - Requirement R5: The UI label for `accounting` must strictly be **Accounts** ("Accounting" must never appear in user-facing UI). Verified in navigation item definitions and breadcrumb resolution.

## 2. Logic Chain
1. **Separation of Concerns**: Splitting the shell chrome into 4 dedicated standalone components (`ShellComponent`, `ShellNavComponent`, `ShellTopbarComponent`, `ShellBreadcrumbComponent`) isolates discrete behaviors (navigation vs topbar org switching/actions vs route-derived breadcrumbs vs layout orchestration).
2. **CSS Grid Alignment**: Decomposing into a root CSS grid container (`grid-template-columns: 56px 1fr; grid-template-rows: 46px auto 1fr`) ensures rigid 56px left rail placement, 46px top bar height, and seamless content scrolling in row 3 col 2 without layout thrashing or overlay collisions.
3. **Z-Index Layering**: Enforcing explicit z-indices (Topbar: 6, Nav: 5, Breadcrumbs: 4, Table Header: 3, Content: 1) prevents dropdown clipping, sticky header overlap, and mobile tab bar occlusion.
4. **Regression Safety**: Retaining delegated method signatures and properties on `ShellComponent` guarantees that existing integration tests and route configurations in `apps/web/src/app/app.routes.ts` function without breaking changes.

## 3. Caveats
- No caveats. All 314 test assertions across the entire workspace are green, and the architectural contract is precisely defined in `analysis.md`.

## 4. Conclusion
Milestone 3 investigation and decomposition planning is complete. A comprehensive implementation blueprint is available at `C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_m3_1\analysis.md`.
The implementer (worker) should proceed with creating the subcomponent folders (`nav/`, `topbar/`, `breadcrumb/`), updating `shell/`, exporting all 4 components in `index.ts`, and verifying all tests pass.

## 5. Verification Method
1. Run app-shell unit and integration tests:
   ```powershell
   cd frontend
   npx vitest run libs/app-shell
   ```
2. Run full frontend test suite:
   ```powershell
   cd frontend
   npm test
   ```
3. Run lint and typecheck:
   ```powershell
   cd frontend
   npm run lint
   npm run typecheck
   ```
