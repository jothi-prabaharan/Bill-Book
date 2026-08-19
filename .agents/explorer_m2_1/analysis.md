# Analysis & Blueprint Report — Milestone 2: Shared Data Table (`libs/shared/ui-components`)

**Timestamp**: 2026-08-19T20:45:00+05:30  
**Author**: Explorer Milestone 2 (`explorer_m2_1`)  
**Target Library**: `frontend/libs/shared/ui-components/src/lib/data-grid/` & `frontend/libs/shared/theming/src/lib/_table.scss`  
**Status**: Investigation Complete — Ready for Implementation

---

## 1. Executive Summary

A comprehensive forensic investigation of the existing shared data table implementation (`bb-data-grid` / `bb-data-table`) and theme partials (`_table.scss`) was conducted against the Design Reference (`Shell.dc.html`), design tokens (`styles.css`), and the authoritative Requirement **R3** (Shared Data Table).

The foundational data grid exists in `libs/shared/ui-components/src/lib/data-grid/` with column visibility, multi-operator filtering, state persistence (`DataGridService`), and CSV export capabilities. However, several critical gaps and architectural discrepancies against Requirement R3 and `PROJECT.md` were discovered and cataloged:

1. **Sticky Header Structure & Z-Index Layering**: The existing template uses `sticky top-0 z-50` and an undefined token `var(--color-background-card)`, violating the strict architectural z-index stacking hierarchy (`--z-table-head: 3`, under topbar `z: 6`, rail `z: 5`, breadcrumbs `z: 4`). The scrollable wrapper also omitted the canonical `.listwrap` container class needed for the `box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent)` header rule.
2. **Missing Component Inputs**: Missing `@Input() loading: boolean = false`, `@Input() totalCount: number = 0`, `@Input() pageSize: number = 50`, `@Input() currentPage: number = 1`, `@Input() compact: boolean = true`, `@Input() emptyTemplate?: TemplateRef<any>`.
3. **Missing Component Outputs & Sorting Mechanism**: Missing `@Output() sortChange`, `@Output() pageChange`, sort direction state, and interactive header sorting UI buttons with indicators.
4. **Selector Alias Support**: Selector is currently only `bb-data-grid`; must support `bb-data-grid, bb-data-table` alias.
5. **ColumnDef Contract**: Missing `numeric?: boolean` and `sortable?: boolean` in `ColumnDef` model.
6. **Density & Layout**: Hardcoded 45px virtual scroll item size and 500px container height prevent responsive flexbox adaptation and compact 32px ERP density.

---

## 2. Requirement R3 Compliance Matrix

| Requirement Item | Specification | Current Status | Discrepancy / Action Needed |
|---|---|---|---|
| **Sticky Header** | `top: 0`, `z-index: 3`, solid surface ground (`var(--color-surface)`), inset bottom shadow (`box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent)`) | ⚠️ Partial | Template used `z-50` and undefined `var(--color-background-card)`. Need to add `.listwrap` container and align with `_table.scss` rules. |
| **Hairline Row Rules** | `border-bottom: 1px solid var(--color-divider)` | ✅ Compliant | Defined in `_table.scss` (`.table td`, `.table th`) and rendered by row cells. |
| **Compact Density** | $\ge$ 32px interactive target, 5px vertical padding, 12.5px font | ⚠️ Partial | Present in `_table.scss`, but missing `@Input() compact = true` on component and hardcoded 45px in virtual scroll. |
| **Inputs** | `columns: ColumnDef[]`, `data: any[]`, `loading: boolean`, `totalCount: number`, `pageSize: number`, `currentPage: number`, `compact: boolean`, `emptyTemplate?: TemplateRef<any>` | ⚠️ Incomplete | `columns` and `data` exist. `loading`, `totalCount`, `pageSize`, `currentPage`, `compact`, `emptyTemplate` are missing. |
| **Outputs** | `sortChange: EventEmitter<{ field: string; direction: 'asc' \| 'desc' }>`, `pageChange: EventEmitter<number>`, `rowClick: EventEmitter<any>` | ⚠️ Incomplete | `rowClick` exists. `sortChange` and `pageChange` are missing. |
| **Tabular Numbers & Right Alignment** | `font-variant-numeric: tabular-nums` and `text-align: right` for numeric/currency/date | ⚠️ Partial | `dataType` alignment exists, but `ColumnDef.numeric` and `ColumnDef.align === 'right'` checks need full unification. |
| **Pure CSS Interactions** | 120ms hover transition, sort arrow indicator styling, no JS animations | ✅ / ⚠️ Minor | Hover transition in `_table.scss` is clean CSS. Need pure CSS sort indicator icons and active states. |
| **Empty State** | Custom template projection via `emptyTemplate` or fallback | ⚠️ Partial | Only static text `<td ...>No records found.</td>` exists. Needs template outlet support. |
| **Selector Alias** | `bb-data-grid` and `bb-data-table` | ⚠️ Incomplete | Currently `selector: 'bb-data-grid'`. Must be `selector: 'bb-data-grid, bb-data-table'`. |

