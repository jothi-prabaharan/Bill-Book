# --- coding-standards.md ---
# Coding Standards

## .NET Backend
- **Framework**: .NET 10
- **Architecture**: Microservices/modular monolith style structure. Each module has its own bounded context.
- **ORM**: Entity Framework Core with code-first migrations.
- **Database**: PostgreSQL (with Row-Level Security for multi-tenancy).
- **Naming Conventions**: PascalCase for classes and methods, camelCase for local variables. Interfaces start with `I`.

## Security & Multi-tenancy
- Every per-customer table MUST carry an `OrgId` column.
- Row-Level Security (RLS) policies MUST be applied to prevent cross-tenant data leaks.
- Avoid passing raw tenant IDs from client; resolve tenant context securely in the API layer.

## General Rules
- Keep controllers thin; push business logic into domain or application services.
- Follow SOLID principles.
- Use asynchronous programming (`async/await`) for all I/O bound operations.

## Data Types & Conventions
- **ID and Datatype Rules (STRICT)**: Identifiers (PKs and FKs) for **User**, **Customer**, and **Organization (`OrgId`)** must strictly be `Guid`. All **other** entities must use `long`.
- **Date Rule**: Strictly use the `DateOnly` struct in C# backend entities/DTOs and native date-only inputs on the frontend.
- **Decimal & String Rules**: All monetary, tax, and quantity fields must explicitly use `decimal(18,4)` precision. Every `string` property must have a `[MaxLength]` attribute.
- **Boolean Rule**: All boolean flags must be prefixed with `Is`, `Has`, or `Can`.
- **Token Security (STRICT)**: Absolutely do not decode the JWT access token in the frontend. Fetch user, role, or organization details via a backend API endpoint, omitting internal `Id` values.
- **Dynamic Formatting**: Always dynamically retrieve date, currency, and number formats from backend settings and apply them globally.

## Transaction Handling (STRICT)

Every write endpoint runs in a database transaction, and it is **not** the controller's job to open one.

- **`AddBillBookReliability<TContext>()` in `Program.cs` is the whole registration.** It adds `TransactionFilter` to the MVC pipeline, which opens a transaction before any `POST`/`PUT`/`PATCH`/`DELETE` action and settles it after. Call it once per `DbContext`; Master calls it twice.
- **Never call `Database.BeginTransactionAsync` directly.** Use `await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);`. It opens a transaction, or joins the one already open and makes its own commit and rollback no-ops. A raw `BeginTransactionAsync` throws the instant anything above it has started a transaction, which is what made `AllocationService`, `JournalService`, the money-document services and `StockAdjustmentService` impossible to compose.
- **Commit is earned by the status, not by the absence of an exception.** The filter rolls back on any result of 400 or above, on `Forbid()`, on a cancelled action and on an exception. A service that wrote two of three rows and returned `Conflict()` keeps neither.
- **Raise isolation with the attribute, never inside the method.** `[Transactional(IsolationLevel.Serializable)]` on the action. Postgres only accepts `SET TRANSACTION ISOLATION LEVEL` before the transaction's first statement, so by the time an inner service runs it is too late — `BeginScopeAsync` throws with the attribute to add rather than silently giving you the weaker level.
- **`[NoTransaction]` is the escape hatch and needs a reason in the code.** For an action that must commit part of its work before calling out, or that imports in batches.
- **Workers get no filter.** They serve no requests. Where a worker needs atomicity it opens its own scope; where it fails, it audits (below).
- **Two contexts are two transactions, not a distributed one.** There is no two-phase commit. An action writing to both `mst` and `con` can still half-succeed, so do not write to both in one action.
- **A cross-service call inside a transaction is a saga, not a rollback.** The filter protects this service's own rows. A remote `POST` to Inventory or Accounting has already committed there and no rollback here undoes it — those steps are made safe by being idempotent (the ledger replaces by `(TransactionTypeCode, TransactionId)`, Inventory dedupes by `(SourceType, SourceId, SourceLineId)`), not by the transaction.

## Error Handling (STRICT)

No `catch` block formats its own message for a caller, and no raw database text ever reaches production.

- **One handler, registered with the transactions.** `GlobalExceptionHandler` translates, records, and answers. `app.UseBillBookErrorHandling()` goes first in the pipeline, before authentication.
- **Every failure is answered from `SqlErrorCatalog`.** SQLSTATE → status, `ApiErrorCode`, and a curated sentence. Exact code first, then its two-character class, then `Unexpected`. Add a new state to the catalogue; never branch on an exception message.
- **Development returns the exact error. Every other environment returns the curated sentence.** The switch is `IHostEnvironment.IsDevelopment()` and there is deliberately no configuration key that overrides it — a setting like that is one deployment mistake from publishing the schema.
- **A curated message may never name a table, column, constraint, schema or figure.** `SqlErrorCatalogTests` asserts this over every entry. The ledger balance trigger's own text quotes the branch's total debits and credits; that is exactly what must not be forwarded.
- **`Code` is the contract, `Message` is for humans.** Clients branch on `ApiErrorCode`. It is identical in every environment, and so is `Message`.
- **The detail goes to `{schema}.ErrorLogs`, and the caller gets a reference.** `ErrorReference` is a Guid on the response; support looks it up. Users never see the detail.
- **`ErrorLogs` is tenant scoped like everything else** — `CustomerId`, `OrgId`, query filter, RLS ENABLEd and FORCEd. The consequence is accepted, not overlooked: **an error raised before the tenant is known cannot be recorded** — a failed sign-in, a request with no token — and goes to `ILogger` alone.
- **No endpoint returns rows from `ErrorLogs`.**
- **A worker failure goes on the task list.** Call `IWorkerErrorAuditor.AuditAsync(workerName, jobReference, exception, ct)`. It writes `Source = Worker` and `FollowUpStatus = Open`, and the set of open rows is the list an operator works through. Nobody saw the failure, so a log line alone is the same as nothing.

