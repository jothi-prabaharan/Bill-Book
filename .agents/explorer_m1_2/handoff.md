# Explorer 2 Handoff Report: Form Input Patterns & ControlValueAccessor (CVA) Contracts

**Date**: 2026-08-18  
**Author**: Explorer 2 (Milestone 1 — Shared Primitive UI Components)  
**Target Audience**: Sub-orchestrator M1, Implementers (Coder Agents), Test Engineers  

---

## 1. Observation

A systematic audit was conducted across the frontend codebase to analyze all input usage patterns, form bindings, validation rules, data formatting, and edge cases across `frontend/libs/`.

### 1.1 Codebase Survey Across Domain Modules

| Domain Module | Key Pages Audited | Primary Form Paradigm | Input Types Observed | Critical Attributes & Events Observed |
|---|---|---|---|---|
| **Accounting** (`libs/accounting/accounting-ui`) | `opening-balance`, `journals`, `bank-accounts`, `banks`, `chart-of-accounts`, `closing-dates`, `money-document`, `numbering-series`, `payment-terms`, `tax-master`, `transfer-money` | Template-driven forms (`[(ngModel)]`, `(ngModelChange)`) with Signals state | `date`, `text`, `number`, `checkbox` | `[disabled]="finalized()"`, `maxlength="11"` with `class="uppercase"` (IFSC, SWIFT), `step="0.01"` / `step="0.001"`, `min="0"`, `inputmode="decimal"`, `(ngModelChange)="onDebit(row)"` |
| **Inventory** (`libs/inventory/inventory-ui`) | `items`, `item-categories`, `metal-purities`, `stock-adjustments`, `unit-types`, `warehouses` | Template-driven forms (`[(ngModel)]`), signal-based form objects, nested maps | `search`, `text`, `number`, `checkbox`, `date` | `type="search"` with `(keyup.enter)="load()"`, `placeholder="Search this list"`, `step="0.0001"` (prices, purity factor), `step="0.001"` (weights, reorder levels), `min="0.0001" max="1"`, `maxlength="200"`, `[disabled]="editingId() !== null"` |
| **Master** (`libs/master/master-ui`) | `contacts`, `organizations`, `configurations`, `contact-person-roles`, `hsn-sac`, `roles`, `smtp-settings`, `users` | Template-driven forms (`[(ngModel)]`), dynamic data lists | `search`, `text`, `email`, `number`, `date`, `checkbox` | `class="uppercase"` with `maxlength="15"` (GSTIN), `maxlength="10"` (PAN), `type="email"`, `creditLimit` (step 0.01), `maxDiscountPercent` (min 0 max 100), `type="date"` (licence issued/expires), dynamic type switching in `configurations.page.html` |
| **Purchase** (`libs/purchase/purchase-ui`) | `bill-form`, `debit-note-form`, `goods-receipt-form`, `purchase-order-form` | Signal-driven template forms (`[ngModel]="sig()"`, `(ngModelChange)="sig.set($event)"`) | `date`, `text`, `number` | `[disabled]="readonlyDoc()"`, `maxlength="50"`, `name="vendorBillNo"`, `contactGstin` (maxlength 15), `placeOfSupplyStateCode` (maxlength 2) |
| **Sales** (`libs/sales/sales-ui`) | `invoice-form`, `credit-note-form`, `delivery-challan-form`, `quote-form`, `sales-order-form` | Reactive Forms (`[formGroup]="form"`, `formControlName="..."`) | `date`, `text`, `number`, `textarea` | `formControlName="documentDate"`, `formControlName="dueDate"`, `formControlName="contactId"`, `formControlName="currencyCode"`, `formControlName="exchangeRate"`, `step="0.01"` |

### 1.2 Exact Code References & Observations

1. **`frontend/libs/accounting/accounting-ui/src/lib/opening-balance/opening-balance.page.html` (Lines 31, 35, 121, 142-203)**:
   - Line 31: `<input type="date" [(ngModel)]="asOfDate" name="asOfDate" [disabled]="finalized()" />`
   - Lines 142-151:
     ```html
     <input
       type="number"
       inputmode="decimal"
       step="0.001"
       min="0"
       [(ngModel)]="row.quantity"
       [name]="'qty' + lines().indexOf(row)"
       (ngModelChange)="touch()"
       [disabled]="finalized()"
     />
     ```
   - Lines 178-187:
     ```html
     <input
       type="number"
       inputmode="decimal"
       step="0.01"
       min="0"
       [(ngModel)]="row.debit"
       [name]="'debit' + lines().indexOf(row)"
       (ngModelChange)="onDebit(row)"
       [disabled]="finalized()"
     />
     ```
   - TypeScript model in `opening-balance.page.ts` (lines 88–98):
     `quantity: string; unitCost: string; debit: string; credit: string;`
     *Observation*: String models are used in some accounting forms so empty inputs stay blank rather than becoming `0`.

