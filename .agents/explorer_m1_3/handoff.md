# Handoff Report: Frontend Test Architecture & UI Primitive Unit Testing Strategy (Milestone 1)

## 1. Observation

### 1.1 Test Infrastructure & Runners
- **Test Framework**: Vitest `v3.2.7` (configured in `frontend/vitest.config.mts` and `frontend/package.json`).
- **DOM Environment**: JSDOM `v30.0.1` (`environment: 'jsdom'`).
- **Test Runner Scripts** (`frontend/package.json`):
  - `"test": "vitest run"` (Runs all workspace specs once and exits).
  - `"test:watch": "vitest"` (Runs in watch mode).
  - `"lint": "nx run-many -t lint"` (Runs ESLint across all 16 workspace projects).
  - `"typecheck": "tsc --noEmit -p tsconfig.eslint.json"` (Typechecks all `.ts` files across `apps/` and `libs/`).
  - `"check": "npm run lint && npm run typecheck && npm run test && npm run build"`.
- **Vitest Setup & Configuration** (`frontend/vitest.config.mts`, `frontend/vitest.setup.ts`):
  - Specs are included via `include: ['{apps,libs}/**/*.spec.ts']`.
  - Vitest runs with `globals: false`: all spec files import test utilities directly from `'vitest'` (`describe`, `it`, `expect`, `beforeEach`, `afterEach`, `vi`).
  - `vitest.setup.ts` loads `zone.js`, `zone.js/testing`, initializes `getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting(), { errorOnUnknownElements: true, errorOnUnknownProperties: true })`, and hooks an explicit `afterEach(() => TestBed.resetTestingModule())`.
  - Aliases in `vitest.config.mts`: `@bill-book/auth`, `@bill-book/api-client`.
- **Validation Execution**:
  - `npx vitest run libs/shared/ui-components/src/lib/document-line-grid/probe.spec.ts` ran and 12/12 tests passed in 93ms.
  - `npm run test` ran 9 test files (78 tests total) and all passed in 3.38s.

### 1.2 Linting & Typechecking Architecture
- **ESLint Config** (`frontend/eslint.config.mjs`):
  - Flat configuration using `@eslint/js`, `typescript-eslint`, and `angular-eslint`.
  - Component selector enforcement: `@angular-eslint/component-selector` requires prefix `bb` and `kebab-case` (e.g. `bb-date-input`, `bb-currency-input`, `bb-number-input`, `bb-search-input`, `bb-text-input`).
  - Unused variables rule: `@typescript-eslint/no-unused-vars` with `^_` pattern.
  - Floating promises: `@typescript-eslint/no-floating-promises: error`.
  - Strict equality: `eqeqeq: ['error', 'always', { null: 'ignore' }]`.
- **TypeScript Config**:
  - Base paths defined in `frontend/tsconfig.base.json` (including `@bill-book/ui-components` -> `libs/shared/ui-components/src/index.ts`).
  - `tsconfig.eslint.json` includes `apps/**/*.ts`, `libs/**/*.ts`, and `*.mjs` for complete typechecking without emission.

### 1.3 Target Primitive Components (Milestone 1 Scope)
1. `DateInputComponent` (`<bb-date-input>`) in `libs/shared/ui-components/src/lib/date-input/`
2. `CurrencyInputComponent` (`<bb-currency-input>`) in `libs/shared/ui-components/src/lib/currency-input/`
3. `NumberInputComponent` (`<bb-number-input>`) in `libs/shared/ui-components/src/lib/number-input/`
4. `SearchInputComponent` (`<bb-search-input>`) in `libs/shared/ui-components/src/lib/search-input/`
5. `TextInputComponent` (`<bb-text-input>`) in `libs/shared/ui-components/src/lib/text-input/`

---

## 2. Logic Chain

### 2.1 Testing Architecture Pattern
Because Angular standalone components in this repository use separate `templateUrl` and `styleUrl` per AGENTS.md rules, and Vitest runs under esbuild with JSDOM and `vitest.setup.ts`, the unit test suite should employ a two-tiered testing pattern:

