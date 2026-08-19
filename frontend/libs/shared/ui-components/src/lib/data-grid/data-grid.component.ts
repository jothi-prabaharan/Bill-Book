import { Component, Input, Output, EventEmitter, OnInit, signal, computed, inject, ContentChildren, QueryList, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ColumnDef, FilterState } from './data-grid.models';
import { DataGridService } from './data-grid.service';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { DataGridRowComponent } from './data-grid-row/data-grid-row.component';
import { DataGridCellTemplateDirective } from './data-grid-cell-template.directive';

@Component({
  selector: 'bb-data-grid',
  standalone: true,
  imports: [CommonModule, FormsModule, ScrollingModule, DataGridRowComponent],
  templateUrl: './data-grid.component.html',
  styleUrl: './data-grid.component.scss'
})
export class DataGridComponent implements OnInit {
  @ContentChildren(DataGridCellTemplateDirective) cellTemplates!: QueryList<DataGridCellTemplateDirective>;

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

  @Input() gridCode = '';
  @Input() columns: ColumnDef[] = [];
  @Input() data: readonly any[] = [];
  
  @Output() rowClick = new EventEmitter<any>();

  // State
  visibleColumns = signal<ColumnDef[]>([]);
  activeFilters = signal<FilterState[]>([]);
  openFilterField = signal<string | null>(null);

  // Filter popup temp state
  filterOp = signal<'equals' | 'contains' | 'starts'>('contains');
  filterVal = signal<string>('');

  ngOnInit() {
    this.loadState();
  }

  private loadState() {
    const savedState = this.stateService.loadState(this.gridCode);
    
    // Default visibility
    let activeCols = this.columns.map(c => ({
      ...c,
      visible: c.visible !== false
    }));

    if (savedState) {
      // Merge saved visibility
      const savedColMap = new Map(savedState.columns.map(c => [c.field, c]));
      activeCols = activeCols.map(c => {
        const sc = savedColMap.get(c.field);
        if (sc) return { ...c, visible: sc.visible, width: sc.width };
        return c;
      });
      
      this.activeFilters.set(savedState.filters || []);
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
      pageSize: 50
    });
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
    let result = this.data;
    const filters = this.activeFilters();
    
    if (filters.length > 0) {
      result = result.filter(row => {
        return filters.every(f => {
          const cellVal = String(row[f.field] || '').toLowerCase();
          const searchVal = f.value.toLowerCase();
          
          if (f.operator === 'equals') return cellVal === searchVal;
          if (f.operator === 'starts') return cellVal.startsWith(searchVal);
          return cellVal.includes(searchVal);
        });
      });
    }
    return result;
  });

  onRowClick(row: any) {
    this.rowClick.emit(row);
  }

    exportToCsv() {
    const data = this.filteredData();
    const cols = this.visibleColumns();
    if (data.length === 0) return;

    const header = cols.map(c => `"${c.header}"`).join(',');
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