2. **`frontend/libs/inventory/inventory-ui/src/lib/items/items.page.html` (Lines 20, 110, 219, 423)**:
   - Line 20: `<input type="search" [(ngModel)]="search" (keyup.enter)="load()" placeholder="Search this list" aria-label="Search items" ... />`
   - Line 219: `<input type="number" [(ngModel)]="form.salesPrice" step="0.0001" />`
   - Line 423: `<input type="number" [(ngModel)]="form.jewellery['grossWeight']" step="0.001" />`

3. **`frontend/libs/master/master-ui/src/lib/contacts/contacts.page.html` (Lines 27, 156, 174)**:
   - Line 27: `<input type="search" [(ngModel)]="search" (keyup.enter)="load()" placeholder="Name, code or GSTIN" ... />`
   - Line 156: `<input type="text" [(ngModel)]="form.gstin" maxlength="15" class="uppercase" />`
   - Line 174: `<input type="text" [(ngModel)]="form.pan" maxlength="10" class="uppercase" />`

4. **`frontend/libs/purchase/purchase-ui/src/lib/bill-form/bill-form.page.html` (Lines 58-137)**:
   - Lines 58-66:
     ```html
     <input
       type="text"
       class="input"
       maxlength="50"
       [disabled]="readonlyDoc()"
       [ngModel]="vendorBillNo()"
       (ngModelChange)="vendorBillNo.set($event)"
       name="vendorBillNo"
     />
     ```

5. **`frontend/libs/sales/sales-ui/src/lib/invoice-form/invoice-form.component.html` (Lines 20-40)**:
   - Lines 22-24:
     ```html
     <div class="field">
       <label for="docDate">Document Date</label>
       <input id="docDate" type="date" class="input" formControlName="documentDate" />
     </div>
     ```
   - Lines 38-40:
     ```html
     <div class="field">
       <label for="exchangeRate">Exchange Rate</label>
       <input id="exchangeRate" formControlName="exchangeRate" step="0.01" />
     </div>
     ```

6. **Current `frontend/libs/shared/ui-components/src/`**:
   - `NG_VALUE_ACCESSOR` is not yet implemented in any shared component.
   - All primitive inputs in feature libraries currently use raw HTML `<input>` elements with various inline styles, classes, and manual event bindings.

---

## 2. Logic Chain

From the observations above, the following logical inferences dictate the CVA and component architecture:

1. **Diverse Form Paradigms Must Coexist**:
   - `sales-ui` relies exclusively on Reactive Forms (`formControlName`, `formGroup`).
   - `purchase-ui` uses Angular Signal state with unidirectional template bindings `[ngModel]="sig()"` `(ngModelChange)="sig.set($event)"`.
   - `accounting-ui`, `inventory-ui`, `master-ui` rely on two-way `[(ngModel)]` with `(ngModelChange)` and template-driven validation.
   - *Conclusion*: Every new primitive component must strictly implement `ControlValueAccessor` (`NG_VALUE_ACCESSOR` multi-provider) to guarantee compatibility across all three patterns without requiring changes to form architecture.

2. **Signal-Based Disabled State Precedence**:
   - In template-driven forms, disabled state is controlled via `[disabled]="condition"`.
   - In Reactive Forms, disabled state is managed via `formControl.disable()` which triggers `setDisabledState(isDisabled)`.
   - If an input component only binds `[disabled]`, `setDisabledState` has no effect. If it only binds `setDisabledState`, `[disabled]="true"` from template has no effect.
   - *Conclusion*: The component must combine an internal signal `cvaDisabled = signal(false)` and an input signal `disabled = input<boolean>(false)` into a computed `effectiveDisabled = computed(() => this.disabled() || this.cvaDisabled())`.

3. **CVA Lifecycle & Purity (Loop Prevention)**:
   - When `writeValue(value)` is invoked by Angular Forms, updating internal DOM/signal state must NEVER call `onChange()` or emit `valueChange`. Calling `onChange()` inside `writeValue()` marks pristine forms as dirty and can trigger infinite feedback loops in reactive forms.
   - User DOM events (`input`, `change`, `blur`) MUST call `onChange(val)`, `onTouched()`, and emit component outputs (`valueChange`, `blur`, `focus`).

