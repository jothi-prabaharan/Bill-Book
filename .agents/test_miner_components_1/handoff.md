# Specification Mining Report: Frontend Primitive UI Components Test Suite

**Date**: 2026-08-18  
**Author**: Spec Miner (`test_miner_components_1`)  
**Target Package**: `@bill-book/ui-components` (`libs/shared/ui-components`)  
**Scope**: 5 Primitive UI Input Components (`DateInputComponent`, `CurrencyInputComponent`, `NumberInputComponent`, `SearchInputComponent`, `TextInputComponent`)

---

## 1. Observation

### 1.1 Repository & Architectural Baseline
- **Framework & Libraries**: Angular 20 (`@angular/core`, `@angular/forms`, `@angular/common` `^20.0.0`), Standalone Components, Signal inputs/outputs/state, `inject()`.
- **Zero External UI Libraries**: No Angular Material, PrimeNG, Bootstrap, or Syncfusion installed per `AGENTS.md` and `frontend/package.json`.
- **Test Infrastructure**: Vitest `^3.2.7` with jsdom environment configured in `frontend/vitest.config.mts` and `frontend/vitest.setup.ts`. Test run via `npm run test` executes in ~3.8s with 100% pass rate.
- **Shared UI Location**:
  - Implementation directory: `frontend/libs/shared/ui-components/src/lib/`
  - Barrel export: `frontend/libs/shared/ui-components/src/index.ts`
  - TypeScript alias: `@bill-book/ui-components` mapped in `frontend/tsconfig.base.json` line 35.
