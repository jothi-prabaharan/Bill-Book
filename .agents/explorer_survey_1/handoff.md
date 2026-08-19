# Frontend Shared UI Library Survey & Primitive Input Component Architecture Report

## 1. Observation

### 1.1 Codebase & Library Architecture
- **Location & Exports**: Shared UI components reside in `frontend/libs/shared/ui-components/src/lib/` and are exported via barrel `frontend/libs/shared/ui-components/src/index.ts`.
- **Existing Components**:
  - `DocumentLineGridComponent` (`bb-document-line-grid`) in `document-line-grid/`
  - `AllocationGridComponent` (`bb-allocation-grid`) in `allocation-grid/`
  - `DataGridComponent` (`bb-data-grid`) in `data-grid/`
  - `LookupDialogComponent` (`bb-lookup-dialog`) in `lookup-dialog/`
  - `ReportGridComponent` (`bb-report-grid`), `FilterBarComponent` (`bb-filter-bar`), `ColumnChooserDialogComponent`, `GroupPanelComponent`, `PivotPanelComponent` in `report-grid/`
  - `BankGraphCardComponent` (`bb-bank-graph-card`), `CardTableComponent` (`bb-card-table`)
- **Package Configuration**:
  - `frontend/package.json` pins `@angular/core`, `@angular/forms`, `@angular/common`, `@angular/cdk`, `@angular/router` at `^20.0.0`.
  - Zero external UI libraries (e.g., Material, PrimeNG, Bootstrap, Syncfusion) are installed or permitted per `AGENTS.md`.
- **Path Aliases**:
  - `frontend/tsconfig.base.json` registers `@bill-book/ui-components` -> `libs/shared/ui-components/src/index.ts`.

### 1.2 Form Binding Patterns Across Features
Across the 5 UI libraries (`accounting-ui`, `inventory-ui`, `master-ui`, `purchase-ui`, `sales-ui`), two distinct form binding mechanisms are in active use:
1. **Template-Driven 2-Way Binding (`[(ngModel)]` / `[ngModel]` / `(ngModelChange)`)**:
   - `opening-balance.page.html` (lines 31, 35, 114, 122, 131, 147, 164, 183, 200)
   - `account-ledger.page.html` (lines 22, 26)
   - `bank-accounts.page.html` (lines 104, 135, 141, 167, 173)
   - `bill-form.page.html` (lines 63-64, 80-81, 93-94, 105-106, 119-120, 132-133)
   - `items.page.html` (lines 99, 104, 110, 219, 225, 230, 238, 376, 381, 423, 428)
   - `contacts.page.html` (lines 109, 114, 156, 174, 179, 194, 200, 206)
2. **Reactive Forms (`formControlName` / `formGroup`)**:
   - `invoice-form.component.html` (lines 20-53: `formControlName="documentDate"`, `formControlName="dueDate"`, `formControlName="contactId"`, `formControlName="currencyCode"`, `formControlName="exchangeRate"`)
   - `credit-note-form.component.html` (lines 10, 14, 18, 34, 38)
   - `delivery-challan-form.component.html` (lines 10, 14, 26, 30, 34, 38)
   - `quote-form.component.html` (lines 10, 14, 18, 22, 26, 30, 34)
   - `sales-order-form.component.html` (lines 10, 14, 18, 22, 26, 30, 34)
   - `shared/auth` login, signup, forgot-password pages.

### 1.3 Absence of Universal ControlValueAccessor
- Grep search for `ControlValueAccessor` across `frontend/` returned 0 matches.
- Currently, raw `<input>` elements are duplicated directly in HTML templates, leading to inconsistent styling, duplicated parsing/formatting logic, and fragmented accessibility attributes.

