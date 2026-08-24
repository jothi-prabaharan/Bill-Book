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
  TemplateRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ColumnDef, FilterState, SortDirection, SortState } from './data-grid.models';
import { DataGridService } from './data-grid.service';
import { DataGridRowComponent } from './data-grid-row/data-grid-row.component';
import { DataGridCellTemplateDirective } from './data-grid-cell-template.directive';

changeDetection: ChangeDetectionStrategy.OnPush,
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

    if (total > 0) {
      // Server-side pagination: parent already passed the sliced page
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