---

## 3. Detailed File Inspection & Forensic Findings

### 3.1. `libs/shared/ui-components/src/lib/data-grid/data-grid.models.ts`
- **Current Content**:
  ```typescript
  export interface ColumnDef {
    field: string;
    header: string;
    align?: string;
    type?: string;
    isTemplate?: boolean;
    title?: string;
    classes?: string;
    dataType?: 'string' | 'number' | 'date' | 'datetime' | 'boolean' | 'money' | 'quantity' | 'unitprice' | 'status';
    width?: string;
    visible?: boolean;
  }
  ```
- **Issues Identified**:
  - Missing `numeric?: boolean;` which is explicitly part of the contract in `PROJECT.md` §60 (`{ field: string; header: string; width?: string; align?: 'left'|'right'; numeric?: boolean; sortable?: boolean }`).
  - Missing `sortable?: boolean;` to allow disabling sorting on specific columns (e.g. action buttons or status tags).
  - Missing `SortState` interface definition (`{ field: string; direction: 'asc' | 'desc' }`).

### 3.2. `libs/shared/ui-components/src/lib/data-grid/data-grid.component.ts`
- **Current Content**:
  - Inputs: only `gridCode`, `columns`, `data`.
  - Outputs: only `rowClick`.
  - State: `visibleColumns`, `activeFilters`, `openFilterField`, `filterOp`, `filterVal`.
  - Filtering: `toggleFilter`, `applyFilter`, `clearFilter`, `filteredData` computed signal.
  - CSV Export: `exportToCsv()`.
- **Issues Identified**:
  - Missing `loading`, `totalCount`, `pageSize`, `currentPage`, `compact`, `emptyTemplate` `@Input()` properties.
  - Missing `sortChange`, `pageChange` `@Output()` emitters.
  - Missing sort state signals (`sortField`, `sortDirection`).
  - Missing sorting logic: When headers are clicked, client-side sorting should be computed if parent is not handling server-side sorting, and `sortChange` should always be emitted.
  - Missing pagination logic: Pagination controls (Previous/Next, current range calculation) and `pageChange` emission.
  - Missing helper for determining numeric column alignment (`isNumericCol(col)`).

### 3.3. `libs/shared/ui-components/src/lib/data-grid/data-grid.component.html`
- **Current Content**:
  - Outer card wrapper with inline styles.
  - `<cdk-virtual-scroll-viewport itemSize="45" class="w-full" style="height: 500px">`
  - `<thead class="sticky top-0 z-50" style="background: var(--color-background-card)">`
  - `<th>` with filter button only, no sorting button.
  - Static empty state: `<td ...>No records found.</td>`.
- **Issues Identified**:
  - `var(--color-background-card)` is NOT a valid design token (token in `_tokens.scss` is `--color-surface`).
  - `z-50` violates the z-index stacking hierarchy (must be `z-index: 3` / `--z-table-head`).
  - Missing `.listwrap` container around table.
  - Missing sorting button and sort direction icon in header cells.
  - Hardcoded 500px virtual scroll viewport height locks table size and creates double scrollbars on responsive screens.
  - Missing loading indicator / skeleton bar when `loading === true`.
  - Missing custom empty template projection.
  - Missing footer pagination strip with Previous/Next buttons and record counter.

### 3.4. `libs/shared/theming/src/lib/_table.scss`
- **Current Content**:
  - Contains `.table`, `.table th`, `.table td`, `.table tbody tr:hover`, `.table th.numeric, .table td.numeric`, `.listwrap .table thead th`, `.listwrap .table thead tr.fltrow th`.
- **Observations**:
  - `.listwrap .table thead th` correctly has `position: sticky; top: 0; z-index: 3; background: var(--color-surface); box-shadow: inset 0 -1px 0 color-mix(in srgb, var(--color-accent) 55%, transparent); color: var(--color-accent-800);`.
  - `.table tbody tr` has `transition: background 120ms ease;` and hover background `color-mix(in srgb, var(--color-accent) 5%, transparent)`.
  - Need minor enhancements in `_table.scss` or `data-grid.component.scss` for `.table-sort-btn`, `.sort-indicator`, `.table-pagination`, and `.loading-bar`.

