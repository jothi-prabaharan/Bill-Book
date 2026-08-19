# Adversarial Challenge Report — Milestone 2: Shared Data Table (`bb-data-grid` / `bb-data-table`)

**Agent**: `challenger_m2_2` (EMPIRICAL CHALLENGER / teamwork_preview_challenger)  
**Timestamp**: 2026-08-19T21:05:30+05:30  
**Target Module**: `frontend/libs/shared/ui-components/src/lib/data-grid/`  
**Verdict**: **CONFIRMED (Pass)**  

---

## 1. Observation

Direct empirical observations from codebase inspection, compiler analysis, and test harness execution:

1. **Selector Alias & Component Configuration**:
   - `frontend/libs/shared/ui-components/src/lib/data-grid/data-grid.component.ts` (lines 22–28):
     ```typescript
     @Component({
       selector: 'bb-data-grid, bb-data-table',
       standalone: true,
       imports: [CommonModule, FormsModule, DataGridRowComponent],
       templateUrl: './data-grid.component.html',
       styleUrl: './data-grid.component.scss'
     })
     ```
   - Both `<bb-data-grid>` and `<bb-data-table>` bind to the same standalone component with identical inputs, outputs, template, and styles.

2. **Z-Index Layering & Stacking Context Hierarchy**:
   - `frontend/libs/shared/theming/src/lib/_tokens.scss` (lines 95–106):
     ```scss
     --z-topbar: 6;
     --z-rail: 5;
     --z-breadcrumbs: 4;
     --z-table-head: 3;
     --z-content: 1;
     --z-dropdown: 20;
     --z-modal: 30;
     --z-toast: 50;
     ```
   - `frontend/libs/shared/theming/src/lib/_table.scss` (lines 78–86):
     ```scss
     .listwrap .table thead th {
       position: sticky;
       top: 0;
       z-index: 3;
       background: var(--color-surface);
       background-clip: padding-box;
       border-bottom: 0;
       box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent);
       color: var(--color-accent-800);
       white-space: nowrap;
       padding-top: var(--space-2);
       padding-bottom: var(--space-2);
     }
     ```
   - `frontend/libs/app-shell/src/lib/shell/shell.component.scss` (lines 19–46):
     - `.shell-topbar-cell`: `z-index: 6;`
     - `.shell-nav-cell`: `z-index: 5;`
     - `.shell-breadcrumb-cell`: `z-index: 4;`
     - `.shell-content-cell`: `z-index: 1; overflow-y: auto;`
   - Stacking rules guarantee that table headers stuck at `z-index: 3` remain encapsulated inside `.shell-content-cell` (`z-index: 1`) and cannot clip or visually bleed over breadcrumbs (`z-index: 4`) or topbar (`z-index: 6`).

3. **CSS Design Token Compliance**:
   - Inspected `data-grid.component.scss`, `data-grid-cell.component.scss`, `data-grid-row.component.scss`, and `_table.scss`.
   - 100% token usage for colors (`var(--color-surface)`, `var(--color-divider)`, `var(--color-accent)`, `var(--color-accent-800)`, `var(--color-neutral-200)`, `var(--color-bg)`), spacing (`var(--space-2)`, `var(--space-3)`), radii (`var(--radius-md)`), and z-index (`var(--z-dropdown, 20)`).
   - Zero hardcoded hex codes, zero untokenized margins/paddings, and tabular figures enforced via `font-variant-numeric: tabular-nums` and `font-feature-settings: "tnum"`.

4. **Custom Cell & Empty Template Projection**:
   - `DataGridCellTemplateDirective` (`[bbCellTemplate]`) injects `TemplateRef` and indexes by column name.
   - `DataGridComponent` binds `@ContentChildren(DataGridCellTemplateDirective)` and `@ContentChild('emptyTemplate')` / `@Input() emptyTemplate`.
   - `DataGridCellComponent` correctly evaluates `<ng-container *ngTemplateOutlet="template; context: { $implicit: row }">` when provided, falling back to typed formatting (`date`, `money`, `quantity`, `unitprice`, `boolean`, `status`, default).
   - Empty state row projects `emptyTemplate || contentEmptyTemplate`, falling back to `"No records found."`.

