# Milestone 1: Shared Primitive UI Components — Investigation & Architectural Report

## 1. Observation

### 1.1 Existing Shared UI Components Library Structure
The library is located at `frontend/libs/shared/ui-components/` with path alias `@bill-book/ui-components` configured in `frontend/tsconfig.base.json`:
- **Current Barrel**: `frontend/libs/shared/ui-components/src/index.ts` currently exports:
  - `document-line-grid/` (`document-line.model`, `line-math`, `document-line-grid.component`)
  - `allocation-grid/` (`allocation-grid.component`)
  - `lookup-dialog/` (`lookup-row.model`, `lookup-dialog.component`)
  - `report-grid/` (`report-grid.component`, `filter-operators`, `filter-bar.component`, `column-chooser.dialog`, `group-panel.component`, `pivot-panel.component`)
  - `bank-graph-card/` (`bank-graph-card.component`)
  - `card-table/` (`card-table.component`)
  - `data-grid/` (`data-grid.component`, `data-grid.models`, `data-grid.service`, `data-grid-cell-template.directive`)

### 1.2 Global Design System & Styling Tokens (`frontend/apps/web/src/styles.scss`)
- **Design Tokens**:
  - `--color-bg`: `#f3f2f2`
  - `--color-surface`: `#eae9e9`
  - `--color-text`: `#201f1d`
  - `--color-accent`: `#f06311` (with tones `--color-accent-100` through `--color-accent-800`)
  - `--color-divider`: `color-mix(in srgb, #201f1d 16%, transparent)`
  - `--font-heading`: `"Cormorant Garamond", system-ui, sans-serif`
  - `--font-body`: `"Lora", system-ui, sans-serif`
  - `--radius-sm`: `2px`, `--radius-md`: `4px`, `--radius-lg`: `7px`
  - `--shadow-sm`, `--shadow-md`, `--shadow-lg`
- **Global `.input` class definition**:
  ```scss
  .input {
    width: 100%;
    min-height: 36px;
    padding: 6px 10px;
    font: inherit;
    font-size: 14px;
    color: var(--color-text);
    caret-color: var(--color-accent);
    background: transparent;
    border: 1px solid var(--color-divider);
    border-radius: var(--radius-md);
  }
  .input:hover { border-color: color-mix(in srgb, var(--color-text) 45%, transparent); }
  .input:focus-visible { border-color: var(--color-accent); outline: 2px solid var(--color-accent); outline-offset: 0; }
  .input:disabled { opacity: .6; cursor: not-allowed; background: color-mix(in srgb, var(--color-text) 4%, transparent); }
  .input[type='date'] { font-family: var(--font-body); }
  .input[type='date']::-webkit-calendar-picker-indicator { opacity: .45; cursor: pointer; }
  .input:disabled::-webkit-calendar-picker-indicator { cursor: not-allowed; }
  ```
- **360px Mobile Responsiveness**:
  - Components must use `:host { display: block; width: 100%; }`
  - Form fields stack vertically at `@media (max-width: 860px)` and `< 480px`.
  - Min touch target height >= 36px (38px on mobile).

### 1.3 Pre-existing Compilation & Lint Errors in `ui-components`
1. **Typecheck TS2304 / NG1010 in `group-panel.component.ts`**:
   - `libs/shared/ui-components/src/lib/report-grid/group-panel.component.ts(24,13): error TS2304: Cannot find name 'CdkDrag'.`
   - In `group-panel.component.ts`: `CdkDrag` is listed in `imports: [CdkDrag]`, but neither `CdkDrag` nor `DragDropModule` is imported from `'@angular/cdk/drag-drop'`.
2. **Typecheck TS2345 in `column-chooser.dialog.ts` / `.html`**:
   - `Argument of type 'Event' is not assignable to parameter of type 'CdkDragDrop<string[], string[], any>'.`
   - In `column-chooser.dialog.ts`: `imports: [FormsModule]` lacks `DragDropModule` / `CdkDropList`, causing Angular template compiler to treat `(cdkDropListDropped)` as a native DOM event.