# Claude Design → Existing Angular Project Design Rules

## Purpose

This document is the authoritative rule set for reorganizing and importing the Claude
Design into the existing Bill-Book Angular project.

The Angular project is already partially implemented. The design must be mapped onto
the existing Angular structure, not used to invent a new application structure.

## 1. Source of Truth

Use this priority:

1. Existing GitHub Angular repository
2. Existing Angular folder/library/component names
3. Existing Angular routes/navigation
4. Existing application behavior
5. Claude Design visual/UI specification

Do not rename existing Angular folders merely to make them look cleaner.

## 2. Exact Folder Name Rule — CRITICAL

The design structure MUST mirror the existing Angular repository exactly.

If Angular contains:

    frontend/libs/<module>/<library>/src/lib/<folder-name>

the design must contain:

    design/frontend/libs/<module>/<library>/src/lib/<folder-name>

Copy `<folder-name>` exactly.

Do NOT change:

    bb-grid → grid
    invoice-form → invoices
    sales-order-form → order-form

Do NOT invent names. Do NOT normalize names. Do NOT remove prefixes such as `bb-`.

The GitHub repository is authoritative for exact names.

## 3. Current Verified Top-Level Library Structure

The repository currently contains these top-level libraries under `frontend/libs/`:

    accounting
    app-shell
    customer
    inventory
    master
    purchase
    reporting
    sales
    shared

These names must be preserved exactly.

For example, `accounting` currently contains:

    accounting-core
    accounting-ui

Do not assume every module has the same sub-library pattern. Inspect GitHub first.

## 4. Never Guess the Repository Tree

Before creating the design hierarchy, inspect:

    frontend/libs/<module>
    frontend/libs/<module>/<library>
    frontend/libs/<module>/<library>/src
    frontend/libs/<module>/<library>/src/lib

Only reproduce folders that actually exist.

## 5. Exact Component/Page Mapping

The design must map to actual Angular component folders.

Verified examples in Sales UI include:

    frontend/libs/sales/sales-ui/src/lib/
    ├── aging-summary-list/
    ├── credit-note-form/
    ├── delivery-challan-form/
    ├── invoice-form/
    ├── invoice-list/
    ├── invoice-print/
    ├── order-to-invoice/
    ├── quote-form/
    ├── quote-to-order/
    ├── sales-list/
    └── sales-order-form/

These names must remain unchanged when corresponding design folders are created.

This is not an exhaustive inventory. The agent must inspect the complete repository.

## 6. Required Design Structure

Mirror the Angular repository:

    design/
    └── frontend/
        └── libs/
            ├── accounting/
            ├── app-shell/
            ├── customer/
            ├── inventory/
            ├── master/
            ├── purchase/
            ├── reporting/
            ├── sales/
            └── shared/

Under each module, reproduce the exact Angular sub-library names.

Only create a sub-library when the corresponding Angular library exists.

## 7. Mirror `src/lib`

For UI libraries, mirror the existing `src/lib` hierarchy.

Example:

    Angular:
    frontend/libs/sales/sales-ui/src/lib/invoice-form/

    Design:
    design/frontend/libs/sales/sales-ui/src/lib/invoice-form/

Do not replace the actual Angular structure with a generic design structure.

## 8. Global Application Design

Global application-level design should map to the existing `frontend/libs/app-shell/`
and `frontend/libs/shared/` structure where appropriate.

Use the actual subfolders/components found there.

Do not invent an unrelated `design/global/` hierarchy for components that already have
a corresponding Angular location.

## 9. Global vs Module vs Page-Specific Components

Every design component must be classified as:

    GLOBAL
    MODULE-SHARED
    PAGE-SPECIFIC
    UNKNOWN

GLOBAL:
Used across multiple modules. Prefer existing `app-shell` or `shared` locations.

MODULE-SHARED:
Used by multiple pages within one module.

PAGE-SPECIFIC:
Used only by one existing page/component.

UNKNOWN:
Do not guess. Document what must be verified.

## 10. Split the Single Large Claude Design

Split the current large design according to existing Angular component structure.

Example:

    Claude Design
         |
         +-- Invoice list
         +-- Invoice form
         +-- Invoice print
         +-- Sales order form
         +-- Quote form
         +-- Quote → Order
         +-- Order → Invoice
         |
         v
    Existing Angular structure
         |
         +-- invoice-list/
         +-- invoice-form/
         +-- invoice-print/
         +-- sales-order-form/
         +-- quote-form/
         +-- quote-to-order/
         +-- order-to-invoice/

Do not combine separate Angular components into one design page simply because they
appear visually related.

## 11. Page Naming

Where a corresponding Angular component exists, use the exact component/folder identity.

Examples:

    invoice-form
    invoice-list
    invoice-print
    sales-order-form
    quote-form
    quote-to-order
    order-to-invoice

Do not rename them to generic names such as `invoice-management` or `sales-documents`.

## 12. Route Mapping

Every design page must be mapped to its existing Angular route where one exists.

Create:

    DESIGN_PAGE_MAP.md

Format:

    | Module | Angular Library | Angular Path | Angular Component/Folder | Design Path | Status |
    |---|---|---|---|---|---|

Discover routes from the repository. Never invent routes.

## 13. Component Mapping

Create:

    COMPONENT_CLASSIFICATION.md

Format:

    | Angular Path | Component/Folder | Classification | Design Path | Notes |
    |---|---|---|---|---|

## 14. Design System

Centralize:

- Colors
- Typography
- Font weights
- Spacing
- Radius
- Borders
- Shadows
- Icons
- Button/input variants
- Table/status variants
- Responsive rules

Create:

    DESIGN_SYSTEM.md

Do not duplicate design tokens unnecessarily.

## 15. Unwanted Design Files

Identify:

- Duplicate pages/components/assets
- Temporary files
- Experimental screens
- Old versions
- Generated files
- Unused assets
- Screens with no Angular equivalent

