# Original User Request

## Initial Request — 2026-08-18T16:50:00Z

# Teamwork Project Prompt — Draft

> Status: Launched
> Goal: Craft prompt → get user approval → delegate to teamwork_preview
> Requested team: Full multi-agent team

Scan the entire frontend project to identify all recurring primitive data input types (e.g., date pickers, currency formatting, number inputs). Create a global, reusable Angular UI component for each data type in `@bill-book/ui-components`, and refactor the entire frontend project to use these new global components instead of raw HTML inputs or duplicated styling.

Working directory: C:\Users\Praba\Source\repos\Bill-Book\frontend
Integrity mode: development

## Requirements

### R1. Component Creation
Identify common UI input patterns (dates, currencies, numbers) currently using raw HTML elements (e.g., `<input type="date">`, `<input type="number">`). Create centralized standalone Angular components for them in `libs/shared/ui-components`.

### R2. Global Refactoring
Replace the raw HTML inputs across all frontend pages (`accounting-ui`, `inventory-ui`, `master-ui`, `purchase-ui`, `sales-ui`) with the newly created global UI components. Preserve all existing `ngModel` bindings, disabled states, and validation logic.

### R3. Strict Build Compliance
The refactored project must build cleanly with no warnings or errors, as `TreatWarningsAsErrors` is active.

## Acceptance Criteria

### Verification Suite
- [ ] `npm run check` (which runs lint, typecheck, tests, and production build) passes cleanly without errors.
- [ ] At least three new global input components (e.g., Date, Currency, Number) are exported from `libs/shared/ui-components/src/index.ts`.
- [ ] Spot check: `C:\Users\Praba\Source\repos\Bill-Book\frontend\libs\accounting\accounting-ui\src\lib\opening-balance\opening-balance.page.html` (or similar data-heavy pages) successfully uses the new components instead of raw `<input>` tags.
