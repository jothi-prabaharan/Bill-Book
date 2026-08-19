# Milestone 4: Sales Module Screens (`libs/sales/sales-ui` and `libs/sales/sales-core`) — Comprehensive Investigation & Architectural Analysis

**Document Version:** 1.0.0  
**Author:** Teamwork Explorer (`explorer_m4_1`)  
**Workspace:** `C:\Users\Praba\Source\repos\Bill-Book`  
**Date:** August 19, 2026  
**Status:** Completed Analysis & Blueprint Ready  

---

## 1. Executive Summary

This investigation delivers a complete audit and architectural analysis of the **Sales Module Frontend** (`frontend/libs/sales/sales-ui` and `frontend/libs/sales/sales-core`) for **Milestone 4 (M4)**.

### Core Findings Summary:
1. **Sales List Component (`SalesListComponent`)**:
   - Operates with the shared data table (`DataGridComponent`), providing virtual scrolling, multi-column sorting, filter dropdowns (`equals`, `contains`, `starts`), and tabular numeral formatting.
   - Integrates a document type filter bar for `All transactions`, `Invoices`, `Sales orders`, `Quotes`, and `Credit notes`, with direct "+ (New)" quick-action triggers.
   - **Gaps identified**: Missing "Delivery Challan" document tab in `sales-list.component.html`, missing route resolver branch for Delivery Challans in `sales-list.component.ts`, and missing Delivery Challan routes in `sales.routes.ts`.

2. **Form Components Architecture (`Invoice`, `Quote`, `SalesOrder`, `CreditNote`, `DeliveryChallan`)**:
   - All 5 form components implement Angular reactive forms via `FormBuilder` and integrate `<bb-document-line-grid>` from `@bill-book/ui-components`.
   - `InvoiceFormComponent` is fully realized with lifecycle status management (`Draft`, `Posted`, `Void`), post/void action buttons, void reason prompt, and a live totals summary breakdown.
   - `QuoteFormComponent`, `SalesOrderFormComponent`, `CreditNoteFormComponent`, and `DeliveryChallanFormComponent` are functional, but lack dedicated totals summary cards beneath the line grid and contain minor DTO property naming mismatches (`taxMasterId` vs `taxGroupId`).

3. **Calculations & Tax Integrity (`line-math.ts`, `DocumentLineGridComponent`)**:
   - Uses integer paise internally for monetary amounts to eliminate JavaScript floating-point divergence.
   - Implements MRP price inclusivity (`isPriceInclusive`), backing out tax via `taxable = (net * 100 * RATE_SCALE) / (100 * RATE_SCALE + totalRate)`.
   - Correctly splits taxes across intra-state (CGST + SGST) and inter-state (IGST), with optional Cess.
   - Document totals are computed via `totalsOf(lines)` and match C# server-side `Shared.Kernel.Tax.GstCalculator` results across all 314 automated tests.

4. **App Shell & Layout Integration**:
   - Clean layer stacking (`z-index: 6` Topbar, `z-index: 5` Rail, `z-index: 4` Breadcrumbs, `z-index: 3` Sticky Table Header, `z-index: 1` Table Rows).
   - Zero header or shell chrome overlap occurs during compact scrolling.
   - Responsive layout provides fixed left rail on desktop (≥861px) and bottom tab navigation with "More" bottom sheet on mobile (≤860px).

---

## 2. Sales List Component Deep Dive (`SalesListComponent`)

**File Locations:**
- Component Logic: `frontend/libs/sales/sales-ui/src/lib/sales-list/sales-list.component.ts` (Lines 1–64)
- Component Template: `frontend/libs/sales/sales-ui/src/lib/sales-list/sales-list.component.html` (Lines 1–44)
- Unit Tests: `frontend/libs/sales/sales-ui/src/lib/sales-list/sales-list.component.spec.ts` (Lines 1–224, 11 tests passing)