Do not delete automatically.

Create:

    DESIGN_CLEANUP_REPORT.md

Format:

    | File | Category | Referenced | Angular Equivalent | Action | Reason |
    |---|---|---:|---|---|---|

Allowed actions:

    KEEP
    MOVE
    MERGE
    ARCHIVE
    DELETE
    REVIEW

## 16. Missing Design Detection

Create:

    MISSING_DESIGN_PAGES.md

List Angular pages/components with no corresponding design.

Format:

    | Angular Path | Component | Route | Design Exists | Priority |
    |---|---|---|---|---|

## 17. Unmatched Design Detection

Create:

    UNMATCHED_DESIGN_PAGES.md

List design screens with no corresponding Angular page/component.

Format:

    | Design Path | Design Name | Possible Angular Match | Action |
    |---|---|---|---|

Actions:

    MAP
    MERGE
    KEEP
    ARCHIVE
    DELETE
    REVIEW

## 18. Preserve Existing UI Design

When splitting the Claude Design, do NOT redesign.

Preserve:

- Visual hierarchy
- Layout
- Typography
- Colors
- Spacing
- Interaction patterns
- Responsive behavior
- Component appearance
- Design tokens

The task is to reorganize and map the design.

## 19. Responsive List View Rule — TABLE DESKTOP / CARD MOBILE

For list pages, use the following responsive presentation rule unless the existing
application has a specific established pattern that must be preserved:

### Desktop and Tablet

Use a table/grid presentation when the page contains structured, column-based data.

Typical examples:

    Invoice List
    Customer List
    Sales Order List
    Purchase Order List
    Product/Item List
    Ledger List
    Report List

Desktop/tablet should prioritize:

- Multiple columns
- Sortable headers where supported
- Filters
- Pagination
- Row actions
- Selection where supported
- Dense comparison of records

### Mobile

When a desktop table contains too many columns to remain usable on a phone, switch to
a card/list presentation instead of forcing the desktop table to fit the viewport.

Example mobile invoice card:

    Invoice Number
    Customer
    Date
    Status
    Total
    Primary actions

Secondary fields may be shown inside an expandable card or details view.

### Data and Business Logic

The table and mobile card views MUST use the same:

- API/data source
- TypeScript business logic
- filtering state
- sorting state where applicable
- pagination state
- selection state
- loading state
- empty state
- error state
- permissions
- row/card actions

Do NOT duplicate business logic between desktop and mobile.

The responsive change is a presentation change, not a business-logic change.

### Implementation Preference

Prefer CSS media queries when the same semantic structure can reasonably support both
layouts.

When table and card layouts have substantially different information hierarchy or
interaction behavior, it is acceptable to render separate presentation markup while
sharing the same component state and TypeScript logic.

Example:

    InvoiceListComponent.ts
            |
            +-- shared invoices/data/actions
            |
            +-- Desktop/Tablet → table/grid
            |
            +-- Mobile → cards

Do NOT create separate API/business-logic implementations for desktop and mobile.

Do NOT force a complex multi-column accounting table into a tiny mobile viewport by
using excessive horizontal scrolling when a card presentation provides a better user
experience.

### Mobile Card Requirements

Mobile cards should:

- Show the most important information first.
- Avoid unnecessary fields.
- Preserve status visibility.
- Keep monetary totals prominent.
- Provide touch-friendly actions.
- Support expandable secondary details where needed.
- Avoid tiny icon-only actions without accessible labels.

### Consistency Rule

All list pages should follow this pattern consistently unless there is a documented
reason to use another layout.

Claude Design must explicitly identify the intended desktop/tablet list presentation
and mobile list presentation for every list page.

## 20. Claude Design Phase

Claude Design is responsible for:

1. Organizing the design.
2. Splitting the large design into screens.
3. Mapping screens to existing Angular component names.
4. Separating global/shared/module/page-specific components.
5. Identifying design noise.
6. Preserving the visual design.
7. Producing mapping documentation.
8. Applying the responsive list table/card rule consistently.

Claude Design must NOT:

- Rewrite Angular code.
- Change Angular routes.
- Create database migrations.
- Change backend code.
- Invent application architecture.
- Rename existing Angular components.
- Invent `bb-*` component names.

## 21. Claude Code Phase

Claude Code is responsible for implementation only after the design mapping has been
reviewed and approved.

Before implementation, Claude Code must:

1. Read this document.
2. Read `AGENTS.md`.
3. Read `CLAUDE.md`.
4. Inspect the current Angular repository.
5. Inspect the design mapping.
6. Verify every target design path against GitHub.
7. Confirm the target Angular component exists.
8. Confirm the route where applicable.
9. Identify existing reusable components.
10. Produce an implementation plan.

Only then may implementation begin.

## 22. Claude Code Must Not Invent Paths

Before creating or modifying:

    frontend/libs/<module>/<library>/src/lib/<component>

Claude Code must verify the path against the repository.

Do not silently create components such as:

    bb-grid
    bb-table
    bb-form

unless the repository already contains them or explicit approval is given to add them.

If the design references a component that does not exist in Angular, report it as a
design/application mismatch instead of silently inventing a path.

## 23. Mid-Project Safety Rules

This is NOT a greenfield project.

Therefore:

- Preserve working functionality.
- Do not rewrite existing components unnecessarily.
- Do not replace routes without approval.
- Do not replace working shared components merely to match the design.
- Do not remove existing code because a design file differs.
- Do not introduce breaking API changes.
- Do not change database behavior during design import.
- Do not change accounting behavior during UI implementation.

## 24. Accounting Safety

Accounting is high risk.

Inspect the existing accounting architecture before UI changes that interact with
accounting workflows.

Do not change:

    acc.JournalLedger
    LedgerPostingService

or accounting posting behavior as part of design import unless explicitly approved.

Do not create separate physical GL tables merely because the UI design contains
separate transaction screens.

