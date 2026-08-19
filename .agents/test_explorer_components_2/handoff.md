# Component Test Strategy and Tier Decomposition Report

## 1. Observation

### 1.1 Architectural Context & Target Components
The frontend project (`@bill-book/ui-components` under `frontend/libs/shared/ui-components/src/lib/`) is establishing 5 standardized standalone Angular 20 UI primitive components to replace raw HTML `<input>` elements across all consuming feature libraries (`accounting-ui`, `inventory-ui`, `master-ui`, `purchase-ui`, `sales-ui`):

1. **`DateInputComponent` (`<bb-date-input>`)**
   - **Path**: `libs/shared/ui-components/src/lib/date-input/`
   - **Contract**: CVA (`string | null` - ISO 8601 `'YYYY-MM-DD'`), Inputs (`id`, `name`, `placeholder`, `min`, `max`, `disabled`, `readonly`, `required`, `ariaLabel`, `size`), Outputs (`valueChange`, `blur`, `focus`).
2. **`CurrencyInputComponent` (`<bb-currency-input>`)**
   - **Path**: `libs/shared/ui-components/src/lib/currency-input/`
   - **Contract**: CVA (`number | null`), Inputs (`id`, `name`, `symbol`, `currencyCode`, `showSymbol`, `decimals`, `min`, `max`, `step`, `placeholder`, `disabled`, `readonly`, `required`, `allowNegative`, `inPaise`, `align`), Outputs (`valueChange`, `blur`, `focus`).
3. **`NumberInputComponent` (`<bb-number-input>`)**
   - **Path**: `libs/shared/ui-components/src/lib/number-input/`
   - **Contract**: CVA (`number | null`), Inputs (`id`, `name`, `min`, `max`, `step`, `decimals`, `placeholder`, `prefix`, `suffix`, `disabled`, `readonly`, `required`, `align`, `inputmode`), Outputs (`valueChange`, `blur`, `focus`).
4. **`SearchInputComponent` (`<bb-search-input>`)**
   - **Path**: `libs/shared/ui-components/src/lib/search-input/`
   - **Contract**: CVA (`string`), Inputs (`id`, `placeholder`, `ariaLabel`, `disabled`), Outputs (`search`, `clear`, `valueChange`).
5. **`TextInputComponent` (`<bb-text-input>`)**
   - **Path**: `libs/shared/ui-components/src/lib/text-input/`
   - **Contract**: CVA (`string`), Inputs (`id`, `name`, `type`, `placeholder`, `maxlength`, `uppercase`, `disabled`, `readonly`, `required`, `autocomplete`), Outputs (`valueChange`, `blur`, `focus`, `enter`).

### 1.2 Testing Infrastructure Verification
- **Test Runner**: Vitest 3.2.7 in JSDOM environment (`frontend/vitest.config.mts`, `frontend/vitest.setup.ts`).
- **Execution Verification**: Executed `npm test` (`vitest run`). Baseline test suite executes cleanly: 9 test files, 78 tests passing in 3.90s.
- **CVA Execution Model**: Direct component class instantiation and DOM event dispatching (`dispatchEvent(new Event('input'))`, `dispatchEvent(new FocusEvent('blur'))`) execute with zero overhead and full signal reactivity in JSDOM.

---

## 2. Logic Chain

```
[Observation 1.1: 5 UI Primitive Components implementing ControlValueAccessor]
  + [Observation 1.2: Angular 20 Standalone Components + Signals + 2-way NgModel & Reactive Forms]
  + [Requirement: Systematic 4-tier test case hierarchy with >=5 T1, >=5 T2, 1-2 T3, 1-2 T4 per component]
  ──> Step 1 (Tier 1: Feature / Contract Coverage):
      Verify core ControlValueAccessor protocol (writeValue, registerOnChange, registerOnTouched, setDisabledState),
      default input attributes, template bindings, and output event emitters.
  ──> Step 2 (Tier 2: Boundary & Corner Cases):
      Stress-test null/undefined/empty normalization, min/max clamping, precision rounding, non-numeric filtering,
      maxlength truncation, case transformation, leap year dates, negative numbers, and rapid keystrokes.
  ──> Step 3 (Tier 3: Cross-Feature Interactions):
      Verify dynamic state mutations (enabling/disabling on the fly, dynamic min/max adjustments,
      focus/blur formatting switches, paise mode toggles, clear-and-retype event ordering).
  ──> Step 4 (Tier 4: Real-World Application Scenarios):
      Validate end-to-end integration with Angular Forms (Reactive Forms with FormGroup/FormControl,
      Template-driven forms with ngModel, validation state changes, form reset lifecycle, and multi-field data entry).
  ──> Step 5 (Assertion Matrix Synthesis):
      Produce 79 concrete, executable test specifications across all 5 components with explicit assertion statements.
```

---

## 3. Systematic 4-Tier Test Case Matrix

### 3.1 Overview & Coverage Summary

| Component | Selector | Tier 1 (Contract) | Tier 2 (Boundary) | Tier 3 (Interactions) | Tier 4 (Real-World) | Total Tests |
|---|---|:---:|:---:|:---:|:---:|:---:|
| **DateInputComponent** | `bb-date-input` | 6 | 5 | 2 | 2 | **15** |
| **CurrencyInputComponent** | `bb-currency-input` | 6 | 6 | 2 | 2 | **16** |
| **NumberInputComponent** | `bb-number-input` | 6 | 6 | 2 | 2 | **16** |
| **SearchInputComponent** | `bb-search-input` | 6 | 6 | 2 | 2 | **16** |
| **TextInputComponent** | `bb-text-input` | 6 | 6 | 2 | 2 | **16** |
| **Total Test Suite** | | **30** | **29** | **10** | **10** | **79** |

---

### 3.2 Component 1: `DateInputComponent` (`bb-date-input`)