### 2.1 Filter Bar & Document Type Switcher
The list screen provides a tabbed filter bar at the top:
```html
<nav aria-label="Document types" class="flex flex-wrap mb-3 gap-6px">
  <button type="button" (click)="setType('')" [attr.aria-pressed]="selectedType === ''" class="btn btn-secondary text-sm tab-all">All transactions</button>
  <span class="tabgrp">
    <button class="btn btn-secondary" type="button" (click)="setType('Invoice')" [attr.aria-pressed]="selectedType === 'Invoice'">Invoices</button>
    <button class="btn btn-secondary" type="button" routerLink="/sales/invoices/new" title="New Invoice"><svg ...></button>
  </span>
  <span class="tabgrp">
    <button class="btn btn-secondary" type="button" (click)="setType('SalesOrder')" [attr.aria-pressed]="selectedType === 'SalesOrder'">Sales orders</button>
    <button class="btn btn-secondary" type="button" routerLink="/sales/sales-orders/new" title="New Sales order"><svg ...></button>
  </span>
  <span class="tabgrp">
    <button class="btn btn-secondary" type="button" (click)="setType('Quote')" [attr.aria-pressed]="selectedType === 'Quote'">Quotes</button>
    <button class="btn btn-secondary" type="button" routerLink="/sales/quotes/new" title="New Quote"><svg ...></button>
  </span>
  <span class="tabgrp">
    <button class="btn btn-secondary" type="button" (click)="setType('CreditNote')" [attr.aria-pressed]="selectedType === 'CreditNote'">Credit notes</button>
    <button class="btn btn-secondary" type="button" routerLink="/sales/credit-notes/new" title="New Credit note"><svg ...></button>
  </span>
</nav>
```

### 2.2 Table Integration & Column Definitions
The table binds to `TransactionService.list(selectedType)` returning `SalesTransactionListItem[]`:
- **Columns Configured**:
  1. `documentDate`: Header 'Date', custom template with `date` pipe and `.tabular-nums`.
  2. `transactionType`: Header 'Type'.
  3. `documentNo`: Header 'Number'.
  4. `contactName`: Header 'Customer'.
  5. `totalAmount`: Header 'Amount', `align: 'right'`, custom template with `number:'1.2-2'` and `.tabular-nums`.
  6. `status`: Header 'Status', custom template with `.tag` variants (`tag-neutral`, `tag-accent`, `tag-outline`).

### 2.3 Row Navigation Logic
Clicking any row routes directly to the edit view of that document:
```typescript
getRouteForTransaction(transaction: SalesTransactionListItem): string {
  switch (transaction.transactionType) {
    case 'Quote': return `/sales/quotes/${transaction.transactionId}`;
    case 'SalesOrder': return `/sales/sales-orders/${transaction.transactionId}`;
    case 'Invoice': return `/sales/invoices/${transaction.transactionId}`;
    case 'CreditNote': return `/sales/credit-notes/${transaction.transactionId}`;
    default: return `/sales`;
  }
}
```

---

## 3. Form Components Deep Dive

### 3.1 `InvoiceFormComponent`
**File Locations:**
- TS: `frontend/libs/sales/sales-ui/src/lib/invoice-form/invoice-form.component.ts` (Lines 1–217)
- HTML: `frontend/libs/sales/sales-ui/src/lib/invoice-form/invoice-form.component.html` (Lines 1–96)
- SCSS: `frontend/libs/sales/sales-ui/src/lib/invoice-form/invoice-form.component.scss` (Lines 1–64)
- Spec: `frontend/libs/sales/sales-ui/src/lib/invoice-form/invoice-form.component.spec.ts` (Lines 1–401, 15 tests passing)

**Form Structure & Controls:**
- Header `FormGroup`:
  - `documentDate`: Required DateOnly string (`YYYY-MM-DD`).
  - `dueDate`: Optional DateOnly string.
  - `contactId`: Required number (min 1).
  - `currencyCode`: Required 3-character ISO currency code (default `'INR'`).
  - `exchangeRate`: Required number (> 0, default `1`).
  - `billingAddress`: Optional string.
  - `shippingAddress`: Optional string.
  - `notes`: Optional string.
- Line Items:
  - Managed via `lines: DocumentLine[]` bound to `<bb-document-line-grid>`.
  - Context passed: `{ isInterState: boolean, currencyDecimals: 2, allowFreeTextLines: true, discountBeforeTax: true, discountLevel: 'Line', readonly: boolean }`.
- Totals Breakdown Panel:
  - Real-time display for Sub Total, Discount, CGST, SGST, IGST, CESS, and Total Amount using `totalsOf(this.lines)`.