### 1.4 Styling Conventions & Design Tokens
- **Source of Truth**: `frontend/apps/web/src/styles.scss` defines core CSS custom properties:
  - Colors: `--color-bg`, `--color-surface`, `--color-text`, `--color-accent` (`#f06311`), `--color-divider`, `--color-ink`
  - Radii: `--radius-sm: 2px`, `--radius-md: 4px`, `--radius-lg: 7px`
  - Typography: `--font-heading: 'Cormorant Garamond'`, `--font-body: 'Lora'`
  - Input styling class: `.input` (lines 146-154, 513-518):
    ```scss
    .input {
      width: 100%; min-height: 36px; padding: 6px 10px; font: inherit;
      font-size: 14px; color: var(--color-text); caret-color: var(--color-accent);
      background: transparent;
      border: 1px solid var(--color-divider); border-radius: var(--radius-md);
    }
    .input:hover { border-color: color-mix(in srgb, var(--color-text) 45%, transparent); }
    .input:focus-visible { border-color: var(--color-accent); outline-offset: 0; }
    .input:disabled { opacity: .6; cursor: not-allowed; background: color-mix(in srgb, var(--color-text) 4%, transparent); }
    ```
  - Responsive rule: Every component and modal is designed to fit a 360px viewport without horizontal scrolling, switching table layouts to stacked cards or full-screen sheets on mobile.

---

## 2. Logic Chain

```
Observation 1.1 (Angular 20 Standalone library with strict package constraints)
  + Observation 1.2 (Coexistence of Template-driven [(ngModel)] and Reactive formControlName)
  + Observation 1.3 (Zero existing CVA implementations; 150+ raw HTML inputs)
  + Observation 1.4 (Centralized CSS tokens & .input design system rules)
  ──> Step 1: Any reusable input component MUST implement Angular's `ControlValueAccessor` interface
              with `NG_VALUE_ACCESSOR` provider to support both `[(ngModel)]` and `formControlName` transparently.
  ──> Step 2: Components must be standalone (`standalone: true`), provide `forwardRef` CVA bindings,
              and expose signals/computed properties internally for responsive, reactive state updates.
  ──> Step 3: Primitive input categories must be standardized into distinct, specialized components:
              (A) `DateInputComponent` (`bb-date-input`) for calendar & ISO date manipulation
              (B) `CurrencyInputComponent` (`bb-currency-input`) for monetary values with symbols, decimals, tabular alignment
              (C) `NumberInputComponent` (`bb-number-input`) for quantities, integers, percentages, and scaling factors
              (D) `SearchInputComponent` (`bb-search-input`) for filter/search bars with debounce and clear triggers
  ──> Step 4: All components must be exported from `@bill-book/ui-components` (`libs/shared/ui-components/src/index.ts`)
              and referenced in consuming pages to replace raw `<input>` elements.
```

---

## 3. Component Architecture & API Specification

### 3.1 Date Input Component (`bb-date-input`)

#### Purpose
Standardized date input supporting ISO 8601 strings (`'YYYY-MM-DD'`), min/max constraints, placeholder, disabled/readonly states, and mobile touch targets.

#### Location
`frontend/libs/shared/ui-components/src/lib/date-input/`
- `date-input.component.ts`
- `date-input.component.html`
- `date-input.component.scss`

#### API Contract
```typescript
@Component({
  selector: 'bb-date-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => DateInputComponent),
      multi: true,
    },
  ],
  templateUrl: './date-input.component.html',
  styleUrl: './date-input.component.scss',
})
export class DateInputComponent implements ControlValueAccessor {
  // Inputs
  readonly id = input<string>('');
  readonly name = input<string>('');
  readonly placeholder = input<string>('YYYY-MM-DD');
  readonly min = input<string | null>(null);
  readonly max = input<string | null>(null);
  readonly disabled = input<boolean>(false);
  readonly readonly = input<boolean>(false);
  readonly required = input<boolean>(false);
  readonly ariaLabel = input<string>('Date');
  readonly size = input<'sm' | 'md' | 'lg'>('md');

  // Outputs
  readonly valueChange = output<string | null>();
  readonly blur = output<FocusEvent>();
  readonly focus = output<FocusEvent>();

  // CVA State
  protected readonly innerValue = signal<string | null>(null);
  protected readonly isDisabled = signal<boolean>(false);
  // ... ControlValueAccessor implementation
}
```

#### Template & Styles
- Renders `<input type="date" class="input bb-date-input" ... />`
- Supports WebKit calendar indicator styling, 360px viewport full width touch targets, and design token integration.

---

### 3.2 Currency / Money Input Component (`bb-currency-input`)

#### Purpose
Specialized input for currency amounts (e.g. Invoices, Bills, Debits, Credits, Prices, Bank Limits) supporting currency prefix (₹), decimal places, right alignment, tabular numbers, and formatted view vs editable view.

#### Location
`frontend/libs/shared/ui-components/src/lib/currency-input/`
- `currency-input.component.ts`
- `currency-input.component.html`
- `currency-input.component.scss`