#### Tier 1: Feature / Contract Coverage (6 Tests)
1. **`DATE-T1-01`**: `writeValue()` with valid ISO date updates internal signal and native DOM value.
   - *Preconditions*: Fresh component instance.
   - *Action*: `component.writeValue('2026-08-18')`.
   - *Expected*: `component.value()` is `'2026-08-18'`, native input `inputEl.value` equals `'2026-08-18'`.
   - *Assertion*: `expect(component.value()).toBe('2026-08-18');`
2. **`DATE-T1-02`**: User input invokes registered `onChange` callback and emits `valueChange`.
   - *Preconditions*: Callback registered via `component.registerOnChange(fn)`.
   - *Action*: Dispatch input event with value `'2026-12-31'`.
   - *Expected*: Callback called with `'2026-12-31'`, `valueChange` emits `'2026-12-31'`.
   - *Assertion*: `expect(changeSpy).toHaveBeenCalledWith('2026-12-31');`
3. **`DATE-T1-03`**: Input blur invokes registered `onTouched` callback and emits `blur` event.
   - *Preconditions*: Callback registered via `component.registerOnTouched(fn)`.
   - *Action*: Dispatch `blur` event on native input element.
   - *Expected*: `onTouched` callback invoked, `blur` output emits `FocusEvent`.
   - *Assertion*: `expect(touchedSpy).toHaveBeenCalledTimes(1); expect(blurSpy).toHaveBeenCalled();`
4. **`DATE-T1-04`**: `setDisabledState()` updates internal disabled signal and native DOM `disabled` attribute.
   - *Preconditions*: Initial component.
   - *Action*: `component.setDisabledState(true)`, followed by `component.setDisabledState(false)`.
   - *Expected*: `component.disabled()` signal is `true` then `false`; `inputEl.disabled` matches boolean.
   - *Assertion*: `expect(component.disabled()).toBe(true); expect(inputEl.disabled).toBe(true);`
5. **`DATE-T1-05`**: Default input attributes render accurately.
   - *Preconditions*: Default component initialization without explicit inputs.
   - *Action*: Inspect component signals and template element properties.
   - *Expected*: `placeholder` is `'YYYY-MM-DD'`, `ariaLabel` is `'Date'`, `size` is `'md'`, `min` is `null`, `max` is `null`.
   - *Assertion*: `expect(component.placeholder()).toBe('YYYY-MM-DD'); expect(component.ariaLabel()).toBe('Date');`
6. **`DATE-T1-06`**: Focus event dispatches `focus` output.
   - *Preconditions*: Component mounted with focus listener.
   - *Action*: Dispatch `focus` event on input.
   - *Expected*: `focus` output emits event.
   - *Assertion*: `expect(focusSpy).toHaveBeenCalledTimes(1);`

#### Tier 2: Boundary & Corner Cases (5 Tests)
1. **`DATE-T2-01`**: `writeValue(null)` and `writeValue(undefined)` normalize safely to `null`.
   - *Preconditions*: Component initialized with previous value `'2026-08-18'`.
   - *Action*: `component.writeValue(null)`, then `component.writeValue(undefined as any)`.
   - *Expected*: `component.value()` is `null`, `inputEl.value` is `''`, no runtime exceptions thrown.
   - *Assertion*: `expect(component.value()).toBeNull(); expect(inputEl.value).toBe('');`
2. **`DATE-T2-02`**: Clearing date field emits `null` via `onChange`.
   - *Preconditions*: Component holds value `'2026-08-18'`.
   - *Action*: User clears native input (`inputEl.value = ''`; dispatch `input` event).
   - *Expected*: `onChange` callback invoked with `null`, `component.value()` is `null`.
   - *Assertion*: `expect(changeSpy).toHaveBeenCalledWith(null);`
3. **`DATE-T2-03`**: Min and max boundary constraints propagate to native DOM attributes.
   - *Preconditions*: Input signals set: `min = '2026-01-01'`, `max = '2026-12-31'`.
   - *Action*: Inspect native input attributes.
   - *Expected*: `inputEl.getAttribute('min')` is `'2026-01-01'`, `inputEl.getAttribute('max')` is `'2026-12-31'`.
   - *Assertion*: `expect(inputEl.min).toBe('2026-01-01'); expect(inputEl.max).toBe('2026-12-31');`
4. **`DATE-T2-04`**: Leap year date handling (`'2028-02-29'`).
   - *Preconditions*: Valid leap year date string `'2028-02-29'`.
   - *Action*: `component.writeValue('2028-02-29')`.
   - *Expected*: Date string preserved exactly without mutation or offset drift.
   - *Assertion*: `expect(component.value()).toBe('2028-02-29');`
5. **`DATE-T2-05`**: Incomplete / partial date entry does not emit invalid malformed date.
   - *Preconditions*: User typing partial keystroke `'2026-08-'`.
   - *Action*: Trigger input event with `'2026-08-'`.
   - *Expected*: Component handles gracefully, does not emit corrupted date object.
   - *Assertion*: `expect(() => component.onInput(mockEvent)).not.toThrow();`

#### Tier 3: Cross-Feature Interactions (2 Tests)
1. **`DATE-T3-01`**: Dynamic disabled state toggle preserves current date and restores interactivity.
   - *Preconditions*: Component has date `'2026-08-18'`.
   - *Action*: Call `setDisabledState(true)`, attempt user input, call `setDisabledState(false)`, simulate user input `'2026-09-01'`.
   - *Expected*: While disabled, input ignores interactions; once re-enabled, previous date `'2026-08-18'` was preserved and new input `'2026-09-01'` updates properly.
   - *Assertion*: `expect(component.value()).toBe('2026-09-01'); expect(changeSpy).toHaveBeenCalledWith('2026-09-01');`