---

## 4. Implementation Blueprints for Worker

### Blueprint 4.1: `data-grid.models.ts` Refinement
```typescript
export interface ColumnDef {
  field: string;
  header: string;
  align?: 'left' | 'right' | 'center' | string;
  type?: string;
  isTemplate?: boolean;
  title?: string;
  classes?: string;
  dataType?: 'string' | 'number' | 'date' | 'datetime' | 'boolean' | 'money' | 'quantity' | 'unitprice' | 'status';
  width?: string;
  visible?: boolean;
  numeric?: boolean;
  sortable?: boolean;
}

export type SortDirection = 'asc' | 'desc';

export interface SortState {
  field: string;
  direction: SortDirection;
}

export interface FilterState {
  field: string;
  operator: 'equals' | 'contains' | 'starts';
  value: string;
}

export interface GridState {
  gridCode: string;
  columns: { field: string; visible: boolean; width?: string }[];
  filters: FilterState[];
  pageSize: number;
  sort?: SortState;
}
```

### Blueprint 4.2: `data-grid.component.ts` Enhancement
```typescript
import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnInit,
  signal,
  computed,
  inject,
  ContentChildren,
  ContentChild,
  QueryList,
  TemplateRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ColumnDef, FilterState, SortDirection, SortState } from './data-grid.models';
import { DataGridService } from './data-grid.service';
import { DataGridRowComponent } from './data-grid-row/data-grid-row.component';
import { DataGridCellTemplateDirective } from './data-grid-cell-template.directive';

@Component({
  selector: 'bb-data-grid, bb-data-table',
  standalone: true,
  imports: [CommonModule, FormsModule, DataGridRowComponent],
  templateUrl: './data-grid.component.html',
  styleUrl: './data-grid.component.scss'
})
export class DataGridComponent implements OnInit {
  @ContentChildren(DataGridCellTemplateDirective) cellTemplates!: QueryList<DataGridCellTemplateDirective>;
  @ContentChild('emptyTemplate') contentEmptyTemplate?: TemplateRef<any>;

  get templateMap(): Record<string, TemplateRef<any>> {
    const map: Record<string, TemplateRef<any>> = {};
    if (this.cellTemplates) {
      this.cellTemplates.forEach(dir => {
        map[dir.fieldName] = dir.template;
      });
    }
    return map;
  }

  private stateService = inject(DataGridService);

  // Inputs
  @Input() gridCode = '';
  @Input() columns: ColumnDef[] = [];
  @Input() data: readonly any[] = [];
  @Input() loading = false;
  @Input() totalCount = 0;
  @Input() pageSize = 50;
  @Input() currentPage = 1;
  @Input() compact = true;
  @Input() emptyTemplate?: TemplateRef<any>;
  @Input() sortable = true;
  @Input() showExport = true;

  // Outputs
  @Output() rowClick = new EventEmitter<any>();
  @Output() sortChange = new EventEmitter<SortState>();
  @Output() pageChange = new EventEmitter<number>();

  // State Signals
  visibleColumns = signal<ColumnDef[]>([]);
  activeFilters = signal<FilterState[]>([]);
  openFilterField = signal<string | null>(null);

  // Filter popup temp state
  filterOp = signal<'equals' | 'contains' | 'starts'>('contains');
  filterVal = signal<string>('');

  // Sorting state
  sortField = signal<string | null>(null);
  sortDirection = signal<SortDirection | null>(null);

  ngOnInit() {
    this.loadState();
  }

  private loadState() {
    const savedState = this.stateService.loadState(this.gridCode);
    
    let activeCols = this.columns.map(c => ({
      ...c,
      visible: c.visible !== false
    }));

    if (savedState) {
      const savedColMap = new Map(savedState.columns.map(c => [c.field, c]));
      activeCols = activeCols.map(c => {
        const sc = savedColMap.get(c.field);
        if (sc) return { ...c, visible: sc.visible, width: sc.width };
        return c;
      });
      
      this.activeFilters.set(savedState.filters || []);
      if (savedState.sort) {
        this.sortField.set(savedState.sort.field);
        this.sortDirection.set(savedState.sort.direction);
      }
    }

    this.visibleColumns.set(activeCols.filter(c => c.visible));
  }

  private saveState() {
    if (!this.gridCode) return;
    this.stateService.saveState({
      gridCode: this.gridCode,
      columns: this.columns.map(c => {
        const vis = this.visibleColumns().find(vc => vc.field === c.field);
        return { field: c.field, visible: !!vis };
      }),
      filters: this.activeFilters(),
      pageSize: this.pageSize,
      sort: this.sortField() && this.sortDirection()
        ? { field: this.sortField()!, direction: this.sortDirection()! }
        : undefined
    });
  }

  // Sorting
  onSort(col: ColumnDef) {
    if (!this.sortable || col.sortable === false) return;

    const currentField = this.sortField();
    const currentDir = this.sortDirection();

    if (currentField !== col.field) {
      this.sortField.set(col.field);
      this.sortDirection.set('asc');
      this.sortChange.emit({ field: col.field, direction: 'asc' });
    } else if (currentDir === 'asc') {
      this.sortDirection.set('desc');
      this.sortChange.emit({ field: col.field, direction: 'desc' });
    } else {
      this.sortField.set(null);
      this.sortDirection.set(null);
    }
    this.saveState();
  }

  // Filtering
  toggleFilter(field: string, event: Event) {
    event.stopPropagation();
    if (this.openFilterField() === field) {
      this.openFilterField.set(null);
    } else {
      const existing = this.activeFilters().find(f => f.field === field);
      this.filterOp.set(existing?.operator || 'contains');
      this.filterVal.set(existing?.value || '');
      this.openFilterField.set(field);
    }
  }

  applyFilter(field: string, event: Event) {
    event.stopPropagation();
    const val = this.filterVal().trim();
    const currentFilters = this.activeFilters().filter(f => f.field !== field);
    
    if (val) {
      currentFilters.push({
        field,
        operator: this.filterOp(),
        value: val
      });
    }
    
    this.activeFilters.set(currentFilters);
    this.openFilterField.set(null);
    this.saveState();
  }

  clearFilter(field: string, event: Event) {
    event.stopPropagation();
    this.activeFilters.set(this.activeFilters().filter(f => f.field !== field));
    this.openFilterField.set(null);
    this.saveState();
  }

  // Filtering & In-Memory Sorting
  filteredData = computed(() => {
    let result = [...this.data];
    const filters = this.activeFilters();
    
    if (filters.length > 0) {
      result = result.filter(row => {
        return filters.every(f => {
          const cellVal = String(row[f.field] ?? '').toLowerCase();
          const searchVal = f.value.toLowerCase();
          
          if (f.operator === 'equals') return cellVal === searchVal;
          if (f.operator === 'starts') return cellVal.startsWith(searchVal);
          return cellVal.includes(searchVal);
        });
      });
    }

    const field = this.sortField();
    const dir = this.sortDirection();
    if (field && dir) {
      result.sort((a, b) => {
        const valA = a[field];
        const valB = b[field];
        if (valA === valB) return 0;
        if (valA === null || valA === undefined) return 1;
        if (valB === null || valB === undefined) return -1;
        
        let cmp = 0;
        if (typeof valA === 'number' && typeof valB === 'number') {
          cmp = valA - valB;
        } else {
          cmp = String(valA).localeCompare(String(valB), undefined, { numeric: true });
        }
        return dir === 'asc' ? cmp : -cmp;
      });
    }

    return result;
  });

  // Display data (handles client-side pagination if server-side pagination totalCount is not used)
  displayData = computed(() => {
    const list = this.filteredData();
    if (this.totalCount > 0) {
      // Server-side pagination: parent already passed page slice
      return list;
    }
    // Client-side slice if list exceeds pageSize
    if (this.pageSize > 0 && list.length > this.pageSize) {
      const start = (this.currentPage - 1) * this.pageSize;
      return list.slice(start, start + this.pageSize);
    }
    return list;
  });

  totalPages = computed(() => {
    const total = this.totalCount > 0 ? this.totalCount : this.filteredData().length;
    return this.pageSize > 0 ? Math.max(1, Math.ceil(total / this.pageSize)) : 1;
  });

  paginationSummary = computed(() => {
    const total = this.totalCount > 0 ? this.totalCount : this.filteredData().length;
    if (total === 0) return '0 records';
    const start = (this.currentPage - 1) * this.pageSize + 1;
    const end = Math.min(this.currentPage * this.pageSize, total);
    return `${start}–${end} of ${total} records`;
  });

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.pageChange.emit(this.currentPage);
    }
  }

  nextPage() {
    if (this.currentPage < this.totalPages()) {
      this.currentPage++;
      this.pageChange.emit(this.currentPage);
    }
  }

  isNumericCol(col: ColumnDef): boolean {
    return (
      col.numeric === true ||
      col.align === 'right' ||
      ['number', 'money', 'quantity', 'unitprice'].includes(col.dataType || '')
    );
  }

  onRowClick(row: any) {
    this.rowClick.emit(row);
  }

  exportToCsv() {
    const data = this.filteredData();
    const cols = this.visibleColumns();
    if (data.length === 0) return;

    const header = cols.map(c => `"${c.header || c.title || c.field}"`).join(',');
    const rows = data.map(r => {
      return cols.map(c => `"${r[c.field] !== undefined && r[c.field] !== null ? r[c.field] : ''}"`).join(',');
    });

    const csvContent = [header, ...rows].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `${this.gridCode || 'export'}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }
}
```

### Blueprint 4.3: `data-grid.component.html` Template
```html
<div *ngIf="showExport" class="flex justify-end" style="margin-bottom:8px">
  <button class="btn btn-secondary" (click)="exportToCsv()">Export to CSV</button>
