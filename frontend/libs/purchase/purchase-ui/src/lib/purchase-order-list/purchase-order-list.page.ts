import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import {
  PurchaseOrderListItem,
  PurchaseOrderService,
} from '@bill-book/purchase-core';

/**
 * Every purchase order in the branch, newest first.
 *
 * The vendor's name is not stored on the order — the server resolves it in one
 * call for the whole page — so a row whose name came back empty falls back to
 * the contact code rather than showing a blank cell.
 */
@Component({
  selector: 'bb-purchase-order-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './purchase-order-list.page.html',
  styleUrl: './purchase-order-list.page.scss',
})
export class PurchaseOrderListPage {
  private readonly orders = inject(PurchaseOrderService);

  protected readonly rows = signal<PurchaseOrderListItem[]>([]);
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
        (row.contactCode ?? '').toLowerCase().includes(term),
    );
  });

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.rows.set(await this.orders.list());
    } catch {
      this.error.set('Purchase orders could not be loaded. Try again.');
    } finally {
      this.loading.set(false);
    }
  }

  protected onSearch(value: string): void {
    this.search.set(value);
  }

  /**
   * Two statuses run side by side and mean different things: `status` is where
   * the document is in its lifecycle, `fulfilmentStatus` is how much of it has
   * arrived. An order can be Posted and still Open.
   */
  protected statusClass(status: string): string {
    return `pill pill--${status.toLowerCase()}`;
  }

  protected fulfilmentClass(status: string): string {
    return `pill pill--${status.toLowerCase()}`;
  }
}