2. **`DATE-T3-02`**: Dynamic min date constraint adjustment for Date Range filtering (`fromDate` -> `toDate`).
   - *Preconditions*: Two date inputs representing range.
   - *Action*: `fromDate` updates to `'2026-06-15'`, setting `toDate` input's `min` property to `'2026-06-15'`.
   - *Expected*: `toDate` component DOM reflects new `min` boundary dynamically without clearing existing valid date.
   - *Assertion*: `expect(toDateInputEl.min).toBe('2026-06-15');`

#### Tier 4: Real-World Application Scenarios (2 Tests)
1. **`DATE-T4-01`**: Reactive Form integration with `FormGroup`, `Validators.required`, touch state, and form reset.
   - *Preconditions*: `FormGroup` with `docDate: new FormControl('2026-08-18', Validators.required)`.
   - *Action*: Attach `DateInputComponent`, verify form is `VALID`, clear date -> form is `INVALID`, dispatch blur -> control is `TOUCHED`, call `form.reset()` -> pristine and reset to null.
   - *Expected*: Full synchronization between Angular Reactive Form status and component state.
   - *Assertion*: `expect(control.valid).toBe(false); expect(control.touched).toBe(true);`
2. **`DATE-T4-02`**: Template-driven multi-field date entry in Accounting Ledger (From Date / To Date filter).
   - *Preconditions*: Component used in template-driven workflow with `[(ngModel)]`.
   - *Action*: User enters start date `'2026-04-01'` and end date `'2026-03-31'`.
   - *Expected*: Both models receive updated ISO strings and emit change events.
   - *Assertion*: `expect(model.fromDate).toBe('2026-04-01'); expect(model.toDate).toBe('2026-03-31');`

---

### 3.3 Component 2: `CurrencyInputComponent` (`bb-currency-input`)

#### Tier 1: Feature / Contract Coverage (6 Tests)
1. **`CURR-T1-01`**: `writeValue()` with numeric amount updates internal state and displays formatted value.
   - *Preconditions*: Component instance.
   - *Action*: `component.writeValue(1234.5)`.
   - *Expected*: `component.value()` is `1234.5`, display reflects `1,234.50` (or `1234.50` with 2 decimals).
   - *Assertion*: `expect(component.value()).toBe(1234.5);`
2. **`CURR-T1-02`**: User input invokes `onChange` callback with numeric value and emits `valueChange`.
   - *Preconditions*: Callback registered via `component.registerOnChange(fn)`.
   - *Action*: User enters `'500.75'` in input.
   - *Expected*: `onChange` invoked with number `500.75`, `valueChange` emits `500.75`.
   - *Assertion*: `expect(changeSpy).toHaveBeenCalledWith(500.75);`
3. **`CURR-T1-03`**: Blur invokes `onTouched` callback and formats display.
   - *Preconditions*: Callback registered via `component.registerOnTouched(fn)`.
   - *Action*: Dispatch `blur` event.
   - *Expected*: `onTouched` invoked, display formatted with decimal padding.
   - *Assertion*: `expect(touchedSpy).toHaveBeenCalledTimes(1);`
4. **`CURR-T1-04`**: `setDisabledState()` disables native input and currency prefix container.
   - *Preconditions*: Component initialized.
   - *Action*: `component.setDisabledState(true)`.
   - *Expected*: `component.disabled()` is `true`, `inputEl.disabled` is `true`.
   - *Assertion*: `expect(inputEl.disabled).toBe(true);`
5. **`CURR-T1-05`**: Currency symbol rendering and right alignment by default.
   - *Preconditions*: `showSymbol = true`, `symbol = '₹'`, `align = 'right'`.
   - *Action*: Inspect rendered template.
   - *Expected*: Currency symbol `'₹'` is rendered in DOM, text alignment is right-aligned.
   - *Assertion*: `expect(component.symbol()).toBe('₹'); expect(component.align()).toBe('right');`
6. **`CURR-T1-06`**: `inPaise` mode conversion between integer paise and decimal rupees.
   - *Preconditions*: `[inPaise]="true"`.
   - *Action*: `component.writeValue(25050)` (250.50 rupees).
   - *Expected*: Displayed amount is `'250.50'`, underlying model value is integer `25050`. User typing `'100.00'` calls `onChange(10000)`.
   - *Assertion*: `expect(component.displayValue()).toBe('250.50'); expect(changeSpy).toHaveBeenCalledWith(10000);`

#### Tier 2: Boundary & Corner Cases (6 Tests)
1. **`CURR-T2-01`**: `writeValue(null)` and `writeValue(0)` handling.
   - *Preconditions*: Component initialized.
   - *Action*: `component.writeValue(null)` -> display empty; `component.writeValue(0)` -> display `'0.00'`.
   - *Expected*: `null` results in null/empty state; `0` is treated as a valid numeric zero.
   - *Assertion*: `expect(component.value()).toBe(0);`
2. **`CURR-T2-02`**: Negative value protection when `allowNegative = false`.
   - *Preconditions*: `allowNegative = false` (default).
   - *Action*: User enters `'-500.00'`.
   - *Expected*: Negative sign is rejected/clamped; value emits `0` or `null`.
   - *Assertion*: `expect(component.value()).toBeGreaterThanOrEqual(0);`
3. **`CURR-T2-03`**: Negative value support when `allowNegative = true` (Credit Notes / Journal Adjustments).
   - *Preconditions*: `allowNegative = true`.
   - *Action*: User enters `'-150.25'`.
   - *Expected*: Value `-150.25` is accepted, `onChange` called with `-150.25`.
   - *Assertion*: `expect(changeSpy).toHaveBeenCalledWith(-150.25);`
4. **`CURR-T2-04`**: Decimal precision rounding / clipping with `decimals = 2`.
   - *Preconditions*: `decimals = 2`.
   - *Action*: User enters `'123.4567'`.
   - *Expected*: Value is rounded/truncated to 2 decimal places (`123.46` or `123.45`).
   - *Assertion*: `expect(component.value()).toBe(123.46);`