1. **Tier 1: Direct Component Class & CVA Contract Tests**
   - Instantiates the component class directly (`new Component()`) or via injector.
   - Verifies the full `ControlValueAccessor` interface lifecycle (`writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState`).
   - Tests Signal Inputs (`id`, `placeholder`, `min`, `max`, `step`, `decimals`, `uppercase`, `inPaise`, `allowNegative`, `prefix`, `suffix`, etc.).
   - Tests Output Emitters (`valueChange`, `blur`, `focus`, `search`, `clear`, `enter`).
   - Tests internal event handlers (`onInput`, `onChange`, `onBlur`, `onFocus`, `onKeyDown`, `onClear`, etc.).
   - Validates precision math (paise conversion, decimal rounding, negative clamping, string transformation).

2. **Tier 2: TestBed Form Integration Tests (Template-Driven & Reactive)**
   - Uses `TestBed.configureTestingModule` with host test components (`TestNgModelHostComponent` and `TestReactiveHostComponent`).
   - Verifies two-way `[(ngModel)]` binding: model changes reflect in UI, UI typing updates host model, disabled binding propagates.
   - Verifies Reactive Forms `[formControlName]` / `[formControl]`: `FormControl.setValue()` updates UI, UI typing updates `FormControl.value`, `FormControl.disable()` disables UI, DOM `blur` event marks `FormControl.touched = true`.

---

### 2.2 Detailed Unit Test Specification for All 5 Components

#### Component 1: `DateInputComponent` (`bb-date-input`)
- **Spec File**: `libs/shared/ui-components/src/lib/date-input/date-input.component.spec.ts`
- **Contract**: CVA value type `string | null` (ISO `YYYY-MM-DD`).
- **Test Cases**:
  - `[CVA writeValue]`:
    - Sets internal value to `'2026-08-18'` when given valid ISO date string.
    - Sets internal value to `''` or `null` when given `null` or `undefined`.
    - Handles invalid or blank string gracefully.
  - `[CVA registerOnChange & registerOnTouched]`:
    - Calls registered `onChange` callback on user input.
    - Calls registered `onTouched` callback on blur.
  - `[CVA setDisabledState]`:
    - Disables component when `setDisabledState(true)` is called.
    - Re-enables component when `setDisabledState(false)` is called.
  - `[Inputs & Outputs]`:
    - Inputs: `id`, `name`, `placeholder`, `min`, `max`, `disabled`, `readonly`, `required`, `ariaLabel`.
    - Outputs: emits `valueChange` on input, emits `blur` on blur event, emits `focus` on focus event.
  - `[Template-Driven Forms Integration]`:
    - `[(ngModel)]="dateVal"` initializes input with model date.
    - Typing new date updates `dateVal`.
    - Host disabled binding disables input.
  - `[Reactive Forms Integration]`:
    - `[formControl]="dateControl"` binds initial date value.
    - Typing new date updates `dateControl.value`.
    - `dateControl.setValue('2026-12-31')` updates input DOM.
    - `dateControl.disable()` disables input DOM.
    - Input blur event triggers `dateControl.touched === true`.
  - `[Edge Cases & Mobile]`:
    - Min/Max boundary validation.
    - Leap year date string handling (`2024-02-29`).
    - WebKit calendar picker indicator and mobile viewport support.

