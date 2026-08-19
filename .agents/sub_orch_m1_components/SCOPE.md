# Scope: Milestone 1 — Shared Primitive UI Components

## Architecture
- **Framework**: Angular 20 (Standalone Components, Signals, `inject()`, ControlValueAccessor).
- **Location**: `libs/shared/ui-components/src/lib/`
- **Barrel**: `libs/shared/ui-components/src/index.ts`
- **Global Styles & Tokens**: `styles.scss` (CSS variables: `--color-accent`, `--color-divider`, `--color-text`, `--radius-md`, `--font-mono`, etc.) and `.input` / form controls.
- **Responsiveness**: Fully responsive down to 360px width.

## Feature Inventory
| # | Feature | Description | Status |
|---|---------|-------------|--------|
| 1 | `DateInputComponent` (`bb-date-input`) | Standalone CVA date input (`libs/shared/ui-components/src/lib/date-input/`) | PLANNED |
| 2 | `CurrencyInputComponent` (`bb-currency-input`) | Standalone CVA currency input (`libs/shared/ui-components/src/lib/currency-input/`) | PLANNED |
| 3 | `NumberInputComponent` (`bb-number-input`) | Standalone CVA number input (`libs/shared/ui-components/src/lib/number-input/`) | PLANNED |
| 4 | `SearchInputComponent` (`bb-search-input`) | Standalone CVA search input (`libs/shared/ui-components/src/lib/search-input/`) | PLANNED |
| 5 | `TextInputComponent` (`bb-text-input`) | Standalone CVA text input (`libs/shared/ui-components/src/lib/text-input/`) | PLANNED |
| 6 | Barrel Export & Fixes | Export all components in `src/index.ts` and fix any compilation/lint issues in `ui-components` | PLANNED |
| 7 | Unit Test Suite | Comprehensive `.spec.ts` unit tests covering CVA, inputs, outputs, formatting, edge cases | PLANNED |

## Interface Contracts

### 1. `DateInputComponent` (`bb-date-input`)
- Path: `libs/shared/ui-components/src/lib/date-input/date-input.component.ts` (with template and style)
- CVA: `NG_VALUE_ACCESSOR` (`string | null` - ISO format `YYYY-MM-DD`)
- Inputs: `id`, `name`, `placeholder`, `min`, `max`, `disabled`, `readonly`, `required`, `ariaLabel`
- Outputs: `valueChange`, `blur`, `focus`

### 2. `CurrencyInputComponent` (`bb-currency-input`)
- Path: `libs/shared/ui-components/src/lib/currency-input/currency-input.component.ts`
- CVA: `NG_VALUE_ACCESSOR` (`number | null`)
- Inputs: `id`, `name`, `symbol`, `currencyCode` ('INR'), `showSymbol` (false), `decimals` (2), `min`, `max`, `step` (0.01), `placeholder` ('0.00'), `disabled`, `readonly`, `required`, `allowNegative` (false), `inPaise` (false), `align` ('right')
- Outputs: `valueChange`, `blur`, `focus`

### 3. `NumberInputComponent` (`bb-number-input`)
- Path: `libs/shared/ui-components/src/lib/number-input/number-input.component.ts`
- CVA: `NG_VALUE_ACCESSOR` (`number | null`)
- Inputs: `id`, `name`, `min`, `max`, `step` (1), `decimals`, `placeholder`, `prefix`, `suffix`, `disabled`, `readonly`, `required`, `align` ('left'), `inputmode` ('decimal')
- Outputs: `valueChange`, `blur`, `focus`

### 4. `SearchInputComponent` (`bb-search-input`)
- Path: `libs/shared/ui-components/src/lib/search-input/search-input.component.ts`
- CVA: `NG_VALUE_ACCESSOR` (`string`)
- Inputs: `id`, `placeholder` ('Search...'), `ariaLabel` ('Search'), `disabled`
- Outputs: `search`, `clear`, `valueChange`

### 5. `TextInputComponent` (`bb-text-input`)
- Path: `libs/shared/ui-components/src/lib/text-input/text-input.component.ts`
- CVA: `NG_VALUE_ACCESSOR` (`string`)
- Inputs: `id`, `name`, `type` ('text' | 'email' | 'password' | 'tel' | 'url'), `placeholder`, `maxlength`, `uppercase` (false), `disabled`, `readonly`, `required`, `autocomplete` ('off')
- Outputs: `valueChange`, `blur`, `focus`, `enter`

## Code Layout & Ownership
- `libs/shared/ui-components/src/lib/date-input/`
- `libs/shared/ui-components/src/lib/currency-input/`
- `libs/shared/ui-components/src/lib/number-input/`
- `libs/shared/ui-components/src/lib/search-input/`
- `libs/shared/ui-components/src/lib/text-input/`
- `libs/shared/ui-components/src/index.ts`
