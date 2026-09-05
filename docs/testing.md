# E2E Test Infra: Bill-Book Desktop App Shell & Module Screens

## Test Philosophy
- Opaque-box, requirement-driven testing. Derived directly from `ORIGINAL_REQUEST.md`, design specifications, and API contracts.
- Independent decomposition across 5 tiers:
  - **Tier 1 - Feature Coverage**: >=5 tests per feature for happy path and core isolation.
  - **Tier 2 - Boundary & Corner Cases**: >=5 tests per feature covering extreme values, empty states, boundary inputs.
  - **Tier 3 - Cross-Feature Combinations**: Pairwise interactions (e.g. Org switch -> Nav active state -> Breadcrumb updates -> Table reload).
  - **Tier 4 - Real-World Application Scenarios**: Complete end-to-end workflows (e.g. Create Sales Invoice -> Navigate to List -> Filter & Sort -> Verify totals and tabular numeric rendering).
  - **Tier 5 - Adversarial Coverage Hardening**: Deep white-box stress testing, regression guards, and forensic integrity verification.

## Feature Inventory & Test Coverage Goals
| # | Feature | Requirement | Tier 1 | Tier 2 | Tier 3 |
|---|---------|-------------|:------:|:------:|:------:|
| 1 | SCSS Design Tokens (`shared/theming`) | R1 | 5 | 5 | ✓ |
| 2 | Tabular Numbers & Stroke-over-fill | R1 | 5 | 5 | ✓ |
| 3 | Themed Outline Focus & CSS States | R1 | 5 | 5 | ✓ |
| 4 | Fixed Left Rail with User Menu | R2 | 5 | 5 | ✓ |
| 5 | Top Bar (Org Switcher, FY Tag, Actions) | R2 | 5 | 5 | ✓ |
| 6 | Breadcrumb Strip & Action Host | R2 | 5 | 5 | ✓ |
| 7 | Shell Grid Layout & Layer Stacking | R2 | 5 | 5 | ✓ |
| 8 | Shared Data Table (Sticky Header & Shadow) | R3 | 5 | 5 | ✓ |
| 9 | Hairline Row Rules & Compact Density (>=32px) | R3 | 5 | 5 | ✓ |
| 10 | Data Table Sorting & Pagination | R3 | 5 | 5 | ✓ |
| 11 | Sales Module List Page | R4 | 5 | 5 | ✓ |
| 12 | Sales Module Create/Edit Reactive Forms | R4 | 5 | 5 | ✓ |
| 13 | Sales Module End-to-End Flow | R4 | 5 | 5 | ✓ |
| 14 | Purchases Module List & Forms | R4 | 5 | 5 | ✓ |
| 15 | Accounts Module Screens ("Accounts" Label) | R4, R5 | 5 | 5 | ✓ |
| 16 | Inventory Module Screens | R4 | 5 | 5 | ✓ |
| 17 | Architecture & Placement Rules | R5 | 5 | 5 | ✓ |

## Test Runner Architecture
- Framework: Vitest / Angular Component Testing harness in `frontend/`
- Execution: `npm run check` (Lint, Typecheck, Vitest unit/integration tests, Nx builds)
- Expected: All test suites pass cleanly with 0 warnings/errors and exit code 0.


# Test Suite Delivery: Bill-Book Desktop App Shell & Module Screens (TEST_READY)

**Timestamp**: 2026-08-19T15:05:00Z  
**Author**: E2E Test Writer (`test_writer_1`)  
**Status**: 100% Passing (0 Failures, 0 Compile Errors, 0 Lint Violations)

---

## 1. Executive Summary

A comprehensive, opaque-box, multi-tier test suite was designed and implemented for the Bill-Book Angular 20 Nx workspace. The test suite thoroughly covers all design tokens, application shell layout and interactions, shared data tables, sales module list & reactive forms (Invoices, Quotes, Sales Orders, Credit Notes, Delivery Challans), and cross-module integration with strict forensic auditing of the forbidden `"Accounting"` UI string.

- **Total Test Files**: 24
- **Total Tests Executed**: 301
- **Pass Rate**: 100% (301 passed, 0 failed, 0 skipped)
- **Pipeline Verification (`npm run check`)**: Clean exit code 0 (Lint, Typecheck, Unit/Integration Tests, Production Builds for `web`, `desktop`, `docs`).