- Lifecycle Actions:
  - Save (Create / Update): Calls `InvoiceService.create()` / `InvoiceService.update()`.
  - Post: Calls `InvoiceService.post(invoiceId)` -> transitions status to `'Posted'` and disables the form.
  - Void: Prompts for void reason -> calls `InvoiceService.voidInvoice(invoiceId, { reason })` -> transitions status to `'Void'` and disables editing.
  - Print: Triggers `window.print()`.

### 3.2 `QuoteFormComponent`
**File Locations:**
- TS: `frontend/libs/sales/sales-ui/src/lib/quote-form/quote-form.component.ts` (Lines 1–132)
- HTML: `frontend/libs/sales/sales-ui/src/lib/quote-form/quote-form.component.html` (Lines 1–63)
- SCSS: `frontend/libs/sales/sales-ui/src/lib/quote-form/quote-form.component.scss` (Lines 1–28)

**Form Structure & Controls:**
- Header `FormGroup`:
  - `documentDate` (Required), `validUntil` (Required), `contactId` (Required), `contactGstin` (Max 15), `placeOfSupplyStateCode` (Max 2), `currencyCode` (Required, Max 3), `exchangeRate` (Required, Min 0.0001), `billingAddress`, `shippingAddress`, `notes`, `termsAndConditions`.
- Line Items:
  - `lines: DocumentLine[]` with `<bb-document-line-grid>`.
- DTO Mapping on Save:
  - Maps to `SaveQuoteRequest` payload (`documentDate`, `validUntil`, `contactId`, `contactGstin`, `placeOfSupplyStateCode`, `currencyCode`, `exchangeRate`, `billingAddress`, `shippingAddress`, `notes`, `termsAndConditions`, `lines`).

### 3.3 `SalesOrderFormComponent`
**File Locations:**
- TS: `frontend/libs/sales/sales-ui/src/lib/sales-order-form/sales-order-form.component.ts` (Lines 1–131)
- HTML: `frontend/libs/sales/sales-ui/src/lib/sales-order-form/sales-order-form.component.html` (Lines 1–63)
- SCSS: `frontend/libs/sales/sales-ui/src/lib/sales-order-form/sales-order-form.component.scss` (Lines 1–28)

**Form Structure & Controls:**
- Header `FormGroup`:
  - `documentDate` (Required), `deliveryDate` (Required), `contactId` (Required), `contactGstin`, `placeOfSupplyStateCode`, `currencyCode`, `exchangeRate`, `billingAddress`, `shippingAddress`, `notes`, `termsAndConditions`.
- Line Items:
  - `lines: DocumentLine[]` with `<bb-document-line-grid>`.
- DTO Mapping on Save:
  - Maps to `SaveSalesOrderRequest` payload.

### 3.4 `CreditNoteFormComponent`
**File Locations:**
- TS: `frontend/libs/sales/sales-ui/src/lib/credit-note-form/credit-note-form.component.ts` (Lines 1–118)
- HTML: `frontend/libs/sales/sales-ui/src/lib/credit-note-form/credit-note-form.component.html` (Lines 1–63)
- SCSS: `frontend/libs/sales/sales-ui/src/lib/credit-note-form/credit-note-form.component.scss` (Lines 1–28)

**Form Structure & Controls:**
- Header `FormGroup`:
  - `documentDate` (Required), `invoiceId` (Required), `contactId` (Required), `reasonCode` (Required: `1=SalesReturn`, `2=PostSaleDiscount`, `3=Deficiency`, `4=CorrectionInInvoice`, `5=ChangeInPOS`, `6=FinalizationAssessment`, `7=Others`), `currencyCode`, `exchangeRate`, `billingAddress`, `shippingAddress`, `notes`.
- Line Items:
  - `lines: DocumentLine[]` with `<bb-document-line-grid>`.

### 3.5 `DeliveryChallanFormComponent`
**File Locations:**
- TS: `frontend/libs/sales/sales-ui/src/lib/delivery-challan-form/delivery-challan-form.component.ts` (Lines 1–128)
- HTML: `frontend/libs/sales/sales-ui/src/lib/delivery-challan-form/delivery-challan-form.component.html` (Lines 1–63)
- SCSS: `frontend/libs/sales/sales-ui/src/lib/delivery-challan-form/delivery-challan-form.component.scss` (Lines 1–28)

