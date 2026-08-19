# Orchestration Plan: Frontend Primitive UI Components & Refactoring

## Objectives
1. Survey the frontend project to identify all recurring primitive data input types (date pickers, currency, numbers, text, search, etc.) and their patterns.
2. Build standalone Angular UI components under `libs/shared/ui-components` and export them in `libs/shared/ui-components/src/index.ts`.
3. Refactor all consumer modules:
   - `accounting-ui`
   - `inventory-ui`
   - `master-ui`
   - `purchase-ui`
   - `sales-ui`
   preserving bindings, disabled states, validations, and styling.
4. Verify with `npm run check` (lint, typecheck, tests, build) + reviewer + challenger + forensic audit.

## Phases
- **Phase 0: Survey** (3 parallel Explorers to catalog input types, locations, existing shared components, and patterns)
- **Phase 1: Architecture & Decomposition** (Synthesize survey, author `PROJECT.md`, define interface contracts and component specs)
- **Phase 2: Milestone Execution** (Spawn workers/subagents per milestone with Explorer -> Worker -> Reviewer -> Challenger -> Auditor gates)
- **Phase 3: Final Verification & Audit** (`npm run check`, end-to-end verification, reporting to Sentinel)
