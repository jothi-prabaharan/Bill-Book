# Handoff Report: Milestone 3 — App Shell Decomposition (`libs/app-shell`)

## 1. Observation
- Monolithic `ShellComponent` in `frontend/libs/app-shell/src/lib/shell/shell.component.ts` housed all navigation rail, topbar, org switching, popup dialogs, and breadcrumbs in a single file.
- Architecture blueprint (`.agents/explorer_m3_1/analysis.md`) and requirements (R2, R5, `PROJECT.md`) required decomposition into **4 standalone components**:
  1. `ShellComponent` (`bb-shell`) in `src/lib/shell/`
  2. `ShellNavComponent` (`bb-shell-nav`) in `src/lib/nav/`
  3. `ShellTopbarComponent` (`bb-shell-topbar`) in `src/lib/topbar/`
  4. `ShellBreadcrumbComponent` (`bb-shell-breadcrumb`) in `src/lib/breadcrumb/`
- Strict UI label rule (R5): UI label for the accounting module is strictly **Accounts** ("Accounting" must never appear anywhere in the UI).
- Public exports required all 4 components in `frontend/libs/app-shell/src/index.ts`.
- Layer stacking hierarchy:
  - Topbar: `z-index: 6`
  - Rail: `z-index: 5`
  - Breadcrumb: `z-index: 4`
  - Content: `z-index: 1`
- Test commands executed:
  - `npx vitest run libs/app-shell`: 5 test files, 53 tests passed (100% pass).
  - `npx nx lint app-shell`: 0 errors, 0 warnings.
  - `npx nx build web --skip-nx-cache`: Application bundle generation complete with zero errors.
  - `npm run test`: 28 test files, 350 tests passed across all frontend libraries.

## 2. Logic Chain
1. **`ShellNavComponent` (`bb-shell-nav`)**:
   - Created with inputs (`userDisplayName`, `userRoleName`) and output (`logout`).
   - Implemented navigation model with 9 core modules where `/accounting` is explicitly labeled `'Accounts'`.
   - Filtered with `auth.canView(item.module)` to reflect user permissions dynamically.
   - Styled with 56px fixed left rail (`z-index: 5`), `--color-ink` ground, active cutout with 4px left accent rule (`box-shadow: inset 4px 0 0 var(--color-accent), inset 13px 0 12px -10px rgba(32,31,29,.55)`), user profile item, and responsive mobile tab bar (<860px).
2. **`ShellTopbarComponent` (`bb-shell-topbar`)**:
   - Created with inputs (`financialYear`, `userDisplayName`, `userRoleName`) and outputs (`organizationChange`, `quickAction`, `logout`).
   - Implemented searchable organization switcher dropdown using `bb-search-input`, showing active company name and branch role.
   - Added display-only financial year tag (`tag tag-outline fy-tag`).
   - Added quick action groups modal (`New transaction` for Sales/Purchase/Banking), Favourites modal, Help, and Sign out button.
   - Integrated HostListeners for escape key and click-outside dismissing.
3. **`ShellBreadcrumbComponent` (`bb-shell-breadcrumb`)**:
   - Created with input (`crumbsInput`) and output (`crumbClick`).
   - Implemented dynamic path resolution from `NavigationEnd` events and constructor URL inspection.
   - Formatted hyphenated paths (`stock-adjustments` -> `Stock adjustments`), expanded `coa` -> `Chart of Accounts`, and strictly mapped `accounting` -> `Accounts`.
   - Embedded action projection host (`<ng-content select="[bbShellActions], .acts" />`) alongside dashboard/register action buttons.
4. **`ShellComponent` (`bb-shell`)**:
   - Implemented as the root CSS Grid layout container:
     `grid-template-columns: 56px 1fr; grid-template-rows: 46px auto 1fr; height: 100dvh; overflow: hidden;`
   - Composed `<bb-shell-nav>`, `<bb-shell-topbar>`, `<bb-shell-breadcrumb>`, and `<main class="shell-content-cell"><router-outlet /></main>`.
   - Handled responsive layout (<860px) stacking topbar, breadcrumb, scrollable content, and mobile bottom nav.
   - Maintained complete backward compatibility for signals, methods, and existing consumers.
5. **Public API & Integration**:
   - Cleanly exported all 4 standalone components and types in `frontend/libs/app-shell/src/index.ts`.
   - Verified that `apps/web/src/app/app.routes.ts` mounts `ShellComponent` seamlessly.

## 3. Caveats
- `window.location.reload()` is invoked on organization switch to refresh app context. In jsdom/unit test environments, this is safely guarded in a `try...catch` block.
- No third-party animation libraries or javascript timers were added; all transitions strictly use 120ms pure CSS transitions.

## 4. Conclusion
Milestone 3 App Shell Decomposition is fully completed. All 4 standalone components (`bb-shell`, `bb-shell-nav`, `bb-shell-topbar`, `bb-shell-breadcrumb`) adhere strictly to architectural contracts, CSS Grid orchestration, z-index hierarchy, design tokens, and the strict UI rule ("Accounts"). All unit and integration test suites pass with 100% success.

## 5. Verification Method
To independently verify the implementation:
1. **Unit & Integration Tests**:
   ```powershell
   cd frontend
   npx vitest run libs/app-shell
   ```
   *Expected output*: 5 test files passed, 53 tests passed.

2. **Full Frontend Test Suite**:
   ```powershell
   cd frontend
   npm run test
   ```
   *Expected output*: 28 test files passed, 350 tests passed.

3. **Lint Verification**:
   ```powershell
   cd frontend
   npx nx lint app-shell
   ```
   *Expected output*: Successfully ran target lint with 0 errors and 0 warnings.

4. **Production Web Build**:
   ```powershell
   cd frontend
   npx nx build web --skip-nx-cache
   ```
   *Expected output*: Successfully generated production bundle with zero errors.

5. **Strict UI Label Rule Verification**:
   - Inspect `frontend/libs/app-shell/src/lib/nav/shell-nav.component.ts`: line 37 verifies `label: 'Accounts'` for path `'/accounting'`.
   - Inspect `frontend/libs/app-shell/src/lib/breadcrumb/shell-breadcrumb.component.ts`: line 75 verifies `label = 'Accounts'` for `accounting` route segment.
   - Run `INT-T1-01` and `INT-T1-02` in `shell-module-integration.spec.ts`.
