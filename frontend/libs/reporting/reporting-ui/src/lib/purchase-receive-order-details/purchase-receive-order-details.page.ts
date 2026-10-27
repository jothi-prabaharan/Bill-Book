import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedPayablesVendorChoice,
  PayableInvoiceItemChoice,
  PurchaseReceiveOrderDetailsGridRow,
  PurchaseReceiveOrderDetailsOrderChoice,
  PurchaseReceiveOrderDetailsResult,
  PurchaseReceiveOrderDetailsService,
  SalesAnalysisWarehouseChoice,
  appendPurchaseReceiveOrderDetailsPage,
  hasMorePurchaseReceiveOrderDetails,
  itemChoiceLabel,
  purchaseReceiveOrderDetailsRows,
  purchaseReceiveOrderDetailsSentence,
  receiveOrderChoiceLabel,
  serialsNotListed,
  vendorChoiceLabel,
  warehouseChoiceLabel,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  SelectComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'contactId' | 'itemId' | 'warehouseId' | 'purchaseOrderId';

/**
 * The Purchase Receive Order Details (TK-241): its own page on its own endpoint. Every posted goods
 * receipt dated in a period, newest first, each with its lines (the item, the warehouse it went into,
 * what was delivered, rejected, accepted and billed, what a unit cost and what the accepted goods are
 * worth, and the batch or serial numbers the receipt created) and its value, with the total of every
 * receipt the filters match under it. A page of receipts loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a missing
 * or out-of-order one shows at its field, as does any other refusal; a failure shows in the message
 * box. Choosing an item or a warehouse narrows the lines as well as the receipts, and the page says so.
 * A foreign-currency receipt's amounts carry its currency code and its value in base. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-purchase-receive-order-details-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './purchase-receive-order-details.page.html',
  styleUrl: './purchase-receive-order-details.page.scss',
})
export class PurchaseReceiveOrderDetailsPage implements OnInit {
  private readonly report = inject(PurchaseReceiveOrderDetailsService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each receipt is a card rather than a run of rows and columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    itemId: new FormControl<number | null>(null),
    warehouseId: new FormControl<number | null>(null),
    purchaseOrderId: new FormControl<number | null>(null),
  });

  protected readonly vendors = signal<AgedPayablesVendorChoice[]>([]);
  protected readonly items = signal<PayableInvoiceItemChoice[]>([]);
  protected readonly warehouses = signal<SalesAnalysisWarehouseChoice[]>([]);
  protected readonly orders = signal<PurchaseReceiveOrderDetailsOrderChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<PurchaseReceiveOrderDetailsResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly vendorOptions = computed<BbSelectOption<number>[]>(() =>
    this.vendors().map((c) => ({ value: c.contactId, label: vendorChoiceLabel(c) })),
  );

  protected readonly itemOptions = computed<BbSelectOption<number>[]>(() =>
    this.items().map((i) => ({ value: i.itemId, label: itemChoiceLabel(i) })),
  );

  protected readonly warehouseOptions = computed<BbSelectOption<number>[]>(() =>
    this.warehouses().map((w) => ({ value: w.warehouseId, label: warehouseChoiceLabel(w) })),
  );

  protected readonly orderOptions = computed<BbSelectOption<number>[]>(() =>
    this.orders().map((o) => ({ value: o.purchaseOrderId, label: receiveOrderChoiceLabel(o) })),
  );

  protected readonly rows = computed<PurchaseReceiveOrderDetailsGridRow[]>(() => {
    const result = this.result();
    return result ? purchaseReceiveOrderDetailsRows(result) : [];
  });

  /** The cards of the phone layout: a receipt's heading, lines and value together; the total last. */
  protected readonly groups = computed<PurchaseReceiveOrderDetailsGridRow[][]>(() => {
    const groups: PurchaseReceiveOrderDetailsGridRow[][] = [];
    for (const row of this.rows()) {
      if (row.kind === 'heading' || row.kind === 'total' || groups.length === 0) {
        groups.push([row]);
      } else {
        groups[groups.length - 1].push(row);
      }
    }
    return groups;
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMorePurchaseReceiveOrderDetails(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'item', header: 'Receipt / item' },
    { field: 'warehouse', header: 'Warehouse', width: '7.5rem' },
    { field: 'delivered', header: 'Delivered', numeric: true, width: '5.5rem' },
    { field: 'rejected', header: 'Rejected', numeric: true, width: '5rem' },
    { field: 'accepted', header: 'Accepted', numeric: true, width: '5rem' },
    { field: 'billed', header: 'Billed', numeric: true, width: '4.5rem' },
    { field: 'unitCost', header: 'Unit cost', numeric: true, width: '6rem' },
    { field: 'value', header: 'Value', numeric: true, width: '8rem' },
  ];

  /** The period's total in words; a failure above it. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalReceipts > 0) {
      messages.push({ tone: 'info', text: purchaseReceiveOrderDetailsSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: PurchaseReceiveOrderDetailsGridRow): string =>
    row.kind === 'heading' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'total' ? 'row--total' : '';

  /** A quantity and a unit cost in the branch's own decimals. */
  protected quantity(value: number | null): string {
    return this.formats.formatQuantity(value);
  }

  protected unitPrice(value: number | null): string {
    return this.formats.formatUnitPrice(value);
  }

  /** How many serial numbers a line has beyond the ones listed. */
  protected readonly notListed = serialsNotListed;

  ngOnInit(): void {
    if (typeof window !== 'undefined' && window.matchMedia) {
      const phone = window.matchMedia('(max-width: 640px)');
      const follow = (): void => this.narrow.set(phone.matches);
      follow();
      phone.addEventListener('change', follow);
      this.destroyRef.onDestroy(() => phone.removeEventListener('change', follow));
    }

    for (const name of Object.keys(this.filters.controls)) {
      const control: AbstractControl = this.filters.controls[name as keyof typeof this.filters.controls];
      control.valueChanges.subscribe(() => this.clearServerError(name));
    }

    void this.loadChoices();
    void this.run();
  }

  /** The words under a field: the server's refusal, else a missing required date once touched. */
  protected err(name: FilterName, label: string): string {
    const server = this.serverErrors()[name];
    if (server) {
      return server;
    }

    const control = this.filters.controls[name];
    if (!control.touched || !control.errors) {
      return '';
    }

    return control.errors['required'] ? `${label} is required.` : '';
  }

  /** Runs for the period and filters now chosen, from the first page. */
  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});

    try {
      this.result.set(await this.report.run(this.request(1)));
    } catch (err: unknown) {
      // Receipts from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of receipts to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMorePurchaseReceiveOrderDetails(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendPurchaseReceiveOrderDetailsPage(loaded, next));
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  private request(pageNumber: number) {
    const value = this.filters.getRawValue();
    return {
      fromDate: value.fromDate as string,
      toDate: value.toDate as string,
      contactId: value.contactId,
      itemId: value.itemId,
      warehouseId: value.warehouseId,
      purchaseOrderId: value.purchaseOrderId,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    const [vendors, items, warehouses, orders] = await Promise.all([
      this.report.vendors().catch(() => [] as AgedPayablesVendorChoice[]),
      this.report.items().catch(() => [] as PayableInvoiceItemChoice[]),
      this.report.warehouses().catch(() => [] as SalesAnalysisWarehouseChoice[]),
      this.report.purchaseOrders().catch(() => [] as PurchaseReceiveOrderDetailsOrderChoice[]),
    ]);
    this.vendors.set(vendors);
    this.items.set(items);
    this.warehouses.set(warehouses);
    this.orders.set(orders);
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Purchase Receive Order Details on this branch.');
      return;
    }

    const fieldErrors: Partial<Record<FilterName, string>> = {};
    for (const [key, words] of Object.entries(failure?.error?.errors ?? {})) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as FilterName;
      if (name in this.filters.controls && words?.[0]) {
        fieldErrors[name] = words[0];
      }
    }

    if (Object.keys(fieldErrors).length > 0) {
      this.serverErrors.set(fieldErrors);
      return;
    }

    this.failure.set(failure?.error?.message || 'The Purchase Receive Order Details could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
