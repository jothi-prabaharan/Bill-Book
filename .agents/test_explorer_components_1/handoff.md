# Frontend Test Infrastructure & CVA Component Testing Investigation Report

## 1. Observation

### 1.1 Test Configuration & Workspace Infrastructure
Direct inspection of `frontend/vitest.config.mts`, `frontend/vitest.setup.ts`, and `frontend/package.json` revealed:

1. **Test Runner**: Vitest 3.2.7 with `jsdom` 30.0.1 environment (`frontend/vitest.config.mts:26`).
2. **Execution Scope**: Single root configuration scanning `include: ['{apps,libs}/**/*.spec.ts']` (`vitest.config.mts:27`).
3. **Platform Initialization**: `frontend/vitest.setup.ts` initializes Angular's test environment once per test run:
   ```ts
   import 'zone.js';
   import 'zone.js/testing';
   import { afterEach } from 'vitest';
   import { TestBed, getTestBed } from '@angular/core/testing';
   import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';

   getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting(), {
     errorOnUnknownElements: true,
     errorOnUnknownProperties: true,
   });

   afterEach(() => {
     TestBed.resetTestingModule();
   });
   ```
4. **Globals Configuration**: `globals: false`. All spec files MUST import test primitives explicitly from `vitest` (`describe`, `it`, `expect`, `beforeEach`, `afterEach`, `vi`).
5. **TypeScript & Decorators**: `vitest.config.mts:31-41` configures esbuild with `experimentalDecorators: true` and `useDefineForClassFields: false`.
6. **Path Aliases**: `vitest.config.mts:43-51` explicitly aliases `@bill-book/auth` and `@bill-book/api-client`. Other internal libraries use relative imports across existing specs (e.g., `./document-line.model`).

### 1.2 Test Suite Execution Baseline
Running `npm test` in `frontend` executes:
- **Files**: 9 spec files passed (78 tests total) in 3.29s.
- **Spec Inventory**:
  - `libs/reporting/reporting-core/src/lib/report-state.spec.ts` (5 tests)
  - `libs/shared/api-client/src/lib/api-base-url.interceptor.spec.ts` (5 tests)
  - `libs/shared/auth/src/lib/auth.interceptor.spec.ts` (7 tests)
  - `libs/shared/auth/src/lib/auth.service.spec.ts` (11 tests)
  - `libs/shared/auth/src/lib/license.guard.spec.ts` (12 tests)
  - `libs/shared/auth/src/lib/token-claims.spec.ts` (6 tests)
  - `libs/shared/ui-components/src/lib/document-line-grid/line-math.spec.ts` (9 tests)
  - `libs/shared/ui-components/src/lib/document-line-grid/tax-fixture.spec.ts` (16 tests)
  - `libs/shared/ui-components/src/lib/report-grid/filter-operators.spec.ts` (7 tests)

### 1.3 Angular 20 Signal Authoring (`input()`, `output()`) & Injection Context
During empirical probing of CVA components utilizing Angular 20 signal inputs/outputs:
- Direct instantiation via `new MyCvaComponent()` throws verbatim:
  `NG0203: inputFunction() can only be used within an injection context such as a constructor, a factory function, a field initializer, or a function used with 'runInInjectionContext'.`
- Instantiation within an injection context (`TestBed.runInInjectionContext(() => new MyCvaComponent())`) or via `TestBed.createComponent(...)` resolves cleanly.

### 1.4 Template Compilation in Vitest + esbuild
- Vitest compiles TypeScript via `esbuild` without `@analogjs/vite-plugin-angular` (in adherence to project decisions documented in `vitest.config.mts:7-20`).
- Attempting `TestBed.createComponent(ComponentWithExternalTemplateUrl)` in JIT mode without template inlining fails with:
  `Component 'XComponent' is not resolved: - templateUrl: ./x.component.html. Did you run and wait for 'resolveComponentResources()' ?`
- Components tested via:
  1. Direct class contract execution within `TestBed.runInInjectionContext`, and
  2. Inline Test Host component harnesses (`template: \`<bb-... [(ngModel)]="val" />\``) with inline component stubs
  execute deterministically with sub-millisecond execution times.