---

## 2. Four-Tier Test Suite Breakdown

### Tier 1: Feature Coverage (R1–R5 Requirements)
- **SCSS Design Tokens & Theming (`libs/shared/theming`)**:
  - Core variables on `:root` (`--color-bg`, `--color-surface`, `--color-text`, `--color-accent`, `--color-divider`).
  - Full tonal ramps (Neutral 100–900, Accent 100–900, Accent-2 100–900).
  - Typography tokens (Cormorant Garamond + Lora, `--font-heading-weight: 600`).
  - Base 4.6px classical spacing scale (`--space-1` through `--space-8`) and radius tokens (`--radius-sm`, `--radius-md`, `--radius-lg`).
  - Whisper elevation shadows (`--shadow-sm`, `--shadow-md`, `--shadow-lg`).
- **App Shell Chrome (`libs/app-shell`)**:
  - Left rail 56px fixed navigation with active indicator cutout rule.
  - Topbar 46px sticky bar with searchable org dropdown, FY tag, and quick-action menu.
  - Breadcrumb strip dynamic derivation from URL replacing `<h1>` headings.
  - User profile menu and logout delegation.
  - **CRITICAL**: Strict labeling of `/accounting` as `'Accounts'`, zero occurrences of `'Accounting'`.
- **Shared Data Grid / Data Table (`libs/shared/ui-components`)**:
  - `ColumnDef` mapping, visible column initialization, sticky header with inset shadow.
  - Sorting and text filtering (`contains`, `equals`, `starts`).
  - State persistence via `DataGridService`.
  - RFC4180 CSV export.
- **Sales Module UI (`libs/sales/sales-ui`)**:
  - `SalesListComponent`: Filter bar, type switcher, grid binding, route resolution to Quotes, Orders, Invoices, Credit Notes.
  - `InvoiceFormComponent`: Reactive form controls, `totals` calculation (`totalsOf`), DTO alignment (`SaveInvoiceRequest`), create/edit/post/void lifecycles.
  - `QuoteFormComponent`: Form controls, `SaveQuoteRequest` DTO mapping.
  - `SalesOrderFormComponent`: Delivery date controls, `SaveSalesOrderRequest` DTO mapping.
  - `CreditNoteFormComponent`: Invoice ID and reason code mapping, `SaveCreditNoteRequest` DTO mapping.
  - `DeliveryChallanFormComponent`: Dispatch date, vehicle number, challan type mapping, `SaveDeliveryChallanRequest` DTO mapping.

### Tier 2: Boundary & Corner Cases
- **Design Tokens**: Stroke-over-fill verification (transparent default backgrounds on buttons, cards), tabular numeric enforcement (`font-variant-numeric: tabular-nums`), themed `:focus-visible` outlines.
- **App Shell**: Empty/dashboard routes return empty crumbs list; deep nested URL parsing; case-insensitive and special character org search; escape key listener and outside-click popup dismissals; role permission lockdowns.
- **Data Grid**: Empty datasets (`data = []`), null/undefined row values, case-insensitive matching, multi-column conjunction filtering, literal matching for regex metacharacters (`[`, `]`, `*`, `?`).
- **Sales Forms**: Validation prevention on invalid forms, non-draft disabled states, void cancellation handling, API error recovery.

### Tier 3: Cross-Feature Combinations & State Sync
- Dynamic router `NavigationEnd` events updating breadcrumbs in real time.
- Org switching updating context and active org signals.
- Custom cell templates via `DataGridCellTemplateDirective`.
- Reactive updates to grid data input recalculating `filteredData` computed signal.
- Inter-state vs intra-state tax calculation switching between CGST+SGST and IGST.

### Tier 4: Real-World Application Workflows
- **End-to-End Retail ERP Workflow**: Shell initialization -> Navigation to Sales Register -> Document filtering -> Transaction selection -> Form load -> Line item calculations -> Form submission -> Navigation back -> CSV export generation.
- **Multi-Line Document Calculation**: Correct precision arithmetic across gross, discounts, multi-component taxes, and grand totals.

---

## 3. Test File Registry & Metrics

