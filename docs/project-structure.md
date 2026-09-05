# Project Structure

This is a multi-tenant retail ERP and accounting system.

## Root Directories
- `backend/`: The .NET 10 solution containing the API and various bounded context modules.
- `frontend/`: The client application (Angular v20/Nx monorepo style).
- `docs/`: Unified documentation containing architecture flows, module schemas, and project standards.
- `scripts/`: Powershell and SQL scripts for local developer setup (e.g., setting up the dev database and seeding it).

## Backend Modules (`backend/Api/`)
- `Accounting`
- `Customer`
- `Inventory`
- `Master`
- `Purchase`
- `Reporting`
- `Sales`

Each module generally maintains its own domain entities, EF Core DbContext, migrations, and API endpoints.

### Transaction Architecture Rule
For any transactional document (e.g., Invoice), create distinct tables: `[TransactionName]`, `[TransactionName]Details`, `[TransactionName]Tax`, `[TransactionName]StockMovement`, and `[TransactionName]Ledger`. A dedicated Ledger posting table and a Stock Movement table must be created for each specific transaction type.

---

## Frontend (Nx + Angular) — layout and conventions

The frontend is an Nx monorepo. We use apps/ for runnable applications and libs/ for reusable code. Libraries are split into `-core` (view-models, services, models, no templates or direct DOM access) and `-ui` (presentational components and pages).

Guiding principle: apps orchestrate, -core contains behaviour and side-effects, -ui contains presentational components.

### Apps vs libs
- apps/
  - `web`, `portal`, `admin`, `desktop`, `docs` — full applications that compose libs and provide routes.
- libs/
  - `libs/{module}/{module}-core` — models, services, state, HTTP clients, facades. Must be platform-agnostic (no `window`, `document`, Node, or Electron APIs).
  - `libs/{module}/{module}-ui` — presentational components, pages and shared UI widgets.
  - `libs/shared/` — shared theme, tokens, utilities, and small wrappers for platform-specific features.

**Global Component Rule (STRICT STOP RULE)**: You must exclusively use existing global shared components for all UI elements (grids, buttons, inputs). If a component does not exist for a specific use case, development must STOP and explicit confirmation must be obtained from the user before creating a new one.

### Component folder layout
- Each reusable component lives in its own folder and contains at minimum:
  - `my-widget.component.ts`
  - `my-widget.component.html`
  - `my-widget.component.scss` (or .css)
  - `my-widget.component.spec.ts` (unit tests) or `my-widget.component.test.ts`
- Example path: `libs/{module}/{module}-ui/src/lib/my-widget/`.

### Naming conventions
- Files: kebab-case (e.g. `my-widget.component.ts`).
- Component classes: PascalCase with suffix `Component` (e.g. `MyWidgetComponent`).
- Selectors: kebab-case prefixed with `bb-` (project prefix). Example: `selector: 'bb-my-widget'`.

### Change detection & performance
- Prefer `ChangeDetectionStrategy.OnPush` for all components unless there's a documented reason not to.
- Prefer Angular Signals and pure computations in `-core` libs; use RxJS where Streams/observables are appropriate for complex async flows.
- Keep components small and focused; prefer composition over large monolithic components.

### Standalone components vs NgModules
- Prefer standalone components for small, reusable widgets and for route pages where convenient.
- Use feature modules when grouping related routes, or when a logical boundary benefits from its own module.
- Follow Nx generator defaults unless there is a strong reason to deviate.

### Container vs Presentational separation
- Container (page) components live under `apps/` and implement data fetching, permission checks and orchestration.
- Presentational components live in `libs/*-ui` and only accept Inputs/emit Outputs.
- State, HTTP calls and side-effects belong in `-core` libs or in the app's facade service. `-ui` libs must remain side-effect free.

### Inputs / Outputs best-practices
- Inputs are treated as immutable by components — never mutate input objects in-place.
- Avoid two-way binding (`[(ngModel)]`) on publicly exposed Inputs. Use `@Output()` events to communicate changes.

### Dependency injection and services
- Place domain services (API clients, facades, stores) in `-core` libs. Provide them from the app or core libs, not from `-ui` libs.
- If platform-specific APIs (window/document, printers, USB) are required, wrap them behind an injectable interface and provide the platform implementation in the app (not in `-core`).