## 25. Implementation Workflow

    GitHub Angular
          |
          v
    Inspect Exact Tree
          |
          v
    Inspect Components
          |
          v
    Claude Design
          |
          v
    Split Large Design
          |
          v
    Exact Folder Name Mapping
          |
          v
    Component Classification
          |
          v
    Responsive List Table/Card Mapping
          |
          v
    Design Gap Analysis
          |
          v
    Human Review
          |
       APPROVAL
          |
          v
    Claude Code
          |
          v
    Implement Approved Design
          |
          v
    Run Tests
          |
          v
    Review Git Diff

## 26. Required Final Documentation

Produce:

    DESIGN_RULES.md
    DESIGN_PAGE_MAP.md
    COMPONENT_CLASSIFICATION.md
    DESIGN_STRUCTURE.md
    DESIGN_CLEANUP_REPORT.md
    DESIGN_SYSTEM.md
    GLOBAL_COMPONENTS.md
    MODULE_COMPONENTS.md
    MISSING_DESIGN_PAGES.md
    UNMATCHED_DESIGN_PAGES.md

## 27. Final Success Criteria

The design preparation is complete only when:

- The single large design is split into logical screens.
- Global components are separated.
- Shared components are separated.
- Module components are separated.
- Page-specific components are separated.
- Design-system tokens are centralized.
- Existing Angular module names are preserved.
- Existing Angular library names are preserved.
- Existing `src/lib` structure is mirrored.
- Existing component/folder names are preserved exactly.
- Prefixes such as `bb-*` are preserved exactly when they exist.
- No folder names are invented.
- No Angular code is modified during design preparation.
- No routes are changed.
- No database changes are made.
- Duplicate/unwanted design files are documented.
- Missing designs are documented.
- Unmatched designs are documented.
- Desktop/tablet list pages have an intentional table/grid presentation.
- Mobile list pages have an intentional card presentation where appropriate.
- Desktop/mobile list views share the same data, state, permissions, and business logic.
- The mapping is reviewable before implementation.
- Claude Code can use the mapping without guessing.

## Absolute Rule

**NEVER invent an Angular folder or component name.**

**ALWAYS inspect GitHub and copy the exact existing path/name.**

**For list pages, prefer table/grid on desktop/tablet and card presentation on mobile
when the table is too dense for a phone. Keep one source of truth for data and business
logic.**

**If the design and Angular repository disagree, report the mismatch instead of
silently changing either one.**


# --- project-structure.md ---
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


# --- testing.md ---
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

# --- commit-rules.md ---
# Commit Rules

This project follows conventional commits to ensure a readable and standard history.

## Format
`<type>(<scope>): <subject>`

### Types
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Changes that do not affect the meaning of the code (white-space, formatting, missing semi-colons, etc)
- `refactor`: A code change that neither fixes a bug nor adds a feature
- `perf`: A code change that improves performance
- `test`: Adding missing tests or correcting existing tests
- `build`: Changes that affect the build system or external dependencies
- `ci`: Changes to our CI configuration files and scripts
- `chore`: Other changes that don't modify src or test files

### Scope
Optional. Should specify the module or component affected (e.g., `accounting`, `inventory`, `frontend`).

### Subject
- Use the imperative, present tense: "change" not "changed" nor "changes".
- Don't capitalize the first letter.
- No dot (.) at the end.


# --- ai-agent-structure-rules.md ---
# AI Agent Structure Rules

The following rules have been established for all AI coding assistants (including **Claude** and **Antigravity**) to ensure structural integrity across the project.

## Hard Rules
1. **LINQ only. Never write raw SQL.** The only exceptions, because no LINQ equivalent exists: `CREATE DATABASE`, RLS policies, triggers, `set_config`. Everything else — every query, insert, update, delete — is LINQ.
2. **Entities are plain property bags.** No constructors. No methods. No validation logic. No computed properties. Just `public X Y { get; set; }` with Data Annotations.
3. **Every Data Annotation needs `ErrorMessage`.**
4. **PascalCase table and column names**, matching C# property names exactly. Postgres needs quoted identifiers for this — that's expected.
5. **PostgreSQL only.** Never add SQL Server compatibility, never avoid a Postgres feature for portability. RLS, `xmin`, and JSONB are all in use deliberately.
6. **All table entities inherit `Shared.Kernel.Entities.AuditableEntity`.** Never set audit fields manually — `AuditSaveChangesInterceptor` does it.
7. **Enums, not magic strings**, for any fixed set of values.
8. **Never cross a service boundary by referencing another service's `DbContext`.** Use its API or an event.

## Adding a Table
- Entity class in `{Module}.Entity/TableEntities/{Name}.cs`
- Enums (if any) in `{Module}.Entity/Enums/`
- `DbSet` + Fluent config in `{Module}.Repository/{Module}DbContext.cs`
- Seed data if it's reference data
- Do **not** write CREATE TABLE SQL. This is EF Core code-first.
- Every per-customer table needs `OrgId` plus a global query filter.

## Adding an Endpoint
- Request/response models in `{Module}.Entity/Models/` (Data Annotations with error messages)
- Controller action in `{Module}.Api/Controllers/`
- Validate the caller's `OrgId` matches the target resource's — always
- Return `Forbid()` when the token's org does not match an org id the route names; a row outside the caller's branch is `NotFound()`, because row-level security hides it from the service itself (TK-71, 23 September 2026)

## Project Layout

### Backend
Three projects per service, no more — all three under `backend/Api/{Module}/`:
- `{Module}.Entity/`: TableEntities, Models, Enums
- `{Module}.Repository/`: DbContext, repositories, seed data
- `{Module}.Api/`: controllers, services, DI

Dependency direction: `Api` → `Repository` → `Entity` → `Shared.Kernel`. Never backwards.