| Test File Path | Focus Area | Tests | Status |
|---|---|:---:|:---:|
| `libs/shared/theming/src/lib/design-tokens.spec.ts` | Design Tokens, Spacing, Ramps, Whisper Shadows, Stroke-over-fill | 18 | PASS |
| `libs/app-shell/src/lib/shell/shell.component.spec.ts` | Shell Chrome, Left Rail, Topbar, Breadcrumbs, Org Switcher | 21 | PASS |
| `libs/app-shell/src/lib/integration/shell-module-integration.spec.ts` | E2E Integration, Layer Stacking, Forensic "Accounting" Audit | 8 | PASS |
| `libs/shared/ui-components/src/lib/data-grid/data-grid.component.spec.ts` | Reusable Data Grid, Filtering, State, CSV Export | 17 | PASS |
| `libs/sales/sales-ui/src/lib/sales-list/sales-list.component.spec.ts` | Sales Register, Type Filtering, Route Mapping | 11 | PASS |
| `libs/sales/sales-ui/src/lib/invoice-form/invoice-form.component.spec.ts` | Invoice Form, Totals Math, Create/Edit/Post/Void Workflows | 15 | PASS |
| `libs/sales/sales-ui/src/lib/sales-forms.spec.ts` | Quotes, Sales Orders, Credit Notes, Delivery Challans | 13 | PASS |
| `libs/shared/ui-components/src/lib/cva-form-lifecycle.spec.ts` | CVA Forms Lifecycle across all 5 Input Components | 14 | PASS |
| `libs/shared/ui-components/src/lib/challenger-adversarial-stress.spec.ts` | Adversarial Stress Testing on Form Controls | 15 | PASS |
| `libs/shared/ui-components/src/lib/document-line-grid/line-math.spec.ts` | Document Line Arithmetic & Rounding | 9 | PASS |
| `libs/shared/ui-components/src/lib/document-line-grid/tax-fixture.spec.ts` | Tax Calculation Fixtures | 16 | PASS |
| `libs/shared/ui-components/src/lib/currency-input/currency-input.component.spec.ts` | Currency Input Component | 16 | PASS |
| `libs/shared/ui-components/src/lib/date-input/date-input.component.spec.ts` | Date Input Component | 15 | PASS |
| `libs/shared/ui-components/src/lib/number-input/number-input.component.spec.ts` | Number Input Component | 16 | PASS |
| `libs/shared/ui-components/src/lib/search-input/search-input.component.spec.ts` | Search Input Component | 16 | PASS |
| `libs/shared/ui-components/src/lib/text-input/text-input.component.spec.ts` | Text Input Component | 16 | PASS |
| `libs/shared/ui-components/src/lib/report-grid/filter-operators.spec.ts` | Report Grid Filter Operators | 7 | PASS |
| `libs/reporting/reporting-core/src/lib/report-state.spec.ts` | Reporting Core State | 5 | PASS |
| `libs/shared/auth/src/lib/auth.service.spec.ts` | Auth Service & Tenancy Switch | 11 | PASS |
| `libs/shared/auth/src/lib/auth.interceptor.spec.ts` | Auth Interceptor & Token Injection | 7 | PASS |
| `libs/shared/auth/src/lib/license.guard.spec.ts` | License & Permission Guards | 12 | PASS |
| `libs/shared/auth/src/lib/token-claims.spec.ts` | JWT Token Claims Parsing | 6 | PASS |
| `libs/shared/api-client/src/lib/api-base-url.interceptor.spec.ts` | API Gateway Base URL Interceptor | 5 | PASS |
| `libs/shared/theming/src/lib/tokens.spec.ts` | Token Contract Fixtures | 12 | PASS |
| **Total** | | **301** | **PASS** |

---

## 4. How to Run the Tests

```bash
# Run all Vitest tests
cd frontend && npm run test

# Run full project check (Lint, Typecheck, Tests, Builds)
cd frontend && npm run check
```


# Backend tests

`dotnet test` from `backend/`.

## Status

**110 tests, passing.** They compiled and ran green the first time an SDK was
available, which is what the scaffolding was written for — the wiring (csproj,
solution entry, package versions) is the tedious part to retrofit, and having it
in place meant one command rather than an afternoon.

If `dotnet` is missing from a container, install it from the distribution
repository — some environments deny `dot.net` by egress policy:

```bash
apt-get update && apt-get install -y dotnet-sdk-10.0
```

## What is covered, and why only this

Everything here is **pure logic**: no `DbContext`, no HTTP, no mocks.

