## 2026-08-20T18:50:00Z
You are the Worker agent implementing Milestone 3: Frontend Construction (libs/sales & apps/web) for Stage T3.1 - Invoices.
Your working directory is: C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m3_frontend_ui_2
Maintain your progress.md and handoff.md in your working directory.

Context & Inputs:
- Read C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md
- Read C:\Users\Praba\Source\repos\Bill-Book\AGENTS.md
- Read C:\Users\Praba\Source\repos\Bill-Book\.agents\orchestrator_inv_1\PROJECT.md
- Read survey reports:
  - C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_frontend_ui\analysis.md
  - C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_survey_frontend_ui\handoff.md

Write Ownership (Exclusive):
- frontend/libs/sales/sales-core/src/lib/models/
- frontend/libs/sales/sales-core/src/lib/services/
- frontend/libs/sales/sales-core/src/index.ts
- frontend/libs/sales/sales-ui/src/lib/invoice-form/
- frontend/libs/sales/sales-ui/src/lib/invoice-view/
- frontend/libs/sales/sales-ui/src/lib/invoice-gl-preview/
- frontend/libs/sales/sales-ui/src/lib/sales-list/
- frontend/libs/sales/sales-ui/src/lib/sales.routes.ts
- frontend/libs/sales/sales-ui/src/index.ts
- frontend/apps/web/src/

Scope & Tasks:
1. Update `sales-core` models and service:
   - Complete `sales-invoice.model.ts` with invoice headers, lines, tax views, GL preview models (`GlPreviewResult`, `GlEntryLegView`), and DTOs.
   - Update `sales.service.ts` with `getInvoices`, `getInvoice`, `getInvoiceGlPreview`, `saveInvoiceDraft`, `postInvoice`, `voidInvoice`.
2. Construct/refactor `InvoiceFormComponent`:
   - Standalone Angular 20 component with `inject()`, `signal()`, `computed()`, and `async`/`await`.
   - Scaled line grid arithmetic via `document-line-scale.ts` and `toGridLine` / `toApiLine`.
   - Customer and Item pickers using `LookupDialogComponent` (`bb-lookup-dialog`).
   - Workflows: Direct creation, Convert from Sales Order via query param (`?salesOrderId=...`), Save Draft, Post/Finalize, Void (with reason dialog).
   - Visual GL Breakdown preview panel or dialog showing real-time Dr Accounts Receivable, Cr Sales, Cr Output GST, Dr/Cr Round-off and balance verification indicator prior to final posting.
   - View / Print / PDF printable layout formatted for GST tax invoices.
   - Responsive layout down to ~360px mobile viewport, CSS variables, Bootstrap utility classes, FontAwesome icons.
   - Field-level validation on top of inputs (`.field-error`) and shared message banner (`.banner--error`) for GL/inventory posting errors.
3. Update routing in `sales.routes.ts` to wire `/invoices`, `/invoices/new`, `/invoices/:id`, `/invoices/:id/print`, and ensure list integration in `SalesListComponent`.
4. Verify by running `npm run typecheck` and `npm run test` in `frontend/`.
5. Write your handoff report to `C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m3_frontend_ui_2\handoff.md` and message parent when complete.