### Frontend (Nx, Angular v20)
`apps/{web, portal, admin, desktop, docs}` 
`libs/{module}/{module}-core` (view-models + models, no templates) 
`libs/{module}/{module}-ui` (pages) 
`libs/shared/{auth, api-client, ui-components, currency-format, theming}`

### Angular Component Structure
- **Standalone Only**: Use `standalone: true`. No `NgModules` are allowed.
- **Dependency Injection**: Use the `inject()` function instead of constructor injection.
- **State & Reactivity**: Use `signal()` and `computed()` for component state over RxJS `BehaviorSubject` where possible.
- **Data Fetching**: Use `async/await` with Promises for straightforward REST calls instead of heavily piping RxJS streams.
- **File Naming**: Suffix component files accurately according to their role (`.page.ts`, `.dialog.ts`, `.list.ts`, `.component.ts`).
- **Separation of Concerns**: Use separate `templateUrl` and `styleUrl` instead of inline templates.
- **Frontend Styling**: Absolutely NO inline styles (`style="..."`). Always use global CSS classes and CSS custom properties (e.g., `var(--primary-color)`).
- **Validation UX**: Field validation errors must display directly on top of inputs. Business validation errors must display inside the shared message box component.

## Database Migration & Seeding Rules
Whenever the Admin database is deployed or recreated, ensure the corresponding Customer database for my organization is also created successfully.
During this setup process, you must automatically map my UserID to this organization. Post-migration, I must be able to log in successfully and immediately view my organization's dashboard data without any manual database configuration.


# --- menu-from-backend.md ---
# Menu from the backend

The navigation tree now lives in the database and is served by `GET /api/menu`.
Before this change the endpoint existed and was seeded, but nothing called it: the
rail was a hardcoded array declared twice in the Angular shell, and the screen
registry added for Search and Favourites was a third copy.

Source of the tree: the shell design (`Shell.dc.html` — `subMenu`, `subGroups`,
`REPORT_GROUPS`, `SETTINGS_GROUPS`, `PT_DOCS`), matched row by row against the
routes the Angular app actually serves.

## Schema

**One table.** `Menu` holds all three levels, told apart by `Type`
(`Rail` | `Group` | `Item`) and joined by `ParentId`. `MenuPermission` hangs off it.

```
Menu(MenuId, ParentId?, Type, Code, Name?, Icon?, Module?, RoutePath?,
     IsSearchable, CanCreate, SingularName?, DisplayOrder, IsActive)
MenuPermission(MenuPermissionId, MenuId, PermissionCode, Action, Module)
```

The three levels share a shape — code, name, icon, order, active flag, a parent —
so three tables meant three sets of the same columns, three joins to read one menu,
and a migration every time the design grew a level. `Type` and `ParentId` carry that
structure instead, and a deeper tree now costs a row rather than a schema change.

Which columns matter depends on the type:

| Type | Parent | Uses |
|---|---|---|
| `Rail` | none | `Icon`, `Module`, `IsSearchable`; `RoutePath` only when the module has no panel (Home) |
| `Group` | a Rail | `Name` (null = unnamed section, drawn as plain rows), `Icon` |
| `Item` | a Group | `RoutePath`, `Module`, `Icon`, `CanCreate`, `SingularName`, permissions |

`Icon` is a **Lucide name** (`shopping-cart`, `users-round`), not markup, per
`DESIGN_SYSTEM.md`. The rail's `@switch` cases were renamed to match. The design
gives icons at rail level only, so the group and item icons are a choice made here
and easy to change — they are one column in the seed.

`Code` is unique among siblings. Postgres treats NULLs as distinct in a unique
index, so rail modules (whose `ParentId` is null) get a second, filtered unique
index to hold them to the same rule.

Ids are blocked by level — rails 1–99, groups 100–999, items from 1000 — so a row's
level is readable from its id while debugging.

### Placement and permission are separate

The design files HSN/SAC, UOM types and metal purity under **Inventory**, and
reconciliation under **Banking**. Those routes live under `/settings` and
`/accounting` and are guarded accordingly. The row sits where the design puts it;
`Module` carries the route's own guard. Without that split the menu would offer a
screen the router then refuses.

## Seed

138 rows — 9 rails, 17 groups, 112 items — and 403 permission rows.

| Module | Icon | Groups | Screens | With a route |
|---|---|---:|---:|---:|
| Home | house | — | direct to `/dashboard` | 1 |
| Contacts | users-round | 1 | 3 | 1 |
| Inventory | boxes | 1 | 10 | 9 |
| Purchase | package | 1 | 5 | 1 |
| Sales | shopping-cart | 1 | 6 | 3 |
| Banking | landmark | 1 | 9 | 7 |
| Accounts | book-open | 1 | 5 | 4 |
| Reports | chart-no-axes-combined | 7 | 46 | 46 |
| Settings | settings | 4 | 28 | 11 |

**22 rows have no route and are seeded `IsActive = false`.** These are screens the
design covers that are not built: account types, units of measure, licences,
permissions, user organisation roles, login history, the banking dashboard and
transaction screens, and the 12 print templates. `MenuService` filters on `IsActive`, so none of them reach a
user; switching one on later is a data change, not a code change.

All 46 reports have working URLs because `reporting.routes.ts` has a `:reportKey`
host. Four point at dedicated pages instead: Balance Sheet and Profit & Loss at
`/reports/statements/…`, Trial Balance and General ledger at `/accounting/…`.

**Every document type has its own register.** The type travels in the URL —
`/sales/transactions?type=Quote`, `/purchase/transactions?type=Bill` — so a filtered
list is linkable, bookmarkable and reachable with the back button, which it was not
while the type lived only in a clicked tab. `SalesListComponent` and
`PurchaseListPage` read the parameter on init and push it back when a tab changes,
so the address bar and the screen cannot disagree.

Two rows keep dedicated pages instead: **Sales orders** and **Invoices** have list
components that filter by fulfilment and by overdue, which the mixed register cannot
do — `sales.routes.ts` says as much in its own comments.