### Lazy loading and routing
- Pages/routes should be lazy-loaded with separate route modules where it reduces initial bundle size.
- Avoid bundling unrelated pages in the same eagerly loaded module.

### Styling
- Keep component styles encapsulated. Use SCSS tokens and variables defined in `libs/shared/theme`.
- Follow the project naming convention for CSS classes (e.g., BEM or agreed project style) so global styles don't conflict.

### Accessibility (a11y)
- Interactive components must support keyboard navigation and provide appropriate ARIA attributes where necessary.
- Run automated a11y checks in CI for pages and address critical failures before merging.

### Testing & CI
- Every component should have unit tests (Vitest). Use Angular testing helpers or host-component patterns as appropriate.
- The frontend pre-check is `npm run check` which includes lint, typecheck, test and build. Run it locally before declaring a page/component as done.
- Maintain test coverage for critical UI flows (login, org-switch, invoice pages) and fix regressions.

### Documentation
- Ship documentation with any user-visible UI change. Add/update a page under `frontend/apps/docs/content/` and update `docs.manifest.ts` to include the new/changed doc.
- For public `-ui` components, include a short usage example, the Inputs/Outputs table and any required tokens/themes in the component's docs.

---

## Suggested coding role & expectations (frontend + backend)

These are short, actionable responsibilities to keep work consistent and reviewable.

- Authoring code
  - Follow repository conventions: project layout in `docs/project-structure.md` and the decisions in `CLAUDE.md`.
  - Keep each change small and self-contained. Ship documentation with the change in the same commit.
  - Run and pass required checks locally before committing: `dotnet build && dotnet test` for backend, and `npm run check` for frontend.

- Testing
  - Add unit tests and, where applicable, integration tests for new behaviour.
  - For backend that depends on Postgres features (deferred constraints, RLS), prefer tests that run against a real Postgres instance or provide a clear reason when using in-memory substitutes.

- Code reviews
  - Provide a short PR description with what changed and why, and list any follow-ups.
  - Include screenshots or brief reproduction steps for UI changes.
  - If scope grows during implementation, stop and propose a short plan before continuing.

- Commits & branches
  - Follow the project policy in `CLAUDE.md` regarding `main` as the primary branch. (If alternative branching is introduced, document the change clearly.)
  - Write clear commit messages: concise summary, followed by a short body explaining reason and impact.

- Documentation
  - Update docs for public behaviour changes (API, UI, provisioning). For UI include usage examples and expected screens.

---

## Back-end notes

(Existing backend layout notes belong in CLAUDE.md but repeated here for developer convenience)

- Each service generally has three projects: `{Module}.Entity`, `{Module}.Repository`, `{Module}.Api`.
- Dependency direction: `Api` → `Repository` → `Entity` → `Shared.Kernel`.




# Project: Bill-Book Desktop Application Shell & Module Screens

## Architecture
- **Framework**: Angular 20.0.0 (Standalone Components, `inject()`, `signal()`, `computed()`), TypeScript 5.8.0, Nx 21.0.0.
- **Microservices Gateway**: YARP Gateway at `/api/` routing to `Master.Api`, `Sales.Api`, `Purchase.Api`, `Inventory.Api`, `Accounting.Api`, `Reporting.Api`.
- **Layout Stacking & Z-Index Layering**:
  - Top Bar Header: `z-index: 6` (sticky, 46px)
  - Fixed Left Rail: `z-index: 5` (fixed, 56px, dark ink ground)
  - Breadcrumb Strip: `z-index: 4` (sticky under topbar, replaces `<h1>` headings, hosts module actions)
  - Sticky Table Header: `z-index: 3` (sticky, `top: 0`, solid surface ground with inset bottom shadow rule)
  - Table Rows & Content: `z-index: 1`
- **Design Language ("Classical")**:
  - Color applied as stroke (borders, hairline rules, underlines), never filled blocks.
  - Whisper drop shadows (`color-mix(in srgb, #2d2b2b 14%, transparent)`).
  - Cormorant Garamond / Lora typography pairing.
  - Tabular numerals (`font-feature-settings: "tnum"`) for financial tables and KPI figures.
  - Pure CSS interaction states and themed outline focus (`:focus-visible`).

