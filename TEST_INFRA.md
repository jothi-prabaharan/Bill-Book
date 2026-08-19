# E2E & Component Test Infra: Frontend Primitive UI Components

## Test Philosophy
- Opaque-box and contract-driven verification.
- Verify full ControlValueAccessor compliance: `writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState`.
- Verify template-driven `[(ngModel)]` two-way bindings, event emissions (`(ngModelChange)`, `(blur)`, `(focus)`, `(keyup.enter)`), and Reactive Forms (`formControlName`).
- Verify input boundary conditions: decimal scaling, min/max clipping/validation, uppercase transform, negative value handling, empty/null values, disabled interactions.

## Feature Inventory
| # | Feature | Source | Tier 1 (Contract) | Tier 2 (Boundary) | Tier 3 (Cross-Feature) | Tier 4 (Real-World) |
|---|---------|--------|:-----------------:|:-----------------:|:----------------------:|:-------------------:|
| 1 | DateInputComponent (`bb-date-input`) | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ | ✓ |
| 2 | CurrencyInputComponent (`bb-currency-input`) | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ | ✓ |
| 3 | NumberInputComponent (`bb-number-input`) | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ | ✓ |
| 4 | SearchInputComponent (`bb-search-input`) | Survey §3.4 | 5 | 5 | ✓ | ✓ |
| 5 | TextInputComponent (`bb-text-input`) | Survey §3.5 | 5 | 5 | ✓ | ✓ |

## Test Architecture
- Framework: Vitest in jsdom environment (`npm run test` or `npx vitest run`).
- Location: `libs/shared/ui-components/src/lib/**/*.spec.ts` and page integration specs.
- Criteria: 100% tests pass, zero lint errors, zero typecheck errors, clean build (`npm run check`).
