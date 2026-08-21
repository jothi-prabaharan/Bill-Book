# Frontend UI & Architecture Analysis — Stage T3.1: Invoices (apps/web & libs/sales)

**Investigation Date:** 2026-08-20  
**Investigator:** Explorer Agent (Frontend Architecture & UI Specialist)  
**Target Milestone:** Stage T3.1 — Sales Invoices (INV)  
**Status:** Completed Investigation  

---

## 1. Executive Summary

This investigation surveys the frontend architecture in `frontend/apps/web`, `frontend/apps/docs`, and `frontend/libs/` (especially `libs/sales`, `libs/shared`, `libs/accounting`, and `libs/app-shell`) to establish the architectural patterns, component blueprints, state management workflows, validation mechanics, responsive styling rules, and documentation standards required for **Stage T3.1 — Invoices**.

### Key Findings
1. **Frontend Architecture & Angular 20 Standalone Pattern**:
   - Modern Angular 20 standalone components utilizing `inject()`, `signal()`, `computed()`, and `async`/`await` over RxJS streams.
   - The sales module is split cleanly into `libs/sales/sales-core` (business models, HTTP services, mathematical scale mappers) and `libs/sales/sales-ui` (UI components, pages, routes).
   - In `apps/web/src/app/app.routes.ts`, feature routes are lazy-loaded under `path: 'sales'`, guarded by `authGuard`, `licenseActiveGuard`, and `permissionGuard`.

2. **Scaling Boundary & Integer Arithmetic**:
   - The frontend enforces an exact mathematical boundary (`document-line-scale.ts`): the API transfers decimal rupees and plain numbers, whereas the UI grid (`bb-document-line-grid`) operates strictly in **integer paise** (factor of 100) and **6-decimal quantities** (factor of 1,000,000) to eliminate JavaScript IEEE 754 floating-point drift.

3. **Invoice Workflows & Lifecycle**:
   - **List View (`SalesListComponent`)**: Central transaction list with tab filters for Invoices, Sales Orders, Quotes, Delivery Challans, and Credit Notes using `bb-data-grid`.
   - **Form View (`InvoiceFormComponent` / `InvoiceFormPage`)**: Create/edit form supporting direct entry or conversion from Sales Orders (`?salesOrderId=...`), lookup dialogs (`bb-lookup-dialog`) for customers and items, line grid with tax grouping, and header controls.
   - **Lifecycle Actions**:
     - *Save Draft*: Saves/updates document with status `Draft`.
     - *Post/Finalize*: Prompts with live GL breakdown preview, calls `/api/sales/invoices/{id}/post`, transitions to `Posted`, commits balanced GL double entries in `acc.JournalLedger`, triggers inventory depletion, and renders document immutable (`readonly = true`).
     - *Void*: Prompts for mandatory void reason, calls `/api/sales/invoices/{id}/void`, transitions to `Void`, and creates offsetting reversing GL legs.
     - *View / Print / PDF*: High-fidelity printable invoice layout adhering to Indian GST and CAS standards.

4. **Visual GL Breakdown Preview**:
   - Before posting/finalization, an interactive GL Breakdown panel computes and previews the double-entry accounting legs:
     - **Debit**: Accounts Receivable Control Account (`dr. Accounts Receivable` = Grand Total) tagged with Customer Sub-Account.
     - **Credit**: Sales Revenue (`cr. Sales Revenue` = Taxable line totals).
     - **Credit**: Output GST Accounts (`cr. Output CGST`, `cr. Output SGST`, `cr. Output IGST`, `cr. Output Cess` grouped per GST rate and component sub-account).
     - **Debit/Credit**: Round-Off (if applicable).
   - Displays real-time balanced indicator (`Total Debits === Total Credits`).