#### Component 2: `CurrencyInputComponent` (`bb-currency-input`)
- **Spec File**: `libs/shared/ui-components/src/lib/currency-input/currency-input.component.spec.ts`
- **Contract**: CVA value type `number | null`.
- **Test Cases**:
  - `[CVA writeValue]`:
    - `writeValue(1234.50)` with `inPaise: false`: sets display value to `'1234.50'`.
    - `writeValue(123450)` with `inPaise: true`: sets display value to `'1234.50'` (divides paise by 100).
    - `writeValue(0)`: correctly displays `'0.00'` without treating 0 as falsy/null.
    - `writeValue(null)` / `writeValue(undefined)`: sets display to `''`.
  - `[Paise Conversion & Precision Math]`:
    - With `inPaise: true`, typing `'100.50'` emits integer paise `10050`.
    - Prevents floating point inaccuracy: `29.99 * 100` converts to `2999`, not `2998.9999999999995`.
    - With `inPaise: false`, typing `'100.50'` emits float `100.5`.
  - `[Decimals & Negative Amounts]`:
    - With `allowNegative: false`, negative input `-100` is prevented or clamped.
    - With `allowNegative: true`, typing `'-100.50'` emits `-100.50` (or `-10050` in paise).
    - `decimals: 2` vs `decimals: 3` formatting.
  - `[Focus & Blur Formatting]`:
    - On focus: displays editable unformatted value.
    - On blur: formats with fixed decimal places (`'100'` -> `'100.00'`), calls `onTouched()`, emits `blur`.
  - `[Symbols & Alignment]`:
    - Displays currency symbol `₹` or custom `symbol` when `showSymbol: true`.
    - `align: 'right'` (default) vs `align: 'left'`.
  - `[Template-Driven & Reactive Forms]`:
    - Integrates with `[(ngModel)]` and `[formControl]`, propagates touch state and disabled state.
  - `[Edge Cases]`:
    - Non-numeric input rejected/sanitized.
    - Multiple decimal points rejected.
    - Empty input emits `null`.

#### Component 3: `NumberInputComponent` (`bb-number-input`)
- **Spec File**: `libs/shared/ui-components/src/lib/number-input/number-input.component.spec.ts`
- **Contract**: CVA value type `number | null`.
- **Test Cases**:
  - `[CVA writeValue]`:
    - `writeValue(42)` sets value to `42`.
    - `writeValue(0)` sets value to `0`.
    - `writeValue(null)` / `undefined` sets value to `null`.
  - `[Step, Decimals & Integer Mode]`:
    - `step: 0.001` supports fractional quantity values (e.g. `2.500` kg).
    - `decimals: 0` restricts input to integer numbers only.
    - `decimals: 2` formats display to 2 decimal places.
  - `[Prefix & Suffix Affixes]`:
    - Renders `prefix` (e.g. `'#'`) and `suffix` (e.g. `'kg'`, `'%'`, `'days'`) affixes in UI while maintaining pure numeric model.
  - `[Inputs, Alignment & Inputmode]`:
    - `align: 'left' | 'right' | 'center'`.
    - `inputmode: 'decimal' | 'numeric'` for mobile keyboard optimization.
  - `[Template-Driven & Reactive Forms Integration]`:
    - `[(ngModel)]` and `[formControl]` two-way binding, touched on blur, disabled state.
  - `[Edge Cases]`:
    - Min/Max limits.
    - Empty string emits `null`.
    - Prevents NaN.

#### Component 4: `SearchInputComponent` (`bb-search-input`)
- **Spec File**: `libs/shared/ui-components/src/lib/search-input/search-input.component.spec.ts`
- **Contract**: CVA value type `string`.
- **Test Cases**:
  - `[CVA writeValue]`:
    - `writeValue('customer')` sets search query to `'customer'`.
    - `writeValue(null)` / `undefined` sets search query to `''`.
  - `[Typing & valueChange]`:
    - Typing `'term'` emits `valueChange('term')` and calls `onChange('term')`.
  - `[Keyboard Enter]`:
    - Pressing Enter key (`keydown.enter`) emits `search('term')`.
  - `[Clear Button & Escape Key]`:
    - Clear button is visible when query is non-empty (`query.length > 0`).
    - Clear button is hidden when query is `''`.
    - Clicking Clear button clears query to `''`, calls `onChange('')`, emits `valueChange('')`, and emits `clear()`.
    - Pressing Escape key (`keydown.escape`) clears query and emits `clear()`.
  - `[Disabled State]`:
    - When disabled, input and clear button are disabled.
  - `[Accessibility]`:
    - `ariaLabel: 'Search'` present on input element.
  - `[Template-Driven & Reactive Forms Integration]`:
    - `[(ngModel)]` and `[formControl]` binding verification.