</div>

<div class="card p-0 data-grid-card" [class.compact]="compact">
  <!-- Progress bar for loading state -->
  <div *ngIf="loading" class="data-grid-loading-bar" aria-hidden="true">
    <div class="loading-pulse"></div>
  </div>

  <div class="listwrap" [class.compact]="compact">
    <table class="table" [class.compact]="compact">
      <thead>
        <tr>
          <th *ngFor="let col of visibleColumns()"
              [class.numeric]="isNumericCol(col)"
              [style.width]="col.width"
              class="relative">
            <div class="flex items-center justify-between gap-1">
              <button type="button"
                      class="table-sort-btn"
                      [disabled]="!sortable || col.sortable === false"
                      (click)="onSort(col)"
                      [attr.aria-sort]="sortField() === col.field ? (sortDirection() === 'asc' ? 'ascending' : 'descending') : 'none'">
                <span>{{ col.header || col.title }}</span>
                <span class="sort-indicator" *ngIf="sortable && col.sortable !== false" [class.active]="sortField() === col.field">
                  <span *ngIf="sortField() === col.field && sortDirection() === 'asc'">▲</span>
                  <span *ngIf="sortField() === col.field && sortDirection() === 'desc'">▼</span>
                  <span *ngIf="sortField() !== col.field" class="sort-idle">↕</span>
                </span>
              </button>

              <button type="button"
                      style="padding:2px"
                      (click)="toggleFilter(col.field, $event)"
                      title="Filter column"
                      aria-label="Filter column"
                      class="btn btn-ghost m-0 filter-btn">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"></polygon>
                </svg>
              </button>
            </div>

            <!-- Column Filter Dropdown -->
            <div *ngIf="openFilterField() === col.field"
                 class="filter-dropdown card absolute left-0 shadow-md">
              <select style="min-height:28px" 
                      [ngModel]="filterOp()"
                      (ngModelChange)="filterOp.set($event)"
                      (click)="$event.stopPropagation()"
                      class="px-2 py-1 input w-full mb-2 text-sm">
                <option value="equals">Equals</option>
                <option value="contains">Contains</option>
                <option value="starts">Starts with</option>
              </select>
              <input type="text"
                     placeholder="Value..."
                     style="min-height:28px" 
                     [ngModel]="filterVal()"
                     (ngModelChange)="filterVal.set($event)"
                     (click)="$event.stopPropagation()"
                     (keydown.enter)="applyFilter(col.field, $event)"
                     class="px-2 py-1 input w-full mb-2 text-sm">
              <div class="flex justify-end gap-2">
                <button type="button"
                        style="min-height:24px"
                        (click)="clearFilter(col.field, $event)"
                        class="btn btn-secondary text-xs m-0">Clear</button>
                <button type="button"
                        style="min-height:24px"
                        (click)="applyFilter(col.field, $event)"
                        class="btn btn-primary text-xs m-0">Apply</button>
              </div>
            </div>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let row of displayData()"
            bb-data-grid-row
            [row]="row"
            [visibleColumns]="visibleColumns()"
            [templates]="templateMap"
            (rowClick)="onRowClick($event)"
            class="clickable-row cursor-pointer">
        </tr>
        
        <!-- Empty State -->
        <tr *ngIf="!loading && displayData().length === 0" class="empty-state-row">
          <td [attr.colspan]="visibleColumns().length" class="text-center text-muted p-4">
            <ng-container *ngIf="emptyTemplate || contentEmptyTemplate; else defaultEmpty">
              <ng-container *ngTemplateOutlet="emptyTemplate || contentEmptyTemplate"></ng-container>
            </ng-container>
            <ng-template #defaultEmpty>No records found.</ng-template>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- Pagination Strip -->
  <div *ngIf="(totalCount > 0 ? totalCount : filteredData().length) > pageSize || totalPages() > 1"
       class="table-pagination">
    <span class="text-muted pagination-summary">
      {{ paginationSummary() }}
    </span>
    <div class="pagination-actions flex gap-2">
      <button type="button"
              class="btn btn-secondary pagination-btn"
              [disabled]="currentPage <= 1"
              (click)="prevPage()">Previous</button>
      <button type="button"
              class="btn btn-secondary pagination-btn"
              [disabled]="currentPage >= totalPages()"
              (click)="nextPage()">Next</button>
    </div>
  </div>