5. **Design System, Responsiveness (~360px) & Styling**:
   - Custom Classical Design System (`libs/shared/theming/src/lib/`): CSS variables, OKLCH neutral ramp, warm gold accents, BEM class nomenclature (`.page`, `.card`, `.field`, `.banner`, `.btn`).
   - Mobile breakpoint (`max-width: 640px` / `~360px`): Forms collapse to single column (`grid-template-columns: 1fr`), line grids transform into stacked cards with `data-label` pseudo-elements, buttons expand to 100% width, dialogs become full-screen modal sheets.
   - Zero external icon packages; clean inline SVG icons matching Feather/Lucide stroke geometry (`stroke-width="2"`, `stroke-linecap="round"`).

6. **Validation & Shared Error Messaging**:
   - Field-level validation errors rendered directly on top of / above input fields (`.field-error`).
   - Shared message banner system (`.banner .banner--error`, `.banner--ok`, `.banner--info`) for server-side GL, inventory depletion, or cross-org errors.

7. **Documentation & Quality Gates**:
   - User documentation in `frontend/apps/docs/content/invoices.md`, indexed in `frontend/apps/docs/src/app/docs.manifest.ts`.
   - Release notes in `frontend/apps/docs/content/releases.md` under `## Unreleased` > `### Added`.
   - Quality gate `npm run check` (runs `lint`, `typecheck`, `test`, `build`) passes 100% across all 34 test files (448 unit tests).

---

## 2. Directory Layout & Module Structure

The Bill-Book frontend is an Nx monorepo using Angular 20 standalone components.

```
frontend/
├── apps/
│   ├── web/                     # Main ERP client application
│   │   └── src/app/
│   │       ├── app.routes.ts    # Central route definitions
│   │       ├── app.config.ts    # Application configuration & HTTP providers
│   │       └── dashboard/       # Dashboard home page
│   └── docs/                    # Integrated documentation site
│       ├── content/             # Markdown docs (accounting.md, purchase.md, quotes.md, releases.md)
│       └── src/app/
│           └── docs.manifest.ts # Documentation TOC manifest
└── libs/
    ├── sales/
    │   ├── sales-core/          # Models, HTTP services, scaling conversion helpers
    │   │   └── src/lib/
    │   │       ├── invoice.service.ts
    │   │       ├── sales-order.service.ts
    │   │       ├── quote.service.ts
    │   │       ├── credit-note.service.ts
    │   │       ├── delivery-challan.service.ts
    │   │       ├── ledger.service.ts
    │   │       ├── transaction.models.ts
    │   │       └── document-line-scale.ts
    │   └── sales-ui/            # Sales page & form components
    │       └── src/lib/
    │           ├── sales.routes.ts
    │           ├── sales-list/          # Unified transactions data grid
    │           ├── invoice-form/        # Invoice Create/Edit & View
    │           ├── sales-order-form/    # Sales Order Form
    │           ├── quote-form/          # Quote Form
    │           ├── credit-note-form/    # Credit Note Form with invoice allocation
    │           └── delivery-challan-form/
    ├── shared/
    │   ├── ui-components/       # Reusable ERP components (document-line-grid, lookup-dialog, inputs, data-grid)
    │   ├── theming/             # Design tokens, variables, typography, utilities, forms
    │   ├── auth/                # AuthService, guards (authGuard, licenseActiveGuard, permissionGuard)
    │   └── api-client/          # HTTP base URL interceptor
    ├── accounting/              # Accounting UI & core services
    ├── inventory/               # Inventory UI & core services
    └── master/                  # Organization settings, contacts, HSN/SAC, currencies
```

---

## 3. Detailed Component & Service Architecture

### 3.1 `InvoiceService` & Data Models (`libs/sales/sales-core`)
Located at `frontend/libs/sales/sales-core/src/lib/invoice.service.ts`.

