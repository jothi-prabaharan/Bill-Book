# Milestone 3 Empirical Challenger Report: App Shell Decomposition (`libs/app-shell`)

**Agent ID**: challenger_m3_2
**Milestone**: Milestone 3 — App Shell Decomposition
**Target Library**: `libs/app-shell`
**Verdict**: **CONFIRMED (Pass)**

---

## 1. Observation

### 1.1 Z-Index Layer Stacking Verification
Direct inspection of CSS/SCSS source rules reveals the exact stacking order:
- **Topbar** (`libs/app-shell/src/lib/shell/shell.component.scss:29`, `libs/app-shell/src/lib/topbar/shell-topbar.component.scss:8`):
  `.shell-topbar-cell { z-index: 6; }`, `.shell-header { z-index: 6; }`
- **Left Rail** (`libs/app-shell/src/lib/shell/shell.component.scss:22`, `libs/app-shell/src/lib/nav/shell-nav.component.scss:8`):
  `.shell-nav-cell { z-index: 5; }`, `.shell-sidebar { z-index: 5; }`
- **Breadcrumb Strip** (`libs/app-shell/src/lib/shell/shell.component.scss:35`, `libs/app-shell/src/lib/breadcrumb/shell-breadcrumb.component.scss:8`):
  `.shell-breadcrumb-cell { z-index: 4; }`, `.crumbs { z-index: 4; }`
- **Sticky Table Header** (`libs/shared/theming/src/lib/_table.scss:81`, `apps/web/src/styles.scss:283`):
  `.table thead th { z-index: 3; box-shadow: inset 0 -1px 0 var(--color-divider); }`
- **Content Outlet** (`libs/app-shell/src/lib/shell/shell.component.scss:44`):
  `.shell-content-cell { z-index: 1; }`
- **Overlays & Dialogs**:
  - Organization dropdown (`libs/app-shell/src/lib/topbar/shell-topbar.component.scss:87`): `z-index: 20;`
  - Mobile bottom more overlay (`libs/app-shell/src/lib/nav/shell-nav.component.scss:168,184`): `z-index: 20;`, panel `z-index: 21;`
  - Modal backdrops (`libs/app-shell/src/lib/topbar/shell-topbar.component.html:69,94`): `z-index: 30;`

### 1.2 Action Projection Verification
- `libs/app-shell/src/lib/breadcrumb/shell-breadcrumb.component.html:15`:
  `<div class="acts"><ng-content select="[bbShellActions], .acts" /> ... </div>`
- Successfully projects child elements carrying `[bbShellActions]` attribute or `.acts` class directly into the breadcrumb header actions toolbar.
- Contextual dashboard controls (Accrual/Cash basis toggle, Customize/Done button) and register controls (Export/Import buttons) conditionally render inside `.acts` alongside projected actions.

### 1.3 Navigation Accessibility & Role Filtering Verification
- Landmark accessibility:
  - Desktop Left Rail: `<nav aria-label="Modules" class="shell-sidebar desktop-nav">`
  - Mobile Tab Navigation: `<nav aria-label="Modules" class="shell-mobile-nav mobile-nav">`
  - Breadcrumbs: `<nav aria-label="Breadcrumb" class="crumbs">`
  - Active breadcrumb current item: `<span aria-current="page">{{ crumb.label }}</span>`
  - Active navigation routes: `routerLinkActive="active"`, styled with 4px left accent rule `box-shadow: inset 4px 0 0 var(--color-accent), inset 13px 0 12px -10px rgba(32, 31, 29, 0.55);`
  - Topbar Organization toggle button: `aria-label="Switch organization"`, `[attr.aria-expanded]="orgOpen()"`
  - Organization list item: `[attr.aria-current]="org.orgId === currentOrgId() ? 'true' : null"`
  - Display-only Financial Year tag: `<span class="tag tag-outline fy-tag" aria-label="Current financial year">{{ financialYear() }}</span>`
- Role Permissions Filtering:
  - `nav = computed(() => this.allNavItems.filter((item) => item.module === null || this.auth.canView(item.module)));`
  - Derived sub-signals (`primaryNav`, `settingsItem`, `mobileTopNav`, `mobileMoreNav`) immediately respect permission lockdowns.

