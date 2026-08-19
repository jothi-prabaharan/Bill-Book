## 2026-08-18T17:00:26Z
You are the Worker for Milestone 1 (Shared Primitive UI Components).
Your working directory is: C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1

MANDATORY READING (Read before starting):
- C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md
- C:\Users\Praba\Source\repos\Bill-Book\PROJECT.md
- C:\Users\Praba\Source\repos\Bill-Book\AGENTS.md
- C:\Users\Praba\Source\repos\Bill-Book\.agents\sub_orch_m1_components\SCOPE.md
- C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_m1_1\handoff.md
- C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_m1_2\handoff.md
- C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_m1_3\handoff.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Your exclusive write ownership:
- `frontend/libs/shared/ui-components/src/lib/date-input/` (date-input.component.ts, .html, .scss, .spec.ts)
- `frontend/libs/shared/ui-components/src/lib/currency-input/` (currency-input.component.ts, .html, .scss, .spec.ts)
- `frontend/libs/shared/ui-components/src/lib/number-input/` (number-input.component.ts, .html, .scss, .spec.ts)
- `frontend/libs/shared/ui-components/src/lib/search-input/` (search-input.component.ts, .html, .scss, .spec.ts)
- `frontend/libs/shared/ui-components/src/lib/text-input/` (text-input.component.ts, .html, .scss, .spec.ts)
- `frontend/libs/shared/ui-components/src/index.ts`
- Fix pre-existing compilation/typecheck errors in `frontend/libs/shared/ui-components/src/lib/report-grid/group-panel.component.ts` (import CdkDrag / DragDropModule from @angular/cdk/drag-drop) and `frontend/libs/shared/ui-components/src/lib/report-grid/column-chooser.dialog.ts` (import DragDropModule from @angular/cdk/drag-drop).

Detailed Scope & Tasks:
1. Implement the 5 standalone Angular 20 primitive input components per the exact specifications and CVA contracts in the Explorer handoffs:
   - `DateInputComponent` (`bb-date-input`)
   - `CurrencyInputComponent` (`bb-currency-input`)
   - `NumberInputComponent` (`bb-number-input`)
   - `SearchInputComponent` (`bb-search-input`)
   - `TextInputComponent` (`bb-text-input`)
   All components must:
   - Implement `ControlValueAccessor` providing `NG_VALUE_ACCESSOR` with `forwardRef`
   - Use `input()` and `output()` signals, separate `templateUrl` and `styleUrl`, `inject()` where needed
   - Seamlessly support template-driven `[(ngModel)]` and Reactive Forms `formControlName`/`[formControl]`
   - Respect disabled state (both template `[disabled]` and CVA `setDisabledState`), touch/blur events, readonly, required
   - Adhere to design tokens in `styles.scss` (`--color-text`, `--color-accent`, `--color-divider`, etc.) and be 360px mobile responsive
   - Support specific features: paise conversion (`inPaise`), currency symbol, number step/affixes (`prefix`/`suffix`), search clear button and Enter trigger, text uppercase transformation (`uppercase`).
2. Export all 5 components from `frontend/libs/shared/ui-components/src/index.ts`.
3. Create comprehensive `.spec.ts` unit tests for each of the 5 components using Vitest, testing CVA lifecycle, Reactive Forms, Template-driven forms, DOM events, and boundary edge cases.
4. Run tests and typechecking:
   - Run Vitest tests: `npx vitest run libs/shared/ui-components`
   - Run typecheck: `npm run typecheck`
   - Ensure all tests pass cleanly and with zero warnings/errors.
5. Write your complete handoff report to `C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m1\handoff.md` including exact commands run, test results, files changed, and evidence. Send a message to parent when done.
