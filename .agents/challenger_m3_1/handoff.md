# Milestone 3 Empirical Challenger Report: App Shell Decomposition (`libs/app-shell`)

## Verdict: CONFIRMED (Pass)

---

## 1. Observation

### 1.1 Test Execution Results
Direct execution of Vitest suite:
```
cd frontend && npx vitest run libs/app-shell
```
- **Test Files**: 7 passed (7 total)
  - `libs/app-shell/src/lib/breadcrumb/shell-breadcrumb.component.spec.ts` (8 tests passed)
  - `libs/app-shell/src/lib/nav/shell-nav.component.spec.ts` (7 tests passed)
  - `libs/app-shell/src/lib/app-shell-challenger.spec.ts` (14 tests passed)
  - `libs/app-shell/src/lib/shell/shell.component.spec.ts` (21 tests passed)
  - `libs/app-shell/src/lib/topbar/shell-topbar.component.spec.ts` (9 tests passed)
  - `libs/app-shell/src/lib/adversarial-shell.spec.ts` (21 tests passed)
  - `libs/app-shell/src/lib/integration/shell-module-integration.spec.ts` (8 tests passed)
- **Total Tests**: 88 passed, 0 failed.

### 1.2 Typecheck and Lint
- `npm run typecheck`: Exit code 0 (clean).
- `npx eslint libs/app-shell`: Exit code 0 (0 errors, 0 warnings).

### 1.3 Direct Code & Template Inspections
1. **Layout & Viewport Structure** (`shell.component.scss`, `shell-nav.component.scss`):
   - Desktop grid (`> 860px`): `grid-template-columns: 56px 1fr; grid-template-rows: 46px auto 1fr;` with `:host` and `.shell-grid-container` constrained to `100dvh; overflow: hidden;`.
   - Content viewport `.shell-content-cell` defines `overflow-y: auto; min-height: 0; min-width: 0;` eliminating nested/double scrollbars.
   - Mobile/Tablet grid (`<= 860px`): `grid-template-columns: 1fr; grid-template-rows: 46px auto 1fr auto;` switching desktop rail to mobile bottom navigation tab bar with `.more-sheet` slide-up panel.
   - Stacking order verified: Topbar (`z-index: 6`) > Left Rail (`z-index: 5`) > Breadcrumbs (`z-index: 4`) > Sticky Table Header (`z-index: 3`) > Content Viewport (`z-index: 1`), with popups/dropdowns at `z-index: 20` to `30`.

2. **Route Path Resolution & Breadcrumb Extraction** (`shell.component.ts`, `shell-breadcrumb.component.ts`):
   - `/sales/invoices/new` -> `['Sales', 'Invoices', 'New']` (Level 3 is non-link, marked `isLast: true`).
   - `/sales/invoices/123` -> `['Sales', 'Invoices', '123']`.
   - `/inventory/stock-adjustments` -> `['Inventory', 'Stock adjustments']`.
   - `/accounting/coa` -> `['Accounts', 'Chart of Accounts']` (`coa` expanded to `Chart of Accounts`, `accounting` transformed strictly to `Accounts`).
   - `/invalid-route` -> `['Invalid route']` without crashing.
   - Complex/malformed routes (e.g. `///purchase//bills///105?filter=all#details`) cleanly filter empty segments and query params.

3. **Organization Switcher Stress Behavior** (`shell-topbar.component.ts`, `shell.component.ts`):
   - Empty search string (`""`): returns all organizations intact.
   - Non-matching search string (`"@@@XYZ-NO-MATCH###"`): returns empty array with zero exceptions, displaying `.org-empty-msg` ("No organization matches that.").
   - Switching organization: Selecting current org closes dropdown without invoking API switch; selecting different org delegates to `AuthService.switchOrganization(orgId)` and emits `organizationChange`.
   - Escape key: `@HostListener('document:keydown.escape')` dismisses org switcher, quick action popup, and favourites modal.
   - Outside click: `@HostListener('document:click')` cleanly closes dropdown when click occurs outside `.org-dropdown-container`.

4. **Strict UI Label Audit ("Accounting" Rule R5)**:
   - Full forensic scan across all `.html`, `.ts`, `.scss` files in `libs/app-shell/`.
   - No user-visible template string, button text, crumb label, placeholder, or aria-label contains "Accounting".
   - Left rail navigation label for `/accounting` is strictly `'Accounts'`.
   - Dynamic breadcrumb generation for `/accounting` route segment strictly outputs `'Accounts'`.

---

## 2. Logic Chain

1. **Premise 1**: The App Shell must accommodate viewports from 360px mobile up to 4K desktop without grid breakage, clipping, or double scrollbars.
   - *Evidence*: `shell.component.scss` enforces viewport height lock (`100dvh; overflow: hidden`) and isolates scrolling strictly to `.shell-content-cell`. The media query breakpoint at 860px alternates between the 56px left rail and the mobile bottom tab bar without overlapping elements.
2. **Premise 2**: Breadcrumbs and navigation must dynamically resolve multi-level deep routes and hyphenated paths while gracefully handling invalid paths and special abbreviations.
   - *Evidence*: `updateCrumbs()` handles string normalization, splits query params, handles special cases (`coa` -> `Chart of Accounts`, `accounting` -> `Accounts`), and correctly assigns `isLink` and `isLast` states.
3. **Premise 3**: The organization switcher must maintain state integrity under extreme user interactions (mismatched queries, empty inputs, Escape dismissals, outside clicks, self-selection).
   - *Evidence*: Unit and adversarial tests (`ADV-ORG-01` through `ADV-ORG-06`, `TOPBAR-01` through `TOPBAR-09`) verify that all state transitions function deterministically with zero unhandled exceptions.
4. **Premise 4**: The UI string "Accounting" must be completely eradicated from all user-facing interfaces per R5 and project rules.
   - *Evidence*: `grep_search` and automated adversarial test `ADV-AUDIT-04` scanned every file in `libs/app-shell/` and confirmed that all user-facing references use "Accounts".

---

## 3. Caveats

- In headless Vitest test environments, `window.location.reload()` is not implemented by JSDOM; this is safely guarded in `ShellComponent` and `ShellTopbarComponent` via try-catch blocks and does not affect production browser runtime.
- Module-specific page contents (Sales list/create forms) are verified in integration specs (`shell-module-integration.spec.ts`), while presentation within the shell content viewport adheres strictly to the defined z-index contract.

---

## 4. Conclusion

**Verdict: CONFIRMED (Pass)**

Milestone 3 (App Shell Decomposition - `libs/app-shell`) meets all architectural, functional, layout, and UI constraints defined in `ORIGINAL_REQUEST.md` and `AGENTS.md`. All 88 tests across 7 test suites pass cleanly.

---

## 5. Verification Method

To independently reproduce and verify this assessment:

1. Run the Vitest test suite for `libs/app-shell`:
   ```powershell
   cd frontend
   npx vitest run libs/app-shell
   ```
   *Expected Output*: 7 test files passed, 88 tests passed.

2. Run TypeScript typecheck:
   ```powershell
   cd frontend
   npm run typecheck
   ```
   *Expected Output*: Clean exit code 0.

3. Run ESLint on `libs/app-shell`:
   ```powershell
   cd frontend
   npx eslint libs/app-shell
   ```
   *Expected Output*: 0 errors, 0 warnings.