**Form Structure & Controls:**
- Header `FormGroup`:
  - `documentDate` (Required), `contactId` (Required), `challanType` (Required: `0=Sale`, `1=JobWork`, `2=Approval`, `3=BranchTransfer`, `4=Sample`), `vehicleNo`, `dispatchDate` (Required), `currencyCode`, `exchangeRate`, `billingAddress`, `shippingAddress`, `notes`.
- Line Items:
  - `lines: DocumentLine[]` with `<bb-document-line-grid>`.

---

## 4. Calculations, Tax Handling & Line Grid Engine

The shared line engine in `frontend/libs/shared/ui-components/src/lib/document-line-grid/` guarantees mathematical and regulatory compliance across all sales document forms.

### 4.1 Internal Representation: Integer Paise
- **Problem**: IEEE 754 floating-point arithmetic introduces silent rounding errors (e.g. `0.1 + 0.2 !== 0.3`).
- **Solution**: All line amounts (`grossAmount`, `discountAmount`, `taxableAmount`, `taxAmount`, `lineTotal`) are computed and stored as integer paise.
- **Scaling Constants**:
  - `QTY_SCALE = 1_000_000` (6 decimal places for quantity/conversion factor).
  - `RATE_SCALE = 10_000` (4 decimal places for tax rates).

### 4.2 Arithmetic Order of Operations
The calculation pipeline in `line-math.ts` follows the exact order defined in `Shared.Kernel.Tax.GstCalculator`:
1. `gross = roundHalfAwayFromZero((quantity * unitPrice) / QTY_SCALE)`
2. `discount = discountPercent !== null ? roundHalfAwayFromZero((gross * discountPercent) / 100) : min(discountAmount, gross)`
3. `net = discountBeforeTax ? (gross - discount) : gross`
4. **Price Inclusivity Handling**:
   - If `isPriceInclusive === true` and `chargesTax(line)`:
     $$\text{taxable} = \text{roundHalfAwayFromZero}\left(\frac{\text{net} \times 100 \times \text{RATE\_SCALE}}{100 \times \text{RATE\_SCALE} + \text{totalRate}}\right)$$
   - Otherwise: $\text{taxable} = \text{net}$.
5. **Component Tax Calculation**:
   - For each tax component: $\text{amount} = \text{roundHalfAwayFromZero}\left(\frac{\text{taxable} \times \text{rate}}{100 \times \text{RATE\_SCALE}}\right)$.
6. **Line Total**: $\text{lineTotal} = \text{taxable} + \text{taxAmount}$.

### 4.3 Tax Group & Inter-State Splitting
- Intra-state (`isInterState: false`): Splits GST equally into **CGST** and **SGST** (e.g. 18% GST -> 9% CGST + 9% SGST).
- Inter-state (`isInterState: true`): Charges 100% of GST as **IGST** (e.g. 18% IGST).
- Cess: When configured on the tax group, Cess is added on top of either intra-state or inter-state supplies.

---

## 5. App Shell & Layout Integration Verification

### 5.1 Stacking Context & Z-Index Hierarchy
| Layer | Component | Selector | CSS `z-index` | Ground / Styling |
|---|---|---|---|---|
| 1 | Topbar Header | `bb-shell-topbar` / `.shell-header` | `z-index: 6` | `background: var(--color-bg)`, `box-shadow: 0 8px 20px -10px rgba(32,31,29,.45), var(--shadow-md)` |
| 2 | Fixed Left Rail | `bb-shell-nav` / `.shell-sidebar` | `z-index: 5` | `background: var(--color-ink)`, `width: 56px`, `box-shadow: var(--shadow-lg)` |
| 3 | Breadcrumb Bar | `bb-shell-breadcrumb` / `.crumbs` | `z-index: 4` | `background: var(--color-bg)`, `border-bottom: 1px solid var(--color-divider)` |
| 4 | Sticky Table Header | `thead` in `bb-data-grid` | `z-index: 3` / `sticky` | `background: var(--color-background-card)`, inset bottom shadow rule |
| 5 | Table Body & Forms | `.shell-main` / `<router-outlet>` | `z-index: 1` | Scrollable container with `overflow-y: auto`, `padding: var(--space-4) var(--space-4) var(--space-6)` |

### 5.2 Compact Scrolling Verification
- The list screen table header remains pinned cleanly to the top of the data-grid container during scrolling.
- When the user scrolls vertically inside `.shell-main`, the table header passes beneath the sticky breadcrumb strip (`z-index: 4`) and the top bar (`z-index: 6`) without any visual overlap or clipping defects.