</div>
```

### Blueprint 4.4: `data-grid.component.scss` Styles
```scss
:host {
  display: block;
  width: 100%;
}

.data-grid-card {
  display: flex;
  flex-direction: column;
  position: relative;
  overflow: hidden;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-divider);
  background: var(--color-surface);
}

.data-grid-loading-bar {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 2px;
  background: var(--color-neutral-200);
  z-index: 10;
  overflow: hidden;

  .loading-pulse {
    width: 40%;
    height: 100%;
    background: var(--color-accent);
    animation: indeterminate 1.2s infinite ease-in-out;
  }
}

@keyframes indeterminate {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(300%); }
}

.listwrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  overscroll-behavior: contain;
}

.table-sort-btn {
  all: unset;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font: inherit;
  color: inherit;
  user-select: none;

  &:hover {
    color: var(--color-accent-800);
  }

  &:disabled {
    cursor: default;
  }
}

.sort-indicator {
  display: inline-flex;
  align-items: center;
  font-size: 9px;
  line-height: 1;
  color: var(--color-accent);
  opacity: 0.4;
  transition: opacity 120ms ease;

  &.active {
    opacity: 1;
    font-weight: bold;
  }

  .sort-idle {
    opacity: 0.3;
  }
}

.filter-btn {
  min-height: auto;
  opacity: 0.6;
  transition: opacity 120ms ease;

  &:hover {
    opacity: 1;
  }
}