### 1.5 Codebase Lint & Typecheck Status
- `npm run lint`: Passed (0 errors, 12 warnings in `ui-components` for `any` types in data-grid).
- `npm run typecheck`: Discovered 1 compile error in `libs/shared/ui-components/src/lib/report-grid/group-panel.component.ts:24:13`:
  `Cannot find name 'CdkDrag'` (missing import from `@angular/cdk/drag-drop` in header).

---

## 2. Logic Chain

1. **Requirement Analysis**: Milestone M1 requires implementing 5 standalone primitive CVA components in `libs/shared/ui-components`:
   - `DateInputComponent` (`bb-date-input`)
   - `CurrencyInputComponent` (`bb-currency-input`)
   - `NumberInputComponent` (`bb-number-input`)
   - `SearchInputComponent` (`bb-search-input`)
   - `TextInputComponent` (`bb-text-input`)
   along with comprehensive unit and integration tests per `PROJECT.md` and `TEST_INFRA.md`.
2. **CVA Contract Verification**: A robust ControlValueAccessor implementation must fulfill four contractual responsibilities:
   - `writeValue(val)`: Receive model updates from Angular forms (or null/undefined) and update internal signal state and formatted display values.
   - `registerOnChange(fn)`: Store callback and execute it when value is modified by user interaction.
   - `registerOnTouched(fn)`: Store callback and execute it when element loses focus (blur).
   - `setDisabledState(isDisabled)`: Update disabled signal/state when the enclosing form control is disabled/enabled.
3. **Form Integration Testing**: Both template-driven (`[(ngModel)]`, `(ngModelChange)`) and reactive forms (`[formControl]`, `formControlName`, `[formGroup]`) must be verified to ensure bi-directional data flow, validation reflection, and disabled state propagation.
4. **Execution Strategy**: Direct class tests via `TestBed.runInInjectionContext` verify 100% of the internal logic, edge cases, formatting, clipping, and parsing logic without DOM overhead. For DOM & form binding validation, standalone test harnesses with inline templates verify the full Angular form synchronization pipeline.
5. **Async Handling**: Utilizing `async`/`await` with `await fixture.whenStable()` guarantees synchronization for Angular change detection and microtask queues across `ngModel` and `FormControl` updates. For timer-based debouncing (such as `SearchInputComponent`), `fakeAsync` with `tick()` / `flush()` from `zone.js/testing` provides precise time control.

---

## 3. Caveats

1. **Closed Dependency List**: `package.json` and `Directory.Packages.props` are strictly immutable per repository rules. No testing libraries (e.g. `@testing-library/angular`, `ng-mocks`, or `@analogjs/vite-plugin-angular`) can be added.
2. **No Test Globals**: Tests must not rely on global `describe`, `it`, `expect`, `vi`, or Jasmine mocks (`spyOn`, `jasmine.createSpy`). Always import from `vitest`.
3. **Component Instantiation Rule**: Never call `new Component()` in tests for Angular 20 components that use `input()` or `output()`. Always wrap with `TestBed.runInInjectionContext(() => new Component())` or use `TestBed.createComponent()`.
4. **External Template URLs in JIT**: Because esbuild does not bundle external HTML files into JIT components at test time, unit specs for `ui-components` must test component logic and CVA methods via `TestBed.runInInjectionContext` and CVA contract harnesses.

---

## 4. Conclusion & Recommended Spec Architecture

For the 5 primitive UI components (`bb-date-input`, `bb-currency-input`, `bb-number-input`, `bb-search-input`, `bb-text-input`), all `.spec.ts` files in `libs/shared/ui-components/src/lib/` should adhere to the following unified test structure:

### 4.1 Spec Structure Blueprint

Each component spec (`<component-name>.spec.ts`) will contain 4 testing tiers:
- **Tier 1: Direct CVA Contract Unit Tests** (via `TestBed.runInInjectionContext`)
  - `writeValue()` handling `null`, `undefined`, empty string, valid values, and invalid formats.
  - `registerOnChange()` callback execution on input changes.
  - `registerOnTouched()` callback execution on blur.
  - `setDisabledState()` updating internal disabled signal.
- **Tier 2: Boundary & Transformation Tests**
  - Numeric scaling, step rounding, min/max clamping, negative amounts.
  - Indian currency formatting (paise conversion, comma formatting, ₹ symbol prefix).
  - Uppercase transforms for GSTIN, PAN, IFSC codes.
  - ISO 8601 date parsing, min/max date boundaries.