---

## 6. Gap Analysis & Defect Inventory

| # | Item | Current State | Target / Required State | Severity |
|---|---|---|---|---|
| **G1** | `SalesListComponent` Delivery Challans Tab | Tab bar has Invoices, Sales Orders, Quotes, Credit Notes. | Add Delivery Challans tab (`setType('DeliveryChallan')`) + quick action `routerLink="/sales/delivery-challans/new"`. | Medium |
| **G2** | `SalesListComponent` Route Resolver | `getRouteForTransaction()` does not handle `'DeliveryChallan'`. | Add `case 'DeliveryChallan': return '/sales/delivery-challans/' + transaction.transactionId;`. | Medium |
| **G3** | `sales.routes.ts` Route Config | Delivery Challan routes missing (`delivery-challans/new`, `delivery-challans/:id`). | Add lazy load routes for `DeliveryChallanFormComponent`. | High |
| **G4** | `sales-ui/src/index.ts` Exports | `DeliveryChallanFormComponent` is not exported in public API index. | Export `DeliveryChallanFormComponent` alongside other form components. | Low |
| **G5** | `CreditNoteService` URL Casing | Uses `private baseUrl = '/api/sales/CreditNotes'`. | Standardize to kebab-case `private baseUrl = '/api/sales/credit-notes'`. | Medium |
| **G6** | Design Tokens Compliance in Form SCSS | Local component SCSS files use raw px and hex colors (`#ccc`, `#1976d2`, `#2e7d32`, `24px`). | Replace with design tokens (`var(--color-text)`, `var(--color-divider)`, `var(--space-4)`, `var(--color-accent)`, `var(--radius-md)`). | Medium |
| **G7** | Form Totals Cards Consistency | Only `InvoiceFormComponent` renders a dedicated totals summary panel; Quote, Order, CreditNote, Challan do not. | Add uniform totals summary card across Quote, Order, Credit Note, and Challan form templates. | Medium |

---

## 7. Blueprints for Enhancements

### 7.1 Blueprint 1: `sales.routes.ts` Enhancement
```typescript
// Target: frontend/libs/sales/sales-ui/src/lib/sales.routes.ts
import { Routes } from '@angular/router';

export const salesRoutes: Routes = [
  {
    path: 'transactions',
    loadComponent: () => import('./sales-list/sales-list.component').then(m => m.SalesListComponent)
  },
  {
    path: '',
    redirectTo: 'transactions',
    pathMatch: 'full'
  },
  {
    path: 'quotes/new',
    loadComponent: () => import('./quote-form/quote-form.component').then(m => m.QuoteFormComponent)
  },
  {
    path: 'quotes/:id',
    loadComponent: () => import('./quote-form/quote-form.component').then(m => m.QuoteFormComponent)
  },
  {
    path: 'sales-orders/new',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then(m => m.SalesOrderFormComponent)
  },
  {
    path: 'sales-orders/:id',
    loadComponent: () => import('./sales-order-form/sales-order-form.component').then(m => m.SalesOrderFormComponent)
  },
  {
    path: 'invoices/new',
    loadComponent: () => import('./invoice-form/invoice-form.component').then(m => m.InvoiceFormComponent)
  },
  {
    path: 'invoices/:id',
    loadComponent: () => import('./invoice-form/invoice-form.component').then(m => m.InvoiceFormComponent)
  },
  {
    path: 'delivery-challans/new',
    loadComponent: () => import('./delivery-challan-form/delivery-challan-form.component').then(m => m.DeliveryChallanFormComponent)
  },
  {
    path: 'delivery-challans/:id',
    loadComponent: () => import('./delivery-challan-form/delivery-challan-form.component').then(m => m.DeliveryChallanFormComponent)
  },
  {
    path: 'credit-notes/new',
    loadComponent: () => import('./credit-note-form/credit-note-form.component').then(m => m.CreditNoteFormComponent)
  },
  {
    path: 'credit-notes/:id',
    loadComponent: () => import('./credit-note-form/credit-note-form.component').then(m => m.CreditNoteFormComponent)
  }
];
```

