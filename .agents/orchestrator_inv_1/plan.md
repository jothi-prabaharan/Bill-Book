# Plan — Stage T3.1: Invoices (INV)

## 1. Objectives
Implement end-to-end support for Invoices (INV) in RetailErp:
- Backend: `sal.SalesInvoice` & `sal.SalesInvoiceDetail` entities, `SalesDbContext` integration with OrgId query filter & RLS, posting engine creating balanced `acc.JournalLedger` double-entries and triggering inventory depletion, invoice immutability after posting, `SalesInvoicesController` with strict org isolation and authorization.
- Frontend: Standalone Angular 20 components in `apps/web` and `libs/sales`, full workflows (Direct, Convert from SO, Draft, Post/Finalize, Void), interactive GL Breakdown preview before posting, responsive design (~360px), field validation and error handling.
- Verification: Clean `dotnet build`, unit tests passing (tax calculation, posting engine balance, RLS isolation, cross-org 403, posted immutability), `npm run check` clean, documentation updated (`docs/content/`, `docs.manifest.ts`, `release-notes.md`).

## 2. Orchestration Roadmap
- **Survey Phase**: Dispatch 3 Explorer subagents:
  1. Backend Sales & Accounting Architecture Explorer (inspect existing Sales entities, SalesDbContext, SalesOrder, Accounting JournalLedger, Inventory depletion patterns, migrations, RLS policies).
  2. Backend Controller & Authorization / API Patterns Explorer (inspect existing Controllers, permissions, OrgContext, error handling, unit test suites).
  3. Frontend Architecture & UI Patterns Explorer (inspect `libs/sales`, `apps/web`, existing order/quote pages, components, CSS variables, validation styling, routing, docs).
- **Decomposition & Specification**: Synthesize survey findings into `PROJECT.md` with full feature inventory, milestone definitions, interface contracts, and code layout.
- **Milestone Execution & Dual-Track**:
  - Implementation track (Milestones M1 -> M2 -> M3 -> M4).
  - E2E Testing track.
  - Verification & Audit.
