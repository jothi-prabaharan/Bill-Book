import { Component, Input, ChangeDetectionStrategy, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ColumnDef } from '../data-grid.models';

@Component({
  selector: 'bb-data-grid-cell',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './data-grid-cell.component.html',
  styleUrl: './data-grid-cell.component.scss'
})
export class DataGridCellComponent {
  @Input() col!: ColumnDef;
  @Input() row: any;
  @Input() template?: TemplateRef<any>;

  get value(): any {
    return this.row[this.col.field];
  }
}
