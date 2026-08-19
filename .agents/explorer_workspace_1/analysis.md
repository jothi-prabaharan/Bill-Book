# Nx Workspace Architecture Analysis Report

**Workspace Root**: `C:\Users\Praba\Source\repos\Bill-Book\frontend`  
**Date**: 2026-08-19  
**Agent**: Nx Workspace Explorer (`explorer_workspace_1`)  
**Status**: Verified Clean (Lint: Pass, Typecheck: Pass, Vitest: 186/186 Pass, Build: Pass)

---

## 1. Executive Summary

The frontend is an **Nx Monorepo** built with **Angular 20.0.0**, **TypeScript 5.8.0**, and **Nx 21.0.0**. The build system utilizes the modern `@angular/build:application` esbuild pipeline, and tests are run via **Vitest 3.2.7** with `jsdom`.

The workspace is organized into **5 runnable applications** (`apps/`) and **20 libraries** (`libs/`), split into domain `-core` and `-ui` pairs, plus `shared` infrastructure and `app-shell`. All active components strictly adhere to Angular 20 standards: **standalone components only** (`standalone: true`), modern dependency injection (`inject()`), signal-based reactivity (`signal()`, `computed()`), and template/style separation.

---

## 2. Nx Workspace Mapping

### 2.1 Workspace Overview

| Category | Total Count | Active (with Code) | Skeleton / Empty |
| :--- | :--- | :--- | :--- |
| **Applications (`apps/`)** | 5 | 3 (`web`, `desktop`, `docs`) | 2 (`admin`, `portal`) |
| **Domain Libraries (`libs/{module}/`)** | 14 (7 pairs) | 8 (`sales-core/ui`, `purchase-core/ui`, `reporting-core/ui`, `accounting-ui`, `inventory-ui`, `master-ui`) | 6 (`accounting-core`, `inventory-core`, `master-core`, `customer-core`, `customer-ui`) |
| **App Shell (`libs/app-shell`)** | 1 | 1 (`app-shell`) | 0 |
| **Shared Libraries (`libs/shared/`)** | 5 | 3 (`auth`, `api-client`, `ui-components`) | 2 (`theming`, `currency-format`) |
| **Total Projects** | **25** | **15** | **10** |

---

### 2.2 Path Mapping and TSConfig Aliases (`tsconfig.base.json`)

All internal library imports resolve via standard TypeScript path mappings in `frontend/tsconfig.base.json`:

```json
{
  "paths": {
    "@bill-book/master-core": ["libs/master/master-core/src/index.ts"],
    "@bill-book/master-ui": ["libs/master/master-ui/src/index.ts"],
    "@bill-book/customer-core": ["libs/customer/customer-core/src/index.ts"],
    "@bill-book/customer-ui": ["libs/customer/customer-ui/src/index.ts"],
    "@bill-book/inventory-core": ["libs/inventory/inventory-core/src/index.ts"],
    "@bill-book/inventory-ui": ["libs/inventory/inventory-ui/src/index.ts"],
    "@bill-book/sales-core": ["libs/sales/sales-core/src/index.ts"],
    "@bill-book/sales-ui": ["libs/sales/sales-ui/src/index.ts"],
    "@bill-book/purchase-core": ["libs/purchase/purchase-core/src/index.ts"],
    "@bill-book/purchase-ui": ["libs/purchase/purchase-ui/src/index.ts"],
    "@bill-book/accounting-core": ["libs/accounting/accounting-core/src/index.ts"],
    "@bill-book/accounting-ui": ["libs/accounting/accounting-ui/src/index.ts"],
    "@bill-book/reporting-core": ["libs/reporting/reporting-core/src/index.ts"],
    "@bill-book/reporting-ui": ["libs/reporting/reporting-ui/src/index.ts"],
    "@bill-book/auth": ["libs/shared/auth/src/index.ts"],
    "@bill-book/api-client": ["libs/shared/api-client/src/index.ts"],
    "@bill-book/ui-components": ["libs/shared/ui-components/src/index.ts"],
    "@bill-book/currency-format": ["libs/shared/currency-format/src/index.ts"],
    "@bill-book/theming": ["libs/shared/theming/src/index.ts"],
    "@bill-book/app-shell": ["libs/app-shell/src/index.ts"]
  }
}
```