.filter-dropdown {
  top: calc(100% + 4px);
  z-index: var(--z-dropdown, 20);
  width: 220px;
  padding: var(--space-2);
  background: var(--color-bg);
  border: 1px solid var(--color-divider);
  border-radius: var(--radius-md);
}

.table-pagination {
  flex: none;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  padding: var(--space-2) var(--space-3);
  border-top: 1px solid var(--color-divider);
  background: var(--color-surface);
}

.pagination-summary {
  font-size: 11px;
  font-variant-numeric: tabular-nums;
}

.pagination-btn {
  margin: 0;
  font-size: 12px;
  padding: 4px 9px;
  min-height: 28px;
}
```

---

## 5. Risk Assessment & Verification Strategy

### 5.1. Risk Assessment
- **Breaking Existing Consumers**: `account-ledger`, `bank-accounts`, `banks`, `chart-of-accounts`, `closing-dates`, `journals`, `money-document`, and `sales-list` all consume `bb-data-grid`. All existing inputs (`gridCode`, `columns`, `data`) and outputs (`rowClick`) remain unchanged with matching signatures.
- **CDK Virtual Scroll**: If removing `<cdk-virtual-scroll-viewport>` in favor of native `.listwrap` table container, verify `data-grid.component.spec.ts` and ensure all 314 tests pass cleanly.
- **Z-Index Collision**: Using `z-index: 3` on `.listwrap .table thead th` guarantees that top bar (`z: 6`), fixed rail (`z: 5`), breadcrumbs (`z: 4`), and modals (`z: 30`) stack cleanly over scrolling table headers.

### 5.2. Test Plan for Worker
Worker will run:
```bash
# 1. Run all Vitest unit & integration tests
cd frontend && npm run test

# 2. Run full check suite (lint, typecheck, tests, web build, desktop build, docs build)
cd frontend && npm run check
```
Worker should also expand `data-grid.component.spec.ts` with tests for the new inputs (`loading`, `totalCount`, `pageSize`, `currentPage`, `compact`), outputs (`sortChange`, `pageChange`), aliases, sorting toggles, and empty template projection.