## Feature Inventory
| # | Feature | Description | Milestone | Source | Status |
|---|---------|-------------|-----------|--------|--------|
| 1 | SCSS Design Tokens (`:root`) | Color ramps, neutral 100-900, accent 100-900, accent-2, fonts, whisper shadows, compact spacing scale | M1 | `styles.css`, R1 | DONE |
| 2 | Theming Partials Architecture | `_tokens.scss`, `_typography.scss`, `_buttons.scss`, `_forms.scss`, `_cards.scss`, `_tags.scss`, `_table.scss` in `shared/theming` | M1 | R1, R5 | DONE |
| 3 | Stroke-Over-Fill Styling & Whisper Shadows | Global utility classes for outlined buttons, bordered cards, whisper elevation without filled colored blocks | M1 | R1 | DONE |
| 4 | Tabular Numbers & Themed Focus | Monospaced numeric figures for currency/tables, `:focus-visible` 2px solid accent outline | M1 | R1 | DONE |
| 5 | Shared Data Table Component (`bb-data-table` / `bb-data-grid`) | Reusable table with sticky header, inset bottom shadow (z-index 3), hairline row rules, compact density (>=32px) | M2 | R3 | DONE |
| 6 | Data Table Inputs & Outputs | Columns, rows, loading state, pagination, sorting change emitters, empty state template | M2 | R3 | DONE |
| 7 | Numeric Right-Alignment & Column Formatting | Right alignment and tabular figures automatically applied for numeric columns | M2 | R3 | DONE |
| 8 | App Shell Root Component (`ShellComponent` / `bb-shell`) | CSS grid layout managing 56px rail, 46px topbar, breadcrumb bar, scrolling content outlet, mobile responsiveness | M3 | R2 | DONE |
| 9 | Shell Left Rail Component (`ShellNavComponent` / `bb-shell-nav`) | 56px fixed rail with module navigation links, active item cutout rule, bottom user menu | M3 | R2 | DONE |
| 10 | Shell Topbar Component (`ShellTopbarComponent` / `bb-shell-topbar`) | 46px sticky bar with searchable org dropdown, display-only FY tag, action group buttons | M3 | R2 | DONE |
| 11 | Shell Breadcrumb Component (`ShellBreadcrumbComponent` / `bb-shell-breadcrumb`) | Breadcrumb trail replacing page `<h1>` headings and hosting module-level action buttons | M3 | R2 | DONE |
| 12 | Sales Module List Screen | Filter bar, shared data table with compact density, sorting, pagination for Quotes, Orders, Invoices, Delivery Challans | M4 | R4 | DONE |
| 13 | Sales Module Create/Edit Screens | Reactive forms exactly mirroring backend DTOs (`SaveQuoteRequest`, `SaveSalesOrderRequest`, `SaveInvoiceRequest`, `SaveCreditNoteRequest`, `SaveDeliveryChallanRequest`) | M4 | R4 | DONE |
| 14 | Sales Module End-to-End Verification | Full integration and verification of Sales List + Form screens with zero overlap and clean pipeline | M4 | R4 | DONE |
| 15 | Purchase Module List & Form Screens | List page with shared table and reactive forms for Bills, Purchase Orders, Goods Receipts, Debit Notes | M5 | R4 | DONE |
| 16 | Inventory Module List & Form Screens | List and create screens for Items, Categories, Stock Movements, Adjustments | M5 | R4 | DONE |
| 17 | Accounts Module List & Form Screens | Chart of Accounts, Journals, Ledger, Banking screens labeled strictly as **Accounts** (zero "Accounting" UI strings) | M5 | R4, R5 | DONE |
| 18 | Master / Contacts Screens | Contacts list, Roles, Settings with shared table and reactive forms | M5 | R4 | DONE |
| 19 | E2E Test Suite (Tiers 1-4) | Comprehensive opaque-box test suite verifying all features, boundary cases, interactions, and realistic workflows | M6 | Acceptance Criteria | DONE (411 tests) |
| 20 | Adversarial Coverage Hardening (Tier 5) | White-box adversarial testing, edge-case probing, zero regressions, and forensic audit | M6 | Acceptance Criteria | DONE |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Design Tokens & Theming | `libs/shared/theming`: Port tokens from `styles.css` into SCSS `:root`, partials for typography, buttons, inputs, tables, cards, tags | None | DONE |
| M2 | Shared Data Table | `libs/shared/ui-components`: Enhance data table/grid for sticky header z-index 3 with inset shadow, compact density, hairline rules, sorting, pagination | M1 | DONE |
| M3 | App Shell Decomposition | `libs/app-shell`: Emit `ShellComponent`, `ShellNavComponent`, `ShellTopbarComponent`, `ShellBreadcrumbComponent`, verify CSS grid layout & layer stacking | M1 | DONE |
| M4 | Sales Module Screens & E2E Verification | `libs/sales/sales-ui` & `libs/sales/sales-core`: List pages with shared table, DTO-aligned create/edit reactive forms, end-to-end verification | M2, M3 | DONE |
| M5 | Remaining Module Screens & Accounts Audit | `libs/purchase/`, `libs/inventory/`, `libs/accounting/`, `libs/master/`: List/create pages, strict "Accounts" UI label enforcement | M4 | DONE |
| M6 | E2E Verification & Adversarial Hardening | Run and pass 100% E2E tests (Tiers 1-4), execute Tier 5 adversarial hardening and forensic audit | M5 | DONE |

