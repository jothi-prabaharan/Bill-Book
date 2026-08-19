# E2E Test Infra: Bill-Book Desktop App Shell & Module Screens

## Test Philosophy
- Opaque-box, requirement-driven testing. Derived directly from `ORIGINAL_REQUEST.md`, design specifications, and API contracts.
- Independent decomposition across 5 tiers:
  - **Tier 1 - Feature Coverage**: >=5 tests per feature for happy path and core isolation.
  - **Tier 2 - Boundary & Corner Cases**: >=5 tests per feature covering extreme values, empty states, boundary inputs.
  - **Tier 3 - Cross-Feature Combinations**: Pairwise interactions (e.g. Org switch -> Nav active state -> Breadcrumb updates -> Table reload).
  - **Tier 4 - Real-World Application Scenarios**: Complete end-to-end workflows (e.g. Create Sales Invoice -> Navigate to List -> Filter & Sort -> Verify totals and tabular numeric rendering).
  - **Tier 5 - Adversarial Coverage Hardening**: Deep white-box stress testing, regression guards, and forensic integrity verification.

## Feature Inventory & Test Coverage Goals
| # | Feature | Requirement | Tier 1 | Tier 2 | Tier 3 |
|---|---------|-------------|:------:|:------:|:------:|
| 1 | SCSS Design Tokens (`shared/theming`) | R1 | 5 | 5 | ✓ |
| 2 | Tabular Numbers & Stroke-over-fill | R1 | 5 | 5 | ✓ |
| 3 | Themed Outline Focus & CSS States | R1 | 5 | 5 | ✓ |
| 4 | Fixed Left Rail with User Menu | R2 | 5 | 5 | ✓ |
| 5 | Top Bar (Org Switcher, FY Tag, Actions) | R2 | 5 | 5 | ✓ |
| 6 | Breadcrumb Strip & Action Host | R2 | 5 | 5 | ✓ |
| 7 | Shell Grid Layout & Layer Stacking | R2 | 5 | 5 | ✓ |
| 8 | Shared Data Table (Sticky Header & Shadow) | R3 | 5 | 5 | ✓ |
| 9 | Hairline Row Rules & Compact Density (>=32px) | R3 | 5 | 5 | ✓ |
| 10 | Data Table Sorting & Pagination | R3 | 5 | 5 | ✓ |
| 11 | Sales Module List Page | R4 | 5 | 5 | ✓ |
| 12 | Sales Module Create/Edit Reactive Forms | R4 | 5 | 5 | ✓ |
| 13 | Sales Module End-to-End Flow | R4 | 5 | 5 | ✓ |
| 14 | Purchases Module List & Forms | R4 | 5 | 5 | ✓ |
| 15 | Accounts Module Screens ("Accounts" Label) | R4, R5 | 5 | 5 | ✓ |
| 16 | Inventory Module Screens | R4 | 5 | 5 | ✓ |
| 17 | Architecture & Placement Rules | R5 | 5 | 5 | ✓ |

## Test Runner Architecture
- Framework: Vitest / Angular Component Testing harness in `frontend/`
- Execution: `npm run check` (Lint, Typecheck, Vitest unit/integration tests, Nx builds)
- Expected: All test suites pass cleanly with 0 warnings/errors and exit code 0.
