# Progress - Worker M4/M5 (Sales & UI Forensic Audit)

Last visited: 2026-08-19T21:08:00Z
Status: COMPLETE

## Completed Tasks
1. [x] Sales List Enhancements:
   - Added Delivery Challans tab and "+ New" button to `sales-list.component.html`.
   - Added `DeliveryChallan` route resolver to `sales-list.component.ts`.
   - Added `delivery-challans/new` and `delivery-challans/:id` routes to `sales.routes.ts`.
   - Exported `DeliveryChallanFormComponent` from `frontend/libs/sales/sales-ui/src/index.ts`.
2. [x] Sales Form Totals & DTO Binding:
   - Integrated `totalsOf(this.lines)` and `.totals-panel` summary cards (Sub Total, Discount, CGST, SGST, IGST, CESS, Total Amount) in:
     - `QuoteFormComponent` (`quote-form.component.ts`, `.html`, `.scss`)
     - `SalesOrderFormComponent` (`sales-order-form.component.ts`, `.html`, `.scss`)
     - `CreditNoteFormComponent` (`credit-note-form.component.ts`, `.html`, `.scss`)
     - `DeliveryChallanFormComponent` (`delivery-challan-form.component.ts`, `.html`, `.scss`)
   - Verified DTO mappings align with backend request DTOs (`SaveQuoteRequest`, `SaveSalesOrderRequest`, `SaveCreditNoteRequest`, `SaveDeliveryChallanRequest`, `SaveInvoiceRequest`).
3. [x] Standardized API endpoint URL:
   - Updated `CreditNoteService` baseUrl to `/api/sales/credit-notes`.
4. [x] Forensic UI String Audit ("Accounts" vs "Accounting"):
   - Replaced all user-visible instances of "Accounting" with "Accounts":
     - `frontend/apps/docs/src/app/docs.manifest.ts`
     - `frontend/libs/shared/auth/src/lib/components/auth-shell/auth-shell.component.html`
     - `frontend/libs/shared/auth/src/lib/pages/accept-invitation/accept-invitation.page.html`
     - `frontend/libs/shared/auth/src/lib/pages/trial-expired/trial-expired.page.html`
   - Verified that zero instances of user-visible "Accounting" remain anywhere in frontend templates.
5. [x] Remaining Module Verification:
   - Audited Purchase, Inventory, Master, Accounts modules for compact `bb-data-grid` data table bindings and reactive form DTO contracts.
6. [x] Unit Test Suite & Verification:
   - Enhanced `sales-list.component.spec.ts` with `DeliveryChallan` route resolution test.
   - Enhanced `sales-forms.spec.ts` with `comp.totals` computation assertions across Quote, SalesOrder, CreditNote, and DeliveryChallan forms.
   - Passed all 31 test files / 411 tests.
   - Fixed lint issues and typecheck errors in app-shell and ui-components spec files.
   - Verified full `npm run check` (17 project lints, tsc typecheck, 411 vitest tests, 3 production builds) passed with code 0.
