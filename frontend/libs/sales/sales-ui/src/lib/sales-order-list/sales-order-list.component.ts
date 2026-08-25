import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { SalesOrderListItem, SalesOrderService } from '@bill-book/sales-core';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  UiMessage,
} from '@bill-book/ui-components';

/** What one page asks for. Matches the grid's own default. */
const PAGE_SIZE = 25;

/**
 * The sales order list.
 *
 * **Paged on the server, not in the browser.** A branch accumulates orders for
 * as long as it trades, and a list that fetched all of them to show twenty-five
 * gets slower every month until somebody notices. The grid already knows how to
 * be a server-side pager — give it `totalCount` and listen to `pageChange` —
 * so what this page owns is the request, not the paging.
 *
 * At ~360px the grid becomes a card per order; the filters stack. Both are in
 * the stylesheet, and neither is a second template.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-sales-order-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    MessageBoxComponent,
  ],
  templateUrl: './sales-order-list.component.html',
  styleUrl: './sales-order-list.component.scss',
})
export class SalesOrderListComponent implements OnInit {
  private readonly orders = inject(SalesOrderService);
  private readonly router = inject(Router);

  protected readonly rows = signal<SalesOrderListItem[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly loading = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly pageSize = PAGE_SIZE;

  /** Bound with ngModel, so they are plain fields rather than signals. */
  protected status = '';
  protected search = '';

  protected readonly statuses: readonly { value: string; label: string }[] = [
    { value: '', label: 'All statuses' },
    { value: 'Draft', label: 'Draft' },
    { value: 'ReadyToPost', label: 'Ready to confirm' },
    { value: 'Posted', label: 'Confirmed' },
    { value: 'Void', label: 'Void' },
  ];

  protected readonly columns: ColumnDef[] = [
    { field: 'documentDate', header: 'Date', dataType: 'date' },
    { field: 'documentNo', header: 'Number', isTemplate: true },
    { field: 'contactName', header: 'Customer' },
    { field: 'deliveryDate', header: 'Delivery', dataType: 'date' },
    { field: 'fulfilmentStatus', header: 'Fulfilment' },
    { field: 'totalAmount', header: 'Amount', align: 'right', dataType: 'money' },
    { field: 'status', header: 'Status', isTemplate: true },
  ];

  protected readonly isEmpty = computed(() => !this.loading() && this.rows().length === 0);

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.messages.set([]);

    try {
      const result = await this.orders.list({
        // The server clamps this again. Both do it, because a page that trusted
        // the server and a server that trusted the page would each be relying
        // on the other.
        skip: Math.max(0, (this.page() - 1) * PAGE_SIZE),
        take: PAGE_SIZE,
        status: this.status || undefined,
        search: this.search.trim() || undefined,
      });

      this.rows.set(result.rows);
      this.total.set(result.total);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
      this.rows.set([]);
      this.total.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  /** A changed filter goes back to page one — page four of the old result is not page four of the new one. */
  protected async onFilterChange(): Promise<void> {
    this.page.set(1);
    await this.load();
  }

  protected async onPageChange(page: number): Promise<void> {
    this.page.set(page);
    await this.load();
  }

  protected open(order: SalesOrderListItem): void {
    void this.router.navigate(['/sales/sales-orders', order.salesOrderId]);
  }

  protected create(): void {
    void this.router.navigate(['/sales/sales-orders/new']);
  }

  /**
   * The tag class a status wears, from the shared set in `_tags.scss`.
   *
   * Named for what the status *is* rather than what colour it should be, so a
   * palette change reaches it without anybody editing this file.
   */
  protected statusTag(status: string): string {
    switch (status) {
      case 'Posted':
        return 'tag tag-accent';
      case 'Void':
        return 'tag tag-outline';
      default:
        return 'tag tag-neutral';
    }
  }
}

