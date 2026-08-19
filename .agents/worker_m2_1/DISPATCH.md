## 2026-08-19T15:16:00Z
You are worker_m2_1 (teamwork_preview_worker).
Your working directory is C:\Users\Praba\Source\repos\Bill-Book\.agents\worker_m2_1.

## MANDATORY: Read ORIGINAL_REQUEST.md first
Read C:\Users\Praba\Source\repos\Bill-Book\.agents\ORIGINAL_REQUEST.md before starting work.

## Mission
Implement Milestone 2: Shared Data Table (`bb-data-grid` / `bb-data-table`) in `frontend/libs/shared/ui-components/src/lib/data-grid/` according to Requirement R3, `PROJECT.md`, and the blueprints in `C:\Users\Praba\Source\repos\Bill-Book\.agents\explorer_m2_1\analysis.md`.

## Exclusive Write Ownership
You own ONLY the files in:
`frontend/libs/shared/ui-components/src/lib/data-grid/*`
(e.g., `data-grid.models.ts`, `data-grid.component.ts`, `data-grid.component.html`, `data-grid.component.scss`, `data-grid.component.spec.ts`, and subcomponents/directives if needed).

## Key Requirements to Implement
1. **Selector Alias**: `selector: 'bb-data-grid, bb-data-table'`.
2. **ColumnDef Contract**: Add `numeric?: boolean;` and `sortable?: boolean;` to `ColumnDef`.
3. **Inputs**:
   - `columns: ColumnDef[]`
   - `data: readonly any[]`
   - `loading: boolean = false`
   - `totalCount: number = 0`
   - `pageSize: number = 50`
   - `currentPage: number = 1`
   - `compact: boolean = true`
   - `emptyTemplate?: TemplateRef<any>`
   - `sortable: boolean = true`
   - `showExport: boolean = true`
4. **Outputs**:
   - `rowClick: EventEmitter<any>`
   - `sortChange: EventEmitter<SortState>` (`{ field: string; direction: 'asc' | 'desc' }`)
   - `pageChange: EventEmitter<number>`
5. **Sticky Header & Z-Index Layering**:
   - Wrap table in `.listwrap` container class.
   - Header sticky with `top: 0; z-index: 3; background: var(--color-surface); box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent);`.
   - Pure CSS sort indicators and pure CSS 120ms transitions.
6. **Numeric Formatting**:
   - Right-align numeric columns (`isNumericCol(col)`: `numeric === true`, `align === 'right'`, or `dataType` in `['number', 'money', 'quantity', 'unitprice']`).
   - Tabular numerals (`font-variant-numeric: tabular-nums`).
7. **Pagination Strip**:
   - Show pagination strip with records summary and Previous / Next buttons when data exceeds `pageSize` or `totalPages > 1`.
8. **Loading State & Empty State**:
   - Show top loading bar when `loading === true`.
   - Support `emptyTemplate` projection or default "No records found."
9. **Backward Compatibility**:
   - Retain full compatibility for existing consumers (`account-ledger`, `bank-accounts`, `banks`, `chart-of-accounts`, `closing-dates`, `journals`, `money-document`, `sales-list`).
