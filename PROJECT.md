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