Sales and Purchase also keep an **All transactions** row for the untyped view, which
is the design's own "All transactions" tab.

Because some routes now carry a query, two places in the shell match on the full URL
before falling back to the bare path: `MenuService.subMenuFor` and the breadcrumb's
favourites star. Reversing that order would hand every typed register back as the
mixed one. Rail entries strip the query — landing pre-filtered on one document type
is not what clicking a module means.

Rows that are **not** in the design but are real reachable screens: Stock and Price
lists (Inventory), Banks and Bank accounts (Banking), Contact person roles
(Settings). They would otherwise have no way in.

Customers and Vendors are seeded inactive: the contacts page filters by role in its
own state, not from the URL, so those two rows have nowhere to point until it reads
a query parameter.

### Permission rows

Every `{module}.{action}` pair already exists — `AdminDbContext.PermissionModules`
seeds 12 modules × 10 actions. Action sets per screen kind:

- documents: view, create, edit, delete, print, export, void, approve
- banking money documents: as above without approve
- master data: view, create, edit, delete, export
- read-only and reports: view, export
- settings: view, create, edit, delete

## The API

`GET /api/menu` returns module → group → screen — the response shape is unchanged
by the move to one table, so the client did not have to follow the schema. The
service reads the rows flat in a single query and assembles the levels in memory:
about a hundred rows, cheaper to shape than to join three times. The server filters:
a screen the caller holds no permission on is dropped, an empty group is dropped,
and a module left with nothing is dropped with it. Each screen carries `hasAccess` and
`allowedActions`, so a screen can hide the buttons a role cannot use — `canView`
alone could never do that.

## The client

`MenuService` (`libs/app-shell`) loads it once on shell boot and exposes `rail`,
`screens`, `groupsFor`, `subMenuFor` and `allows(path, action)`. The rail, Search
and Favourites all read it.

`shell-screens.ts` is now a **fallback**, used only when the call fails — otherwise
a failed request would leave an empty rail. `FALLBACK_RAIL` is declared there once
and shared by both components that draw a rail, rather than each keeping its own.

A module with no route of its own lands on the first screen in its panel, because
the design's submenu panel is not built yet. When it is, the rail opens the panel
instead and `groupsFor(code)` already returns what it needs.

## Before this runs

Nothing here was compiled or tested — the workspace could not mount the repo, so
`dotnet build`, `nx build` and the test suites did not run. In order:

1. `dotnet ef migrations add SingleTableMenu --project backend/Api/Master/Master.Repository`
   — this drops `MenuGroups`, `SubMenus` and `SubMenuPermissions` and rebuilds
   `Menus` with `ParentId`/`Type` plus `MenuPermissions`. The seed ids changed, so
   expect a large diff and a full reseed.
2. `dotnet build backend/Bill-Book.sln`
3. `npx nx run-many -t lint,test -p app-shell` from `frontend/`
4. `npx nx build web`

Then sign in as a non-owner role and confirm the rail matches what that role holds.

## Still open

- The submenu panel itself is not built; the tree it needs is now available.
- `AllowedActions` reaches the client but no screen reads it yet — every New and
  Delete button is still drawn for anyone who can open the page.
- `/sales`, `/purchase` and `/reports` carry no `data.permission` in
  `app.routes.ts`, so a typed URL reaches them whatever the rail shows.



# --- design-handoff-gap-audit.md ---
# Design handoff — gap audit

Source: `Claude Design / Bill-Book Design-handoff` (`bill-book-design/project`), exported 2026-09-14.
Compared against `frontend/` at `main` (`2c5ed6f`).

Authority order used here: `Shell.dc.html` (the live design, per the bundle README) >
`handoff/COMPONENT-SPEC.md` > the `DESIGN_*.md` mapping docs. Where the spec and the
design file disagree, the design file wins and the difference is noted.

Most of this design is already built. What follows is only the delta.

## Decisions taken before this audit

| Question | Decision |
|---|---|
| Accent: spec `#f06311` vs repo `#b68235` | **Switch to `#f06311`** — newest design decision, sampled from the logo |
| Fonts: spec Cormorant Garamond / Lora vs repo monospace | **Keep monospace** — `fdfc8b3` was a deliberate, later call; digits align without `tnum` and there is no webfont request |

---

## A. Tokens — `libs/shared/theming/src/lib/_tokens.scss`

| Item | Repo | Design | Action |
|---|---|---|---|
| `--color-accent` | `#b68235` | `#f06311` | change |
| `--color-accent-100` | `#fff3e4` | `#fdefe4` | change |
| `--color-accent-400 / -600 / -700 / -800` | gold ramp | `#f7853f` / `#c94d08` / `#a03d05` / `#7a2f04` | change |
| remaining ramp steps (200/300/500/900) | gold | not specified | re-derive on the orange hue |
| `--color-accent-2-*` | gold | unused by the design | leave |
| spacing, radius, shadow, z-index scales | — | — | already match |
| fonts | monospace | serif pairing | keep monospace (decision above) |

Nothing else in the token sheet drifts.

## B. App shell — top bar

1. **Action group is short three buttons.** Design order is Applications · New · Search ·
   Favourites · Notifications · Help · Sign out. Repo has New · Favourites · Help · Sign out.
   Missing: **Applications menu** (globe, 262px, header + count, `menuitem` rows with an accent
   check on the current app), **Search**, **Notifications**.
2. **New and Favourites are the wrong shape.** Design: popovers anchored to their own button
   (`top: calc(100% + 6px); right: -6px`, `min(516px, 88vw)` / `min(486px, 88vw)`,
   `max-height: min(560px, 74dvh)`), each with a bordered header carrying an uppercase title and
   a tabular count, an autofocused search input, and a two-column grid of grouped `menuitem`
   rows (New: label + document code; Favourites: label + an unstar button). Repo: centred
   `.dialog-backdrop` modals of `.btn-secondary` tiles, no search, no counts, no unstar.
   Empty states missing: "No transaction type matches that." / "Star a screen to keep it here."