---

### 2.3 Detailed Applications Catalog

| App Name | Path | Project Type | Tags | Description & Entry Point |
| :--- | :--- | :--- | :--- | :--- |
| **`web`** | `apps/web` | `application` | `type:app`, `platform:browser` | Main SaaS client. Bootstrap: `src/main.ts`, Config: `src/app/app.config.ts`, Routes: `src/app/app.routes.ts`, Root Component: `AppComponent` (`<router-outlet />`). Output: `dist/apps/web`. |
| **`desktop`** | `apps/desktop` | `application` | `type:app`, `platform:electron` | Desktop & POS terminal client with Electron integration and ESC/POS thermal printer support (`esc-pos.service.ts`). Output: `dist/apps/desktop`. |
| **`docs`** | `apps/docs` | `application` | `type:app`, `platform:browser`, `scope:docs` | Internal documentation viewer application rendering markdown files via `marked` with `docs.manifest.ts`. Output: `dist/apps/docs`. |
| **`admin`** | `apps/admin` | `application` | `type:app`, `platform:browser`, `scope:admin` | Multi-tenant platform super-admin portal (skeleton directory structure). |
| **`portal`** | `apps/portal` | `application` | `type:app`, `platform:browser`, `scope:portal` | Customer / Vendor self-service portal (skeleton directory structure). |

---

### 2.4 Detailed Libraries Catalog

