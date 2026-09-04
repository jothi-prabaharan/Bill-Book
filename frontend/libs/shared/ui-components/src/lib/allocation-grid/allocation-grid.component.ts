import { ChangeDetectionStrategy } from '@angular/core';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  DEFAULT_FORMAT_SETTINGS,
  FormatSettings,
  formatDate,
  formatNumber,
} from '@bill-book/currency-format';

export interface AllocationRow {
  /**
   * The three-letter code the ledger knows the document by — `INV`, `BIL`,
   * `CRN`. Required rather than optional because it is half the key
   * `POST /api/allocations` takes: a row that cannot say what kind of document
   * it is cannot be posted, and discovering that at the API is worse than not
   * being able to build the row.
   */
  transactionTypeCode: string;
  transactionId: number;
  documentNo: string;
  documentDate: string;
  dueDate?: string;
  totalAmount: number;
  outstandingAmount: number;
  allocatedAmount: number;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-allocation-grid',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './allocation-grid.component.html',
  styleUrls: ['./allocation-grid.component.scss']
})
export class AllocationGridComponent {
  @Input() rows: AllocationRow[] = [];

  private _amountToAllocate = 0;

  /**
   * What the document being built is worth. When it shrinks — a note's total
   * dropping as its last line is edited — the rows are trimmed to match, so
   * the grid never claims more than the parent can pay.
   */
  @Input()
  set amountToAllocate(value: number) {
    this._amountToAllocate = Math.max(0, value);
    this.clampToAmount();
  }

  get amountToAllocate(): number {
    return this._amountToAllocate;
  }

  /**
   * The branch's formats. Without this the grid drew Western thousands and a
   * US-style date beside a host showing Indian lakhs and dd/MM/yyyy — three
   * formats for the same figures on one screen, which a screenshot found and
   * no unit test could have.
   */
  @Input() formats: FormatSettings = DEFAULT_FORMAT_SETTINGS;

  /**
   * Whether to draw the grid's own Amount/Allocated/Remaining bar.
   *
   * A host with its own summary — the allocation modal has a richer one,
   * carrying the document total and what it still owes — would otherwise show
   * the same three figures twice. Defaults to true so every existing caller is
   * untouched.
   */
  @Input() showSummary = true;

  @Output() rowsChange = new EventEmitter<AllocationRow[]>();

  /** Money the branch's way: symbol, decimals and lakh-or-thousands grouping. */
  money(value: number | null | undefined): string {
    return formatNumber(value, this.formats.currencyDecimals, this.formats.currencyMask);
  }

  /** A DateOnly in the branch's pattern, without a timezone shifting the day. */
  date(value: string | null | undefined): string {
    return formatDate(value, this.formats.datePattern);
  }

  get totalAllocated(): number {
    return this.rows.reduce((sum, r) => sum + (r.allocatedAmount || 0), 0);
  }

  get remainingToAllocate(): number {
    return this.amountToAllocate - this.totalAllocated;
  }

  onAllocateChange(index: number, newAmount: number) {
    const row = this.rows[index];
    if (!row) return;

    // Never past what the document still owes, and never past what the parent
    // has left to allocate — a row that fills the whole gap leaves nothing for
    // the ones behind it.
    const otherRows = this.totalAllocated - (row.allocatedAmount || 0);
    const cap = Math.min(row.outstandingAmount, this.amountToAllocate - otherRows);
    const validAmount = Math.min(Math.max(0, newAmount), Math.max(0, cap));

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

  /**
   * Trims the tail until the rows fit the amount. The oldest documents come
   * first, so they keep their allocation and the youngest loses theirs — the
   * order the parent handed over is the order the claim is honoured.
   */
  private clampToAmount() {
    let total = this.totalAllocated;
    if (total <= this.amountToAllocate) return;

    const updatedRows = [...this.rows];
    for (let i = updatedRows.length - 1; i >= 0 && total > this.amountToAllocate; i--) {
      const row = updatedRows[i];
      const excess = total - this.amountToAllocate;
      const cut = Math.min(row.allocatedAmount || 0, excess);
      if (cut > 0) {
        updatedRows[i] = { ...row, allocatedAmount: (row.allocatedAmount || 0) - cut };
        total -= cut;
      }
    }

    this.rows = updatedRows;
    this.rowsChange.emit(this.rows);
  }
}