4. **Numeric Parsing and Coercion**:
   - JavaScript DOM `<input>` always produces string `.value`.
   - Blank strings `""` must convert to `null` (not `0` or `NaN`), preserving form optionality.
   - `0` is a valid number and must NOT be coerced to `null`.
   - `inPaise = true` requires scaling: display `value / 100` (e.g. 10050 paise -> "100.50"), parse user input to `Math.round(parsed * 100)` on change.
   - Micro-quantities (`0.0001` purity factor, `0.001` weights, `0.0001` purchase prices) require flexible decimal stepping and precision preservation.

5. **Uppercase Transformation**:
   - GSTIN (15 chars) and PAN (10 chars) require uppercase letters. Visual CSS `text-transform: uppercase` only transforms rendering, leaving lowercase characters in the JS string value.
   - *Conclusion*: `TextInputComponent` with `uppercase: true` must transform values using `val.toUpperCase()` in JavaScript before propagating to `onChange()` and `valueChange`.

6. **Date Normalization**:
   - HTML5 date inputs require `YYYY-MM-DD`. Backend APIs may supply full ISO strings (e.g., `"2026-08-18T00:00:00Z"`).
   - *Conclusion*: `DateInputComponent.writeValue` must parse and normalize strings starting with `YYYY-MM-DD` to the 10-character date portion.

---

## 3. Caveats

1. **No Checkbox / Radio / Select Primitive Components in M1**:
   - M1 scope explicitly covers the 5 primitive input components: `DateInputComponent`, `CurrencyInputComponent`, `NumberInputComponent`, `SearchInputComponent`, `TextInputComponent`.
   - Checkboxes (`<input type="checkbox">`), selects, and textareas will continue using standard HTML or separate future components.
2. **Native Date Picker WebKit Variations**:
   - Mobile and desktop browsers render native date pickers differently. Using native `<input type="date">` internally is intentional for mobile/Ionic support without bundling heavy datepicker dependencies.
3. **No External Library Dependencies**:
   - Per `AGENTS.md` and `PROJECT.md`, `Directory.Packages.props` and `package.json` are strictly closed. All icons (search magnifier, clear cross, currency symbol) are inline SVGs or CSS text.

---

## 4. Conclusion & Complete Design Specifications

### 4.1 Global CVA Base Architecture Pattern

Every primitive component follows this exact Angular 20 Standalone CVA structure:

```typescript
import { Component, forwardRef, signal, computed, input, output } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'bb-[component-name]',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => [ComponentName]),
      multi: true,
    },
  ],
  templateUrl: './[component-name].component.html',
  styleUrl: './[component-name].component.scss',
})
export class [ComponentName] implements ControlValueAccessor {
  // Input Signals
  readonly id = input<string>('');
  readonly name = input<string>('');
  readonly disabled = input<boolean>(false);
  readonly readonly = input<boolean>(false);
  readonly required = input<boolean>(false);

  // Output Signals
  readonly valueChange = output<T | null>();
  readonly blur = output<FocusEvent>();
  readonly focus = output<FocusEvent>();

  // Internal State Signals
  protected readonly innerValue = signal<T | null>(null);
  private readonly cvaDisabled = signal<boolean>(false);
  protected readonly effectiveDisabled = computed(() => this.disabled() || this.cvaDisabled());

  // CVA Callbacks
  private onChange: (value: T | null) => void = () => {};
  private onTouched: () => void = () => {};

  // CVA Contract Methods
  writeValue(value: any): void {
    const normalized = this.normalizeValue(value);
    this.innerValue.set(normalized);
  }

  registerOnChange(fn: (value: T | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.cvaDisabled.set(isDisabled);
  }

  // Event Handlers
  protected handleInput(raw: any): void {
    const parsed = this.parseInput(raw);
    this.innerValue.set(parsed);
    this.onChange(parsed);
    this.valueChange.emit(parsed);
  }

  protected handleBlur(event: FocusEvent): void {
    this.onTouched();
    this.blur.emit(event);
  }

  protected handleFocus(event: FocusEvent): void {
    this.focus.emit(event);
  }
}
```

---

### 4.2 Component Specification & Edge Case Matrix

