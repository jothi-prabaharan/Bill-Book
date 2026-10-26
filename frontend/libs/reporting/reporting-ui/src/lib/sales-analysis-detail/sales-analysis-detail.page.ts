import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedReceivablesCustomerChoice,
  ReceivableInvoiceCategoryChoice,
  ReceivableInvoiceItemChoice,
  SALES_ANALYSIS_GROUPINGS,
  SalesAnalysisDetailGridRow,
  SalesAnalysisDetailResult,
  SalesAnalysisDetailService,
  SalesAnalysisGroupBy,
  SalesAnalysisWarehouseChoice,
  appendSalesAnalysisDetailPage,
  categoryChoiceLabel,
  customerChoiceLabel,
  hasMoreSalesAnalysisDetail,
  itemChoiceLabel,
  loadedDetailLines,
  salesAnalysisDetailRows,
  salesAnalysisDetailSentence,
  warehouseChoiceLabel,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  CheckboxComponent,
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

type FilterName = 'fromDate' | 'toDate' | 'groupBy' | 'itemId' | 'itemCategoryId' | 'contactId' | 'warehouseId';

/**
 * The Sales Analysis Detail Report (TK-229): its own page on its own endpoint. The Sales Analysis
 * before it is added up: one row for each invoice or credit note line sold in a period, in groups by
 * item, item category or customer, each group with its subtotal and the total of everything the
 * filters match at the foot. A page of lines loads at a time, and a group that runs over the break
 * carries on under the same heading.
 *
 * It opens on the financial year to date, by item, and runs straight away. A credit note's lines are
 * negative, and can be left out. A line whose cost has not been settled yet is marked pending and has
 * no margin, and neither has its group. Both dates are required, so a missing or out-of-order one
 * shows at its field, as does any other refusal; a failure shows in the message box. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-sales-analysis-detail-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BranchDateInputComponent,
    SelectComponent,
    CheckboxComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './sales-analysis-detail.page.html',
  styleUrl: './sales-analysis-detail.page.scss',
})
export class SalesAnalysisDetailPage implements OnInit {
  private readonly report = inject(SalesAnalysisDetailService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each group is a card rather than a run of rows and columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    groupBy: new FormControl<SalesAnalysisGroupBy>('Item', { nonNullable: true }),
    itemId: new FormControl<number | null>(null),
    itemCategoryId: new FormControl<number | null>(null),
    contactId: new FormControl<number | null>(null),
    warehouseId: new FormControl<number | null>(null),
    includeCreditNotes: new FormControl<boolean>(true, { nonNullable: true }),
  });

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly items = signal<ReceivableInvoiceItemChoice[]>([]);
  protected readonly categories = signal<ReceivableInvoiceCategoryChoice[]>([]);
  protected readonly warehouses = signal<SalesAnalysisWarehouseChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<SalesAnalysisDetailResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly groupingOptions: BbSelectOption<SalesAnalysisGroupBy>[] = SALES_ANALYSIS_GROUPINGS.map((g) => ({ value: g.value, label: g.label }));

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly itemOptions = computed<BbSelectOption<number>[]>(() =>
    this.items().map((i) => ({ value: i.itemId, label: itemChoiceLabel(i) })),
  );

  protected readonly categoryOptions = computed<BbSelectOption<number>[]>(() =>
    this.categories().map((c) => ({ value: c.itemCategoryId, label: categoryChoiceLabel(c) })),
  );

  protected readonly warehouseOptions = computed<BbSelectOption<number>[]>(() =>
    this.warehouses().map((w) => ({ value: w.warehouseId, label: warehouseChoiceLabel(w) })),
  );

  protected readonly rows = computed<SalesAnalysisDetailGridRow[]>(() => {
    const result = this.result();
    return result ? salesAnalysisDetailRows(result) : [];
  });

  /** The cards of the phone layout: a group's heading, lines and subtotal together; the total last. */
  protected readonly cards = computed<SalesAnalysisDetailGridRow[][]>(() => {
    const cards: SalesAnalysisDetailGridRow[][] = [];
    for (const row of this.rows()) {
      if (row.kind === 'group' || row.kind === 'total' || cards.length === 0) {
        cards.push([row]);
      } else {
        cards[cards.length - 1].push(row);
      }
    }
    return cards;
  });

  protected readonly loaded = computed(() => {
    const result = this.result();
    return result ? loadedDetailLines(result) : 0;
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreSalesAnalysisDetail(result) : false;
  });

  /** What the lines of a row are told apart by besides the document: the customer, or the item when grouped by customer. */
  protected readonly whoHeading = computed(() => (this.result()?.groupBy === 'Customer' ? 'Item' : 'Customer'));

  protected readonly columns = computed<ColumnDef[]>(() => [
    { field: 'date', header: 'Date', width: '6.5rem' },
    { field: 'document', header: 'Document', width: '8rem' },
    { field: 'who', header: this.whoHeading() },
    { field: 'quantity', header: 'Qty', numeric: true, width: '4.5rem' },
    { field: 'unitPrice', header: 'Unit price', numeric: true, width: '6.5rem' },
    { field: 'discount', header: 'Discount', numeric: true, width: '6rem' },
    { field: 'net', header: 'Net sales', numeric: true, width: '7rem' },
    { field: 'cost', header: 'Cost', numeric: true, width: '7rem' },
    { field: 'margin', header: 'Margin', numeric: true, width: '7rem' },
  ]);

  /** The period's totals in words; a failure above them. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalLines > 0) {
      messages.push({ tone: 'info', text: salesAnalysisDetailSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: SalesAnalysisDetailGridRow): string =>
    row.kind === 'group' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'total' ? 'row--total' : '';

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
      // Lines from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of lines to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreSalesAnalysisDetail(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendSalesAnalysisDetailPage(loaded, next));
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
      groupBy: value.groupBy,
      itemId: value.itemId,
      itemCategoryId: value.itemCategoryId,
      contactId: value.contactId,
      warehouseId: value.warehouseId,
      includeCreditNotes: value.includeCreditNotes,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    const [customers, items, categories, warehouses] = await Promise.all([
      this.report.customers().catch(() => [] as AgedReceivablesCustomerChoice[]),
      this.report.items().catch(() => [] as ReceivableInvoiceItemChoice[]),
      this.report.itemCategories().catch(() => [] as ReceivableInvoiceCategoryChoice[]),
      this.report.warehouses().catch(() => [] as SalesAnalysisWarehouseChoice[]),
    ]);
    this.customers.set(customers);
    this.items.set(items);
    this.categories.set(categories);
    this.warehouses.set(warehouses);
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Sales Analysis Detail Report on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Sales Analysis Detail Report could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