#### Data Transfer Objects (DTOs)
```typescript
export interface SaveInvoiceRequest {
  quoteId?: number;
  salesOrderId?: number;
  deliveryChallanId?: number;
  paymentTermId?: number;
  dueDate?: string;
  tillId?: number;
  cashierUserId?: string;
  paymentMode?: string;
  tenderedAmount?: number;
  changeAmount?: number;
  contactId: number;
  documentDate: string;
  currencyCode: string;
  exchangeRate: number;
  notes?: string;
  billingAddress?: string;
  shippingAddress?: string;
  lines: InvoiceLineRequest[];
}

export interface InvoiceLineRequest {
  itemId?: number;
  description?: string;
  hsnSacCode?: string;
  accountId?: number;
  taxTreatment: string;
  taxMasterId?: number;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  lineNotes?: string;
}

export interface VoidInvoiceRequest {
  reason: string;
}

export interface InvoiceView {
  invoiceId: number;
  salesOrderId?: number;
  quoteId?: number;
  documentNo: string;
  documentDate: string;
  dueDate?: string;
  contactId: number;
  contactName: string;
  contactGstin?: string;
  billingAddress?: string;
  shippingAddress?: string;
  placeOfSupplyStateId: number;
  isInterState: boolean;
  currencyCode: string;
  exchangeRate: number;
  subTotal: number;
  discountAmount: number;
  taxableAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  totalAmount: number;
  status: string; // 'Draft' | 'Posted' | 'PartiallyPaid' | 'Paid' | 'Void' | 'Cancelled'
  postedAt?: string;
  postedBy?: string;
  voidedAt?: string;
  voidedBy?: string;
  voidReason?: string;
  notes?: string;
  lines: InvoiceLineView[];
}
```

#### Service Methods
```typescript
@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private http = inject(HttpClient);
  private apiUrl = '/api/sales/invoices';

  list(): Observable<InvoiceListItem[]> { ... }
  get(invoiceId: number): Observable<InvoiceView> { ... }
  create(request: SaveInvoiceRequest): Observable<{ invoiceId: number }> { ... }
  update(invoiceId: number, request: SaveInvoiceRequest): Observable<void> { ... }
  post(invoiceId: number): Observable<void> { ... }
  voidInvoice(invoiceId: number, request: VoidInvoiceRequest): Observable<void> { ... }
}
```

---

### 3.2 Mathematical Scale Boundary (`document-line-scale.ts`)
Located at `frontend/libs/sales/sales-core/src/lib/document-line-scale.ts`.

- **Paise per Rupee**: `const PAISE = 100;`
- **Quantity Scale**: `const QTY_SCALE = 1_000_000;` (matches `decimal(18,6)` on database column).
- **Functions**:
  - `toPaise(rupees: number): number` — converts API decimal rupees to integer paise.
  - `toRupees(paise: number): number` — converts integer paise to API decimal rupees.
  - `toGridLine(line: ApiDocumentLine, lineNumber: number): DocumentLine` — scales API line into integer grid line; leaves computed amounts at 0 so grid recalculates authoritatively.
  - `toApiLine(line: DocumentLine): ApiDocumentLine` — un-scales grid line back into decimal numbers for API serialization.
  - `blankGridLine(lineNumber: number): DocumentLine` — generates default editable line.

---

### 3.3 Unified Sales List Page (`SalesListComponent`)
Located at `frontend/libs/sales/sales-ui/src/lib/sales-list/sales-list.component.ts`.

- **Filter Bar**: Pill button tab group allowing instant filtering:
  - `All transactions` (`""`)
  - `Invoices` (`"Invoice"`) + Quick "+ New Invoice" button
  - `Sales orders` (`"SalesOrder"`) + Quick "+ New Sales order" button
  - `Quotes` (`"Quote"`) + Quick "+ New Quote" button
  - `Delivery challans` (`"DeliveryChallan"`) + Quick "+ New Delivery challan" button
  - `Credit notes` (`"CreditNote"`) + Quick "+ New Credit note" button
- **Columns**: Date, Type, Number, Customer, Amount (right-aligned, tabular nums), Status.
- **Route Dispatcher**: Clicking a row dynamically routes to `/sales/invoices/:id`, `/sales/sales-orders/:id`, `/sales/quotes/:id`, etc.

