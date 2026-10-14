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
  DataGridService,
  DocNumberComponent,
  MessageBoxComponent,
  MoneyCellComponent,
  PeriodFilterBarComponent,
  PeriodRange,
  RegisterDatePipe,
  RegisterHeaderComponent,
  RegisterStatusOption,
  SearchInputComponent,
  StatusTabsComponent,
  UiMessage,
  rangeForPreset,
  registerStatusTone,
  statusTabsFromCounts,
} from '@bill-book/ui-components';

const GRID_CODE = 'SALES_ORDER_LIST';

/** An order's document statuses, named the way an order reads them (TK-187). */
export const SALES_ORDER_STATUSES: readonly RegisterStatusOption[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'ReadyToPost', label: 'Ready to confirm' },
  { value: 'Posted', label: 'Confirmed' },
  { value: 'Void', label: 'Void' },
];

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
    SearchInputComponent,
    PeriodFilterBarComponent,
    RegisterHeaderComponent,
    StatusTabsComponent,
    DocNumberComponent,
    MoneyCellComponent,
    RegisterDatePipe,
  ],
  host: { class: 'register-page' },
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

  /** The register's page size: the person's last choice, else the smallest. */
  protected readonly pageSize = signal(inject(DataGridService).loadState(GRID_CODE)?.pageSize ?? 10);
  protected readonly gridCode = GRID_CODE;

  /** The register's period; a dated register opens on the financial year. */
  protected readonly range = signal<PeriodRange>(rangeForPreset('this-financial-year'));

  /** Matches per status under the other filters, from the server. */
  protected readonly statusCounts = signal<Record<string, number>>({});

  /** What every matching order comes to in base currency, from the server. */
  protected readonly totalBase = signal(0);

  protected readonly tabs = computed(() => statusTabsFromCounts(this.statusCounts(), SALES_ORDER_STATUSES));

  /** Orders in the period under the other filters — the All tab's count. */
  protected readonly periodCount = computed(() => this.tabs()[0]?.count ?? 0);

  protected readonly statusTone = registerStatusTone;

  /** Bound with ngModel, so they are plain fields rather than signals. */
  protected status = '';
  protected search = '';

  protected readonly columns: ColumnDef[] = [
    { field: 'documentNo', header: 'Number', isTemplate: true },
    { field: 'documentDate', header: 'Date', dataType: 'date', isTemplate: true },
    { field: 'contactName', header: 'Customer' },
    { field: 'deliveryDate', header: 'Delivery', dataType: 'date', isTemplate: true },
    { field: 'status', header: 'Status', isTemplate: true },
    { field: 'fulfilmentStatus', header: 'Fulfilment', isTemplate: true },
    { field: 'taxableAmount', header: 'Taxable', dataType: 'money', isTemplate: true },
    { field: 'totalAmount', header: 'Total', dataType: 'money', isTemplate: true },
  ];

  /** How an order's status reads: Confirmed, not Posted. */
  protected statusLabel(status: string): string {
    return SALES_ORDER_STATUSES.find((s) => s.value === status)?.label ?? status;
  }

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
        skip: Math.max(0, (this.page() - 1) * this.pageSize()),
        take: this.pageSize(),
        from: this.range().from || undefined,
        to: this.range().to || undefined,
        status: this.status || undefined,
        search: this.search.trim() || undefined,
      });

      this.rows.set(result.rows);
      this.total.set(result.total);
      this.statusCounts.set(result.statusCounts ?? {});
      this.totalBase.set(result.totalAmountBase ?? result.rows.reduce((sum, row) => sum + row.totalAmount, 0));
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
      this.rows.set([]);
      this.total.set(0);
      this.statusCounts.set({});
      this.totalBase.set(0);
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
    if (this.page() === page) return;
    this.page.set(page);
    await this.load();
  }

  protected async onPageSizeChange(size: number): Promise<void> {
    this.pageSize.set(size);
    this.page.set(1);
    await this.load();
  }

  protected async setStatus(status: string): Promise<void> {
    this.status = status;
    await this.onFilterChange();
  }

  protected async setRange(range: PeriodRange): Promise<void> {
    this.range.set(range);
    await this.onFilterChange();
  }

  protected open(order: SalesOrderListItem): void {
    void this.router.navigate(['/sales/sales-orders', order.salesOrderId]);
  }

  protected create(): void {
    void this.router.navigate(['/sales/sales-orders/new']);
  }
}