#### 1. `DateInputComponent` (`<bb-date-input>`)
- **Selector**: `bb-date-input`
- **Location**: `libs/shared/ui-components/src/lib/date-input/`
- **Model Value**: `string | null` (ISO format `YYYY-MM-DD`)
- **Inputs**:
  - `id: input<string>('')`
  - `name: input<string>('')`
  - `placeholder: input<string>('')`
  - `min: input<string | null>(null)`
  - `max: input<string | null>(null)`
  - `disabled: input<boolean>(false)`
  - `readonly: input<boolean>(false)`
  - `required: input<boolean>(false)`
  - `ariaLabel: input<string>('Date')`
- **Outputs**:
  - `valueChange: output<string | null>()`
  - `blur: output<FocusEvent>()`
  - `focus: output<FocusEvent>()`
- **Edge Cases & Transformation Logic**:
  - `writeValue`:
    - `null` / `undefined` / `""` -> set `innerValue` to `""`.
    - `"2026-08-18T00:00:00Z"` or `"2026-08-18"` -> slice first 10 chars matching `^\d{4}-\d{2}-\d{2}` -> `"2026-08-18"`.
  - `handleInput`:
    - If `raw` is `""` or invalid -> emit `null` via `onChange` and `valueChange`.
    - If valid `YYYY-MM-DD` -> emit `"YYYY-MM-DD"`.
  - Styling:
    - Scoped styling `.date-input` with native `appearance: none`, accent tint on calendar picker indicator (`::-webkit-calendar-picker-indicator { cursor: pointer; opacity: 0.7; }`).

---

#### 2. `CurrencyInputComponent` (`<bb-currency-input>`)
- **Selector**: `bb-currency-input`
- **Location**: `libs/shared/ui-components/src/lib/currency-input/`
- **Model Value**: `number | null` (or integer paise when `inPaise: true`)
- **Inputs**:
  - `id: input<string>('')`
  - `name: input<string>('')`
  - `symbol: input<string>('₹')`
  - `currencyCode: input<string>('INR')`
  - `showSymbol: input<boolean>(false)`
  - `decimals: input<number>(2)`
  - `min: input<number | null>(null)`
  - `max: input<number | null>(null)`
  - `step: input<number | string>(0.01)`
  - `placeholder: input<string>('0.00')`
  - `disabled: input<boolean>(false)`
  - `readonly: input<boolean>(false)`
  - `required: input<boolean>(false)`
  - `allowNegative: input<boolean>(false)`
  - `inPaise: input<boolean>(false)`
  - `align: input<'left' | 'right'>('right')`
  - `ariaLabel: input<string>('Amount')`
- **Outputs**:
  - `valueChange: output<number | null>()`
  - `blur: output<FocusEvent>()`
  - `focus: output<FocusEvent>()`
- **Edge Cases & Transformation Logic**:
  - **Zero vs Empty**:
    - `0` is a valid number -> display `"0.00"` (or `"0"`).
    - `null` / `undefined` / `""` -> display `""`.
  - **`inPaise` Mode**:
    - When `inPaise()` is true:
      - `writeValue(10050)` -> internal display `"100.50"`.
      - User types `"50.25"` -> `onChange(5025)` (`Math.round(50.25 * 100)`).
    - When `inPaise()` is false:
      - `writeValue(100.5)` -> internal display `"100.50"`.
      - User types `"50.25"` -> `onChange(50.25)`.
  - **Negative Values**:
    - If `allowNegative()` is false, negative values are clamped or `-` sign stripped.
    - If `allowNegative()` is true, allow negative numbers (e.g. for ledger adjustments, credit notes).
  - **Blur Formatting vs Focus Editing**:
    - On blur, format to fixed decimals (`val.toFixed(decimals)`).
    - Tabular numbers font: `font-variant-numeric: tabular-nums`.

---

#### 3. `NumberInputComponent` (`<bb-number-input>`)
- **Selector**: `bb-number-input`
- **Location**: `libs/shared/ui-components/src/lib/number-input/`
- **Model Value**: `number | null`
- **Inputs**:
  - `id: input<string>('')`
  - `name: input<string>('')`
  - `min: input<number | null>(null)`
  - `max: input<number | null>(null)`
  - `step: input<number | string>(1)`
  - `decimals: input<number | null>(null)`
  - `placeholder: input<string>('')`
  - `prefix: input<string | null>(null)`
  - `suffix: input<string | null>(null)`
  - `disabled: input<boolean>(false)`
  - `readonly: input<boolean>(false)`
  - `required: input<boolean>(false)`
  - `align: input<'left' | 'right' | 'center'>('left')`
  - `inputmode: input<'decimal' | 'numeric'>('decimal')`
  - `ariaLabel: input<string>('Number')`
- **Outputs**:
  - `valueChange: output<number | null>()`
  - `blur: output<FocusEvent>()`
  - `focus: output<FocusEvent>()`