- **Tier 3: Template-Driven Forms Integration (`[(ngModel)]`)**
  - Host component binding `[(ngModel)]="val"` and `[disabled]="isDisabled"`.
  - DOM event dispatching: `inputEl.value = '...'; inputEl.dispatchEvent(new Event('input'))`.
  - Two-way binding updates and `(ngModelChange)` emissions.
- **Tier 4: Reactive Forms Integration (`[formGroup]`, `formControlName`)**
  - Host component binding `[formGroup]="form"` and `formControlName="fieldName"`.
  - `formControl.setValue(...)` DOM reflection.
  - `formControl.disable()` and `formControl.enable()` disabled state reflection.
  - `inputEl.dispatchEvent(new FocusEvent('blur'))` updating `formControl.touched`.

### 4.2 Standard Code Patterns & Idioms

#### Pattern A: Direct CVA Contract Testing
```ts
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CurrencyInputComponent } from './currency-input.component';

describe('CurrencyInputComponent — Direct Contract', () => {
  let cva: CurrencyInputComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    cva = TestBed.runInInjectionContext(() => new CurrencyInputComponent());
  });

  it('handles writeValue with null and numeric values', () => {
    cva.writeValue(null);
    expect(cva.value()).toBeNull();

    cva.writeValue(1500.5);
    expect(cva.value()).toBe(1500.5);
  });

  it('invokes registered onChange callback on user input', () => {
    const onChangeSpy = vi.fn();
    cva.registerOnChange(onChangeSpy);

    cva.onInputChange('2500');
    expect(onChangeSpy).toHaveBeenCalledWith(2500);
  });

  it('invokes registered onTouched callback on blur', () => {
    const onTouchedSpy = vi.fn();
    cva.registerOnTouched(onTouchedSpy);

    cva.onInputBlur(new FocusEvent('blur'));
    expect(onTouchedSpy).toHaveBeenCalled();
  });

  it('updates disabled state when setDisabledState is called', () => {
    expect(cva.disabled()).toBe(false);
    cva.setDisabledState(true);
    expect(cva.disabled()).toBe(true);
  });
});
```

#### Pattern B: Event Simulation Idioms
```ts
// User input typing:
inputEl.value = '12345';
inputEl.dispatchEvent(new Event('input', { bubbles: true }));
fixture.detectChanges();

// Blur (touch):
inputEl.dispatchEvent(new FocusEvent('blur'));
fixture.detectChanges();

// Focus:
inputEl.dispatchEvent(new FocusEvent('focus'));
fixture.detectChanges();

// Enter key:
inputEl.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true }));
fixture.detectChanges();
```

#### Pattern C: Forms Integration Async Idiom
```ts
it('synchronizes programmatic model updates to view', async () => {
  host.modelValue = '2026-08-18';
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();

  expect(inputEl.value).toBe('2026-08-18');
});
```

---

## 5. Verification Method

### 5.1 Commands to Run
To independently verify the frontend test suite and test infrastructure:

1. **Run Full Test Suite**:
   ```bash
   cd frontend
   npm test
   ```
   *Expected Result*: All 9 existing test files pass cleanly (78 tests) in under 5 seconds with 0 failures.

2. **Run Individual Spec File**:
   ```bash
   cd frontend
   npx vitest run libs/shared/auth/src/lib/auth.service.spec.ts
   ```
   *Expected Result*: Target test file runs and passes in under 2.5 seconds.

3. **Run Linter**:
   ```bash
   cd frontend
   npm run lint
   ```
   *Expected Result*: 0 lint errors across 16 Nx projects.

4. **Run Typecheck**:
   ```bash
   cd frontend
   npm run typecheck
   ```
   *Note*: Currently flags the 1 pre-existing `Cannot find name 'CdkDrag'` issue in `group-panel.component.ts`.

### 5.2 Invalidation Conditions
The testing setup recommendations herein would be invalidated if:
- Vitest is reconfigured with `@analogjs/vite-plugin-angular` (would allow direct `templateUrl` compilation in JIT TestBed).
- The package list is modified (violates repository rules).
- `vitest.config.mts` is altered to enable `globals: true`.