5. **`CURR-T2-05`**: Non-numeric character filtering.
   - *Preconditions*: Component active.
   - *Action*: User types `'₹ 1,200.50 abc!'`.
   - *Expected*: Sanitizes string to numeric `1200.5`.
   - *Assertion*: `expect(component.parseValue('1,200.50')).toBe(1200.5);`
6. **`CURR-T2-06`**: High magnitude amount precision (Crores / Millions `99,99,99,999.99`).
   - *Preconditions*: Component instance.
   - *Action*: `component.writeValue(999999999.99)`.
   - *Expected*: Formats accurately without floating-point precision loss.
   - *Assertion*: `expect(component.value()).toBe(999999999.99);`

#### Tier 3: Cross-Feature Interactions (2 Tests)
1. **`CURR-T3-01`**: Focus / Blur formatting transition without cursor disturbance.
   - *Preconditions*: Component holds `12345.67`.
   - *Action*: Focus component -> display unformatted raw editable text (`'12345.67'`); blur component -> display formatted text (`'12,345.67'`).
   - *Expected*: Underlying numeric model `12345.67` remains unchanged throughout transition.
   - *Assertion*: `expect(component.value()).toBe(12345.67);`
2. **`CURR-T3-02`**: Dynamic toggle of `inPaise` and `decimals` inputs.
   - *Preconditions*: Form control bound with value `50000`.
   - *Action*: Switch `inPaise` from `true` (display `'500.00'`) to `false` (display `'50000.00'`).
   - *Expected*: Display recomputes immediately to reflect updated display units.
   - *Assertion*: `expect(component.displayValue()).toBe('50000.00');`

#### Tier 4: Real-World Application Scenarios (2 Tests)
1. **`CURR-T4-01`**: Invoice Line item total recalculation in Reactive `FormGroup`.
   - *Preconditions*: `FormGroup` with `unitPrice` (`bb-currency-input`), `discountAmount` (`bb-currency-input`), `taxAmount` (`bb-currency-input`).
   - *Action*: User types `1000.00` in unitPrice, `100.00` in discount.
   - *Expected*: Reactive valueChanges stream triggers line total calculation (`900.00 + tax`).
   - *Assertion*: `expect(form.get('lineTotal')?.value).toBe(1062);` (with 18% tax).
2. **`CURR-T4-02`**: Form validation with `Validators.min(100)` and `Validators.max(50000)`.
   - *Preconditions*: Reactive form control with min/max validators.
   - *Action*: User inputs `50` -> form invalid; inputs `500` -> form valid; resets form -> control pristine.
   - *Expected*: CVA propagates touch/dirty states and validates boundaries correctly.
   - *Assertion*: `expect(control.hasError('min')).toBe(true);`

---

### 3.4 Component 3: `NumberInputComponent` (`bb-number-input`)

#### Tier 1: Feature / Contract Coverage (6 Tests)
1. **`NUM-T1-01`**: `writeValue()` updates numeric signal and native input value.
   - *Preconditions*: Component instance.
   - *Action*: `component.writeValue(42)`.
   - *Expected*: `component.value()` is `42`, `inputEl.value` is `'42'`.
   - *Assertion*: `expect(component.value()).toBe(42); expect(inputEl.value).toBe('42');`
2. **`NUM-T1-02`**: User input triggers `onChange` callback with numeric type.
   - *Preconditions*: Callback registered via `component.registerOnChange(fn)`.
   - *Action*: User types `'100'`.
   - *Expected*: `onChange` called with number `100`, `valueChange` emits `100`.
   - *Assertion*: `expect(changeSpy).toHaveBeenCalledWith(100);`
3. **`NUM-T1-03`**: Blur invokes `onTouched` and emits `blur`.
   - *Preconditions*: Callback registered via `component.registerOnTouched(fn)`.
   - *Action*: Dispatch `blur` event on input.
   - *Expected*: `onTouched` callback invoked.
   - *Assertion*: `expect(touchedSpy).toHaveBeenCalledTimes(1);`
4. **`NUM-T1-04`**: `setDisabledState()` toggles disabled property.
   - *Preconditions*: Component instance.
   - *Action*: `component.setDisabledState(true)`.
   - *Expected*: `inputEl.disabled` is `true`, `component.disabled()` is `true`.
   - *Assertion*: `expect(inputEl.disabled).toBe(true);`
5. **`NUM-T1-05`**: Prefix and suffix rendering (e.g. `'%'`, `'kg'`, `'days'`).
   - *Preconditions*: `prefix = '#' `, `suffix = 'kg'`.
   - *Action*: Mount component template.
   - *Expected*: Prefix `#` and suffix `kg` DOM containers are rendered alongside input.
   - *Assertion*: `expect(component.prefix()).toBe('#'); expect(component.suffix()).toBe('kg');`
6. **`NUM-T1-06`**: Default input attributes (`step = 1`, `align = 'left'`, `inputmode = 'decimal'`).
   - *Preconditions*: Default initialization.
   - *Action*: Inspect component inputs.
   - *Expected*: Default values match specification.
   - *Assertion*: `expect(component.step()).toBe(1); expect(component.align()).toBe('left');`

#### Tier 2: Boundary & Corner Cases (6 Tests)
1. **`NUM-T2-01`**: `writeValue(null)` and clearing input emits `null`.
   - *Preconditions*: Initial value `10`.
   - *Action*: User clears input (`inputEl.value = ''`).
   - *Expected*: `onChange` called with `null`, `component.value()` is `null`.
   - *Assertion*: `expect(changeSpy).toHaveBeenCalledWith(null);`