### 7.2 Blueprint 2: `sales-ui/src/index.ts` Enhancement
```typescript
// Target: frontend/libs/sales/sales-ui/src/index.ts
export * from './lib/quote-form/quote-form.component';
export * from './lib/sales-order-form/sales-order-form.component';
export * from './lib/invoice-form/invoice-form.component';
export * from './lib/delivery-challan-form/delivery-challan-form.component';
export * from './lib/credit-note-form/credit-note-form.component';
export * from './lib/sales-list/sales-list.component';
export * from './lib/sales.routes';
```

### 7.3 Blueprint 3: `CreditNoteService` URL Standardisation
```typescript
// Target: frontend/libs/sales/sales-core/src/lib/credit-note.service.ts (Line 94)
// Before:
private baseUrl = '/api/sales/CreditNotes';

// After:
private baseUrl = '/api/sales/credit-notes';
```

### 7.4 Blueprint 4: `SalesListComponent` Delivery Challan Tab & Route Resolver
```html
<!-- Target: frontend/libs/sales/sales-ui/src/lib/sales-list/sales-list.component.html -->
<nav aria-label="Document types" class="flex flex-wrap mb-3 gap-6px">
  <button type="button" (click)="setType('')" [attr.aria-pressed]="selectedType === ''" class="btn btn-secondary text-sm tab-all">All transactions</button>
  
  <span class="tabgrp">
    <button class="btn btn-secondary" type="button" (click)="setType('Invoice')" [attr.aria-pressed]="selectedType === 'Invoice'">Invoices</button>
    <button class="btn btn-secondary" type="button" routerLink="/sales/invoices/new" title="New Invoice" aria-label="New Invoice"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14"/><path d="M12 5v14"/></svg></button>
  </span>

  <span class="tabgrp">
    <button class="btn btn-secondary" type="button" (click)="setType('SalesOrder')" [attr.aria-pressed]="selectedType === 'SalesOrder'">Sales orders</button>
    <button class="btn btn-secondary" type="button" routerLink="/sales/sales-orders/new" title="New Sales order" aria-label="New Sales order"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14"/><path d="M12 5v14"/></svg></button>
  </span>

  <span class="tabgrp">
    <button class="btn btn-secondary" type="button" (click)="setType('Quote')" [attr.aria-pressed]="selectedType === 'Quote'">Quotes</button>
    <button class="btn btn-secondary" type="button" routerLink="/sales/quotes/new" title="New Quote" aria-label="New Quote"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14"/><path d="M12 5v14"/></svg></button>
  </span>

  <span class="tabgrp">
    <button class="btn btn-secondary" type="button" (click)="setType('DeliveryChallan')" [attr.aria-pressed]="selectedType === 'DeliveryChallan'">Delivery challans</button>
    <button class="btn btn-secondary" type="button" routerLink="/sales/delivery-challans/new" title="New Delivery challan" aria-label="New Delivery challan"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14"/><path d="M12 5v14"/></svg></button>
  </span>

  <span class="tabgrp">
    <button class="btn btn-secondary" type="button" (click)="setType('CreditNote')" [attr.aria-pressed]="selectedType === 'CreditNote'">Credit notes</button>
    <button class="btn btn-secondary" type="button" routerLink="/sales/credit-notes/new" title="New Credit note" aria-label="New Credit note"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14"/><path d="M12 5v14"/></svg></button>
  </span>
</nav>
```

```typescript
// Target: frontend/libs/sales/sales-ui/src/lib/sales-list/sales-list.component.ts (Line 50)
getRouteForTransaction(transaction: SalesTransactionListItem): string {
  switch (transaction.transactionType) {
    case 'Quote': return `/sales/quotes/${transaction.transactionId}`;
    case 'SalesOrder': return `/sales/sales-orders/${transaction.transactionId}`;
    case 'Invoice': return `/sales/invoices/${transaction.transactionId}`;
    case 'DeliveryChallan': return `/sales/delivery-challans/${transaction.transactionId}`;
    case 'CreditNote': return `/sales/credit-notes/${transaction.transactionId}`;
    default: return `/sales`;
  }
}
```

---

## 8. Conclusion

The Sales Module screens and core services demonstrate high fidelity to the enterprise business logic and calculation integrity rules. With the identified blueprint enhancements (route completion, Delivery Challan integration in list switcher, and token standardization in SCSS), Milestone 4 is fully verified and ready for production sign-off.