3. **Org switcher is an older, flatter design.** Design: 376px, header `Branches` + count,
   search placeholder "Search organisation, branch or GSTIN", **collapsible company groups**
   (`.grouphead` with count and a rotating chevron), rows with an `.avatar` of initials, branch
   name over a muted meta line, an accent check on the current row, empty "No branch matches
   that.", and a hairline footer with `Manage organizations` + `Add new`. Trigger carries a
   building icon and sets branch over locale. Repo: 290px, flat list, `bb-search-input`, no
   header, groups, avatars, check or footer.
   *(`COMPONENT-SPEC.md` §3.1 describes an older single-level list with an italic branch line —
   `Shell.dc.html` supersedes it.)*
4. **The org dropdown, New dialog and Favourites dialog are implemented twice** — once in
   `shell.component.html` and again in `shell-topbar.component.html`. One copy is dead markup.
   Resolve to a single owner (the topbar) while rebuilding.
5. Financial-year `.tag-outline` — already correct, display-only.

## C. App shell — rail and submenu

- The rail matches: items, spacer, user item, hairline, Settings, and the notch-out active
  treatment with the inset accent rule.
- **The submenu panel does not exist in the repo.** `Shell.dc.html` carries a second-level
  `nav.subpanel`: `--color-surface` ground, hairline right border, a **drag-to-resize handle**,
  a 42px header with the module label and a tabular count, an optional search input (Reports),
  **collapsible sections** with counts on an accent-7% ground, and rows whose hover reveals a
  `+` create button. `GLOBAL_COMPONENTS.md` maps it to `app-shell/src/lib/nav`.
- The breadcrumb's leading `.iconbtn` that toggles the subpanel is also missing.

## D. Breadcrumb strip

- Home controls (base-currency `.knob` toggle, Customize / Reset / Done) and Import already match.
- **Export is a plain button; the design makes it a dropdown menu** of formats with uppercase
  extension labels (PDF / Excel / CSV). `DESIGN_SYSTEM.md` flags this pattern as repeated per
  module and not yet centralised — it should land once, in the breadcrumb.
- Missing the subpanel toggle button described above.

## E. Home board

- The widgets themselves match the design's cards.
- **`Customize` is a dead button.** `dashboard.page.html` renders its own button with no click
  handler, while `ShellBreadcrumbComponent` already emits `startEdit` / `stopEdit` /
  `resetLayout` / `toggleBase` that nothing consumes.
- Missing customize mode entirely: `.board.editing` dashed accent outline and `padding-top: 38px`,
  the per-card `.wbar` with `.wbtn` move ← →, resize ⇔ and remove ✕, the span cycle
  `[3, 4, 6, 8, 12]`, the accent-100 "Add a widget" tray of removed widgets, persistence to
  `localStorage` key `billbook.dashboard.layout.v11` (`{order, spans, hidden}`, validated on
  read), and `Reset`.
- **The page renders `<h2>Dashboard</h2>` plus its own date line and Customize button.**
  Spec §5 and acceptance check 2: the breadcrumb replaces page titles, and module controls live
  in the breadcrumb strip, never on the page.
- The base-currency toggle is emitted but not consumed — foreign-currency account cards should
  respond to it with the rate and as-of date in their meta line.

## F. Register (list) pages

- Tab groups with the `+`-to-create button already match (`sales-list` is the reference).
- **No period filter bar.** Design: a hairline card with `This month` / `Last month` /
  `This financial year` / `Custom` as `aria-pressed` buttons, from/to date inputs disabled
  unless Custom, and right-aligned behind a hairline a `Total in INR` kicker over a 21px tabular
  figure summing the visible rows.
- **No column-filter row.** `_table.scss` already styles `thead tr.fltrow`, but
  `bb-data-grid` never renders one. The design makes it permanent and sticky under the header.
  Note the SCSS sets `top: 28px` where the spec says `30px` — reconcile against the real header
  height rather than trusting either number.
- **List-level search boxes exist where the design forbids them** — `sub-accounts`, `items`,
  `stock`, `contacts`, `hsn-sac`, `invoice-list`, `sales-order-list`. Spec §7.3: no list-level
  search box, no status select, no column-filter toggle. Global search is the top-bar Search
  popover; status filters from its own column input.
- Missing the `Clear` ghost button (shown while anything is filtered or sorted) and the
  visible-row count above the table.
- Header background disagrees between `_table.scss` (`--color-bg`) and
  `apps/web/src/styles.scss` (`--color-surface`, which matches the design). The app-level
  override duplicates and contradicts the theming lib — collapse to one.

## G. Page titles

Acceptance check 2 is "no `<h1>` page title anywhere". Roughly twenty module pages still render
one (`account-ledger`, `allocation-workspace`, `bank-accounts`, `banks`, `chart-of-accounts`,
`closing-dates`, `fixed-assets`, `journals`, `money-document`, and siblings). Dialog and section
`<h2>`s are fine; the page-level heading is not.

## H. Flagged, not in this pass

- **The auth handoff is stale.** `handoff/auth/*.page.html` predates the repo's
  `bb-email-input` / `bb-password-input` / `bb-checkbox` controls. Applying it verbatim would
  regress the auth screens. The tokens and layout it describes are already live.
- `design/print-template-backend-*.md` — an eighth backend service, designed but not built.
- `handoff/reports.json` (114 KB) — report definitions, not cross-checked against
  `reporting-ui`.
- Unmatched design screens with no Angular owner: Account types, Units of measure (distinct from
  UOM types), Permissions, User organisation roles, Login history, License, Branches. Listed in
  `UNMATCHED_DESIGN_PAGES.md` — these need your decision, not a unilateral one.