### 1.4 Downstream Route Integration & Strict "Accounts" UI Rule Audit
- Navigation routes configured across 9 core application surfaces: `/dashboard`, `/contacts`, `/inventory`, `/purchase`, `/sales`, `/banking`, `/accounting`, `/reports`, `/settings`.
- Strict UI Rule R5 Audit:
  - `ShellNavComponent.allNavItems`: `{ path: '/accounting', label: 'Accounts', icon: 'accounting', module: 'accounting' }`
  - `ShellBreadcrumbComponent.updateCrumbs`: `else if (label.toLowerCase() === 'accounting') { label = 'Accounts'; }`
  - Zero instances of user-visible "Accounting" string found across templates.
- Deep nested route breadcrumb expansion accurately handles multi-level segments (e.g. `/sales/invoices/new` -> `['Sales', 'Invoices', 'New']`), hyphenated strings (`/inventory/stock-adjustments` -> `['Inventory', 'Stock adjustments']`), and abbreviations (`/accounting/coa` -> `['Accounts', 'Chart of Accounts']`).

### 1.5 Automated Test Suite Execution
- Running `cd frontend && npx vitest run`:
  - **31 test files passed** (including `libs/app-shell/src/lib/app-shell-challenger.spec.ts` 14/14, `adversarial-shell.spec.ts` 21/21, `shell-module-integration.spec.ts` 8/8, `shell.component.spec.ts` 21/21, `shell-topbar.component.spec.ts` 9/9, `shell-breadcrumb.component.spec.ts` 8/8, `shell-nav.component.spec.ts` 7/7).
  - **411 total tests passed** (0 failed).

---

## 2. Logic Chain

1. **Step 1 — Z-Index Invariant**:
   - `Topbar (z: 6) > Left Rail (z: 5) > Breadcrumb Strip (z: 4) > Sticky Table Header (z: 3) > Scrolling Content (z: 1)`.
   - Popups (`z: 20` for org dropdown, `z: 20/21` for mobile overlay sheet, `z: 30` for dialog backdrops) render above all chrome layers.
   - Therefore, scrolling table content or sticky list headers will never overlap or bleed through the breadcrumb bar or top bar.
2. **Step 2 — Action Projection Host**:
   - `ShellBreadcrumbComponent` provides `<ng-content select="[bbShellActions], .acts" />` inside `<div class="acts">`.
   - Child page components projecting buttons via `bbShellActions` attribute or `.acts` class align rightward in the sticky breadcrumb header.
3. **Step 3 — Accessibility & Role Permutations**:
   - Landmark navigation containers carry descriptive `aria-label` attributes (`Modules`, `Breadcrumb`).
   - Active routes are designated visually with high-contrast accent rules and semantically via `aria-current="page"`.
   - Permissions check `auth.canView(module)` is reactive and automatically excludes disallowed modules across desktop and mobile menus.
4. **Step 4 — Downstream Route Integration & Strict UI Requirements**:
   - Route path tokenizer formats URLs cleanly and enforces "Accounts" for `/accounting` and "Chart of Accounts" for `/accounting/coa`.
   - Full test run across all 31 workspace suites passes with 100% success rate (411/411 tests).

---

## 3. Caveats

- **No caveats.** The implementation strictly complies with all layout, accessibility, role-based filtering, and architectural specifications without regressions.

---

## 4. Conclusion

**Verdict: CONFIRMED (Pass)**

Milestone 3 (`libs/app-shell`) meets all architectural, accessibility, visual stacking, and functional specifications:
1. Z-index hierarchy is strictly structured: `Topbar (6) > Rail (5) > Breadcrumb (4) > Table Header (3) > Content (1)`.
2. Action projection slots `[bbShellActions]` and `.acts` correctly into the breadcrumb header.
3. Navigation accessibility (`aria-label`, `aria-current`, `aria-expanded`) and `auth.canView` role filtering are fully verified.
4. All downstream business module routes integrate seamlessly, and the critical UI rule prohibiting the "Accounting" label in favor of "Accounts" is strictly enforced.
5. Workspace test suite passed completely (31 test files, 411 tests passed).

---

## 5. Verification Method

To independently reproduce and verify this empirical challenge:
```powershell
cd C:\Users\Praba\Source\repos\Bill-Book\frontend
npx vitest run libs/app-shell
npx vitest run
```