3. **ESLint 12 warnings in `data-grid/`**:
   - `data-grid.component.ts`, `data-grid-cell.component.ts`, `data-grid-row.component.ts`, `data-grid-cell-template.directive.ts` trigger `@typescript-eslint/no-explicit-any`.
4. **StyleUrls vs StyleUrl in `allocation-grid.component.ts`**:
   - Uses deprecated `styleUrls: ['./allocation-grid.component.scss']` instead of Angular 20 `styleUrl`.

---

## 2. Logic Chain

1. **Angular 20 & Forms Compatibility**:
   - Consumers across `accounting-ui`, `inventory-ui`, `master-ui` use template-driven forms (`[(ngModel)]="form.field"`), while `sales-ui` and `purchase-ui` use reactive forms (`formControlName="field"`).
   - Therefore, all 5 input components must implement `ControlValueAccessor` (`NG_VALUE_ACCESSOR` provider with `forwardRef`) and manage `writeValue`, `registerOnChange`, `registerOnTouched`, and `setDisabledState`.

2. **Signals & Modern Angular Best Practices**:
   - Inputs should use `input()` and `input.required()` signal inputs.
   - Outputs should use `output()` and `output<T>()`.
   - Internal state tracking should use `signal()` (`internalValue`, `isDisabled`) and `computed()` (`effectiveDisabled`, `formattedDisplayValue`).

3. **Concrete Specifications for All 5 Primitive Components**:

### 1. `DateInputComponent` (`<bb-date-input>`)
- **Directory**: `libs/shared/ui-components/src/lib/date-input/`
- **Files**: `date-input.component.ts`, `date-input.component.html`, `date-input.component.scss`, `date-input.component.spec.ts`
- **Selector**: `bb-date-input`
- **Inputs**:
  - `id = input<string>('')`
  - `name = input<string>('')`
  - `placeholder = input<string>('')`
  - `min = input<string | null>(null)`
  - `max = input<string | null>(null)`
  - `disabled = input<boolean>(false)`
  - `readonly = input<boolean>(false)`
  - `required = input<boolean>(false)`
  - `ariaLabel = input<string>('Date')`
- **Outputs**:
  - `valueChange = output<string | null>()`
  - `blur = output<FocusEvent>()`
  - `focus = output<FocusEvent>()`
- **Value Handling**:
  - Formats date string to ISO `YYYY-MM-DD` or empty `null`.
  - Native WebKit calendar indicator with subtle opacity.

### 2. `CurrencyInputComponent` (`<bb-currency-input>`)
- **Directory**: `libs/shared/ui-components/src/lib/currency-input/`
- **Files**: `currency-input.component.ts`, `currency-input.component.html`, `currency-input.component.scss`, `currency-input.component.spec.ts`
- **Selector**: `bb-currency-input`
- **Inputs**:
  - `id = input<string>('')`
  - `name = input<string>('')`
  - `symbol = input<string>('₹')`
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
- **Outputs**:
  - `valueChange = output<number | null>()`
  - `blur = output<FocusEvent>()`
  - `focus = output<FocusEvent>()`
- **Value Handling**:
  - If `inPaise()` is true, CVA value is in integer paise (`1250` for `12.50`), whereas input/display shows formatted decimal (`12.50`).
  - Supports numeric input with tabular-nums formatting (`font-variant-numeric: tabular-nums`).
  - Optional prefix symbol (e.g. `₹`).

### 3. `NumberInputComponent` (`<bb-number-input>`)
- **Directory**: `libs/shared/ui-components/src/lib/number-input/`
- **Files**: `number-input.component.ts`, `number-input.component.html`, `number-input.component.scss`, `number-input.component.spec.ts`
- **Selector**: `bb-number-input`
- **Inputs**:
  - `id = input<string>('')`
  - `name = input<string>('')`
  - `min = input<number | null>(null)`
  - `max = input<number | null>(null)`
  - `step = input<number | string>(1)`
  - `decimals = input<number | null>(null)`
  - `placeholder = input<string>('')`
  - `prefix = input<string | null>(null)`
  - `suffix = input<string | null>(null)` (e.g. `%`, `days`, `kg`)
  - `disabled = input<boolean>(false)`
  - `readonly = input<boolean>(false)`
  - `required = input<boolean>(false)`
  - `align = input<'left' | 'right' | 'center'>('left')`
  - `inputmode = input<'decimal' | 'numeric'>('decimal')`