| File | Covers | Why it earns a test |
|---|---|---|
| `NumberFormatTests` | Code composition, financial-year rendering, reset timing | Fails silently — a wrong year segment produces a number that reads perfectly and is only caught at audit |
| `ReorderingTests` | Drag-and-drop display order, including the renumber path | The renumber branch only runs when neighbours have no gap between them, so nobody exercises it by hand |
| `PhoneAttributeTests` | Landline pattern, mobile length | A regex that forgets the leading `+` rejects every overseas number |
| `StockAdjustmentServiceTests` | An adjustment sheet against a real PostgreSQL: one document, all-or-nothing posting, numbering at post, reversal by mirror | Half of what a sheet guarantees lives in the database — five check constraints, the guarded decrement, and a number allocated inside the caller's transaction. It found a real defect the first time it ran |
| `StockLedgerMappingTests` | What a stock movement means in the general ledger | The clearest case of failing silently in the product: a wrong guard refuses a sale and somebody rings up, but a wrong account produces a balance sheet that still balances and a gross margin that is simply untrue |

That line is what decides whether something belongs in the pure set.
`StockLedgerMapping` qualifies because it is a `static` function over an entity —
it names accounts and does no I/O. `StockLedgerPoster`, which calls it, does not:
everything interesting about it is a guarded claim and an HTTP retry.

## The database-backed set

`Accounting.Api.Tests` is the exception this file used to say was owed, and it
arrived with the general ledger. The interesting behaviour there — the deferred
balance triggers, the `ExecuteDelete` that makes a posting replace rather than
accumulate, the guarded update that keeps a numbering series gapless inside the
caller's transaction — is behaviour of Postgres. Testing it against an in-memory
provider would assert that the mock behaves like the mock, which is exactly why
it was left undone until it could be done properly.

So those tests need **a real PostgreSQL**:

```bash
service postgresql start                 # or point at your own
export ACCOUNTING_TEST_DB="Host=localhost;Port=5432;Database=accounting_tests;Username=postgres;Password=123"
dotnet test
```

The default connection string is the one above, so on a machine with a local
server and those credentials nothing needs setting.

**They skip themselves, with a reason, when no server answers.** A suite that
fails on a machine without Postgres trains people to ignore red; one that passes
without running is worse. Skipped-with-a-reason is the only honest third option.

Each test builds its own branch with a fresh `OrgId`, so the query filter keeps
them apart — which means the tests exercise the isolation rather than working
around it — and the schema comes from `Database.Migrate()`, not
`EnsureCreated()`, because every trigger and RLS policy lives in the migrations
and `EnsureCreated` skips all of them.

| File | Covers |
|---|---|
| `LedgerArithmeticTests` | Running balances and the trial-balance column split. Pure — always runs |
| `LedgerPostingServiceTests` | The posting door: a whole document's legs in one call, two services replacing independently on one invoice, and withdrawal |
| `JournalServiceTests` | The manual journal: draft, post, reverse, line-level reversal pairing, and a refused post leaving the number series where it was |
| `LedgerReportServiceTests` | The account ledger and the trial balance, read back over postings written through the door |
| `SubAccountServiceTests` | A contact's six sub-accounts under two parents, the purpose that keeps them from colliding, and per-target idempotence |
| `MoneyDocumentSchemaTests` | The money document: a draft may be part-allocated, a posted one may not; transfer and payment shapes; number-on-post |
| `MoneyDocumentServiceTests` | Spend, receive and transfer end to end — allocation, settlement, FX and voiding, against `RecordingLedger` |
| `StatementImportTests` | Reading a bank's CSV and XLSX, the two amount layouts, and re-importing an overlapping period |
| `StatementMatcherTests` | Tying a statement line to a document, and refusing to guess between identical candidates |
| `OpeningBalanceServiceTests` | The migration screen: per-contact AR/AP, the equity net-to-zero check, and the subledger tie |
| `PeriodLockTests` | How far back the books are closed, per role, and what a closed period refuses |

These were two suites until Banking merged into Accounting; the money-document
tests came with it, and `BANKING_TEST_DB` went with them — there is one
`ACCOUNTING_TEST_DB` now.

## Adding a test project

One per project under test, named `{Project}.Tests`, under `backend/tests/`.
Add it to `Bill-Book.sln` and reference the project under test — nothing else.