---

### 3.4 Invoice Form Page & Modern Signal Workflow
To match the standard set by `BillFormPage` (`purchase-ui`) and ensure seamless adherence to AGENTS.md rules:

#### Architecture Pattern
```typescript
@Component({
  selector: 'bb-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    DocumentLineGridComponent,
    LookupDialogComponent,
    DateInputComponent,
    TextInputComponent,
    GlBreakdownPreviewComponent, // or integrated preview panel
  ],
  templateUrl: './invoice-form.component.html',
  styleUrl: './invoice-form.component.scss',
})
export class InvoiceFormComponent implements OnInit {
  // 1. Dependency Injections
  private readonly invoiceService = inject(InvoiceService);
  private readonly salesOrderService = inject(SalesOrderService);
  private readonly lookups = inject(PurchaseLookupService); // or SalesLookupService
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  // 2. Signals for Header State
  readonly invoiceId = signal<number | null>(null);
  readonly documentNo = signal<string | null>(null);
  readonly status = signal<string>('Draft');
  readonly documentDate = signal<string>(new Date().toISOString().split('T')[0]);
  readonly dueDate = signal<string>('');
  readonly contactId = signal<number | null>(null);
  readonly contactLabel = signal<string>('');
  readonly contactGstin = signal<string>('');
  readonly placeOfSupplyStateCode = signal<string>('');
  readonly currencyCode = signal<string>('INR');
  readonly exchangeRate = signal<number>(1);
  readonly billingAddress = signal<string>('');
  readonly shippingAddress = signal<string>('');
  readonly notes = signal<string>('');
  readonly isInterState = signal<boolean>(false);
  readonly salesOrderId = signal<number | null>(null);
  readonly salesOrderNo = signal<string>('');

  // 3. Grid & Tax Signals
  readonly lines = signal<readonly DocumentLine[]>([]);
  readonly taxGroups = signal<readonly TaxGroupOption[]>([]);

  // 4. UI Operation Signals
  readonly saving = signal<boolean>(false);
  readonly posting = signal<boolean>(false);
  readonly voiding = signal<boolean>(false);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  // 5. Lookups & Pickers
  readonly picker = signal<'none' | 'customer' | 'item' | 'order'>('none');
  readonly pickerRows = signal<readonly LookupRow[]>([]);
  readonly pickerLoading = signal<boolean>(false);

  // 6. Computed Properties
  readonly isNew = computed(() => this.invoiceId() === null);
  readonly readonlyDoc = computed(() => this.status() === 'Posted' || this.status() === 'Void');
  readonly totals = computed(() => totalsOf(this.lines()));
  readonly glLegs = computed(() => this.calculateGlPreview());
}
```

---

## 4. Visual GL Breakdown Preview Mechanics

### 4.1 Double-Entry Breakdown Calculations
When an invoice is posted, the backend `LedgerPostingService` expects a balanced set of debit and credit legs in base currency. The visual GL Breakdown component previews these legs directly in the UI before posting:

| Leg # | Side | Account System Name | Sub-Account Reference | Amount (Paise / Currency) | Description |
|---|---|---|---|---|---|
| **1** | **Debit** | `Accounts Receivable` | Contact ID (Customer) | `totals().totalAmount` | Receivable from customer |
| **2** | **Credit** | `Sales` / `Sales Revenue` | None / Item ID | `totals().taxableAmount` | Net revenue from goods/services |
| **3** | **Credit** | `Output CGST` | CGST Tax Rate Sub-Account | `totals().cgstAmount` | Central GST liability |
| **4** | **Credit** | `Output SGST` | SGST Tax Rate Sub-Account | `totals().sgstAmount` | State GST liability |
| **5** | **Credit** | `Output IGST` | IGST Tax Rate Sub-Account | `totals().igstAmount` | Integrated GST liability |
| **6** | **Credit** | `Output CESS` | Cess Tax Rate Sub-Account | `totals().cessAmount` | GST Compensation Cess |
| **7** | **Debit/Credit** | `Round-off` | None | `totals().roundOffAmount` | Rounding adjustment (if any) |

