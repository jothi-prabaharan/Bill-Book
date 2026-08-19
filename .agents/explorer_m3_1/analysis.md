# Milestone 3: App Shell Decomposition (`libs/app-shell`) Analysis & Blueprint

## 1. Executive Summary
The Bill-Book desktop application shell currently houses navigation, top bar controls, organization switching, action modals, and dynamic breadcrumbs within a single monolithic `ShellComponent` (`frontend/libs/app-shell/src/lib/shell/shell.component.ts`).

To achieve full modularity, maintainability, and alignment with `PROJECT.md` and the design reference `Shell.dc.html`, `libs/app-shell` must be decomposed into **4 distinct, standalone Angular 20 components**:
1. **`ShellComponent` (`bb-shell`)**: Root CSS Grid container (`grid-template-columns: 56px 1fr; grid-template-rows: 46px auto 1fr; height: 100dvh; overflow: hidden;`) coordinating the rail, topbar, breadcrumb strip, scrolling `<router-outlet />`, and mobile responsiveness.
2. **`ShellNavComponent` (`bb-shell-nav`)**: 56px fixed left rail (`z-index: 5`, ink ground `--color-ink`), module navigation items, active cutout rule with 4px left accent rule, bottom user profile menu, and mobile bottom tab bar navigation (<860px).
3. **`ShellTopbarComponent` (`bb-shell-topbar`)**: 46px sticky top bar (`z-index: 6`, surface/neutral ground), searchable organization dropdown switcher with search filter, display-only FY tag, and action group buttons (`New`, `Favourites`, `Help`, `Sign out`).
4. **`ShellBreadcrumbComponent` (`bb-shell-breadcrumb`)**: Sticky breadcrumb strip (`z-index: 4`) replacing `<h1>` headings, dynamic path resolution from active route, and right-aligned module action host (`<ng-content select="[bbShellActions]" />` / `.acts`).

All 4 components must be exported cleanly from `frontend/libs/app-shell/src/index.ts`.
Crucially, the UI label for the accounting module is strictly **Accounts** ("Accounting" must never appear anywhere in the UI).

---

## 2. Architectural Layering & Z-Index Stacking Hierarchy

As specified in `PROJECT.md` and design tokens:
| Layer / Element | Component / Selector | Z-Index | Position / Height | Background / Visual Style |
|---|---|---|---|---|
| **Top Bar Header** | `bb-shell-topbar` | `z-index: 6` | Sticky / Fixed top, 46px | `--color-bg`, bottom border `1px solid var(--color-divider)`, shadow `0 8px 20px -10px rgba(32,31,29,.45), var(--shadow-md)` |
| **Fixed Left Rail** | `bb-shell-nav` | `z-index: 5` | Fixed left, 56px | Dark ink `--color-ink`, `#f3f2f2` text, shadow `var(--shadow-lg)` |
| **Breadcrumb Strip** | `bb-shell-breadcrumb` | `z-index: 4` | Sticky under topbar, auto height | `--color-bg`, bottom border `1px solid var(--color-divider)` |
| **Data Table Header** | `thead th` in `bb-data-table` | `z-index: 3` | Sticky `top: 0` in table scroll | Solid surface ground with inset shadow `inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent)` |
| **Overlay Modals / Sheets** | Dialogs, Popups, More sheet | `z-index: 20-30` | Fixed backdrop | Dimmed backdrop, surface card |
| **Content Outlet** | `<main class="shell-main">` | `z-index: 1` | Grid area `2 / 3`, flex-1, `overflow-y: auto` | Scrollable viewport |

### CSS Grid Layout Blueprint
```scss
:host {
  display: block;
  height: 100dvh;
  overflow: hidden;
}

.shell-grid-container {
  display: grid;
  grid-template-columns: 56px 1fr;
  grid-template-rows: 46px auto 1fr;
  height: 100dvh;
  width: 100vw;
  overflow: hidden;
  background: var(--color-bg);
  color: var(--color-text);
  font-family: var(--font-body);
}

.shell-nav-cell {
  grid-column: 1;
  grid-row: 1 / span 3;
  z-index: 5;
}

.shell-topbar-cell {
  grid-column: 2;
  grid-row: 1;
  z-index: 6;
}

.shell-breadcrumb-cell {
  grid-column: 2;
  grid-row: 2;
  z-index: 4;
}

.shell-content-cell {
  grid-column: 2;
  grid-row: 3;
  min-height: 0;
  min-width: 0;
  overflow-y: auto;
  z-index: 1;
  padding: var(--space-4) var(--space-4) var(--space-6);
}

/* Responsive breakpoint: <= 860px */
@media (max-width: 860px) {
  .shell-grid-container {
    grid-template-columns: 1fr;
    grid-template-rows: 46px auto 1fr;
  }
  .shell-nav-cell {
    grid-column: 1;
    grid-row: auto;
  }
  .shell-topbar-cell {
    grid-column: 1;
    grid-row: 1;
  }
  .shell-breadcrumb-cell {
    grid-column: 1;
    grid-row: 2;
  }
  .shell-content-cell {
    grid-column: 1;
    grid-row: 3;
  }
}
```

