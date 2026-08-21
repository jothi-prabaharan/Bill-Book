# Dispatch Record

## 2026-08-20T18:15:33Z

You are the Project Orchestrator for Stage T3.1 — Invoices (INV) for the Bill-Book ERP SaaS application.

## Working Directory
Your working directory is: `C:\Users\Praba\Source\repos\Bill-Book\.agents\orchestrator_inv_1`
Maintain your `BRIEFING.md`, `plan.md`, and `progress.md` in your working directory.

## Authoritative Request
Authoritative request is recorded at: `C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md`

## Task Description
Build Stage T3.1 — Invoices (INV) for the Bill-Book ERP SaaS application, integrating the backend API with the accounting/inventory engine and constructing the frontend UI.

Working directory: `C:\Users\Praba\Source\repos\Bill-Book`
Integrity mode: benchmark

## Requirements
### R1. Backend Construction (sal and acc Integration)
- Create `sal.SalesInvoice` and `sal.SalesInvoiceDetail` in `backend/Api/Sales/Sales.Entity/` as plain property bags inheriting from `AuditableEntity`. 
- Include status enum (`Draft`, `Posted`, `PartiallyPaid`, `Paid`, `Voided`, `Cancelled`), `SalesOrderId`, tax fields, and `OrgId`.
- Register DbSets and configure global query filter on `OrgId` and RLS in `SalesDbContext`. Use LINQ/EF Core only.
- Implement posting service in a database transaction: transition to `Posted`, generate CAS invoice number, create balanced double-entry rows in `acc.JournalLedger`, and trigger inventory depletion.
- Ensure invoices are immutable after posting (voiding via reversing GL entries).
- Create `SalesInvoicesController` with `[Authorize]`, `[RequireModulePermission]`, and strict cross-org `OrgId` checks (return `Forbid()`).

### R2. Frontend Construction (apps/web & libs/sales)
- Build Invoice List, Create/Edit Form, and View/Print/PDF Layout.
- Implement workflows: Direct creation, Convert from Sales Order, Save as Draft, Post/Finalize, Void. Include a visual GL Breakdown preview before finalizing.
- Ensure full mobile responsiveness (~360px breakpoint), no inline styles, use CSS variables, Bootstrap utility classes, and FontAwesome icons.
- Field-level validation errors must display directly on top of inputs. GL/inventory errors must use the shared message box component.

### R3. Verification & Shipment
- Run local verification: `dotnet build` (clean), `dotnet test`, `npm run check`.
- Update `docs/content/`, `docs.manifest.ts`, and `release-notes.md`.

## Acceptance Criteria
### Backend Verification
- [ ] EF Core Migration for `SalesInvoice` executes successfully.
- [ ] Unit tests pass for tax calculation, posting engine balance checks, and RLS isolation.
- [ ] Attempting to access an invoice from a different `OrgId` returns HTTP 403 Forbidden.
- [ ] Attempting to edit a `Posted` invoice is rejected.

### Frontend Verification
- [ ] `npm run check` passes without lint or typecheck errors.
- [ ] The Invoice creation form successfully posts a new invoice to the backend.
- [ ] The GL Breakdown preview correctly renders the Debit/Credit legs before submission.