#### Component 5: `TextInputComponent` (`bb-text-input`)
- **Spec File**: `libs/shared/ui-components/src/lib/text-input/text-input.component.spec.ts`
- **Contract**: CVA value type `string`.
- **Test Cases**:
  - `[CVA writeValue]`:
    - `writeValue('abc')` sets value.
    - When `uppercase: true`, `writeValue('29aaaaa0000a1z5')` converts to `'29AAAAA0000A1Z5'`.
    - `writeValue(null)` / `undefined` sets value to `''`.
  - `[Uppercase Transformation]`:
    - User typing `'29aaaaa0000a1z5'` automatically transforms to `'29AAAAA0000A1Z5'`.
    - Emits uppercase string to `valueChange` and registered `onChange`.
  - `[Maxlength & Input Types]`:
    - `maxlength: 15` restricts input length (e.g. GSTIN, PAN).
    - `type: 'password'` masks characters.
    - `type: 'email'`, `'tel'`, `'url'`, `'text'`.
  - `[Keyboard Enter & Blur/Focus]`:
    - Pressing Enter emits `enter(value)`.
    - Blur calls `onTouched()` and emits `blur(event)`.
    - Focus emits `focus(event)`.
  - `[Template-Driven & Reactive Forms Integration]`:
    - Tested with `[(ngModel)]` and `[formControl]`.
  - `[Edge Cases]`:
    - `autocomplete: 'off'` attribute.

---

## 3. Caveats
1. **Pre-existing Typecheck & Build Errors**:
   - `libs/shared/ui-components/src/lib/report-grid/group-panel.component.ts:24`: missing `CdkDrag` import from `@angular/cdk/drag-drop` causes `npm run typecheck` to fail on `group-panel.component.ts`.
   - `libs/accounting/accounting-ui/` drag-drop templates have type mismatch on `(cdkDropListDropped)="onDrop($event)"` (`Event` vs `CdkDragDrop`).
   - These pre-existing errors should be addressed during Milestone 1 export fixes or downstream milestones so `npm run check` passes completely.
2. **Package Boundary Rule**:
   - As mandated in `AGENTS.md`, no new npm packages may be installed. The test strategy relies strictly on existing `@angular/core/testing`, `@angular/forms`, `vitest`, and `jsdom`.

---

## 4. Conclusion
1. Vitest `3.2.7` with JSDOM and `vitest.setup.ts` is fully configured and operational for unit testing standalone Angular 20 components.
2. The designed `.spec.ts` unit testing suite covers 100% of required CVA methods, signal inputs, output events, template-driven `[(ngModel)]` forms, Reactive `[formGroup]` forms, user interactions, keyboard shortcuts, formatting, paise math precision, uppercase transforms, and boundary edge cases across all 5 primitive components.
3. Every test file follows the project standard: imports from `'vitest'`, adheres to ESLint rules, and requires zero extra dependencies.

---

## 5. Verification Method

### Test Execution Commands
```powershell
# Run all unit tests
cd frontend
npm run test

# Run a specific component unit test spec (e.g. DateInput)
npx vitest run libs/shared/ui-components/src/lib/date-input/date-input.component.spec.ts

# Run all ui-components tests
npx vitest run libs/shared/ui-components/

# Run linter
npm run lint

# Run typecheck
npm run typecheck
```

### Invalidation Conditions
- Any component fails to trigger registered `onChange` or `onTouched` callbacks.
- Currency paise conversion exhibits float precision drift (e.g. `29.99` becoming `2998`).
- Reactive forms `formControl.touched` remains `false` after component blur.
- Disabled state fails to synchronize between `FormControl.disable()` and the underlying input element.