---

## 3. Detailed Component Specifications

### 3.1. `ShellNavComponent` (`bb-shell-nav`)
- **File Location**: `libs/app-shell/src/lib/nav/shell-nav.component.ts` (with `.html`, `.scss`, `.spec.ts`)
- **Selector**: `bb-shell-nav`
- **Imports**: `RouterLink`, `RouterLinkActive`, `CommonModule` / `@if`, `@for`, `@switch`
- **Injected Services**: `AuthService` (for `canView(module)` and `logout()`)
- **Inputs**:
  - `userDisplayName = input<string>('Praba')`
  - `userRoleName = input<string>('Owner')`
- **Outputs**:
  - `logout = output<void>()`
- **Navigation Model**:
  ```typescript
  export interface NavItem {
    path: string;
    label: string;
    icon: string;
    module: string | null;
  }

  readonly allNavItems: NavItem[] = [
    { path: '/dashboard', label: 'Home', icon: 'home', module: null },
    { path: '/contacts', label: 'Contacts', icon: 'contacts', module: 'contacts' },
    { path: '/inventory', label: 'Inventory', icon: 'inventory', module: 'inventory' },
    { path: '/purchase', label: 'Purchase', icon: 'purchase', module: 'purchase' },
    { path: '/sales', label: 'Sales', icon: 'sales', module: 'sales' },
    { path: '/banking', label: 'Banking', icon: 'banking', module: 'banking' },
    { path: '/accounting', label: 'Accounts', icon: 'accounting', module: 'accounting' }, // STRICT: "Accounts"
    { path: '/reports', label: 'Reports', icon: 'reports', module: 'reports' },
    { path: '/settings', label: 'Settings', icon: 'settings', module: 'settings' },
  ];
  ```
- **Active Cutout Styling**:
  ```scss
  .rail-item.active {
    position: relative;
    z-index: 1;
    color: var(--color-accent-700);
    background: var(--color-bg);
    margin: 0 -4px 0 -4px;
    padding-right: 4px;
    padding-left: 4px;
    border-radius: 0;
    box-shadow: inset 4px 0 0 var(--color-accent), inset 13px 0 12px -10px rgba(32, 31, 29, .55);
  }
  ```

---

### 3.2. `ShellTopbarComponent` (`bb-shell-topbar`)
- **File Location**: `libs/app-shell/src/lib/topbar/shell-topbar.component.ts` (with `.html`, `.scss`, `.spec.ts`)
- **Selector**: `bb-shell-topbar`
- **Imports**: `SearchInputComponent`, `FormsModule`, `RouterLink`
- **Injected Services**: `AuthService`, `Router`, `ElementRef`
- **Inputs**:
  - `financialYear = input<string>('FY 2026-27')`
- **Outputs**:
  - `organizationChange = output<string>()`
  - `quickAction = output<string>()`
  - `logout = output<void>()`
- **Internal Signals / State**:
  - `orgOpen = signal(false)`
  - `orgQuery = signal('')`
  - `allOrgs = signal<AccessibleOrg[]>([])`
  - `newOpen = signal(false)`
  - `favOpen = signal(false)`
  - `currentOrgId = computed(() => localStorage.getItem('bb.orgId') || 'org-1')`
  - `currentOrgName = computed(() => 'Eternal Pathway')`
  - `currentOrgRole = computed(() => ...)`
  - `filteredOrgs = computed(() => ...)`
