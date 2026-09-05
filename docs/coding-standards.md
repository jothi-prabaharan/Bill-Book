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
