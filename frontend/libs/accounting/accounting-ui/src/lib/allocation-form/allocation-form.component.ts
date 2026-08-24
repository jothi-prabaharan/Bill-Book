import { ChangeDetectionStrategy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NumberInputComponent } from '@bill-book/ui-components';
import { CommonModule } from '@angular/common';

export interface OutstandingBalanceView {
  contactId: number;
  transactionTypeCode: string;
  transactionId: number;
  documentNo: string;
  documentDate: string;
  dueDate: string | null;
  totalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
}

changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-allocation-form',
  standalone: true,
  imports: [CommonModule, FormsModule, NumberInputComponent],
  templateUrl: './allocation-form.component.html',
  styles: [
    `
      .sheet {
        position: fixed;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        background: white;
        padding: 24px;
        box-shadow: 0 4px 20px rgba(0,0,0,0.15);
        border-radius: 8px;
        z-index: 1000;
        width: 100%;
        max-width: 600px;
        max-height: 90vh;
        overflow-y: auto;
      }
      .backdrop {
        position: fixed;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        background: rgba(0,0,0,0.4);
        z-index: 999;
      }
      .allocation-grid {
        display: table;
        width: 100%;
        border-collapse: collapse;
        margin-bottom: 24px;
      }
      .allocation-grid .row {
        display: table-row;
        border-bottom: 1px solid #eee;
      }
      .allocation-grid .cell, .allocation-grid .header-cell {
        display: table-cell;
        padding: 12px 8px;
        vertical-align: middle;
      }
      .allocation-grid .header-cell {
        font-weight: bold;
        background: #f9f9f9;
        text-align: left;
      }
      .input-cell {
        width: 150px;
      }
      .actions {
        display: flex;
        justify-content: flex-end;
        gap: 12px;
        margin-top: 24px;
      }
    `,
  ],
})
export class AllocationFormComponent implements OnInit {
  private readonly http = inject(HttpClient);

  @Input() contactId!: number;
  @Input() sourceTransactionTypeCode!: string;
  @Input() sourceTransactionId!: number;
  @Input() availableAmount!: number;
  @Input() targetTypeCodes!: string[]; // e.g. ['INV'] for Sales, ['BIL'] for Purchase

  @Output() closeForm = new EventEmitter<boolean>();

  protected readonly targets = signal<(OutstandingBalanceView & { allocateAmount: number | null })[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit() {
    void this.load();
  }

  async load() {
    this.busy.set(true);
    this.error.set(null);
    try {
      const balances = await new Promise<OutstandingBalanceView[]>((resolve, reject) =>
        this.http
          .get<OutstandingBalanceView[]>(`/api/ledger/contacts/${this.contactId}/outstanding-balances/3`)
          .subscribe({ next: resolve, error: reject })
      );

      const validTargets = balances
        .filter(b => this.targetTypeCodes.includes(b.transactionTypeCode))
        .filter(b => !(b.transactionTypeCode === this.sourceTransactionTypeCode && b.transactionId === this.sourceTransactionId))
        .map(b => ({ ...b, allocateAmount: null as number | null }));
      
      this.targets.set(validTargets);
    } catch (err: any) {
      this.error.set(err?.error?.message ?? 'Failed to load outstanding balances.');
    } finally {
      this.busy.set(false);
    }
  }

  get totalAllocated(): number {
    return this.targets().reduce((sum, t) => sum + (t.allocateAmount || 0), 0);
  }

  get remainingAmount(): number {
    return this.availableAmount - this.totalAllocated;
  }

  get isValid(): boolean {
    if (this.totalAllocated <= 0) return false;
    if (this.remainingAmount < 0) return false;

    // Check individual over-allocations
    return !this.targets().some(t => (t.allocateAmount || 0) > t.outstandingAmount);
  }

  async save() {
    if (!this.isValid) return;

    this.busy.set(true);
    this.error.set(null);

    try {
      for (const target of this.targets()) {
        if (target.allocateAmount && target.allocateAmount > 0) {
          await new Promise((resolve, reject) =>
            this.http.post('/api/allocations', {
              sourceTransactionTypeCode: this.sourceTransactionTypeCode,
              sourceTransactionId: this.sourceTransactionId,
              targetTransactionTypeCode: target.transactionTypeCode,
              targetTransactionId: target.transactionId,
              amount: target.allocateAmount
            }).subscribe({ next: resolve, error: reject })
          );
        }
      }
      this.closeForm.emit(true);
    } catch (err: any) {
      this.error.set(err?.error?.message ?? 'Allocation failed.');
    } finally {
      this.busy.set(false);
    }
  }

  cancel() {
    this.closeForm.emit(false);
  }
}

