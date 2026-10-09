import { ChangeDetectionStrategy } from '@angular/core';
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
  TemplateRef,
  booleanAttribute,
  HostListener,
} from '@angular/core';
import { OverlayModule } from '@angular/cdk/overlay';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ColumnDef, FilterState, SortDirection, SortState } from './data-grid.models';
import { DataGridService } from './data-grid.service';
import { DataGridRowComponent } from './data-grid-row/data-grid-row.component';
import { DataGridCellTemplateDirective } from './data-grid-cell-template.directive';
import { DataGridFooterDirective } from './data-grid-footer.directive';

/** Classes for one row, from the row itself â€” an over-allocated line, a short item. */
export type DataGridRowClass = (row: any) => string | string[] | Record<string, boolean> | null | undefined;

/**
 * The product's one table.
 *
 * A list of records someone browses gets the defaults: sorting, a filter per
 * column, a row count and paging. A table the page composes â€” a posting
 * preview with a totals footer, a statement section, rows of editable amounts
 * â€” turns those off (`[sortable]="false"`, `[showColumnFilters]="false"`,
 * `[showCount]="false"`, `[paged]="false"`), adds `flush` when it already sits
 * in a card, and uses `rowClass` and a `bbGridFooter` template for what a list
 * never needs. There is deliberately no second table component: every table in
 * the product draws through this one, so they all look, scroll and read the
 * same. (`bb-report-grid` and `bb-document-line-grid` are not tables in that
 * sense â€” one is the report engine, the other the document line editor.)
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-data-grid',
  standalone: true,
  imports: [CommonModule, FormsModule, OverlayModule, DataGridRowComponent],
  templateUrl: './data-grid.component.html',
  styleUrl: './data-grid.component.scss'
})
export class DataGridComponent implements OnInit {
  @HostListener('document:click')
  onDocumentClick() {
    if (this.openFilterField()) {
      this.openFilterField.set(null);
    }
  }

  @ContentChildren(DataGridCellTemplateDirective) cellTemplates!: QueryList<DataGridCellTemplateDirective>;
  @ContentChild('emptyTemplate') contentEmptyTemplate?: TemplateRef<any>;
  @ContentChild(DataGridFooterDirective) footer?: DataGridFooterDirective;

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

  private _columns = signal<ColumnDef[]>([]);
  @Input()
  get columns(): ColumnDef[] {
    return this._columns();
  }
  set columns(val: ColumnDef[]) {
    this._columns.set(val || []);
    this.loadState();
  }

  private _data = signal<readonly any[]>([]);
  @Input()
  get data(): readonly any[] {
    return this._data();
  }
  set data(val: readonly any[]) {
    this._data.set(val || []);
  }

  @Input() loading = false;

  private _totalCount = signal<number>(0);
  @Input()
  get totalCount(): number {
    return this._totalCount();
  }
  set totalCount(val: number) {
    this._totalCount.set(val || 0);
  }

  private _pageSize = signal<number>(50);
  @Input()
  get pageSize(): number {
    return this._pageSize();
  }
  set pageSize(val: number) {
    this._pageSize.set(val || 50);
  }

  private _currentPage = signal<number>(1);
  @Input()
  get currentPage(): number {
    return this._currentPage();
  }
  set currentPage(val: number) {
    this._currentPage.set(val || 1);
  }

  @Input({ transform: booleanAttribute }) compact = true;

  /**
   * The permanent column-filter row. Filtering is per column and always
   * available â€” there is no list-level search box and no toggle to reveal this,
   * because a filter you have to go and find is a filter nobody uses.
   */
  @Input({ transform: booleanAttribute }) showColumnFilters = true;
  @Input() emptyTemplate?: TemplateRef<any>;
  @Input({ transform: booleanAttribute }) sortable = true;
  @Input({ transform: booleanAttribute }) showExport = true;

  /**
   * Split the rows into pages. Off for a table that is read as a whole â€” a
   * statement section, a posting preview, a timesheet week.
   */
  private _paged = signal(true);
  @Input({ transform: booleanAttribute })
  get paged(): boolean {
    return this._paged();
  }
  set paged(val: boolean) {
    this._paged.set(val);
  }

  /** The "N shown" strip above the table, with its Clear button. */
  @Input({ transform: booleanAttribute }) showCount = true;

  /** No border or radius of its own, for a table already inside a card, panel or drawer. */
  @Input({ transform: booleanAttribute }) flush = false;

  /** Forces the grid card to take up 100% of the parent height. */
  @Input({ transform: booleanAttribute }) fillHeight = false;

  /** The table's accessible name, when no heading beside it gives one. */
  @Input() label = '';

  /** What an empty table says. `emptyTemplate` overrides it when the message needs markup. */
  @Input() emptyText = 'No records found.';

  /** One card per row below 640px, each cell labelled with its column's name. */
  @Input({ transform: booleanAttribute }) stackOnPhone = false;

  /** Classes for a row, computed from it: `row--danger`, `row--warning`, `row--success`, `row--muted`. */
  @Input() rowClass?: DataGridRowClass;
  @Input({ transform: booleanAttribute }) selectable = false;
  @Output() selectionChange = new EventEmitter<any[]>();
  readonly selectedRows = signal<any[]>([]);

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
    const cols = this._columns();
    let activeCols = cols.map(c => ({
      ...c,
      visible: c.visible !== false
    }));

    if (this.gridCode) {
      const savedState = this.stateService.loadState(this.gridCode);
      if (savedState) {
        if (savedState.columns && savedState.columns.length > 0) {
          const savedColMap = new Map(savedState.columns.map(c => [c.field, c]));
          activeCols = activeCols.map(c => {
            const sc = savedColMap.get(c.field);
            if (sc) return { ...c, visible: sc.visible, width: sc.width ?? c.width };
            return c;
          });
        }

        if (savedState.filters) {
          this.activeFilters.set(savedState.filters);
        }
        if (savedState.sort) {
          this.sortField.set(savedState.sort.field);
          this.sortDirection.set(savedState.sort.direction);
        }
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
      pageSize: this._pageSize(),
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

  /** What is currently typed into one column's filter input. */
  columnFilter(field: string): string {
    return this.activeFilters().find((f) => f.field === field)?.value ?? '';
  }

  /**
   * Type into a column's filter. An empty box is not a filter, so it removes
   * itself rather than matching everything.
   */
  setColumnFilter(field: string, value: string): void {
    const trimmed = value.trim();
    const rest = this.activeFilters().filter((f) => f.field !== field);
    this.activeFilters.set(
      trimmed ? [...rest, { field, operator: 'contains' as const, value: trimmed }] : rest,
    );
    this.saveState();
  }

  /** True while anything is narrowing or reordering what the person sees. */
  readonly isNarrowed = computed(
    () => this.activeFilters().length > 0 || this.sortField() !== null,
  );

  /** How many rows survive the filters â€” what the Clear button is measured against. */
  readonly visibleRowCount = computed(() => this.filteredData().length);

  /** Drop every column filter and the sort in one press. */
  clearAll(): void {
    this.activeFilters.set([]);
    this.sortField.set(null);
    this.sortDirection.set(null);
    this.openFilterField.set(null);
    this.saveState();
  }

  // Computed data
  filteredData = computed(() => {
    let result = [...this._data()];
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
        } else if (valA instanceof Date && valB instanceof Date) {
          cmp = valA.getTime() - valB.getTime();
        } else {
          cmp = String(valA).localeCompare(String(valB), undefined, { numeric: true, sensitivity: 'base' });
        }
        return dir === 'asc' ? cmp : -cmp;
      });
    }

    return result;
  });

  // Display data for current page view
  displayData = computed(() => {
    const list = this.filteredData();
    const total = this._totalCount();
    const size = this._pageSize();
    const page = this._currentPage();

    if (total > 0 || !this.paged) {
      // Server-side pagination (the parent passed the page), or no paging at all.
      return list;
    }
    // Client-side pagination if list exceeds pageSize
    if (size > 0 && list.length > size) {
      const start = (page - 1) * size;
      return list.slice(start, start + size);
    }
    return list;
  });

  totalPages = computed(() => {
    const total = this._totalCount() > 0 ? this._totalCount() : this.filteredData().length;
    const size = this._pageSize();
    return size > 0 ? Math.max(1, Math.ceil(total / size)) : 1;
  });

  paginationSummary = computed(() => {
    const total = this._totalCount() > 0 ? this._totalCount() : this.filteredData().length;
    const size = this._pageSize();
    const page = this._currentPage();
    if (total === 0) return '0 records';
    const start = (page - 1) * size + 1;
    const end = Math.min(page * size, total);
    return `${start}–${end} of ${total} records`;
  });

  prevPage() {
    if (this._currentPage() > 1) {
      const newPage = this._currentPage() - 1;
      this._currentPage.set(newPage);
      this.pageChange.emit(newPage);
    }
  }

  nextPage() {
    if (this._currentPage() < this.totalPages()) {
      const newPage = this._currentPage() + 1;
      this._currentPage.set(newPage);
      this.pageChange.emit(newPage);
    }
  }

  /** A column's heading. An empty one stays empty â€” an actions column has none. */
  headerOf(col: ColumnDef): string {
    return col.header ?? col.title ?? col.field;
  }

  isNumericCol(col: ColumnDef): boolean {
    if (!col) return false;
    return (
      col.numeric === true ||
      col.align === 'right' ||
      ['number', 'money', 'quantity', 'unitprice'].includes(col.dataType || '')
    );
  }

  onRowClick(row: any) {
    this.rowClick.emit(row);
  }

  /** Rows only look clickable when somebody is listening for the click. */
  get clickable(): boolean {
    return this.rowClick.observed;
  }

  /** Whether the pager strip is drawn at all. */
  showPager(): boolean {
    if (!this.paged) return false;
    const total = this._totalCount() > 0 ? this._totalCount() : this.filteredData().length;
    return total > this._pageSize() || this.totalPages() > 1;
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