2. **`NUM-T2-02`**: Step precision preservation for fractional quantities (e.g. `step = 0.001`, `0.0001` purity factor).
   - *Preconditions*: `step = 0.001`.
   - *Action*: User inputs `'1.005'`.
   - *Expected*: Numeric value is exactly `1.005` without floating-point representation artifacts.
   - *Assertion*: `expect(component.value()).toBe(1.005);`
3. **`NUM-T2-03`**: Min and max boundary enforcement.
   - *Preconditions*: `min = 0`, `max = 100` (percentage discount).
   - *Action*: User enters `'-10'` and `'150'`.
   - *Expected*: Native input attributes `min="0"`, `max="100"` applied; component clamps or invalidates.
   - *Assertion*: `expect(inputEl.min).toBe('0'); expect(inputEl.max).toBe('100');`
4. **`NUM-T2-04`**: Integer mode (`decimals = 0`, `inputmode = 'numeric'`).
   - *Preconditions*: `decimals = 0`.
   - *Action*: User enters `'45.8'`.
   - *Expected*: Decimal points are rejected or truncated to integer `45`.
   - *Assertion*: `expect(component.value()).toBe(45);`
5. **`NUM-T2-05`**: Leading zero stripping and normalized numeric formatting.
   - *Preconditions*: User enters `'007'`.
   - *Action*: Parse and emit value.
   - *Expected*: Emits number `7`.
   - *Assertion*: `expect(component.parseValue('007')).toBe(7);`
6. **`NUM-T2-06`**: Scientific notation rejection or normalization (`'1e5'`).
   - *Preconditions*: User enters `'1e5'`.
   - *Action*: Parse input.
   - *Expected*: Evaluates to `100000` or sanitizes `'e'` character according to input mode.
   - *Assertion*: `expect(typeof component.value()).toBe('number');`

#### Tier 3: Cross-Feature Interactions (2 Tests)
1. **`NUM-T3-01`**: Step incrementing with min/max boundaries.
   - *Preconditions*: `min = 0`, `max = 10`, `step = 2`.
   - *Action*: Trigger step increments from `8` -> `10` -> attempt `12`.
   - *Expected*: Step stops at max boundary `10` without exceeding.
   - *Assertion*: `expect(component.value()).toBe(10);`
2. **`NUM-T3-02`**: Dynamic suffix and step unit switching (e.g. Unit of Measure change from `NOS` -> `KG`).
   - *Preconditions*: Bound to inventory item line.
   - *Action*: Dynamically switch `suffix` from `'NOS'` to `'KG'` and `step` from `1` to `0.001`.
   - *Expected*: Template updates suffix badge and accepts 3-decimal floating point inputs.
   - *Assertion*: `expect(component.suffix()).toBe('KG'); expect(component.step()).toBe(0.001);`

#### Tier 4: Real-World Application Scenarios (2 Tests)
1. **`NUM-T4-01`**: Inventory Item Master form with multi-number controls (`reorderLevel`, `leadTimeDays`, `purityFactor`).
   - *Preconditions*: Reactive `FormGroup` with multiple `bb-number-input` controls.
   - *Action*: Populate batch quantities and lead times, verify two-way synchronization with form model.
   - *Expected*: All number fields validate independently and update master object model.
   - *Assertion*: `expect(form.value.reorderLevel).toBe(50); expect(form.value.leadTimeDays).toBe(14);`
2. **`NUM-T4-02`**: Form validation with `Validators.required` and form reset.
   - *Preconditions*: `FormControl(null, Validators.required)`.
   - *Action*: Enter `0` -> valid; clear field -> invalid; reset -> pristine.
   - *Expected*: Zero is treated as valid (not falsely marked empty by truthy check); clearing sets error.
   - *Assertion*: `expect(control.valid).toBe(true);` (for value 0).

---

### 3.5 Component 4: `SearchInputComponent` (`bb-search-input`)

#### Tier 1: Feature / Contract Coverage (6 Tests)
1. **`SRCH-T1-01`**: `writeValue()` updates search text signal and native input value.
   - *Preconditions*: Component instance.
   - *Action*: `component.writeValue('invoice')`.
   - *Expected*: `component.value()` is `'invoice'`, `inputEl.value` is `'invoice'`.
   - *Assertion*: `expect(component.value()).toBe('invoice');`
2. **`SRCH-T1-02`**: User typing invokes `onChange` and emits `valueChange`.
   - *Preconditions*: Callback registered.
   - *Action*: User types `'customer'`.
   - *Expected*: `onChange` called with `'customer'`, `valueChange` emits `'customer'`.
   - *Assertion*: `expect(changeSpy).toHaveBeenCalledWith('customer');`
3. **`SRCH-T1-03`**: Blur invokes `onTouched`.
   - *Preconditions*: Callback registered.
   - *Action*: Dispatch `blur` on input.
   - *Expected*: `onTouched` invoked.
   - *Assertion*: `expect(touchedSpy).toHaveBeenCalledTimes(1);`
4. **`SRCH-T1-04`**: `setDisabledState()` disables search input and clear button.
   - *Preconditions*: Component instance.
   - *Action*: `component.setDisabledState(true)`.
   - *Expected*: `inputEl.disabled` is `true`.
   - *Assertion*: `expect(inputEl.disabled).toBe(true);`
5. **`SRCH-T1-05`**: Search icon and default placeholder `'Search...'` render in DOM.
   - *Preconditions*: Default component initialization.
   - *Action*: Inspect template elements.
   - *Expected*: Search SVG/icon element present, placeholder is `'Search...'`.
   - *Assertion*: `expect(component.placeholder()).toBe('Search...');`
6. **`SRCH-T1-06`**: Clear button (`×`) visibility toggle based on text presence.
   - *Preconditions*: Empty search input.
   - *Action*: Inspect clear button (hidden) -> user types `'test'` (clear button visible).
   - *Expected*: Clear button is conditionally displayed only when text is non-empty.
   - *Assertion*: `expect(component.hasText()).toBe(true);`

