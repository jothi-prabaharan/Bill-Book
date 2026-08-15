import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { BillListItem, BillService } from '@bill-book/purchase-core';

/**
 * Every bill in the branch, newest first.
 *
 * The overdue column is the point of the screen: a posted bill past its due date
 * is money owed late, and this is the list somebody works down before a payment
 * run.
 */
@Component({
  selector: 'bb-bill-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './bill-list.page.html',
  styleUrl: './bill-list.page.scss',
})
export class BillListPage {
  private readonly bills = inject(BillService);

  protected readonly rows = signal<BillListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly search = signal('');
  protected readonly overdueOnly = signal(false);

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();

    return this.rows().filter((row) => {
      if (this.overdueOnly() && !this.isOverdue(row)) {
        return false;
      }

      if (!term) {
        return true;
      }

      return (
        row.documentNo.toLowerCase().includes(term) ||
        row.vendorBillNo.toLowerCase().includes(term) ||
        (row.contactName ?? '').toLowerCase().includes(term) ||
        (row.goodsReceiptNo ?? '').toLowerCase().includes(term)
      );
    });
  });

  /** What is owed and late, which is the number a payment run starts from. */
  protected readonly overdueTotal = computed(() =>
    this.rows()
      .filter((row) => this.isOverdue(row))
      .reduce((sum, row) => sum + row.totalAmount, 0),
  );

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.rows.set(await this.bills.list());
    } catch {
      this.error.set('Bills could not be loaded. Try again.');
    } finally {
      this.loading.set(false);
    }
  }

  protected onSearch(value: string): void {
    this.search.set(value);
  }

  protected isOverdue(row: BillListItem): boolean {
    return row.status === 'Posted' && row.daysOverdue > 0;
  }

  protected statusClass(status: string): string {
    return `pill pill--${status.toLowerCase()}`;
  }
}
