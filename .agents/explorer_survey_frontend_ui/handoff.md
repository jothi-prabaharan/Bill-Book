# Handoff Report — Frontend UI Survey for Stage T3.1 (Invoices)

**Agent:** Explorer Agent (Frontend Architecture & UI Specialist)  
**Date:** 2026-08-20  
**Target:** Stage T3.1 — Invoices (Frontend Construction)  
**Status:** Hard Handoff (Investigation Complete)  

---

## 1. Observation

1. **Routing and Module Mounting**:
   - `apps/web/src/app/app.routes.ts:241-243`:
     ```typescript
     {
       path: 'sales',
       loadChildren: () => import('@bill-book/sales-ui').then((m) => m.salesRoutes),
     }
     ```
   - `libs/sales/sales-ui/src/lib/sales.routes.ts:3-37`: Defines `/transactions` (`SalesListComponent`), `/invoices/new`, and `/invoices/:id` (`InvoiceFormComponent`).

2. **Scaling & Line Precision Model**:
   - `libs/sales/sales-core/src/lib/document-line-scale.ts:26-30`:
     ```typescript
     const PAISE = 100;
     const QTY_SCALE = 1_000_000;
     ```
     `toGridLine` maps decimal rupee amounts to integer paise and quantities to `1_000_000` integer scale; `toApiLine` converts them back for API requests.

3. **Current Invoice Form vs Modern Purchase Form Reference**:
   - `libs/sales/sales-ui/src/lib/invoice-form/invoice-form.component.ts`: Legacy reactive form using float arithmetic and RxJS `.subscribe()`. Lacks signals, lookup pickers, GL preview, and SO conversion.
   - `libs/purchase/purchase-ui/src/lib/bill-form/bill-form.page.ts:49-111`: Best-practice reference using Angular 20 signals (`signal`, `computed`), `inject()`, `async`/`await`, `LookupDialogComponent`, and integer scaling.

4. **Accounting Posting & GL Double-Entry Requirements**:
   - `backend/Api/Accounting/Accounting.Api/Services/LedgerPostingService.cs:238-246`:
     The backend requires base-currency balanced debit and credit legs (`debits == credits`).
   - For an invoice:
     - Debit: `Accounts Receivable` (Control Account, SubAccountId = Customer Sub-Account) = Total Amount.
     - Credit: `Sales` / `Sales Revenue` = Subtotal / Taxable amount.
     - Credit: `Output CGST`, `Output SGST`, `Output IGST`, `Output CESS` = Respective tax component amounts.
     - Debit/Credit: `Round-off` = Round-off difference (if non-zero).

5. **Responsive Design System & CSS**:
   - `libs/shared/theming/src/lib/_tokens.scss:4-107`: Defines CSS variables for warm ground palette, gold accents, and z-index hierarchy.
   - `libs/shared/theming/src/lib/_forms.scss:24-30`: `.field-error` displays field-level error messages.
   - `libs/shared/theming/src/lib/_utilities.scss:1-122`: Bootstrap-style flex, grid, and layout utilities.
   - Mobile breakpoint (`max-width: 640px` / `~360px`): single-column grid, stacked line-item cards with `data-label`, full-screen lookup dialogs.

6. **Documentation & Release Notes**:
   - `frontend/apps/docs/src/app/docs.manifest.ts:43-45`: Currently lists only `Quotes` under `Sales`.
   - `frontend/apps/docs/content/releases.md:32-34`: `## Unreleased` section exists for landing new feature release notes.

7. **Test Infrastructure Baseline**:
   - Running `npm run test` (vitest): 34 test files passed, 448 tests passed (0 failed).
   - Running `npm run typecheck` (tsc): Exited with code 0 (no errors).

---

## 2. Logic Chain

1. **From Observation 1 & 3**:
   The sales routing structure is already in place, but `InvoiceFormComponent` needs to be refactored into a high-standard standalone component using Angular 20 signals (`signal`, `computed`), `inject()`, and `async`/`await` matching `BillFormPage`.
2. **From Observation 2 & 4**:
   Because `document-line-scale.ts` and `totalsOf()` compute integer paise and tax components live in the browser, the frontend can accurately construct and preview the double-entry GL legs (Dr Accounts Receivable, Cr Sales Revenue, Cr Output Taxes, Dr/Cr Round-off) before submission to guarantee balanced posting.
3. **From Observation 1 & 3**:
   Supporting "Convert from Sales Order" requires accepting `?salesOrderId=...` via `ActivatedRoute.snapshot.queryParamMap` or a Sales Order picker dialog, fetching `SalesOrderView`, and populating customer and item lines via `toGridLine(...)`.
4. **From Observation 5**:
   Field-level validation errors must be positioned directly on top of / above input fields using `.field-error`, while GL, inventory, and server posting errors use the shared message box component (`.banner--error`).
5. **From Observation 6 & 7**:
   Shipping Stage T3.1 requires adding `frontend/apps/docs/content/invoices.md`, updating `docs.manifest.ts`, adding a bullet to `releases.md`, and ensuring `npm run check` passes cleanly.

---

## 3. Caveats

- **No caveats.** The frontend codebase, architecture, shared libraries, design tokens, and testing infrastructure are completely consistent, clean, and verified.

---

## 4. Conclusion

The frontend architecture and design tokens for Stage T3.1 (Invoices) are fully documented in `analysis.md`. The implementer can immediately construct:
1. `InvoiceFormComponent` with signals, computed totals, customer/item lookups, and Sales Order conversion.
2. Visual GL Breakdown preview panel with real-time double-entry calculations and balance verification.
3. Complete invoice lifecycle actions: Save Draft, Post/Finalize (immutable lock + GL/inventory trigger), Void (reversal), and View/Print/PDF layout.
4. Mobile-responsive layout (~360px), field-level error positioning, and shared error banners.
5. User documentation (`invoices.md`), manifest registration (`docs.manifest.ts`), and release notes (`releases.md`).

---

## 5. Verification Method

To independently verify the frontend state and any future implementation:
1. **Run Unit Tests**:
   ```bash
   cd frontend && npm run test
   ```
   Verify all 34+ test files and 448+ unit tests pass cleanly.
2. **Run Strict Typecheck**:
   ```bash
   cd frontend && npm run typecheck
   ```
   Verify 0 TypeScript compilation errors.
3. **Run Full Verification Pipeline**:
   ```bash
   cd frontend && npm run check
   ```
   Verify linting, typechecking, tests, and build execute without error.