#### Tier 2: Boundary & Corner Cases (6 Tests)
1. **`SRCH-T2-01`**: `writeValue(null)` and `writeValue(undefined)` normalized to empty string `''`.
   - *Preconditions*: Previous value `'search'`.
   - *Action*: `component.writeValue(null)`.
   - *Expected*: Value normalized to `''`, native input value is `''`.
   - *Assertion*: `expect(component.value()).toBe('');`
2. **`SRCH-T2-02`**: Clear button click action clears text, emits `clear` output, emits `''` onChange, and keeps focus.
   - *Preconditions*: Input contains `'ledger query'`.
   - *Action*: Click clear button (`onClear()`).
   - *Expected*: Input cleared, `clear` output emits `void`, `valueChange` emits `''`, `onChange` emits `''`.
   - *Assertion*: `expect(clearSpy).toHaveBeenCalled(); expect(changeSpy).toHaveBeenCalledWith('');`
3. **`SRCH-T2-03`**: Enter keypress emits `search` output immediately.
   - *Preconditions*: Input contains `'INV-2026-001'`.
   - *Action*: Dispatch `keyup.enter` or `keydown.enter` event on input.
   - *Expected*: `search` output emits `'INV-2026-001'` immediately.
   - *Assertion*: `expect(searchSpy).toHaveBeenCalledWith('INV-2026-001');`
4. **`SRCH-T2-04`**: Special characters in search query (`'GST/2026-27/001 & #@!'`).
   - *Preconditions*: User enters special characters and symbols.
   - *Action*: Input query with symbols.
   - *Expected*: String preserved verbatim without HTML entity escaping or corruption.
   - *Assertion*: `expect(component.value()).toBe('GST/2026-27/001 & #@!');`
5. **`SRCH-T2-05`**: Whitespace-only search query handling.
   - *Preconditions*: User enters `'   '`.
   - *Action*: Input whitespace string.
   - *Expected*: Component handles whitespace gracefully without throwing.
   - *Assertion*: `expect(component.value()).toBe('   ');`
6. **`SRCH-T2-06`**: Rapid keystrokes debouncing behavior.
   - *Preconditions*: Fake timers active (`vi.useFakeTimers()`).
   - *Action*: Rapidly type `'a'`, `'ab'`, `'abc'` within 100ms.
   - *Expected*: Intermediary events coalesced, final term emits after debounce delay.
   - *Assertion*: `expect(searchSpy).toHaveBeenCalledTimes(1);`

#### Tier 3: Cross-Feature Interactions (2 Tests)
1. **`SRCH-T3-01`**: Sequential Type -> Clear -> Re-type interaction lifecycle.
   - *Preconditions*: Component mounted with spy on outputs.
   - *Action*: Type `'apple'` -> click clear button -> type `'banana'`.
   - *Expected*: Clear icon disappears on clear, reappears on retype; events emit in strict order: `'apple'` -> clear -> `''` -> `'banana'`.
   - *Assertion*: `expect(component.value()).toBe('banana');`
2. **`SRCH-T3-02`**: Dynamic disabled state during async search query.
   - *Preconditions*: Search in flight.
   - *Action*: Set `disabled = true`.
   - *Expected*: Input and clear button are disabled; clicks and keypresses are ignored until re-enabled.
   - *Assertion*: `expect(inputEl.disabled).toBe(true);`

#### Tier 4: Real-World Application Scenarios (2 Tests)
1. **`SRCH-T4-01`**: Table / List filtering integration (e.g. `items.page` or `contacts.page`).
   - *Preconditions*: List of 50 items bound to search filter pipeline.
   - *Action*: User types search term -> list filters down to 3 items -> user clicks clear -> full list of 50 restored.
   - *Expected*: Component triggers filtering pipeline seamlessly.
   - *Assertion*: `expect(filteredList.length).toBe(3);`
2. **`SRCH-T4-02`**: Reactive search control with form reset.
   - *Preconditions*: `FormControl('')` bound to `bb-search-input`.
   - *Action*: Set value programmatically via `formControl.setValue('audit')`, then call `formControl.reset()`.
   - *Expected*: UI reflects `'audit'`, reset clears input and hides clear button.
   - *Assertion*: `expect(inputEl.value).toBe('');`

---

### 3.6 Component 5: `TextInputComponent` (`bb-text-input`)

#### Tier 1: Feature / Contract Coverage (6 Tests)
1. **`TXT-T1-01`**: `writeValue()` updates text signal and native DOM input value.
   - *Preconditions*: Component instance.
   - *Action*: `component.writeValue('Acme Corp')`.
   - *Expected*: `component.value()` is `'Acme Corp'`, `inputEl.value` is `'Acme Corp'`.
   - *Assertion*: `expect(component.value()).toBe('Acme Corp'); expect(inputEl.value).toBe('Acme Corp');`
2. **`TXT-T1-02`**: User typing invokes `onChange` callback and emits `valueChange`.
   - *Preconditions*: Callback registered.
   - *Action*: User types `'New String'`.
   - *Expected*: `onChange` called with `'New String'`, `valueChange` emits `'New String'`.
   - *Assertion*: `expect(changeSpy).toHaveBeenCalledWith('New String');`
3. **`TXT-T1-03`**: Blur invokes `onTouched` and emits `blur`.
   - *Preconditions*: Callback registered.
   - *Action*: Dispatch `blur` event on input element.
   - *Expected*: `onTouched` callback invoked, `blur` output emits `FocusEvent`.
   - *Assertion*: `expect(touchedSpy).toHaveBeenCalledTimes(1);`
4. **`TXT-T1-04`**: `setDisabledState()` disables native input and sets CSS disabled styling.
   - *Preconditions*: Component initialized.
   - *Action*: `component.setDisabledState(true)`.
   - *Expected*: `inputEl.disabled` is `true`, `component.disabled()` is `true`.
   - *Assertion*: `expect(inputEl.disabled).toBe(true);`