#### API Contract
```typescript
@Component({
  selector: 'bb-currency-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CurrencyInputComponent),
      multi: true,
    },
  ],
  templateUrl: './currency-input.component.html',
  styleUrl: './currency-input.component.scss',
})
export class CurrencyInputComponent implements ControlValueAccessor {
  // Inputs
  readonly id = input<string>('');
  readonly name = input<string>('');
  readonly symbol = input<string>('₹');
  readonly currencyCode = input<string>('INR');
  readonly showSymbol = input<boolean>(true);
  readonly decimals = input<number>(2);
  readonly min = input<number | null>(0);
  readonly max = input<number | null>(null);
  readonly step = input<number | string>(0.01);
  readonly placeholder = input<string>('0.00');
  readonly disabled = input<boolean>(false);
  readonly readonly = input<boolean>(false);
  readonly required = input<boolean>(false);
  readonly allowNegative = input<boolean>(false);
  readonly inPaise = input<boolean>(false); // if true, converts paise integer <-> decimal rupees
  readonly align = input<'left' | 'right'>('right');

  // Outputs
  readonly valueChange = output<number | null>();
  readonly blur = output<FocusEvent>();
  readonly focus = output<FocusEvent>();

  // CVA State
  protected readonly innerValue = signal<number | null>(null);
  protected readonly isFocused = signal<boolean>(false);
  protected readonly isDisabled = signal<boolean>(false);
  // ... ControlValueAccessor implementation
}
```

#### Template & Styles
- Displays currency symbol prefix container `.bb-currency-prefix`
- Tabular numerals (`font-variant-numeric: tabular-nums; text-align: right;`)
- Handles formatting on blur and editing on focus cleanly without cursor jumps.

---

### 3.3 Number / Quantity Input Component (`bb-number-input`)

#### Purpose
Versatile numeric input for quantities, percentages (0-100%), integers, days, factors, and batch numbers, supporting prefixes/suffixes (e.g. `%`, `kg`, `days`, `nos`), step scaling, and min/max boundaries.

#### Location
`frontend/libs/shared/ui-components/src/lib/number-input/`
- `number-input.component.ts`
- `number-input.component.html`
- `number-input.component.scss`

#### API Contract
```typescript
@Component({
  selector: 'bb-number-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => NumberInputComponent),
      multi: true,
    },
  ],
  templateUrl: './number-input.component.html',
  styleUrl: './number-input.component.scss',
})
export class NumberInputComponent implements ControlValueAccessor {
  // Inputs
  readonly id = input<string>('');
  readonly name = input<string>('');
  readonly min = input<number | null>(null);
  readonly max = input<number | null>(null);
  readonly step = input<number | string>(1);
  readonly decimals = input<number | null>(null);
  readonly placeholder = input<string>('');
  readonly prefix = input<string | null>(null);
  readonly suffix = input<string | null>(null);
  readonly disabled = input<boolean>(false);
  readonly readonly = input<boolean>(false);
  readonly required = input<boolean>(false);
  readonly align = input<'left' | 'right' | 'center'>('right');
  readonly inputmode = input<'decimal' | 'numeric'>('decimal');

  // Outputs
  readonly valueChange = output<number | null>();
  readonly blur = output<FocusEvent>();
  readonly focus = output<FocusEvent>();

  // CVA State
  protected readonly innerValue = signal<number | null>(null);
  protected readonly isDisabled = signal<boolean>(false);
  // ... ControlValueAccessor implementation
}
```

---

### 3.4 Search Input Component (`bb-search-input`)

#### Purpose
Centralized search input with search icon, clear `×` action, keyboard shortcuts (Enter), and debounced event emission.

#### Location
`frontend/libs/shared/ui-components/src/lib/search-input/`
- `search-input.component.ts`
- `search-input.component.html`
- `search-input.component.scss`

#### API Contract
```typescript
@Component({
  selector: 'bb-search-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SearchInputComponent),
      multi: true,
    },
  ],
  templateUrl: './search-input.component.html',
  styleUrl: './search-input.component.scss',
})
export class SearchInputComponent implements ControlValueAccessor {
  readonly id = input<string>('');
  readonly placeholder = input<string>('Search...');
  readonly ariaLabel = input<string>('Search');
  readonly disabled = input<boolean>(false);
  
  readonly search = output<string>();
  readonly clear = output<void>();
  readonly valueChange = output<string>();
}
```

