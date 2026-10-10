import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ColumnDef } from '../data-grid.models';
import { DataGridCellComponent } from '../data-grid-cell/data-grid-cell.component';

@Component({
  selector: 'bb-data-grid-row, [bb-data-grid-row]',
  standalone: true,
  imports: [CommonModule, DataGridCellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './data-grid-row.component.html',
  styleUrl: './data-grid-row.component.scss'
})
export class DataGridRowComponent {
  @Input() row: any;
  /** The row's position on the page, for a cell template that needs it. */
  @Input() index = 0;
  @Input() visibleColumns: ColumnDef[] = [];
  @Input() templates: Record<string, TemplateRef<any>> = {};

  /** A leading checkbox cell, for a register or a selectable grid. */
  @Input() selectable = false;
  @Input() selected = false;
  @Output() selectToggle = new EventEmitter<any>();

  @Output() rowClick = new EventEmitter<any>();

  onRowClick() {
    this.rowClick.emit(this.row);
  }

  isNumericCol(col: ColumnDef): boolean {
    if (!col) return false;
    return (
      col.numeric === true ||
      col.align === 'right' ||
      ['number', 'money', 'quantity', 'unitprice'].includes(col.dataType || '')
    );
  }
}