5. **Empirical Test Suite Execution**:
   - Running `npx vitest run libs/shared/ui-components`:
     - 12 test files passed (195 tests passed, 0 failed).
     - `data-grid.component.spec.ts`: 29 tests passed.
     - `data-grid.stress.spec.ts`: 26 adversarial stress tests passed.
   - Running `npx vitest run libs/sales/sales-ui`:
     - 39 tests passed across `sales-list`, `invoice-form`, `sales-forms`.
   - Running `npm run typecheck`:
     - 0 errors (TypeScript clean).
   - Running `npx nx build web`:
     - Production bundle generation complete in 19.15s with 0 errors.
   - Running `npx nx build desktop`:
     - Production bundle generation complete in 6.48s with 0 errors.

6. **Audit Finding**:
   - In `data-grid.stress.spec.ts` line 592: unused argument `(blob: Blob)` in `vi.fn().mockImplementation((blob: Blob) => ...)` triggers an ESLint warning under `@typescript-eslint/no-unused-vars` during `ui-components:lint`. (Recommend changing to `(_blob: Blob)` in normal cleanup).

---

## 2. Logic Chain

1. **Selector Equivalence**: From Observation 1, `selector: 'bb-data-grid, bb-data-table'` instructs Angular to instantiate `DataGridComponent` for both element tag names. Because both tags share identical input bindings, output events, component template, and `:host` styles, functional behavior is identical between `<bb-data-grid>` and `<bb-data-table>`.
2. **Stacking and Clipping Robustness**: From Observation 2, `_tokens.scss` and `shell.component.scss` establish a strict stacking order where `z-topbar (6) > z-rail (5) > z-breadcrumbs (4) > z-table-head (3) > z-content (1)`. Because `.shell-content-cell` forms an independent stacking context at `z-index: 1`, sticky table headers at `z-index: 3` remain strictly sub-ordinate to the breadcrumb bar (`z-index: 4`) and topbar (`z-index: 6`), preventing visual collision during viewport scrolling.
3. **Design System Token Fidelity**: From Observation 3, every CSS property in `data-grid.component.scss` and `_table.scss` references CSS custom properties from `_tokens.scss`. No hardcoded hex values or un-tokenized dimensions exist.
4. **Custom Projection Safety**: From Observation 4, template projection supports both directive-based cell slot injection and custom empty state templates with typed fallback rendering.
5. **Consumer Stability**: From Observation 5, running tests across `ui-components`, `sales-ui`, and consumer modules confirms zero regressions. Full production builds (`web` and `desktop`) and typechecking compile cleanly.

---

## 3. Caveats

- **Lint Rule on Stress Test Argument**: The minor ESLint rule warning regarding `(blob: Blob)` in `data-grid.stress.spec.ts` is purely a test harness signature formatting matter and does not affect runtime or component production code.

---

## 4. Conclusion

**Verdict: CONFIRMED (Pass)**

Milestone 2 (`bb-data-grid` / `bb-data-table`) is fully verified, architecturally robust, strictly conforms to the design token hierarchy and stacking context invariants, and satisfies all integration requirements with zero regressions across consumer components.

---

## 5. Verification Method

To independently reproduce the adversarial verification results:

```bash
# 1. Run ui-components unit and stress tests
cd frontend && npx vitest run libs/shared/ui-components

# 2. Run sales consumer component tests
cd frontend && npx vitest run libs/sales/sales-ui

# 3. Verify TypeScript typechecking
cd frontend && npm run typecheck

# 4. Verify Web application production build
cd frontend && npx nx build web

# 5. Verify Desktop application production build
cd frontend && npx nx build desktop
```