## Interface Contracts
### Shell Chrome Components (`libs/app-shell`)
- `ShellComponent`: Main wrapper with `<router-outlet />`, CSS grid `grid-template-columns: 56px 1fr; grid-template-rows: 46px auto 1fr`.
- `ShellNavComponent`: Fixed 56px rail, module routes (`/dashboard`, `/sales`, `/purchase`, `/inventory`, `/accounting`, `/banking`, `/reports`, `/contacts`, `/settings`), bottom user profile menu.
- `ShellTopbarComponent`: Inputs: `organizations: Organization[]`, `currentOrgId: string`, `financialYear: string`. Outputs: `organizationChange: EventEmitter<string>`, `quickAction: EventEmitter<string>`.
- `ShellBreadcrumbComponent`: Dynamic breadcrumb path derived from active route, hosts `<ng-content select="[bbShellActions]" />` for right-aligned module action buttons.

### Shared Data Table (`libs/shared/ui-components`)
- `bb-data-table` / `bb-data-grid`:
  - Inputs:
    - `columns: ColumnDef[]` (`{ field: string; header: string; width?: string; align?: 'left'|'right'; numeric?: boolean; sortable?: boolean }`)
    - `data: any[]`
    - `loading: boolean`
    - `totalCount: number`
    - `pageSize: number`
    - `currentPage: number`
    - `compact: boolean` (default `true`)
  - Outputs:
    - `sortChange: EventEmitter<{ field: string; direction: 'asc' | 'desc' }>`
    - `pageChange: EventEmitter<number>`
    - `rowClick: EventEmitter<any>`

### Sales DTO Contracts (`libs/sales/sales-core`)
- `SaveQuoteRequest`, `SaveSalesOrderRequest`, `SaveInvoiceRequest`, `SaveCreditNoteRequest`, `SaveDeliveryChallanRequest`:
  - Fields: `documentDate: string`, `contactId: number`, `currencyCode: string`, `placeOfSupplyStateId: number`, `lines: LineRequest[]`, `notes?: string`, `termsAndConditions?: string`.
  - Line Fields: `itemId?: number`, `description?: string`, `quantity: number`, `unitPrice: number`, `taxGroupId?: number`, `isPriceInclusive: boolean`, `discountPercent?: number`.

## Code Layout
- Design Tokens & SCSS: `frontend/libs/shared/theming/src/lib/`
- Shared UI Presentational Components: `frontend/libs/shared/ui-components/src/lib/`
- App Shell Chrome: `frontend/libs/app-shell/src/lib/`
- Sales UI: `frontend/libs/sales/sales-ui/src/lib/`
- Sales Core: `frontend/libs/sales/sales-core/src/lib/`
- Purchase UI: `frontend/libs/purchase/purchase-ui/src/lib/`
- Accounting UI (Labeled "Accounts"): `frontend/libs/accounting/accounting-ui/src/lib/`
- Inventory UI: `frontend/libs/inventory/inventory-ui/src/lib/`
- Master UI: `frontend/libs/master/master-ui/src/lib/`