- **Edge Cases & Transformation Logic**:
  - Coercion: Handles both `number` and `string` in `writeValue` (e.g., `writeValue("15.5")` or `writeValue(15.5)`).
  - Empty string parses to `null`.
  - Handles micro-steps like `step="0.0001"` (gold purity factors, forex rates) and `step="0.001"` (weights, kg).
  - Prefix / Suffix display using flex layout with `.input-addon` prefix/suffix span tags.

---

#### 4. `SearchInputComponent` (`<bb-search-input>`)
- **Selector**: `bb-search-input`
- **Location**: `libs/shared/ui-components/src/lib/search-input/`
- **Model Value**: `string`
- **Inputs**:
  - `id: input<string>('')`
  - `name: input<string>('')`
  - `placeholder: input<string>('Search...')`
  - `ariaLabel: input<string>('Search')`
  - `disabled: input<boolean>(false)`
  - `debounceMs: input<number>(300)`
- **Outputs**:
  - `search: output<string>()` (triggered immediately on Enter key press or when debounce timer fires)
  - `clear: output<void>()` (triggered when clear button is clicked)
  - `valueChange: output<string>()` (triggered immediately on every keystroke)
- **Edge Cases & Transformation Logic**:
  - Native clear button reset: `::-webkit-search-cancel-button { display: none; }`.
  - Accessible clear button with SVG `×` icon appears whenever text length > 0 and component is not disabled.
  - Hitting `Enter` emits `search.emit(term)` immediately and cancels any pending debounce timer.

---

#### 5. `TextInputComponent` (`<bb-text-input>`)
- **Selector**: `bb-text-input`
- **Location**: `libs/shared/ui-components/src/lib/text-input/`
- **Model Value**: `string`
- **Inputs**:
  - `id: input<string>('')`
  - `name: input<string>('')`
  - `type: input<'text' | 'email' | 'password' | 'tel' | 'url'>('text')`
  - `placeholder: input<string>('')`
  - `maxlength: input<number | null>(null)`
  - `uppercase: input<boolean>(false)`
  - `disabled: input<boolean>(false)`
  - `readonly: input<boolean>(false)`
  - `required: input<boolean>(false)`
  - `autocomplete: input<string>('off')`
  - `ariaLabel: input<string>('')`
- **Outputs**:
  - `valueChange: output<string>()`
  - `blur: output<FocusEvent>()`
  - `focus: output<FocusEvent>()`
  - `enter: output<string>()`
- **Edge Cases & Transformation Logic**:
  - `uppercase: true`:
    - Transforms string via `raw.toUpperCase()` in JS.
    - Applies CSS class `.uppercase { text-transform: uppercase; }`.
    - Updates native DOM element value to match uppercase string so caret behavior remains aligned.
  - Handles `null` / `undefined` in `writeValue` safely by setting internal value to `""`.
  - Emits `enter` output on Enter key press.

---

## 5. Verification Method

To verify the design and implementation of these CVA components, the following steps and commands should be executed:

1. **Unit Test Verification**:
   - Each component must have a co-located `.spec.ts` testing:
     - Standalone instantiation & DOM rendering.
     - CVA `writeValue`, `registerOnChange`, `registerOnTouched`, and `setDisabledState`.
     - Two-way binding with `[(ngModel)]`.
     - Reactive forms binding with `[formControl]`.
     - Signal input changes (`disabled`, `min`, `max`, `uppercase`, `inPaise`).
     - Key edge cases: empty strings vs null, `0` handling, NaN rejection, uppercase conversion, debounce & clear actions.
   - Test execution command:
     ```powershell
     cd C:\Users\Praba\Source\repos\Bill-Book\frontend
     npx vitest run libs/shared/ui-components
     ```

2. **Lint and Typecheck Verification**:
     ```powershell
     cd C:\Users\Praba\Source\repos\Bill-Book\frontend
     npm run lint
     npm run typecheck
     ```

3. **Full Project Build Verification**:
     ```powershell
     cd C:\Users\Praba\Source\repos\Bill-Book\frontend
     npm run check
     ```

4. **Consumer Spot-Check Invalidation Conditions**:
   - Invalidation occurs if any existing template-driven form (e.g. `opening-balance.page.html`) or reactive form (e.g. `invoice-form.component.html`) fails to compile or fails two-way binding synchronization when switching to `<bb-date-input>`, `<bb-currency-input>`, `<bb-number-input>`, `<bb-search-input>`, or `<bb-text-input>`.