- **Outputs**:
  - `valueChange = output<number | null>()`
  - `blur = output<FocusEvent>()`
  - `focus = output<FocusEvent>()`
- **Value Handling**:
  - Renders input-group with prefix/suffix ad-on labels.
  - Automatically parses to `number | null`.

### 4. `SearchInputComponent` (`<bb-search-input>`)
- **Directory**: `libs/shared/ui-components/src/lib/search-input/`
- **Files**: `search-input.component.ts`, `search-input.component.html`, `search-input.component.scss`, `search-input.component.spec.ts`
- **Selector**: `bb-search-input`
- **Inputs**:
  - `id = input<string>('')`
  - `name = input<string>('')`
  - `placeholder = input<string>('Search...')`
  - `ariaLabel = input<string>('Search')`
  - `disabled = input<boolean>(false)`
- **Outputs**:
  - `search = output<string>()`
  - `clear = output<void>()`
  - `valueChange = output<string>()`
- **Value Handling**:
  - Integrated search magnifying icon (SVG) and clear button (`×`).
  - Emits `search` on Enter key and `clear` on clear button click.

### 5. `TextInputComponent` (`<bb-text-input>`)
- **Directory**: `libs/shared/ui-components/src/lib/text-input/`
- **Files**: `text-input.component.ts`, `text-input.component.html`, `text-input.component.scss`, `text-input.component.spec.ts`
- **Selector**: `bb-text-input`
- **Inputs**:
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
- **Outputs**:
  - `valueChange = output<string>()`
  - `blur = output<FocusEvent>()`
  - `focus = output<FocusEvent>()`
  - `enter = output<string>()`
- **Value Handling**:
  - If `uppercase()` is true, automatically converts text on typing or pasting to uppercase (for GSTIN, PAN, IFSC codes), applies `.uppercase` CSS class, and propagates uppercase string to CVA.

---

## 3. Caveats
1. **Zero External Libraries**: No third-party date pickers, maskers, or UI toolkits (e.g. Angular Material, PrimeNG, Syncfusion) may be added. All components rely solely on Angular 20 standard APIs and plain SCSS with design system CSS variables.
2. **Non-destructive CVA implementation**: `writeValue` must safely guard against null/undefined without throwing or creating recursive update loops.
3. **CDK Imports in Existing Pages**: Other libraries (`accounting-ui`, `inventory-ui`) have standalone drag-and-drop pages (`bank-accounts`, `banks`, `numbering-series`, `payment-terms`, `unit-types`) that also require `DragDropModule` in their `imports`. For Milestone 1, we must ensure `ui-components` (`group-panel` and `column-chooser`) builds cleanly.

---

## 4. Conclusion
1. **Architectural Blueprints Ready**: Complete component contracts, inputs/outputs, CVA handlers, template layouts, and SCSS styling have been established for all 5 primitive components.
2. **Library Cleanliness**: Pre-existing issues in `ui-components` (`group-panel.component.ts`, `column-chooser.dialog.ts`, `data-grid/`) are diagnosed with exact fixes identified.
3. **Seamless Refactoring Path**: The 5 components provide exact drop-in replacements for raw inputs in later milestones (M2 through M5).

---

## 5. Verification Method

To independently verify the shared UI library and components:
1. **Typecheck Verification**:
   ```bash
   cd frontend
   npm run typecheck
   ```
2. **Lint Verification**:
   ```bash
   cd frontend
   npm run lint
   ```
3. **Unit Test Suite**:
   ```bash
   cd frontend
   npx vitest run libs/shared/ui-components
   ```
4. **Build Verification**:
   ```bash
   cd frontend
   npm run build
   ```