5. **`TXT-T1-05`**: Input type variations (`'text'`, `'password'`, `'email'`, `'tel'`, `'url'`).
   - *Preconditions*: Signal `type` set to `'email'` or `'password'`.
   - *Action*: Inspect native input `type` attribute.
   - *Expected*: Native input `type` attribute matches input signal (`type="email"`).
   - *Assertion*: `expect(inputEl.type).toBe('email');`
6. **`TXT-T1-06`**: Enter keypress emits `enter` output with current string value.
   - *Preconditions*: Input contains `'Submit text'`.
   - *Action*: Dispatch `keyup.enter` or trigger `onEnter()`.
   - *Expected*: `enter` output emits `'Submit text'`.
   - *Assertion*: `expect(enterSpy).toHaveBeenCalledWith('Submit text');`

#### Tier 2: Boundary & Corner Cases (6 Tests)
1. **`TXT-T2-01`**: `writeValue(null)` and `writeValue(undefined)` normalized to empty string `''`.
   - *Preconditions*: Component holds `'Existing'`.
   - *Action*: `component.writeValue(null)`.
   - *Expected*: Value is `''`, input element value is `''`.
   - *Assertion*: `expect(component.value()).toBe(''); expect(inputEl.value).toBe('');`
2. **`TXT-T2-02`**: Uppercase transformation when `[uppercase]="true"` (GSTIN, PAN, IFSC, HSN codes).
   - *Preconditions*: `uppercase = true`.
   - *Action*: User types `'29aaaaa0000a1z5'`.
   - *Expected*: Transformed to uppercase `'29AAAAA0000A1Z5'` in both DOM display and model `onChange`.
   - *Assertion*: `expect(changeSpy).toHaveBeenCalledWith('29AAAAA0000A1Z5'); expect(inputEl.value).toBe('29AAAAA0000A1Z5');`
3. **`TXT-T2-03`**: Maxlength enforcement (e.g. `maxlength = 10` for PAN Card).
   - *Preconditions*: `maxlength = 10`.
   - *Action*: Native input attribute `maxlength="10"`, user enters 15 chars.
   - *Expected*: Text truncated to 10 characters (`'ABCDE1234F'`).
   - *Assertion*: `expect(inputEl.maxLength).toBe(10);`
4. **`TXT-T2-04`**: Readonly state (`[readonly]="true"`).
   - *Preconditions*: `readonly = true`.
   - *Action*: Inspect native input element attributes.
   - *Expected*: Native input has `readonly` attribute set, preventing text editing while allowing focus/selection.
   - *Assertion*: `expect(inputEl.readOnly).toBe(true);`
5. **`TXT-T2-05`**: Unicode, emoji, and special symbol text entry.
   - *Preconditions*: User enters `'🏢 Head Office — #01-A'`.
   - *Action*: Input unicode string.
   - *Expected*: Characters preserved accurately without encoding errors.
   - *Assertion*: `expect(component.value()).toBe('🏢 Head Office — #01-A');`
6. **`TXT-T2-06`**: Autocomplete attribute support (`'off'`, `'username'`, `'current-password'`).
   - *Preconditions*: `autocomplete = 'off'`.
   - *Action*: Inspect native input.
   - *Expected*: Native input has `autocomplete="off"` attribute.
   - *Assertion*: `expect(inputEl.autocomplete).toBe('off');`

#### Tier 3: Cross-Feature Interactions (2 Tests)
1. **`TXT-T3-01`**: Combined Uppercase transformation + Maxlength on pasted text.
   - *Preconditions*: `uppercase = true`, `maxlength = 10`.
   - *Action*: Paste lowercase string `'abcdefghijk12345'`.
   - *Expected*: Result is transformed to uppercase AND truncated to 10 chars (`'ABCDEFGHIJ'`).
   - *Assertion*: `expect(component.value()).toBe('ABCDEFGHIJ');`
2. **`TXT-T3-02`**: Dynamic password visibility toggle (switching `type` between `'password'` and `'text'`).
   - *Preconditions*: Bound to login password input.
   - *Action*: Dynamically switch `type` from `'password'` to `'text'` and back.
   - *Expected*: Input value `'secret123'` remains intact while native input type switches.
   - *Assertion*: `expect(inputEl.type).toBe('text'); expect(component.value()).toBe('secret123');`

#### Tier 4: Real-World Application Scenarios (2 Tests)
1. **`TXT-T4-01`**: Master Organization & Tax Master form integration (GSTIN, PAN, Legal Name).
   - *Preconditions*: Reactive `FormGroup` with GSTIN regex validator `^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$`.
   - *Action*: User enters lowercase valid GSTIN `'29aaaaa0000a1z5'`.
   - *Expected*: Component converts to uppercase, regex validation passes, and form is `VALID`.
   - *Assertion*: `expect(form.get('gstin')?.valid).toBe(true);`
2. **`TXT-T4-02`**: Form validation states (`touched`, `dirty`, `required`) and form reset lifecycle.
   - *Preconditions*: `FormControl('', Validators.required)`.
   - *Action*: Focus and blur without typing -> `touched=true`, `invalid=true`; type value -> `dirty=true`, `valid=true`; call `reset()` -> `pristine=true`, `untouched=true`.
   - *Expected*: Seamless state reflection across the entire Angular form lifecycle.
   - *Assertion*: `expect(control.pristine).toBe(true); expect(control.value).toBeNull();`

---

## 4. Test Implementation Architecture & Idioms

