import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedReceivablesCustomerChoice,
  ReceivableDetailGridRow,
  ReceivableInvoiceCategoryChoice,
  ReceivableInvoiceDetailResult,
  ReceivableInvoiceDetailService,
  ReceivableInvoiceItemChoice,
  ReceivableInvoiceStatus,
  appendDetailPage,
  categoryChoiceLabel,
  customerChoiceLabel,
  hasMoreDetail,
  itemChoiceLabel,
  receivableDetailRows,
  receivableDetailSentence,
  receivableStatusLabel,
  receivableStatusTabs,
  receivableStatusTag,
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
  StatusTabsComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'contactId' | 'itemId' | 'itemCategoryId';

/**
 * The Receivable Invoice Detail (TK-225): its own page on its own endpoint. The Receivable Invoice
 * Summary line by line: every posted sales invoice dated in a period, newest first, each with the
 * items, quantities, prices, discounts and taxes it is made of, and its total, with the status tabs
 * above it counting every status and the totals of every invoice the filters match under it. A page
 * of invoices loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a
 * missing or out-of-order one shows at its field, as does any other refusal; a failure shows in the
 * message box. Choosing an item or a category narrows the lines as well as the invoices, and the
 * page says so. A foreign-currency invoice's amounts carry its currency code. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-receivable-invoice-detail-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BranchDateInputComponent,
    SelectComponent,
    StatusTabsComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './receivable-invoice-detail.page.html',
  styleUrl: './receivable-invoice-detail.page.scss',
})
export class ReceivableInvoiceDetailPage implements OnInit {
  private readonly report = inject(ReceivableInvoiceDetailService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each invoice is a card rather than a run of rows and columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    itemId: new FormControl<number | null>(null),
    itemCategoryId: new FormControl<number | null>(null),
  });

  /** The status tab chosen; empty is All. */
  protected readonly status = signal<ReceivableInvoiceStatus | ''>('');

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly items = signal<ReceivableInvoiceItemChoice[]>([]);
  protected readonly categories = signal<ReceivableInvoiceCategoryChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<ReceivableInvoiceDetailResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly itemOptions = computed<BbSelectOption<number>[]>(() =>
    this.items().map((i) => ({ value: i.itemId, label: itemChoiceLabel(i) })),
  );

  protected readonly categoryOptions = computed<BbSelectOption<number>[]>(() =>
    this.categories().map((c) => ({ value: c.itemCategoryId, label: categoryChoiceLabel(c) })),
  );

  protected readonly tabs = computed(() => receivableStatusTabs(this.result()?.statusCounts));

  protected readonly rows = computed<ReceivableDetailGridRow[]>(() => {
    const result = this.result();
    return result ? receivableDetailRows(result) : [];
  });

  /** The cards of the phone layout: an invoice's heading, lines and subtotal together; the totals last. */
  protected readonly groups = computed<ReceivableDetailGridRow[][]>(() => {
    const groups: ReceivableDetailGridRow[][] = [];
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
    return result ? hasMoreDetail(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'item', header: 'Invoice / item', width: '16rem' },
    { field: 'quantity', header: 'Qty', numeric: true, width: '5.5rem' },
    { field: 'unitPrice', header: 'Unit price', numeric: true, width: '6.5rem' },
    { field: 'discount', header: 'Discount', numeric: true, width: '6.5rem' },
    { field: 'taxable', header: 'Taxable', numeric: true, width: '7.5rem' },
    { field: 'tax', header: 'Tax', numeric: true, width: '7rem' },
    { field: 'total', header: 'Total', numeric: true, width: '8.5rem' },
  ];

  /** The period's totals in words; a failure above them. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalInvoices > 0) {
      messages.push({ tone: 'info', text: receivableDetailSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: ReceivableDetailGridRow): string =>
    row.kind === 'heading' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'total' ? 'row--total' : '';

  /** A quantity and a unit price in the branch's own decimals. */
  protected quantity(value: number | null): string {
    return this.formats.formatQuantity(value);
  }

  protected unitPrice(value: number | null): string {
    return this.formats.formatUnitPrice(value);
  }

  protected readonly statusLabel = receivableStatusLabel;
  protected readonly statusTag = receivableStatusTag;

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

  /** Chooses a status tab and runs again from the first page. */
  protected chooseStatus(value: string): void {
    this.status.set(value as ReceivableInvoiceStatus | '');
    void this.run();
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
      // Invoices from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of invoices to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreDetail(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendDetailPage(loaded, next));
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
      itemCategoryId: value.itemCategoryId,
      status: this.status() || null,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    const [customers, items, categories] = await Promise.all([
      this.report.customers().catch(() => [] as AgedReceivablesCustomerChoice[]),
      this.report.items().catch(() => [] as ReceivableInvoiceItemChoice[]),
      this.report.itemCategories().catch(() => [] as ReceivableInvoiceCategoryChoice[]),
    ]);
    this.customers.set(customers);
    this.items.set(items);
    this.categories.set(categories);
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Receivable Invoice Detail on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Receivable Invoice Detail could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
