import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  GoodsReceiptListItem,
  GoodsReceiptService,
} from '@bill-book/purchase-core';

/**
 * Every goods receipt in the branch, newest first.
 *
 * The rejected column is worth its width: a receipt with anything in it is one
 * where the vendor still owes something, and it is the fastest way to see which
 * deliveries had a problem.
 */
@Component({
  selector: 'bb-goods-receipt-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './goods-receipt-list.page.html',
  styleUrl: './goods-receipt-list.page.scss',
})
export class GoodsReceiptListPage {
  private readonly receipts = inject(GoodsReceiptService);

  protected readonly rows = signal<GoodsReceiptListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly search = signal('');

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) {
      return this.rows();
    }

    return this.rows().filter(
      (row) =>
        row.documentNo.toLowerCase().includes(term) ||
        (row.contactName ?? '').toLowerCase().includes(term) ||
        (row.vendorDeliveryNoteNo ?? '').toLowerCase().includes(term) ||
        (row.purchaseOrderNo ?? '').toLowerCase().includes(term),
    );
  });

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.rows.set(await this.receipts.list());
    } catch {
      this.error.set('Goods receipts could not be loaded. Try again.');
    } finally {
      this.loading.set(false);
    }
  }

  protected onSearch(value: string): void {
    this.search.set(value);
  }

  protected statusClass(status: string): string {
    return `pill pill--${status.toLowerCase()}`;
  }
}