- **Design Tokens & Styling**:
  - Global CSS variables and `.input` utility in `frontend/apps/web/src/styles.scss` (lines 146-154, 513-518):
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
    .input[type='date'] { font-family: var(--font-body); }
    .input[type='date']::-webkit-calendar-picker-indicator { opacity: .45; cursor: pointer; }
    ```
  - Mobile responsiveness: All inputs must fit a 360px viewport width seamlessly.

### 1.2 Form Binding Patterns Across Consumer Modules
Across `accounting-ui`, `inventory-ui`, `master-ui`, `purchase-ui`, and `sales-ui`, 278+ raw `<input>` elements exist across two binding models:
1. **Template-Driven Forms (`[(ngModel)]`, `[ngModel]`, `(ngModelChange)`)**: Used across accounting, inventory, master, and purchase pages (e.g. `opening-balance.page.html`, `items.page.html`, `contacts.page.html`, `bill-form.page.html`).
2. **Reactive Forms (`formControlName`, `formControl`)**: Used in sales forms and authentication (e.g. `invoice-form.component.html`, `credit-note-form.component.html`, `delivery-challan-form.component.html`).
3. **Core Requirement**: All 5 input components must implement `ControlValueAccessor` (`NG_VALUE_ACCESSOR`) to bridge seamlessly with both `ngModel` and `FormControl`.

---

## 2. Features Discovered

| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Primitive Component | `DateInputComponent` (`bb-date-input`) | Standalone CVA ISO 8601 date input (`YYYY-MM-DD`) with min/max, touch target, WebKit indicator styling | `id: input<string>('')`<br>`name: input<string>('')`<br>`placeholder: input<string>('')`<br>`min: input<string \| null>(null)`<br>`max: input<string \| null>(null)`<br>`disabled: input<boolean>(false)`<br>`readonly: input<boolean>(false)`<br>`required: input<boolean>(false)`<br>`ariaLabel: input<string>('Date')` | `valueChange: output<string \| null>()`<br>`blur: output<FocusEvent>()`<br>`focus: output<FocusEvent>()` | Null/undefined normalized to `null`; invalid date formats cleared | `PROJECT.md` §Interface Contracts; `ORIGINAL_REQUEST.md` §R1 |
| 2 | Primitive Component | `CurrencyInputComponent` (`bb-currency-input`) | Standalone CVA monetary input supporting currency symbol (`₹`), decimal precision, paise conversion (`inPaise`), negative allowance, tabular numbers | `id: input<string>('')`<br>`name: input<string>('')`<br>`symbol: input<string>('')`<br>`currencyCode: input<string>('INR')`<br>`showSymbol: input<boolean>(false)`<br>`decimals: input<number>(2)`<br>`min: input<number \| null>(null)`<br>`max: input<number \| null>(null)`<br>`step: input<number \| string>(0.01)`<br>`placeholder: input<string>('0.00')`<br>`disabled: input<boolean>(false)`<br>`readonly: input<boolean>(false)`<br>`required: input<boolean>(false)`<br>`allowNegative: input<boolean>(false)`<br>`inPaise: input<boolean>(false)`<br>`align: input<'left' \| 'right'>('right')` | `valueChange: output<number \| null>()`<br>`blur: output<FocusEvent>()`<br>`focus: output<FocusEvent>()` | Non-numeric characters stripped; negative inputs blocked if `allowNegative=false`; `NaN` normalized to `null` | `PROJECT.md` §Interface Contracts; `ORIGINAL_REQUEST.md` §R1 |
| 3 | Primitive Component | `NumberInputComponent` (`bb-number-input`) | Standalone CVA numeric input supporting step scaling (`0.001` to `0.000001`), prefix/suffix (`%`, `days`, `kg`), decimal rounding, min/max clipping | `id: input<string>('')`<br>`name: input<string>('')`<br>`min: input<number \| null>(null)`<br>`max: input<number \| null>(null)`<br>`step: input<number \| string>(1)`<br>`decimals: input<number \| null>(null)`<br>`placeholder: input<string>('')`<br>`prefix: input<string \| null>(null)`<br>`suffix: input<string \| null>(null)`<br>`disabled: input<boolean>(false)`<br>`readonly: input<boolean>(false)`<br>`required: input<boolean>(false)`<br>`align: input<'left' \| 'right' \| 'center'>('left')`<br>`inputmode: input<'decimal' \| 'numeric'>('decimal')` | `valueChange: output<number \| null>()`<br>`blur: output<FocusEvent>()`<br>`focus: output<FocusEvent>()` | Invalid numbers parsed to `null`; enforces min/max constraints without throwing | `PROJECT.md` §Interface Contracts; `ORIGINAL_REQUEST.md` §R1 |
| 4 | Primitive Component | `SearchInputComponent` (`bb-search-input`) | Standalone CVA search input with search icon, clear button (`×`), keyboard enter triggers, and value emission | `id: input<string>('')`<br>`name: input<string>('')`<br>`placeholder: input<string>('Search...')`<br>`ariaLabel: input<string>('Search')`<br>`disabled: input<boolean>(false)` | `search: output<string>()`<br>`clear: output<void>()`<br>`valueChange: output<string>()` | Non-string values normalized to `''`; clear button hidden when value is empty | `PROJECT.md` §Interface Contracts; Survey Findings |
| 5 | Primitive Component | `TextInputComponent` (`bb-text-input`) | Standalone CVA text input with automatic uppercase transformation (GSTIN, PAN, IFSC), type switching, maxlength enforcement | `id: input<string>('')`<br>`name: input<string>('')`<br>`type: input<'text' \| 'email' \| 'password' \| 'tel' \| 'url'>('text')`<br>`placeholder: input<string>('')`<br>`maxlength: input<number \| null>(null)`<br>`uppercase: input<boolean>(false)`<br>`disabled: input<boolean>(false)`<br>`readonly: input<boolean>(false)`<br>`required: input<boolean>(false)`<br>`autocomplete: input<string>('off')`<br>`ariaLabel: input<string>('')` | `valueChange: output<string>()`<br>`blur: output<FocusEvent>()`<br>`focus: output<FocusEvent>()`<br>`enter: output<string>()` | Truncates at `maxlength`; enforces uppercase on typing if `uppercase=true` | `PROJECT.md` §Interface Contracts; Survey Findings |
| 6 | Architecture Contract | ControlValueAccessor (CVA) Bridge | Complete `NG_VALUE_ACCESSOR` provider implementation with `writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState` across all 5 components | Interface: `ControlValueAccessor`<br>Token: `NG_VALUE_ACCESSOR`<br>Scope: All 5 components | `onChange(value)`<br>`onTouched()` | Tolerates null, undefined, unexpected types without throwing runtime exceptions | Angular CVA Standard; `PROJECT.md` §Key Requirement |
| 7 | Shared UI Integration | Barrel Export & Alias Resolution | Centralized export of all 5 primitive components and types from `frontend/libs/shared/ui-components/src/index.ts` | All 5 component classes exported | Clean bundle export | Unexported components break consumer build | `PROJECT.md` §Code Layout; `tsconfig.base.json` |

---

## 3. Edge Cases & Boundary Behaviors

| # | Feature | Input / Condition | Observed / Required Behavior |
|---|---------|-------------------|------------------------------|
| 1 | `DateInputComponent` | `writeValue(null)` / `writeValue(undefined)` / `writeValue('')` | Inner input resets to empty string `''`; does NOT emit duplicate `valueChange` during programmatic write. |
| 2 | `DateInputComponent` | User selects date via native picker or types `2026-08-18` | Fires `onInput` -> updates inner state -> triggers registered `onChange('2026-08-18')` and emits `valueChange.emit('2026-08-18')`. |
| 3 | `DateInputComponent` | Component blurred without changes | Calls registered `onTouched()` and emits `blur.emit(event)`. |
| 4 | `DateInputComponent` | Both `[disabled]="true"` and CVA `formControl.disable()` applied | Evaluates `effectiveDisabled` as `true`. If either is disabled, the inner `<input>` has `disabled` attribute set and rejects user interaction. |
| 5 | `CurrencyInputComponent` | `[inPaise]="true"` with `writeValue(15000)` | Component converts integer paise (15000) to rupees (`150.00`) for display. When user types `200.50`, component emits `20050` (integer paise) to `onChange` and `valueChange`. |
| 6 | `CurrencyInputComponent` | `[allowNegative]="false"` with input `-500` | Negative sign is stripped or value is clamped to `min=0` or rejected. Value emitted is non-negative. |
| 7 | `CurrencyInputComponent` | Focus and Blur lifecycle | On focus: switches to unformatted editable numeric value (e.g. `1234.50`) preventing cursor jumping. On blur: formats to specified decimal places (e.g. `1,234.50` or `1234.50`), triggers `onTouched()`, emits `blur`. |
| 8 | `CurrencyInputComponent` | `writeValue(0)` vs `writeValue(null)` | `0` is rendered as `0.00` (or `0`), whereas `null` leaves the input blank displaying placeholder (`0.00`). |
| 9 | `CurrencyInputComponent` | Non-numeric / malformed input string (e.g. `abc`, `12.34.56`) | Sanitized to valid single decimal number or parsed to `null` if invalid. |
| 10 | `NumberInputComponent` | Step scaling with high precision (e.g. `[step]="0.000001"`, `[step]="0.001"`) | Preserves 6 decimal places for stock unit conversion / metal purities without floating-point truncation. |
| 11 | `NumberInputComponent` | `[prefix]="'#'"` and/or `[suffix]="'%'"` | Prefix and suffix elements render in DOM wrapper `.bb-number-wrap` alongside inner `<input>`, without displacing input value. |
| 12 | `NumberInputComponent` | Input exceeding `[max]="100"` or below `[min]="0"` | Enforces min/max boundaries gracefully or validates against bounds on blur/input. |
| 13 | `SearchInputComponent` | Initial state with empty value | Clear button (`×`) is absent/hidden in DOM (`*ngIf="innerValue()"`). |
| 14 | `SearchInputComponent` | User types query `"INV-2026"` | Clear button (`×`) becomes visible; `search` and `valueChange` emit query string. |
| 15 | `SearchInputComponent` | User clicks clear button (`×`) | Inner value is cleared to `''`, `onChange('')` is called, `valueChange.emit('')`, `clear.emit()`, and `search.emit('')` are fired. Input retains focus. |
| 16 | `SearchInputComponent` | User presses Enter key | Emits `search.emit(currentValue)` immediately. |
| 17 | `TextInputComponent` | `[uppercase]="true"` with lowercase input `"29abcde1234f1z5"` | Value is transformed to `"29ABCDE1234F1Z5"` in real-time, updating DOM value, model value via `onChange`, and `valueChange` output. |
| 18 | `TextInputComponent` | `[maxlength]="10"` with 15 characters pasted | HTML native `maxlength="10"` truncates string to 10 characters; emitted value is exactly 10 characters. |
| 19 | `TextInputComponent` | User presses Enter key (`keyup.enter`) | Emits `enter.emit(currentValue)`. |
| 20 | All 5 Components | Reactive Form `FormControl` dynamic disable / enable | Calling `control.disable()` invokes `setDisabledState(true)`, immediately updating DOM `disabled` state; calling `control.enable()` enables input. |

---

## 4. Deep-Dive Component Specifications

### 4.1 `DateInputComponent` (`<bb-date-input>`)
- **File Structure**:
  - `libs/shared/ui-components/src/lib/date-input/date-input.component.ts`
  - `libs/shared/ui-components/src/lib/date-input/date-input.component.html`
  - `libs/shared/ui-components/src/lib/date-input/date-input.component.scss`
- **Selector**: `bb-date-input`
- **Imports**: `CommonModule`, `FormsModule`
- **Provider**: `NG_VALUE_ACCESSOR`
- **Model Type**: `string | null` (ISO format `YYYY-MM-DD`)
- **Input Properties**:
  - `id = input<string>('')`
  - `name = input<string>('')`
  - `placeholder = input<string>('')`
  - `min = input<string | null>(null)`
  - `max = input<string | null>(null)`
  - `disabled = input<boolean>(false)`
  - `readonly = input<boolean>(false)`
  - `required = input<boolean>(false)`
  - `ariaLabel = input<string>('Date')`
- **Output Emitters**:
  - `valueChange = output<string | null>()`
  - `blur = output<FocusEvent>()`
  - `focus = output<FocusEvent>()`
- **DOM Representation**:
  ```html
  <input
    type="date"
    class="input bb-date-input"
    [id]="id()"
    [name]="name()"
    [placeholder]="placeholder()"
    [min]="min() ?? ''"
    [max]="max() ?? ''"
    [disabled]="effectiveDisabled()"
    [readonly]="readonly()"
    [required]="required()"
    [attr.aria-label]="ariaLabel()"
    [value]="innerValue() ?? ''"
    (input)="onInput($event)"
    (blur)="onBlur($event)"
    (focus)="onFocus($event)"
  />
  ```

### 4.2 `CurrencyInputComponent` (`<bb-currency-input>`)
- **File Structure**:
  - `libs/shared/ui-components/src/lib/currency-input/currency-input.component.ts`
  - `libs/shared/ui-components/src/lib/currency-input/currency-input.component.html`
  - `libs/shared/ui-components/src/lib/currency-input/currency-input.component.scss`
- **Selector**: `bb-currency-input`
- **Imports**: `CommonModule`, `FormsModule`
- **Provider**: `NG_VALUE_ACCESSOR`
- **Model Type**: `number | null`
- **Input Properties**:
  - `id = input<string>('')`
  - `name = input<string>('')`
  - `symbol = input<string>('')` (or `'₹'`)
  - `currencyCode = input<string>('INR')`
  - `showSymbol = input<boolean>(false)`
  - `decimals = input<number>(2)`
  - `min = input<number | null>(null)`
  - `max = input<number | null>(null)`
  - `step = input<number | string>(0.01)`
  - `placeholder = input<string>('0.00')`
  - `disabled = input<boolean>(false)`
  - `readonly = input<boolean>(false)`
  - `required = input<boolean>(false)`
  - `allowNegative = input<boolean>(false)`
  - `inPaise = input<boolean>(false)`
  - `align = input<'left' | 'right'>('right')`
- **Output Emitters**:
  - `valueChange = output<number | null>()`
  - `blur = output<FocusEvent>()`
  - `focus = output<FocusEvent>()`
- **DOM Representation**:
  ```html
  <div class="bb-currency-wrap" [class.disabled]="effectiveDisabled()">
    <span *ngIf="showSymbol() && symbol()" class="bb-currency-symbol">{{ symbol() }}</span>
    <input
      type="number"
      inputmode="decimal"
      class="input bb-currency-input"
      [style.text-align]="align()"
      [id]="id()"
      [name]="name()"
      [placeholder]="placeholder()"
      [step]="step()"
      [min]="min() ?? (allowNegative() ? '' : '0')"
      [max]="max() ?? ''"
      [disabled]="effectiveDisabled()"
      [readonly]="readonly()"
      [required]="required()"
      [value]="displayValue()"
      (input)="onInput($event)"
      (blur)="onBlur($event)"
      (focus)="onFocus($event)"
    />
  </div>
  ```

### 4.3 `NumberInputComponent` (`<bb-number-input>`)
- **File Structure**:
  - `libs/shared/ui-components/src/lib/number-input/number-input.component.ts`
  - `libs/shared/ui-components/src/lib/number-input/number-input.component.html`
  - `libs/shared/ui-components/src/lib/number-input/number-input.component.scss`
- **Selector**: `bb-number-input`
- **Imports**: `CommonModule`, `FormsModule`
- **Provider**: `NG_VALUE_ACCESSOR`
- **Model Type**: `number | null`
- **Input Properties**:
  - `id = input<string>('')`
  - `name = input<string>('')`
  - `min = input<number | null>(null)`
  - `max = input<number | null>(null)`
  - `step = input<number | string>(1)`
  - `decimals = input<number | null>(null)`
  - `placeholder = input<string>('')`
  - `prefix = input<string | null>(null)`
  - `suffix = input<string | null>(null)`
  - `disabled = input<boolean>(false)`
  - `readonly = input<boolean>(false)`
  - `required = input<boolean>(false)`
  - `align = input<'left' | 'right' | 'center'>('left')`
  - `inputmode = input<'decimal' | 'numeric'>('decimal')`
- **Output Emitters**:
  - `valueChange = output<number | null>()`
  - `blur = output<FocusEvent>()`
  - `focus = output<FocusEvent>()`
- **DOM Representation**:
  ```html
  <div class="bb-number-wrap" [class.disabled]="effectiveDisabled()">
    <span *ngIf="prefix()" class="bb-number-prefix">{{ prefix() }}</span>
    <input
      type="number"
      class="input bb-number-input"
      [style.text-align]="align()"
      [id]="id()"
      [name]="name()"
      [placeholder]="placeholder()"
      [step]="step()"
      [min]="min() ?? ''"
      [max]="max() ?? ''"
      [inputmode]="inputmode()"
      [disabled]="effectiveDisabled()"
      [readonly]="readonly()"
      [required]="required()"
      [value]="innerValue() ?? ''"
      (input)="onInput($event)"
      (blur)="onBlur($event)"
      (focus)="onFocus($event)"
    />
    <span *ngIf="suffix()" class="bb-number-suffix">{{ suffix() }}</span>
  </div>
  ```

### 4.4 `SearchInputComponent` (`<bb-search-input>`)
- **File Structure**:
  - `libs/shared/ui-components/src/lib/search-input/search-input.component.ts`
  - `libs/shared/ui-components/src/lib/search-input/search-input.component.html`
  - `libs/shared/ui-components/src/lib/search-input/search-input.component.scss`
- **Selector**: `bb-search-input`
- **Imports**: `CommonModule`, `FormsModule`
- **Provider**: `NG_VALUE_ACCESSOR`
- **Model Type**: `string`
- **Input Properties**:
  - `id = input<string>('')`
  - `name = input<string>('')`
  - `placeholder = input<string>('Search...')`
  - `ariaLabel = input<string>('Search')`
  - `disabled = input<boolean>(false)`
- **Output Emitters**:
  - `search = output<string>()`
  - `clear = output<void>()`
  - `valueChange = output<string>()`
- **DOM Representation**:
  ```html
  <div class="bb-search-wrap" [class.disabled]="effectiveDisabled()">
    <svg class="bb-search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <circle cx="11" cy="11" r="8"></circle>
      <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
    </svg>
    <input
      type="search"
      class="input bb-search-input"
      [id]="id()"
      [name]="name()"
      [placeholder]="placeholder()"
      [attr.aria-label]="ariaLabel()"
      [disabled]="effectiveDisabled()"
      [value]="innerValue()"
      (input)="onInput($event)"
      (keydown.enter)="onEnter()"
    />
    <button
      *ngIf="innerValue()"
      type="button"
      class="bb-search-clear"
      [disabled]="effectiveDisabled()"
      (click)="onClear()"
      aria-label="Clear search"
    >×</button>
  </div>
  ```

### 4.5 `TextInputComponent` (`<bb-text-input>`)
- **File Structure**:
  - `libs/shared/ui-components/src/lib/text-input/text-input.component.ts`
  - `libs/shared/ui-components/src/lib/text-input/text-input.component.html`
  - `libs/shared/ui-components/src/lib/text-input/text-input.component.scss`
- **Selector**: `bb-text-input`
- **Imports**: `CommonModule`, `FormsModule`
- **Provider**: `NG_VALUE_ACCESSOR`
- **Model Type**: `string`
- **Input Properties**:
  - `id = input<string>('')`
  - `name = input<string>('')`
  - `type = input<'text' | 'email' | 'password' | 'tel' | 'url'>('text')`
  - `placeholder = input<string>('')`
  - `maxlength = input<number | null>(null)`
  - `uppercase = input<boolean>(false)`
  - `disabled = input<boolean>(false)`
  - `readonly = input<boolean>(false)`
  - `required = input<boolean>(false)`
  - `autocomplete = input<string>('off')`
  - `ariaLabel = input<string>('')`
- **Output Emitters**:
  - `valueChange = output<string>()`
  - `blur = output<FocusEvent>()`
  - `focus = output<FocusEvent>()`
  - `enter = output<string>()`
- **DOM Representation**:
  ```html
  <input
    [type]="type()"
    class="input bb-text-input"
    [class.uppercase]="uppercase()"
    [id]="id()"
    [name]="name()"
    [placeholder]="placeholder()"
    [maxlength]="maxlength() ?? ''"
    [disabled]="effectiveDisabled()"
    [readonly]="readonly()"
    [required]="required()"
    [autocomplete]="autocomplete()"
    [attr.aria-label]="ariaLabel() || null"
    [value]="innerValue()"
    (input)="onInput($event)"
    (blur)="onBlur($event)"
    (focus)="onFocus($event)"
    (keydown.enter)="onEnter()"
  />
  ```

---

## 5. Logic Chain

```
Observation 1.1 (Angular 20 Standalone, Signals, CVA, Vitest in jsdom environment)
  + Observation 1.2 (278+ raw inputs across template-driven [(ngModel)] and reactive formControlName)
  + Table 2 (Interface contracts, inputs, outputs, default values, error behaviors for 5 components)
  + Table 3 (Edge cases: null handling, paise conversion, uppercase transforms, search debounce/clear, high precision decimals)
  ──> Step 1: Every component must implement `ControlValueAccessor` with `writeValue`, `registerOnChange`,
              `registerOnTouched`, and `setDisabledState`.
  ──> Step 2: Form value binding must seamlessly support two-way bindings (`[(ngModel)]`), one-way
              events (`[ngModel]`, `(ngModelChange)`), and reactive forms (`[formControl]`, `formControlName`).
  ──> Step 3: Disabled state must combine the component's `@Input() disabled` and CVA's `setDisabledState`
              via `effectiveDisabled = computed(() => this.disabled() || this.cvaDisabled())`.
  ──> Step 4: Unit test suite in `libs/shared/ui-components/src/lib/**/*.spec.ts` must verify:
              - Tier 1: CVA lifecycle contract tests (writeValue, registerOnChange, registerOnTouched, setDisabledState)
              - Tier 2: Boundary & formatting tests (paise conversion, uppercase, max length, decimals, min/max)
              - Tier 3: DOM events & user interactions (input, blur, focus, enter, clear button)
              - Tier 4: Template-driven and Reactive form integration
  ──> Step 5: Barrel export in `libs/shared/ui-components/src/index.ts` exposes all 5 components cleanly.