- **Key Methods**:
  - `toggleOrg()`, `setOrgQuery(query)`, `pickOrg(orgId)`
  - `openNew()`, `closeNew()`, `openFav()`, `closeFav()`
  - `onEscape()` (HostListener on `document:keydown.escape`)
  - `onClickOutside(target)` (HostListener on `document:click`)
  - `doLogout()`

---

### 3.3. `ShellBreadcrumbComponent` (`bb-shell-breadcrumb`)
- **File Location**: `libs/app-shell/src/lib/breadcrumb/shell-breadcrumb.component.ts` (with `.html`, `.scss`, `.spec.ts`)
- **Selector**: `bb-shell-breadcrumb`
- **Imports**: `RouterLink`
- **Injected Services**: `Router`
- **Inputs**:
  - `crumbsInput = input<{ label: string; isLink: boolean; isLast: boolean; path?: string }[] | null>(null)`
- **Outputs**:
  - `crumbClick = output<{ label: string; path?: string }>()`
- **Internal Signals / State**:
  - `crumbs = signal<{ label: string; isLink: boolean; isLast: boolean; path?: string }[]>([])`
  - `isHome = computed(() => this.router.url === '/' || this.router.url.startsWith('/dashboard'))`
  - `isRegister = computed(() => ...)`
  - `base = signal(false)`
  - `baseLabel = signal('Accrual basis')`
  - `editing = signal(false)`
- **Path Resolution Logic**:
  - Automatically splits `urlAfterRedirects` / `router.url` on `/`.
  - Empty or `/dashboard` -> `[]` (replaces traditional `<h1>`).
  - Capitalizes words and handles hyphens (`stock-adjustments` -> `Stock adjustments`).
  - Special abbreviation expansions (`coa` -> `Chart of Accounts`, `accounting` -> `Accounts`).

---

### 3.4. `ShellComponent` (`bb-shell`)
- **File Location**: `libs/app-shell/src/lib/shell/shell.component.ts` (with `.html`, `.scss`, `.spec.ts`)
- **Selector**: `bb-shell`
- **Imports**: `RouterOutlet`, `ShellNavComponent`, `ShellTopbarComponent`, `ShellBreadcrumbComponent`
- **Injected Services**: `AuthService`, `Router`, `ElementRef`
- **Role**: Coordinates the overall grid layout, hosts `<router-outlet />`, and maintains full backward compatibility for existing consumers and tests.

---

## 4. Public API & Clean Library Exports
File: `frontend/libs/app-shell/src/index.ts`
```typescript
// Components
export * from './lib/shell/shell.component';
export * from './lib/nav/shell-nav.component';
export * from './lib/topbar/shell-topbar.component';
export * from './lib/breadcrumb/shell-breadcrumb.component';

// Models / Types (if applicable)
export type { NavItem } from './lib/nav/shell-nav.component';
```

---

## 5. Verification of the Forbidden "Accounting" String Rule
- Rule R5 mandates: Folder names remain as-is (`libs/accounting/`), but all UI labels, navigation buttons, breadcrumb titles, and screen headers MUST strictly be **Accounts**.
- **Audit Verification**:
  1. `ShellNavComponent`: `allNavItems` specifies `{ path: '/accounting', label: 'Accounts', icon: 'accounting', module: 'accounting' }`.
  2. `ShellBreadcrumbComponent`: URL path mapping for `/accounting` derives crumb label `'Accounts'`.
  3. `shell-module-integration.spec.ts` executes a forensic template audit checking all `.html` templates for forbidden `Accounting` strings.

---

## 6. Implementation Checklist for Worker
- [ ] Create `libs/app-shell/src/lib/nav/` (`shell-nav.component.ts`, `.html`, `.scss`, `.spec.ts`).
- [ ] Create `libs/app-shell/src/lib/topbar/` (`shell-topbar.component.ts`, `.html`, `.scss`, `.spec.ts`).
- [ ] Create `libs/app-shell/src/lib/breadcrumb/` (`shell-breadcrumb.component.ts`, `.html`, `.scss`, `.spec.ts`).
- [ ] Refactor `libs/app-shell/src/lib/shell/` (`shell.component.ts`, `.html`, `.scss`, `.spec.ts`) to compose the 3 subcomponents into the CSS Grid container.
- [ ] Update `libs/app-shell/src/index.ts` to export all 4 components.
- [ ] Run `npx vitest run libs/app-shell` to ensure 100% test pass.
- [ ] Run `npm test` to verify zero regression across the entire frontend suite (314+ tests).