---

## 4. Refactoring Matrix across Frontend Pages

| Module | Page / Component | Existing Raw Input | Target New Component | Binding Mode |
|---|---|---|---|---|
| **Accounting** | `opening-balance.page.html` | `<input type="date">` (asOfDate, docDate) | `<bb-date-input>` | `[(ngModel)]` |
| | `opening-balance.page.html` | `<input type="number">` (quantity) | `<bb-number-input [step]="0.001">` | `[(ngModel)]` |
| | `opening-balance.page.html` | `<input type="number">` (unitCost, debit, credit) | `<bb-currency-input>` | `[(ngModel)]` |
| | `account-ledger.page.html` | `<input type="date">` (from, to) | `<bb-date-input>` | `[(ngModel)]` |
| | `journals.page.html` | `<input type="date">` (journalDate) | `<bb-date-input>` | `[(ngModel)]` |
| | `journals.page.html` | `<input type="number">` (debit, credit) | `<bb-currency-input>` | `[(ngModel)]` |
| | `bank-accounts.page.html` | `<input type="number">` (odLimit) | `<bb-currency-input>` | `[(ngModel)]` |
| | `money-document.page.html` | `<input type="date">`, `<input type="number">` | `<bb-date-input>`, `<bb-currency-input>` | `[(ngModel)]` |
| | `numbering-series.page.html`| `<input type="number">` (startNumber, length) | `<bb-number-input>` | `[(ngModel)]` |
| | `payment-terms.page.html` | `<input type="number">` (dueDays, discountDays) | `<bb-number-input>` | `[(ngModel)]` |
| | `tax-master.page.html` | `<input type="date">`, `<input type="number">` (rate) | `<bb-date-input>`, `<bb-number-input [suffix]="'%'">` | `[(ngModel)]` |
| | `transfer-money.page.html` | `<input type="date">`, `<input type="number">` | `<bb-date-input>`, `<bb-currency-input>` | `[(ngModel)]` |
| | `trial-balance.page.html` | `<input type="date">` (from, to) | `<bb-date-input>` | `[(ngModel)]` |
| **Purchase** | `bill-form.page.html` | `<input type="date">` (vendorBillDate, docDate, dueDate) | `<bb-date-input>` | `[ngModel] / (ngModelChange)` |
| | `debit-note-form.page.html`| `<input type="date">`, `<input type="number">` | `<bb-date-input>`, `<bb-currency-input>` | `[ngModel] / (ngModelChange)` |
| | `goods-receipt-form.page.html`| `<input type="date">`, `<input type="number">` | `<bb-date-input>`, `<bb-number-input>` | `[ngModel] / (ngModelChange)` |
| | `purchase-order-form.page.html`| `<input type="date">` | `<bb-date-input>` | `[ngModel] / (ngModelChange)` |
| **Sales** | `invoice-form.component.html` | `<input type="date">` (documentDate, dueDate) | `<bb-date-input>` | `formControlName` |
| | `invoice-form.component.html` | `<input id="contactId">`, `<input id="exchangeRate">` | `<bb-number-input>` | `formControlName` |
| | `credit-note-form.component.html`| `<input type="date">`, `<input id="exchangeRate">` | `<bb-date-input>`, `<bb-number-input>` | `formControlName` |
| | `delivery-challan-form.component.html`| `<input type="date">`, `<input id="exchangeRate">` | `<bb-date-input>`, `<bb-number-input>` | `formControlName` |
| | `quote-form.component.html` | `<input id="documentDate">`, `<input id="validUntil">` | `<bb-date-input>` | `formControlName` |
| | `sales-order-form.component.html`| `<input id="documentDate">`, `<input id="deliveryDate">` | `<bb-date-input>` | `formControlName` |
| **Inventory**| `items.page.html` | `<input type="search">` | `<bb-search-input>` | `[(ngModel)]` |
| | `items.page.html` | `<input type="number">` (salesPrice, mrp, purchasePrice) | `<bb-currency-input>` | `[(ngModel)]` |
| | `items.page.html` | `<input type="number">` (reorderLevel, leadTimeDays, weights) | `<bb-number-input>` | `[(ngModel)]` |
| | `stock-adjustments.page.html`| `<input type="date">`, `<input type="number">` | `<bb-date-input>`, `<bb-number-input>` | `[(ngModel)]` |
| | `metal-purities.page.html` | `<input type="number">` (purityFactor) | `<bb-number-input [step]="0.0001">` | `[(ngModel)]` |
| | `unit-types.page.html` | `<input type="number">` (decimalPlaces, conversion) | `<bb-number-input>` | `[(ngModel)]` |
| **Master** | `contacts.page.html` | `<input type="search">` | `<bb-search-input>` | `[(ngModel)]` |
| | `contacts.page.html` | `<input type="date">` (issuedOn, expiresOn) | `<bb-date-input>` | `[(ngModel)]` |
| | `contacts.page.html` | `<input type="number">` (creditLimit) | `<bb-currency-input>` | `[(ngModel)]` |
| | `contacts.page.html` | `<input type="number">` (maxOutstandingDays, maxDiscount) | `<bb-number-input>` | `[(ngModel)]` |
| | `configurations.page.html`| `<input type="date">`, `<input type="number">` | `<bb-date-input>`, `<bb-number-input>` | `[(ngModel)]` |
| **Reporting**| `report-host.page.html` | `<input [type]="parameter.dataType">` | `<bb-date-input>` / `<bb-number-input>` | `[ngModel] / (ngModelChange)` |
| **Shared** | `allocation-grid.component.html`| `<input type="number" class="amount-input">` | `<bb-currency-input>` | `[ngModel] / (ngModelChange)` |
| | `document-line-grid.component.html`| `<input type="number">` (quantity, unitPrice, discount) | `<bb-number-input>`, `<bb-currency-input>` | `[(ngModel)]` |

