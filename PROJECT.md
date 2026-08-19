# Project: Frontend Primitive UI Components & Global Refactoring

## Architecture
- **Framework**: Angular 20 (Standalone Components, Signals, `inject()`, ControlValueAccessor).
- **Styling**: Scoped SCSS utilizing root design tokens (`styles.scss` CSS variables: `--color-accent`, `--color-divider`, `--color-text`, `--radius-md`, etc.) and `.input` base styles.
- **Component Host**: `libs/shared/ui-components/src/lib/`
- **Barrel Export**: `libs/shared/ui-components/src/index.ts`
- **Package Consumers**:
  - `libs/accounting/accounting-ui` (Template-driven forms with `[(ngModel)]`, Signals)
  - `libs/inventory/inventory-ui` (Template-driven forms with `[(ngModel)]`, dynamic lines)
  - `libs/master/master-ui` (Template-driven forms with `[(ngModel)]`, file uploads, lookups)
  - `libs/purchase/purchase-ui` (Signal state with `[ngModel]` / `(ngModelChange)`)
  - `libs/sales/sales-ui` (Reactive Forms with `[formGroup]` and `formControlName`)
- **Key Requirement**: Every UI component must implement `ControlValueAccessor` (`NG_VALUE_ACCESSOR`) so it seamlessly binds to both `[(ngModel)]` and `formControlName`, handles `disabled` states, respects 360px responsive design, and uses no external UI libraries.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | `DateInputComponent` (`bb-date-input`) | Standalone CVA date input supporting ISO 8601 strings, min/max, placeholder, disabled/readonly, touch target, WebKit indicator | M1 | ORIGINAL_REQUEST §R1 |
| 2 | `CurrencyInputComponent` (`bb-currency-input`) | Standalone CVA currency input supporting symbols (₹), precision decimals, tabular numbers, right/left alignment, negative amounts, paise conversion | M1 | ORIGINAL_REQUEST §R1 |
| 3 | `NumberInputComponent` (`bb-number-input`) | Standalone CVA numeric input supporting step scaling, decimals (quantities, factors, rates), min/max, prefix/suffix (%, days, kg), integer/decimal modes | M1 | ORIGINAL_REQUEST §R1 |
| 4 | `SearchInputComponent` (`bb-search-input`) | Standalone CVA search input with search icon, clear button, debounce, keyboard enter triggers | M1 | Survey Finding |
| 5 | `TextInputComponent` (`bb-text-input`) | Standalone CVA text input with uppercase transformation (GSTIN, PAN, IFSC), maxlength, placeholder, disabled states | M1 | Survey Finding |
| 6 | Shared UI Barrel Exports & Unit Tests | Export all new primitive components from `libs/shared/ui-components/src/index.ts` and write full CVA Vitest unit tests | M1 | ORIGINAL_REQUEST §Acceptance |
| 7 | Refactor `accounting-ui` | Replace raw inputs in opening-balance, account-ledger, bank-accounts, banks, chart-of-accounts, closing-dates, journals, money-document, numbering-series, payment-terms, statements, sub-accounts, tax-master, transfer-money, trial-balance | M2 | ORIGINAL_REQUEST §R2 |
| 8 | Refactor `inventory-ui` | Replace raw inputs in item-categories, items (prices, weights, pharma, reorder), metal-purities, stock, stock-adjustments, unit-types, warehouses | M3 | ORIGINAL_REQUEST §R2 |
| 9 | Refactor `master-ui` | Replace raw inputs in configurations, contact-person-roles, contacts, hsn-sac, organizations, roles, smtp-settings, users | M4 | ORIGINAL_REQUEST §R2 |
| 10 | Refactor `purchase-ui` & `sales-ui` | Replace raw inputs in bill-form, debit-note-form, goods-receipt-form, purchase-order-form, credit-note-form, delivery-challan-form, invoice-form, quote-form, sales-order-form | M5 | ORIGINAL_REQUEST §R2 |
| 11 | Full Verification & Forensic Audit | Verify `npm run check` (lint, typecheck, test, build), run comprehensive review, challenge, and forensic integrity audit | M6 | ORIGINAL_REQUEST §Acceptance |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Shared Primitive UI Components | Build DateInput, CurrencyInput, NumberInput, SearchInput, TextInput in `libs/shared/ui-components`, export in `index.ts`, add unit tests | none | PLANNED |
| M2 | Accounting UI Refactoring | Refactor all pages in `libs/accounting/accounting-ui` to use new components, spot-check opening-balance.page.html | M1 | PLANNED |
| M3 | Inventory UI Refactoring | Refactor all pages in `libs/inventory/inventory-ui` to use new components | M1 | PLANNED |
| M4 | Master UI Refactoring | Refactor all pages in `libs/master/master-ui` to use new components | M1 | PLANNED |
| M5 | Purchase & Sales UI Refactoring | Refactor all forms in `libs/purchase/purchase-ui` and `libs/sales/sales-ui` | M1 | PLANNED |
| M6 | Full Verification & Audit | Execute `npm run check`, E2E/integration tests, spot checks, adversarial challenges, forensic integrity audit | M2, M3, M4, M5 | PLANNED |