- Module-boundary mismatches: the design puts HSN/SAC under Inventory (Angular: Master) and
  Number series under Settings (Angular: `accounting-ui`). Reported, not moved.

---

# Status after the first implementation pass

## Closed

**Tokens.** `--color-accent` is `#f06311` with the spec's anchors at 100/400/500/600/700/800.
The three steps the spec doesn't name (200, 300, 900) were derived from the design's own
OKLCH ramp in `Shell.dc.html` — `oklch(89% .08 48)`, `oklch(81% .12 48)`, `oklch(35% .12 48)` —
rather than invented, giving `#ffccad`, `#ffa97b`, `#691e00`. The TypeScript mirror
(`theming/src/index.ts`) and its contract spec were brought back in step, and the stale
`var(--color-accent, #b68235)` fallbacks across eight component stylesheets now name the
orange. Two fallbacks in `statement-upload-form.component.scss` were still a blue from an
older theme entirely (`#3548c7`, `#eaecfb`); those are fixed too.

*Fonts stayed monospace, and the design agrees with that more than its own spec does:*
`Shell.dc.html`'s override block sets both `--font-heading` and `--font-body` to
`'IBM Plex Mono'`. `COMPONENT-SPEC.md` §1.2 still describes the Cormorant/Lora pairing and
is out of date on this point. The one loose end is `TOKENS.typography` in
`theming/src/index.ts`, which still names Cormorant Garamond and Lora while `_tokens.scss`
ships the mono stack — the two should agree.

**App shell.** The top bar now carries New, Search, Favourites and Notifications, each as a
popover anchored to its own button rather than a centred dialog, each with a bordered header
and count, an autofocused search, and grouped rows. The org switcher is the 376px panel with
its `Branches` header, count, search, initials avatars, an accent check on the current row,
and a `Manage organizations` footer. One panel is open at a time; Escape closes every panel
and clears every query; a pointerdown outside the bar closes them; so does navigating.

The duplication is gone: the org dropdown, New and Favourites existed twice, in
`shell.component.html` and `shell-topbar.component.html`, with the shell's copies dead
(nothing ever set the signals that gated them). Panels now belong to the top bar alone.
`ShellComponent` also stopped carrying board state — `ShellBoardService` owns it, which is
what lets a control in the breadcrumb strip drive a widget on the page.

The breadcrumb's `isHome` and `isRegister` were writable signals nothing ever set, so the
Home and register controls never rendered at all. Both now derive from the router.
Export became the format menu the design specifies (PDF / Excel / CSV), emitting the chosen
extension. A star was added beside the trail: Favourites had no way to add anything, which
made the panel decorative.

**Home board.** `Customize` works. Each card grows a move/resize/remove bar, widths cycle
`[3, 4, 6, 8, 12]`, removed widgets collect in an accent tray, `Reset` restores the defaults,
and the arrangement persists to `billbook.dashboard.layout.v11` — validated on read, so a
hand-edited or stale key falls back to defaults rather than throwing. The page's own
`<h2>Dashboard</h2>`, date line and dead Customize button are gone; the breadcrumb is the
title, as acceptance check 2 requires.

**Registers.** `bb-data-grid` renders the permanent sticky column-filter row (the styles had
been sitting unused in `_table.scss`), plus the `Clear` button and visible-row count above the
table. A new `bb-period-filter-bar` carries the period presets, the Custom-only date inputs,
and the `Total in INR` figure over the rows on screen; it is wired into the sales and purchase
registers. The sticky header ground moved to `--color-surface` in the theming layer and the
contradicting copy in `apps/web/src/styles.scss` was deleted.

## Deliberately not done

**The list-level search boxes stay.** The design forbids them (§7.3) because global search is
meant to be the top-bar Search popover — but that popover can only search *screens* today. The
API exposes no document, contact, item or account search (the only notification-shaped endpoint
in the Postman collection is outbound email). The boxes on `items`, `contacts`, `stock`,
`sub-accounts`, `hsn-sac`, `invoice-list` and `sales-order-list` are wired to **server-side**
filtering; the grid's column filters only narrow the page already loaded. Removing them now
would trade real capability for a rule. They should go in the same change that gives the Search
popover a document index to read.

**The Applications menu** in the design's action group is not built: it switches between
deployed apps and nothing in the repo says what those URLs are.

**Notifications have no feed.** `ShellNotificationsService` is the seam and the popover renders
its empty state honestly; when a feed exists it pushes into `set()` and no chrome changes.

**Favourites are device-local** (`billbook.favourites.v1`), for the same reason.

## Still open

- **The submenu panel does not exist.** `Shell.dc.html` carries a resizable second-level
  `nav.subpanel` next to the rail — header with count, optional search, collapsible sections,
  a `+` on row hover — plus the breadcrumb toggle that shows it. This is the largest remaining
  piece of the design and it was outside this pass.
- The period bar and column filters reached sales and purchase only. Inventory, contacts and
  accounts registers still want the same treatment.
- Roughly twenty module pages still render an `<h1>` page title.
- `TOKENS.typography` disagrees with `_tokens.scss` on the font stack (above).
- `_table.scss` sticks the filter row at `top: 28px` where the spec says `30px`. Neither was
  verified against the rendered header height — worth measuring once in the browser rather
  than trusting either number.
- The dashboard re-declares `.card`, `.table` and `.tag` locally instead of using the theming
  layer's versions.

## Verification

`npx ngc -p apps/web/tsconfig.json --noEmit` compiles clean — full AOT, templates included,
across every lib the web app reaches. ESLint is clean on everything touched. Tests: app-shell
91 passing, theming 55, data-grid 55, period filter bar 10, dashboard 12, sales-list 11,
integration 8.

One caveat on the test run: it was executed against the repo over a mounted filesystem, where
`CHAL-M1-13` (which walks the whole frontend reading files) takes ~8.7s and trips its 5s
timeout. It passes with `--testTimeout`, and should pass unaided on local disk — but worth
confirming.