### 4.2 GL Preview Component Model
```typescript
export interface GlLegPreview {
  accountCode: string;
  accountName: string;
  subAccountName?: string;
  debitAmount: number;  // in rupees
  creditAmount: number; // in rupees
}

export interface GlPreviewSummary {
  legs: GlLegPreview[];
  totalDebit: number;
  totalCredit: number;
  difference: number;
  isBalanced: boolean;
}
```

### 4.3 UI Template Structure for GL Breakdown
```html
<section class="card gl-preview-card" *ngIf="lines().length > 0">
  <div class="card__header">
    <h2 class="card__title">Accounting GL Impact Preview</h2>
    <span class="badge" [class.badge--success]="glPreview().isBalanced" [class.badge--danger]="!glPreview().isBalanced">
      {{ glPreview().isBalanced ? 'Balanced Double-Entry' : 'Unbalanced Posting' }}
    </span>
  </div>
  <p class="field__hint">
    Preview of general ledger entries that will be committed to <code>acc.JournalLedger</code> upon posting.
  </p>

  <table class="table table--compact w-full">
    <thead>
      <tr>
        <th>Account</th>
        <th>Sub-Dimension</th>
        <th class="numeric">Debit (₹)</th>
        <th class="numeric">Credit (₹)</th>
      </tr>
    </thead>
    <tbody>
      <tr *ngFor="let leg of glPreview().legs">
        <td><strong>{{ leg.accountName }}</strong></td>
        <td>{{ leg.subAccountName || '—' }}</td>
        <td class="numeric">{{ leg.debitAmount > 0 ? (leg.debitAmount | number:'1.2-2') : '' }}</td>
        <td class="numeric">{{ leg.creditAmount > 0 ? (leg.creditAmount | number:'1.2-2') : '' }}</td>
      </tr>
    </tbody>
    <tfoot>
      <tr class="font-bold">
        <td colspan="2">Total</td>
        <td class="numeric">{{ glPreview().totalDebit | number:'1.2-2' }}</td>
        <td class="numeric">{{ glPreview().totalCredit | number:'1.2-2' }}</td>
      </tr>
    </tfoot>
  </table>
</section>
```

---

## 5. UI Workflows & State Transitions

```
                    ┌─────────────────────────┐
                    │ Direct Entry / Convert  │
                    │   from Sales Order      │
                    └────────────┬────────────┘
                                 │
                                 ▼
                     ┌───────────────────────┐
                     │     Draft Invoice     │◄─────────┐
                     │   (Editable Form)     │          │
                     └─────┬───────────┬─────┘          │
                           │           │                │ Save
               Save Draft  │           │ Post /         │ Updates
                           ▼           │ Finalize       │
                     ┌───────────┐     │                │
                     │  Saved    │─────┘                │
                     │  Draft    ├──────────────────────┘
                     └─────┬─────┘
                           │
             Post Invoice  │ (Validate, Show GL Preview,
                           │  Balance Check)
                           ▼
                    ┌─────────────────────────┐
                    │     Posted Invoice      │
                    │   - Fixed CAS DocNo     │
                    │   - Ledger Posted       │
                    │   - Stock Depleted      │
                    │   - Readonly / Print    │
                    └────────────┬────────────┘
                                 │
                    Void with    │
                    Reason       ▼
                    ┌─────────────────────────┐
                    │     Voided Invoice      │
                    │   - Offsetting GL Legs  │
                    │   - Permanent Record    │
                    │   - Readonly            │
                    └─────────────────────────┘
```

### 5.1 Conversion from Sales Order (`Convert from Sales Order`)
- **Entry Points**:
  1. Navigating to `/sales/invoices/new?salesOrderId=123`.
  2. Clicking "Convert from Sales Order" picker button on the New Invoice screen.