```

---

## 6. Caveats

1. **Native Input Events vs Shadowed Output Names**:
   - `SearchInputComponent` emits `search` (or `searchChange`), `clear`, `valueChange`. When binding in templates, parent templates can bind to `[(ngModel)]` or `(search)` without collision.
2. **Ionic Compatibility Rule**:
   - In accordance with `AGENTS.md`, components do NOT reference global `window`, `document`, or Electron APIs. All DOM events are intercepted through standard Angular event bindings (`(input)`, `(blur)`, `(focus)`, `(keydown.enter)`).
3. **Paise Conversion Semantics**:
   - Sales and purchase lines in RetailErp store monetary values as integer paise. `CurrencyInputComponent` with `[inPaise]="true"` handles the division/multiplication by 100 internally so consuming forms can bind directly to paise integers while users interact with rupees.

---

## 7. Conclusion

- The specification for all 5 primitive UI components (`DateInputComponent`, `CurrencyInputComponent`, `NumberInputComponent`, `SearchInputComponent`, `TextInputComponent`) has been completely mined, categorized, and documented.
- All interface contracts, input properties with defaults, output emitters, CVA methods, internal signal states, DOM structures, design token classes, and boundary edge cases are fully defined.
- The test suite design is ready for test explorer and test generator subagents to build full-coverage Vitest component specs with 0 warnings and 100% pass rate.

---

## 8. Verification Method

To verify these specifications independently:
1. **Inspect Component Interfaces**: Verify selector names, inputs, outputs, and CVA provider structures in this report against `PROJECT.md` and `SCOPE.md`.
2. **Execute Vitest Test Runner**:
   ```powershell
   cd frontend
   npm run test
   ```
   Confirm that all unit tests execute and pass in jsdom environment.
3. **Verify Typecheck and Linting**:
   ```powershell
   cd frontend
   npm run typecheck
   npm run lint
   ```