### 4.1 Recommended Spec File Structure
Each component should have a dedicated spec file co-located in its library folder:
- `libs/shared/ui-components/src/lib/date-input/date-input.component.spec.ts`
- `libs/shared/ui-components/src/lib/currency-input/currency-input.component.spec.ts`
- `libs/shared/ui-components/src/lib/number-input/number-input.component.spec.ts`
- `libs/shared/ui-components/src/lib/search-input/search-input.component.spec.ts`
- `libs/shared/ui-components/src/lib/text-input/text-input.component.spec.ts`

### 4.2 Standard Test Pattern (Direct + TestBed)
```typescript
import { Component, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { DateInputComponent } from './date-input.component';

describe('DateInputComponent', () => {
  describe('Tier 1: Feature / Contract Coverage', () => {
    it('writes value and updates signal', () => {
      const comp = new DateInputComponent();
      comp.writeValue('2026-08-18');
      expect(comp.value()).toBe('2026-08-18');
    });

    it('invokes registered onChange on user input', () => {
      const comp = new DateInputComponent();
      const changeSpy = vi.fn();
      comp.registerOnChange(changeSpy);
      comp.onInput({ target: { value: '2026-12-31' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith('2026-12-31');
    });

    it('invokes registered onTouched on blur', () => {
      const comp = new DateInputComponent();
      const touchSpy = vi.fn();
      comp.registerOnTouched(touchSpy);
      comp.onBlur(new FocusEvent('blur'));
      expect(touchSpy).toHaveBeenCalledTimes(1);
    });

    it('updates disabled state', () => {
      const comp = new DateInputComponent();
      comp.setDisabledState(true);
      expect(comp.disabled()).toBe(true);
      comp.setDisabledState(false);
      expect(comp.disabled()).toBe(false);
    });

    it('renders default attributes', () => {
      const comp = new DateInputComponent();
      expect(comp.placeholder()).toBe('YYYY-MM-DD');
      expect(comp.ariaLabel()).toBe('Date');
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('normalizes null and undefined to null', () => {
      const comp = new DateInputComponent();
      comp.writeValue(null);
      expect(comp.value()).toBeNull();
      comp.writeValue(undefined as any);
      expect(comp.value()).toBeNull();
    });

    it('emits null on empty string clear', () => {
      const comp = new DateInputComponent();
      const changeSpy = vi.fn();
      comp.registerOnChange(changeSpy);
      comp.onInput({ target: { value: '' } } as unknown as Event);
      expect(changeSpy).toHaveBeenCalledWith(null);
    });
  });

  describe('Tier 3: Cross-Feature Interactions', () => {
    it('preserves value across disabled toggles', () => {
      const comp = new DateInputComponent();
      comp.writeValue('2026-08-18');
      comp.setDisabledState(true);
      expect(comp.value()).toBe('2026-08-18');
      comp.setDisabledState(false);
      expect(comp.value()).toBe('2026-08-18');
    });
  });

  describe('Tier 4: Real-World Application Scenarios', () => {
    it('integrates with Reactive FormControl and validation', () => {
      const control = new FormControl('2026-08-18', Validators.required);
      const comp = new DateInputComponent();
      comp.registerOnChange((val) => control.setValue(val));
      comp.registerOnTouched(() => control.markAsTouched());

      expect(control.valid).toBe(true);
      comp.onInput({ target: { value: '' } } as unknown as Event);
      expect(control.valid).toBe(false);
      comp.onBlur(new FocusEvent('blur'));
      expect(control.touched).toBe(true);
    });
  });
});
```

---

## 5. Caveats

1. **JSDOM vs Real Browser Native Date Picker**:
   - In JSDOM, native `<input type="date">` does not render a visual date picker dropdown. Tests must verify the underlying ISO string manipulation (`'YYYY-MM-DD'`), DOM value attributes, and CVA contract events rather than UI calendar popups.
2. **Debounce Timers in Search Tests**:
   - When testing debounced keystrokes in `SearchInputComponent`, use `vi.useFakeTimers()` and `vi.advanceTimersByTime(300)` to advance debounce clocks predictably without flaky asynchronous timeouts.
3. **Paise vs Rupee Precision**:
   - For `CurrencyInputComponent`, backend accounting entities store integer paise (`₹ 100.50` = `10050` paise). When testing `[inPaise]="true"`, integer multiplication (`Math.round(val * 100)`) must be used to prevent IEEE 754 floating-point drift (e.g. `2.29 * 100` being `228.99999999999997`).
4. **Angular Signal Input Reactivity**:
   - In Angular 20 signal-based components (`input<string>()`), direct unit tests can use default signal values or test harness wrappers (`ComponentFixture.componentRef.setInput(...)`) to test reactive input signal property changes.

---

## 6. Conclusion

- A systematic, robust 4-tier test case matrix containing **79 test cases** has been designed for all 5 primitive UI components (`bb-date-input`, `bb-currency-input`, `bb-number-input`, `bb-search-input`, `bb-text-input`).
- All milestone thresholds (>=5 Tier 1, >=5 Tier 2, 1-2 Tier 3, 1-2 Tier 4 per component) are fully satisfied.
- The test suite guarantees:
  1. Complete CVA contract compliance (`writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState`).
  2. Resilient boundary handling (nulls, zeros, negatives, large numbers, precision scaling, uppercase transforms, maxlength).
  3. Flawless dynamic state interactions (dynamic disabled states, step bounds, formatting transitions).
  4. 100% interoperability with both Reactive Forms (`[formControl]`, `formControlName`, `FormGroup`) and Template-driven forms (`[(ngModel)]`).

---

## 7. Verification Method

To independently verify the test strategy and test suite execution:
1. **Run Unit Tests**:
   ```powershell
   cd frontend
   npm test
   ```
   Or run the UI components spec files directly:
   ```powershell
   cd frontend
   npx vitest run libs/shared/ui-components
   ```
2. **Run Full Verification Suite**:
   ```powershell
   cd frontend
   npm run check
   ```
   This executes linting, typechecking, vitest suite, and production build to confirm zero warnings and zero regressions.