- **Workflow**:
  1. Fetches `SalesOrderView` via `salesOrderService.get(salesOrderId)`.
  2. Populates `contactId`, `contactLabel`, `contactGstin`, `placeOfSupplyStateCode`, `billingAddress`, `shippingAddress`, and `currencyCode`.
  3. Maps order lines via `toGridLine(...)` and passes to `lines.set(mappedLines)`.
  4. Records `salesOrderId` in header state to establish upstream linkage.

### 5.2 Printable / PDF Invoice Layout
- Supports browser printing via `window.print()` and clean CSS print stylesheet (`@media print`):
  - Hides navigation rail, topbar, buttons, banner messages, and picker triggers.
  - Formats standard Indian GST invoice header: Seller Organization Name, Branch Address, GSTIN, CIN, PAN.
  - Buyer Details: "Billed To" & "Shipped To" names, addresses, GSTIN, State code.
  - Tax Invoice title, Invoice Number, Invoice Date, Due Date, Payment Terms, Place of Supply.
  - Tabular Line Items: Sl. No., Item Description, HSN/SAC, Quantity, UOM, Unit Rate, Discount, Taxable Value, CGST (Rate & Amt), SGST (Rate & Amt), Total.
  - HSN/SAC Summary table & Tax Rate breakup.
  - Amount in words, Bank payment details (Account Number, IFSC, Branch), Terms & Conditions, Authorized Signatory box.

---

## 6. Responsive Design, CSS & Styling System

### 6.1 Layout Tokens & Breakpoint Rules
Defined in `libs/shared/theming/src/lib/`:
- **Breakpoints**: Desktop (> 720px), Tablet (641px - 720px), Mobile (<= 640px down to ~360px).
- **Core CSS Variables**:
  - `--color-bg`: Warm paper tone `#f3f2f2`.
  - `--color-surface`: Card surface `#eae9e9` / `#fff`.
  - `--color-text`: High-contrast dark ink `#201f1d`.
  - `--color-accent`: Gold accent `#b68235`.
  - `--color-divider`: Border mix `color-mix(in srgb, #201f1d 16%, transparent)`.
  - `--color-danger`: `#a2332a`, `--color-danger-bg`: `#fdeceb`.
  - `--color-success`: `#187a4b`, `--color-success-bg`: `#e2f3e9`.
  - Spacing: `--space-1` (4.6px) through `--space-8` (36.8px), plus compact ERP density tokens `--space-compact-1` (3px) to `--space-compact-8` (24px).

### 6.2 Mobile Optimizations (~360px)
- **Form Layout**:
  ```scss
  @media (max-width: 640px) {
    .page { padding: 1rem 0.75rem; }
    .grid { grid-template-columns: 1fr; }
    .actions {
      flex-direction: column;
      align-items: stretch;
      .btn { width: 100%; }
    }
  }
  ```
- **Line Grid Transformation**:
  At `max-width: 720px`, the table `thead` is hidden; each row transforms into an individual card block with `data-label` attribute labels on the left and input controls on the right.
- **Lookup Dialog**:
  At `max-width: 640px`, `.lookup-dialog` transitions to a full-screen sheet (`width: 100vw; height: 100dvh; border-radius: 0;`).

### 6.3 Iconography
- SVG icons are embedded inline with `fill="none"`, `stroke="currentColor"`, `stroke-width="2"`, `stroke-linecap="round"`, `stroke-linejoin="round"`.
- Consistent with app-shell topbar and left navigation rail icons.

---

## 7. Validation & Error Handling

### 7.1 Field-Level Validation Display
Per specification: **"Field-level validation errors must display directly on top of inputs."**
In the component HTML:
```html
<div class="field">
  <label class="field__label" for="contactId">Customer <span class="required">*</span></label>
  <span class="field-error" *ngIf="submitted() && !contactId()">Please select a customer.</span>
  <button id="contactId" type="button" class="picker" (click)="openCustomerPicker()">
    {{ contactLabel() || 'Choose a customer' }}
  </button>
</div>
```
In `_forms.scss`:
```scss
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;

  .field-error {
    display: block;
    font-size: 11px;
    font-weight: 600;
    color: var(--color-danger);
    margin-bottom: 2px;
  }
}
```