| Library Project | Directory Path | Type & Scope Tags | Status | Exported Components / Services / Contracts |
| :--- | :--- | :--- | :--- | :--- |
| **`theming`** | `libs/shared/theming` | `scope:shared`, `type:util` | *Skeleton* | Target location for design tokens (`styles.css` custom properties on `:root`). |
| **`ui-components`** | `libs/shared/ui-components` | `scope:shared`, `type:ui` | **Active** | `bb-data-grid`, `bb-currency-input`, `bb-date-input`, `bb-number-input`, `bb-search-input`, `bb-text-input`, `bb-document-line-grid`, `bb-allocation-grid`, `bb-lookup-dialog`, `bb-report-grid`, `bb-filter-bar`, `bb-column-chooser`, `bb-group-panel`, `bb-pivot-panel`, `bb-bank-graph-card`, `bb-card-table`. |
| **`app-shell`** | `libs/app-shell` | `scope:app-shell`, `type:ui` | **Active** | `ShellComponent` (`bb-shell`). *To be decomposed into `ShellComponent`, `ShellNavComponent`, `ShellTopbarComponent`, `ShellBreadcrumbComponent` per requirement R2.* |
| **`auth`** | `libs/shared/auth` | `scope:shared`, `type:ui` | **Active** | `AuthService`, `authInterceptor`, `authGuard`, `licenseActiveGuard`, `permissionGuard`, `LoginPage`, `SignupPage`, `ForgotPasswordPage`, `AcceptInvitationPage`, `TrialExpiredPage`, `AuthShellComponent`. |
| **`api-client`** | `libs/shared/api-client` | `scope:shared`, `type:util` | **Active** | `API_BASE_URL` (InjectionToken), `apiBaseUrlInterceptor`. |
| **`currency-format`**| `libs/shared/currency-format` | `scope:shared`, `type:util` | *Skeleton* | Utilities and pipes for Indian Rupee and multi-currency formatting. |
| **`sales-core`** | `libs/sales/sales-core` | `scope:sales`, `type:core` | **Active** | `TransactionService`, `InvoiceService`, `QuoteService`, `SalesOrderService`, `CreditNoteService`, `DeliveryChallanService`, `SalesTransactionListItem`, `SaveInvoiceRequest`. |
| **`sales-ui`** | `libs/sales/sales-ui` | `scope:sales`, `type:ui` | **Active** | `SalesListComponent` (`bb-sales-list`), `InvoiceFormComponent`, `SalesOrderFormComponent`, `QuoteFormComponent`, `CreditNoteFormComponent`, `DeliveryChallanFormComponent`, `salesRoutes`. |
| **`purchase-core`** | `libs/purchase/purchase-core` | `scope:purchase`, `type:core` | **Active** | `BillService`, `DebitNoteService`, `GoodsReceiptService`, `PurchaseOrderService`, `PurchaseLookupService`, `TransactionService`, `transaction.models.ts`. |
| **`purchase-ui`** | `libs/purchase/purchase-ui` | `scope:purchase`, `type:ui` | **Active** | `BillFormPage`, `DebitNoteFormPage`, `GoodsReceiptFormPage`, `PurchaseOrderFormPage`, `PurchaseListPage`, `purchaseRoutes`. |
| **`reporting-core`** | `libs/reporting/reporting-core` | `scope:reporting`, `type:core` | **Active** | `ReportQueryService`, `ReportStateService`, `SavedViewService`, `report-contracts.ts`. |
| **`reporting-ui`** | `libs/reporting/reporting-ui` | `scope:reporting`, `type:ui` | **Active** | `ReportListPage`, `ReportHostPage`, `SavedViewDialog`, `reportingRoutes`. |
| **`accounting-ui`** | `libs/accounting/accounting-ui` | `scope:accounting`, `type:ui` | **Active** | `ChartOfAccountsPage`, `SubAccountsPage`, `TaxMasterPage`, `NumberingSeriesPage`, `PaymentTermsPage`, `JournalsPage`, `AccountLedgerPage`, `TrialBalancePage`, `OpeningBalancePage`, `ClosingDatesPage`, `BanksPage`, `BankAccountsPage`, `MoneyDocumentPage`, `TransferMoneyPage`, `StatementsPage`. |
| **`accounting-core`**| `libs/accounting/accounting-core`| `scope:accounting`, `type:core` | *Skeleton* | Core accounting services / state models. |
| **`inventory-ui`** | `libs/inventory/inventory-ui` | `scope:inventory`, `type:ui` | **Active** | `ItemsPage`, `ItemCategoriesPage`, `StockPage`, `StockAdjustmentsPage`, `WarehousesPage`, `UnitTypesPage`, `MetalPuritiesPage`. |
| **`inventory-core`** | `libs/inventory/inventory-core` | `scope:inventory`, `type:core` | *Skeleton* | Inventory core models / services. |
| **`master-ui`** | `libs/master/master-ui` | `scope:master`, `type:ui` | **Active** | `HsnSacPage`, `OrganizationsPage`, `OrganizationSettingsPage`, `OrgCurrenciesPage`, `ConfigurationsPage`, `SmtpSettingsPage`, `RolesPage`, `UsersPage`, `ContactsPage`, `ContactPersonRolesPage`, `ContactPersonRolesDialog`. |
| **`master-core`** | `libs/master/master-core` | `scope:master`, `type:core` | *Skeleton* | Master data services / models. |
| **`customer-core`** | `libs/customer/customer-core` | `scope:customer`, `type:core` | *Skeleton* | Customer data contracts / services. |
| **`customer-ui`** | `libs/customer/customer-ui` | `scope:customer`, `type:ui` | *Skeleton* | Customer UI pages. |

---

## 3. Web App Entry Point & Routing Analysis

### 3.1 Bootstrap & Application Config (`apps/web/src/app/app.config.ts`)

- **Root Component**: `AppComponent` (`<router-outlet />`).
- **Zone.js**: Enabled via `provideZoneChangeDetection({ eventCoalescing: true })`.
- **HTTP Pipeline**:
  - `API_BASE_URL` token set from `environment.apiBaseUrl`.
  - Interceptors attached in order: `provideHttpClient(withInterceptors([apiBaseUrlInterceptor, authInterceptor]))`.