---

## 5. Caveats

1. **Pre-existing Typecheck Errors**:
   - `libs/sales/sales-ui/src/lib/delivery-challan-form/delivery-challan-form.component.ts(71,7)`: type mismatch with `DocumentLine[]`.
   - `libs/shared/ui-components/src/lib/data-grid/data-grid.component.ts(56-81)`: renamed variable `_c` referenced as `c`.
   - These are pre-existing issues in the codebase outside this explorer task's scope; implementation agents must ensure new components themselves typecheck cleanly with 0 warnings.
2. **Ionic Compatibility Requirement**:
   - In accordance with `AGENTS.md`, no component may reference DOM global `window`, `document`, or Node APIs. Native inputs and standard Angular event emitters must be used.
3. **Paise vs Rupees Semantics**:
   - Back-end line totals in sales/purchase documents store monetary amounts as integer paise (`Paise = Rupees * 100`). `CurrencyInputComponent` provides an optional `[inPaise]="true"` mode or defaults to standard decimal numbers for accounting pages that store decimals.

---

## 6. Conclusion

- Creating standalone `DateInputComponent`, `CurrencyInputComponent`, `NumberInputComponent`, and `SearchInputComponent` inside `libs/shared/ui-components` with full `ControlValueAccessor` implementations provides a robust, zero-dependency, 100% compliant solution.
- The design seamlessly accommodates both Reactive Forms (`formControlName`) in sales/auth and Template-driven forms (`[(ngModel)]`) across accounting, inventory, master, and purchase libraries.
- The proposed contracts, APIs, and refactoring matrix guarantee unified design-token styling, tabular data alignment, touch-friendly 360px mobile responsiveness, and clean maintainability.

---

## 7. Verification Method

To independently verify the survey and resulting component implementations:
1. **Source Inspection**:
   - Inspect newly created components in `frontend/libs/shared/ui-components/src/lib/{date-input,currency-input,number-input,search-input}/`.
   - Verify barrel exports in `frontend/libs/shared/ui-components/src/index.ts`.
2. **Unit Tests**:
   - Run vitest suite:
     ```powershell
     cd frontend
     npx vitest run
     ```
   - Verify that all CVA methods (`writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState`) have corresponding unit tests.
3. **Typecheck & Build**:
   - Run typecheck and check command:
     ```powershell
     cd frontend
     npm run check
     ```
4. **Visual & Behavioral Spot Check**:
   - Check `opening-balance.page.html` and `invoice-form.component.html` to confirm that `<bb-date-input>`, `<bb-currency-input>`, and `<bb-number-input>` render properly, bind two-way values, honor disabled states, and respond smoothly to user input.