## Interface Contracts

### 1. `DateInputComponent` (`<bb-date-input>`)
- **Selector**: `bb-date-input`
- **Imports**: `CommonModule`, `FormsModule`
- **CVA**: Implements `ControlValueAccessor` (`writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState`)
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

### 2. `CurrencyInputComponent` (`<bb-currency-input>`)
- **Selector**: `bb-currency-input`
- **Imports**: `CommonModule`, `FormsModule`
- **CVA**: Implements `ControlValueAccessor` (`number | null`)
- **Inputs**:
  - `id: input<string>('')`
  - `name: input<string>('')`
  - `symbol: input<string>('')` (optional symbol display, default none or `₹`)
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
- **Outputs**:
  - `valueChange: output<number | null>()`
  - `blur: output<FocusEvent>()`
  - `focus: output<FocusEvent>()`

### 3. `NumberInputComponent` (`<bb-number-input>`)
- **Selector**: `bb-number-input`
- **Imports**: `CommonModule`, `FormsModule`
- **CVA**: Implements `ControlValueAccessor` (`number | null`)
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
- **Outputs**:
  - `valueChange: output<number | null>()`
  - `blur: output<FocusEvent>()`
  - `focus: output<FocusEvent>()`

### 4. `SearchInputComponent` (`<bb-search-input>`)
- **Selector**: `bb-search-input`
- **Imports**: `CommonModule`, `FormsModule`
- **CVA**: Implements `ControlValueAccessor` (`string`)
- **Inputs**:
  - `id: input<string>('')`
  - `placeholder: input<string>('Search...')`
  - `ariaLabel: input<string>('Search')`
  - `disabled: input<boolean>(false)`
- **Outputs**:
  - `search: output<string>()`
  - `clear: output<void>()`
  - `valueChange: output<string>()`

### 5. `TextInputComponent` (`<bb-text-input>`)
- **Selector**: `bb-text-input`
- **Imports**: `CommonModule`, `FormsModule`
- **CVA**: Implements `ControlValueAccessor` (`string`)
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
- **Outputs**:
  - `valueChange: output<string>()`
  - `blur: output<FocusEvent>()`
  - `focus: output<FocusEvent>()`
  - `enter: output<string>()`

## Code Layout
- `libs/shared/ui-components/src/lib/date-input/` (Owned by M1)
- `libs/shared/ui-components/src/lib/currency-input/` (Owned by M1)
- `libs/shared/ui-components/src/lib/number-input/` (Owned by M1)
- `libs/shared/ui-components/src/lib/search-input/` (Owned by M1)
- `libs/shared/ui-components/src/lib/text-input/` (Owned by M1)
- `libs/shared/ui-components/src/index.ts` (Owned by M1)
- `libs/accounting/accounting-ui/` (Owned by M2)
- `libs/inventory/inventory-ui/` (Owned by M3)
- `libs/master/master-ui/` (Owned by M4)
- `libs/purchase/purchase-ui/` (Owned by M5)
- `libs/sales/sales-ui/` (Owned by M5)