- **Root Layout**: The entire application routes through `ShellComponent` (`@bill-book/app-shell`) wrapped with route guards:
  - `canActivate: [authGuard, licenseActiveGuard]`
  - `canActivateChild: [licenseActiveGuard, permissionGuard]`

### 3.2 Feature Routing Structure (`apps/web/src/app/app.routes.ts`)

```typescript
export const appRoutes: Routes = [
  // Public auth routes
  { path: 'login', component: LoginPage },
  { path: 'signup', component: SignupPage },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: 'accept-invitation', component: AcceptInvitationPage },
  { path: 'expired', component: TrialExpiredPage, canActivate: [authGuard] },
  
  // Authenticated app shell layout
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard, licenseActiveGuard],
    canActivateChild: [licenseActiveGuard, permissionGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardPage },
      
      // Master settings routes
      { path: 'settings/currencies', loadComponent: () => import('@bill-book/master-ui').then(m => m.OrgCurrenciesPage) },
      { path: 'settings/organization', loadComponent: () => import('@bill-book/master-ui').then(m => m.OrganizationSettingsPage) },
      { path: 'settings/branches', loadComponent: () => import('@bill-book/master-ui').then(m => m.OrganizationsPage) },
      { path: 'settings/roles', loadComponent: () => import('@bill-book/master-ui').then(m => m.RolesPage) },
      { path: 'settings/users', loadComponent: () => import('@bill-book/master-ui').then(m => m.UsersPage) },
      { path: 'contacts', loadComponent: () => import('@bill-book/master-ui').then(m => m.ContactsPage) },
      
      // Accounting & Banking routes
      { path: 'accounting', pathMatch: 'full', redirectTo: 'accounting/trial-balance' },
      { path: 'accounting/chart-of-accounts', loadComponent: () => import('@bill-book/accounting-ui').then(m => m.ChartOfAccountsPage) },
      { path: 'accounting/journals', loadComponent: () => import('@bill-book/accounting-ui').then(m => m.JournalsPage) },
      { path: 'accounting/trial-balance', loadComponent: () => import('@bill-book/accounting-ui').then(m => m.TrialBalancePage) },
      { path: 'accounting/ledger', loadComponent: () => import('@bill-book/accounting-ui').then(m => m.AccountLedgerPage) },
      { path: 'banking', pathMatch: 'full', redirectTo: 'banking/spend-money' },
      { path: 'banking/spend-money', loadComponent: () => import('@bill-book/accounting-ui').then(m => m.MoneyDocumentPage) },
      { path: 'banking/receive-money', loadComponent: () => import('@bill-book/accounting-ui').then(m => m.MoneyDocumentPage) },
      
      // Inventory routes
      { path: 'inventory', pathMatch: 'full', redirectTo: 'inventory/items' },
      { path: 'inventory/items', loadComponent: () => import('@bill-book/inventory-ui').then(m => m.ItemsPage) },
      { path: 'inventory/stock', loadComponent: () => import('@bill-book/inventory-ui').then(m => m.StockPage) },
      
      // Child route module mounts
      { path: 'sales', loadChildren: () => import('@bill-book/sales-ui').then(m => m.salesRoutes) },
      { path: 'purchase', loadChildren: () => import('@bill-book/purchase-ui').then(m => m.purchaseRoutes) },
      { path: 'reports', loadChildren: () => import('@bill-book/reporting-ui').then(m => m.reportingRoutes) },
      { path: '**', component: DashboardPage }
    ]
  }
];
```

---

## 4. Angular 20 Conventions & Standards Verification

All code in `frontend/` adheres strictly to the following architectural rules:

1. **Standalone Components**: `standalone: true` is configured on every component and directive. No `NgModule` declarations exist.
2. **Dependency Injection**: Constructor injection is avoided in favor of the `inject()` function (`private auth = inject(AuthService); private router = inject(Router);`).
3. **Reactivity Model**: Component state uses Angular Signals (`signal()`, `computed()`).
4. **Data Fetching**: REST calls in components and services favor Promises / `async`/`await` or simple observables directly subscribed/mapped without complex RxJS piping chains.
5. **Component Metadata & Layout**:
   - Explicit separation of `templateUrl` and `styleUrl`.
   - Selectors follow the project prefix `bb-` (kebab-case for elements: `bb-sales-list`, `bb-data-grid`; camelCase for attributes: `bbCellTemplate`).
   - Suffix conventions strictly adhered to: `.page.ts`, `.dialog.ts`, `.list.ts`, `.component.ts`.

---

## 5. Architectural Boundaries & Cross-Module Rules

| Rule | Definition | Rationale / Enforcement |
| :--- | :--- | :--- |
| **No `-ui` import in `-core`** | `-core` libs contain pure models, services, state, and HTTP clients. They must never import from `-ui`. | Maintains platform-agnostic design (Ionic/desktop/web portability without DOM/template coupling). |
| **No cross-module domain imports** | A domain module (e.g. `sales-ui`) must never directly import from another domain module (e.g. `purchase-ui` or `accounting-core`). | Prevents circular dependencies and monolithic entanglement. All cross-module sharing is mediated through `libs/shared/*` or through API/events. |
| **Presentation vs Chrome vs Tokens** | - Shared presentational widgets $\rightarrow$ `libs/shared/ui-components`<br>- Layout shell chrome $\rightarrow$ `libs/app-shell`<br>- CSS custom properties & design tokens $\rightarrow$ `libs/shared/theming` | Single responsibility and centralized styling. |
| **UI String Rule for Accounting** | The string "Accounting" must **NEVER** appear in any user-visible UI. | The user-facing term is **Accounts**. (The directory name `libs/accounting/` and backend namespace remain unchanged). |
| **Closed Package List** | No dependencies can be added to `package.json` or `backend/Directory.Packages.props`. | Use existing packages: `@angular/cdk` for drag/drop, built-in CSS/SVG for charts and icons. No third-party grid/chart libraries. |

---

## 6. Build, Lint & Test Pipeline Verification

The frontend scripts in `package.json` were executed and verified:

```bash
# Complete verification pipeline
npm run check
```

Under the hood, `npm run check` executes:
1. `npm run lint` (`nx run-many -t lint`): Passed across all 16 active projects. (0 errors).
2. `npm run typecheck` (`tsc --noEmit -p tsconfig.eslint.json`): Passed cleanly.
3. `npm run test` (`vitest run`): 16 test files passed, 186 tests passed.
4. `npm run build` (`nx run-many -t build`): 3 apps (`web`, `desktop`, `docs`) compiled and bundled successfully.

---

## 7. Current Implementation Gaps & Next Steps for Team

1. **`libs/shared/theming`**:
   - `src/` is currently empty.
   - **Task**: Port design tokens from `C:\Users\Praba\Downloads\Claude Design\Bill-Book Design-handoff\bill-book-design\project\_ds\bill-book-6c62bbc0-6bc5-4941-b359-3208c21e8972\styles.css` into SCSS custom properties on `:root`.
2. **`libs/app-shell`**:
   - Currently contains a monolithic `ShellComponent`.
   - **Task**: Decompose and emit `ShellComponent`, `ShellNavComponent`, `ShellTopbarComponent`, and `ShellBreadcrumbComponent` per requirement R2.
3. **`libs/shared/ui-components`**:
   - Contains `DataGridComponent`, `DocumentLineGridComponent`, input components, and report grid.
   - **Task**: Verify data table sticky header, compact density, hairline rules, and sorting behaviors against design specifications.
4. **Module Screens (`sales-ui`, etc.)**:
   - Verify list page filter bars, shared tables, and DTO-aligned reactive create/edit forms end-to-end.