### 7.2 Shared Message Box / Banner Component
For server-side GL errors, inventory depletion failures, or cross-org authorization errors:
```html
<div class="banner banner--error" role="alert" *ngIf="error()">
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
    <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
  </svg>
  <span>{{ error() }}</span>
</div>

<div class="banner banner--ok" *ngIf="notice()">
  <span>{{ notice() }}</span>
</div>
```

---

## 8. Documentation & Release Notes Conventions

### 8.1 Documentation Manifest & Content
1. **Manifest Registration** (`frontend/apps/docs/src/app/docs.manifest.ts`):
   Update the `Sales` section:
   ```typescript
   {
     title: 'Sales',
     pages: [
       { slug: 'quotes', title: 'Quotes', status: 'built' },
       { slug: 'invoices', title: 'Invoices', status: 'built' },
     ],
   }
   ```
2. **Content File** (`frontend/apps/docs/content/invoices.md`):
   - Scope: Documenting Sales Invoices, direct creation, sales order conversion, posting to general ledger, GST rate application, voiding via reversal, and mobile usage.

### 8.2 Release Notes (`frontend/apps/docs/content/releases.md`)
Add an entry under `## Unreleased` > `### Added`:
```markdown
- **Sales Invoices** — Sales › Invoices, the core billing document. Raise invoices directly or convert confirmed sales orders with pre-filled items, tax rates, and customer details. Features interactive pre-posting GL Breakdown preview displaying balanced debit/credit legs, real-time GST computation (CGST+SGST or IGST based on place of supply), irreversible posting with automatic inventory depletion and CAS sequence numbering, voiding via double-entry GL reversals, and full mobile optimization at ~360px.
```

---

## 9. Verification & Quality Gates

The frontend test infrastructure enforces strict quality gates:

```bash
cd frontend
npm run check
```
Which executes in sequence:
1. `npm run lint` (`nx run-many -t lint`) — Enforces ESLint rules, standalone component usage, no forbidden JS hover events.
2. `npm run typecheck` (`tsc --noEmit -p tsconfig.eslint.json`) — Enforces strict TypeScript typing across all apps and libs.
3. `npm run test` (`vitest run`) — Runs 448+ unit tests across 34 spec suites (all currently passing).
4. `npm run build` (`nx run-many -t build`) — Validates clean production builds for `web` and `docs`.

---

## 10. Conclusion & Recommended Action Plan for Implementation

To deliver Stage T3.1 (Invoices Frontend UI), the implementer should follow these structured steps:
1. **Sales Core (`libs/sales/sales-core`)**: Ensure `InvoiceService` and lookup helpers provide all necessary methods and DTO mappings (e.g. Sales Order fetching for conversion, GL preview helper).
2. **Invoice Form & Component (`libs/sales/sales-ui`)**:
   - Refactor `InvoiceFormComponent` to use Angular 20 signals (`signal`, `computed`), `inject()`, `LookupDialogComponent`, and `document-line-scale.ts`.
   - Implement "Convert from Sales Order" workflow and query parameter handler (`?salesOrderId=...`).
   - Implement visual GL Breakdown Preview panel before finalizing/posting.
   - Implement field-level error messages directly on top of inputs and shared `.banner--error` message box for GL/inventory posting errors.
3. **Printable / PDF View**: Add clean print styling (`@media print`) and layout for invoice printing.
4. **Docs & Release Notes**: Add `frontend/apps/docs/content/invoices.md`, update `docs.manifest.ts`, and add bullet to `releases.md`.
5. **Testing**: Add comprehensive unit tests in `invoice-form.component.spec.ts` covering direct creation, SO conversion, GL preview calculation, Save/Post/Void actions, validation error display, and mobile layout. Verify with `npm run check`.
