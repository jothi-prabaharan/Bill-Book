import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AllocationRow {
  documentNo: string;
  documentDate: string;
  dueDate?: string;
  totalAmount: number;
  outstandingAmount: number;
  allocatedAmount: number;
}

@Component({
  selector: 'bb-allocation-grid',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './allocation-grid.component.html',
  styleUrls: ['./allocation-grid.component.scss']
})
export class AllocationGridComponent {
  @Input() rows: AllocationRow[] = [];
  @Input() amountToAllocate = 0;
  @Output() rowsChange = new EventEmitter<AllocationRow[]>();

  get totalAllocated(): number {
    return this.rows.reduce((sum, r) => sum + (r.allocatedAmount || 0), 0);
  }

  get remainingToAllocate(): number {
    return this.amountToAllocate - this.totalAllocated;
  }

  onAllocateChange(index: number, newAmount: number) {
    const row = this.rows[index];
    // Ensure we don't allocate more than outstanding
    const validAmount = Math.min(Math.max(0, newAmount), row.outstandingAmount);
    
    const updatedRows = [...this.rows];
    updatedRows[index] = { ...row, allocatedAmount: validAmount };
    this.rows = updatedRows;
    this.rowsChange.emit(this.rows);
  }

  autoAllocate() {
    let remaining = this.amountToAllocate;
    const updatedRows = this.rows.map(row => {
      if (remaining <= 0) {
        return { ...row, allocatedAmount: 0 };
      }
      const toAllocate = Math.min(row.outstandingAmount, remaining);
      remaining -= toAllocate;
      return { ...row, allocatedAmount: toAllocate };
    });
    this.rows = updatedRows;
    this.rowsChange.emit(this.rows);
  }
}
