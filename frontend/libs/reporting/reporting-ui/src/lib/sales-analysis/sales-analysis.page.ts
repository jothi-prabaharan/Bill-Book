import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedReceivablesCustomerChoice,
  ReceivableInvoiceCategoryChoice,
  SALES_ANALYSIS_GROUPINGS,
  SalesAnalysisGridRow,
  SalesAnalysisGroupBy,
  SalesAnalysisResult,
  SalesAnalysisService,
  SalesAnalysisWarehouseChoice,
  appendSalesAnalysisPage,
  categoryChoiceLabel,
  costPendingText,
  customerChoiceLabel,
  groupingHeading,
  hasMoreSalesAnalysis,
  previousPeriod,
  salesAnalysisRows,
  salesAnalysisSentence,
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

type FilterName = 'fromDate' | 'toDate' | 'groupBy' | 'contactId' | 'itemCategoryId' | 'warehouseId' | 'compareFromDate' | 'compareToDate';

/**
 * The Sales Analysis (TK-228): its own page on its own endpoint. What sold in a period, added up by
 * item, item category or customer, largest net sales first: the units, gross value, discount, net
 * sales (before tax), cost of sales and margin, in the branch's currency, with the totals of
 * everything the filters match under it. A page of groups loads at a time.
 *
 * It opens on the financial year to date, by item, and runs straight away. Credit notes reduce the
 * figures unless the tick is off. A second period can be compared with: it adds each group's net sales
 * then and how they changed. Cost comes from the stock movements the sales caused; a line whose cost
 * has not been settled yet is counted and said to be pending, and its group has no margin until it is.
 * Both dates are required, so a missing or out-of-order one shows at its field, as does any other
 * refusal; a failure shows in the message box. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-sales-analysis-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    CheckboxComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './sales-analysis.page.html',
  styleUrl: './sales-analysis.page.scss',
})
export class SalesAnalysisPage implements OnInit {
  private readonly report = inject(SalesAnalysisService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each group is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    groupBy: new FormControl<SalesAnalysisGroupBy>('Item', { nonNullable: true }),
    contactId: new FormControl<number | null>(null),
    itemCategoryId: new FormControl<number | null>(null),
    warehouseId: new FormControl<number | null>(null),
    includeCreditNotes: new FormControl<boolean>(true, { nonNullable: true }),
    compare: new FormControl<boolean>(false, { nonNullable: true }),
    compareFromDate: new FormControl<string | null>(null),
    compareToDate: new FormControl<string | null>(null),
  });

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly categories = signal<ReceivableInvoiceCategoryChoice[]>([]);
  protected readonly warehouses = signal<SalesAnalysisWarehouseChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<SalesAnalysisResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  /** Whether the compare tick is on, so its two dates show. */
  protected readonly comparing = signal(false);

  protected readonly groupingOptions: BbSelectOption<SalesAnalysisGroupBy>[] = SALES_ANALYSIS_GROUPINGS.map((g) => ({ value: g.value, label: g.label }));

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly categoryOptions = computed<BbSelectOption<number>[]>(() =>
    this.categories().map((c) => ({ value: c.itemCategoryId, label: categoryChoiceLabel(c) })),
  );

  protected readonly warehouseOptions = computed<BbSelectOption<number>[]>(() =>
    this.warehouses().map((w) => ({ value: w.warehouseId, label: warehouseChoiceLabel(w) })),
  );

  protected readonly rows = computed<SalesAnalysisGridRow[]>(() => {
    const result = this.result();
    return result ? salesAnalysisRows(result) : [];
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreSalesAnalysis(result) : false;
  });

  /** Whether the answer on show carries a comparison period. */
  protected readonly compared = computed(() => this.result()?.compareFromDate != null);

  protected readonly columns = computed<ColumnDef[]>(() => {
    const columns: ColumnDef[] = [
      { field: 'name', header: groupingHeading(this.result()?.groupBy ?? 'Item') },
      { field: 'quantity', header: 'Qty', numeric: true, width: '5rem' },
      { field: 'gross', header: 'Gross', numeric: true, width: '7rem' },
      { field: 'discount', header: 'Discount', numeric: true, width: '6rem' },
      { field: 'net', header: 'Net sales', numeric: true, width: '7rem' },
      { field: 'cost', header: 'Cost', numeric: true, width: '7rem' },
      { field: 'margin', header: 'Margin', numeric: true, width: '7rem' },
      { field: 'marginPercent', header: 'Margin %', numeric: true, width: '5.5rem' },
    ];

    if (this.compared()) {
      columns.push(
        { field: 'compareNet', header: 'Net then', numeric: true, width: '7rem' },
        { field: 'changePercent', header: 'Change', numeric: true, width: '5.5rem' },
      );
    }

    return columns;
  });

  /** The period's totals in words; a failure above them. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalGroups > 0) {
      messages.push({ tone: 'info', text: salesAnalysisSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: SalesAnalysisGridRow): string => (row.kind === 'total' ? 'row--total' : '');

  protected readonly pendingText = costPendingText;

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

    this.filters.controls.compare.valueChanges.subscribe((on) => this.toggleCompare(on));

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

  /** Turns the comparison on or off; on, it starts with the period just before this one. */
  private toggleCompare(on: boolean): void {
    this.comparing.set(on);
    const compareFrom = this.filters.controls.compareFromDate;
    const compareTo = this.filters.controls.compareToDate;

    if (!on) {
      compareFrom.setValidators(null);
      compareTo.setValidators(null);
      compareFrom.setValue(null);
      compareTo.setValue(null);
      return;
    }

    compareFrom.setValidators(Validators.required);
    compareTo.setValidators(Validators.required);
    const value = this.filters.getRawValue();
    const previous = value.fromDate && value.toDate && value.toDate >= value.fromDate ? previousPeriod(value.fromDate, value.toDate) : null;
    compareFrom.setValue(previous?.from ?? null);
    compareTo.setValue(previous?.to ?? null);
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
      // Groups from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of groups to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreSalesAnalysis(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendSalesAnalysisPage(loaded, next));
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
      contactId: value.contactId,
      itemCategoryId: value.itemCategoryId,
      warehouseId: value.warehouseId,
      includeCreditNotes: value.includeCreditNotes,
      compareFromDate: value.compare ? value.compareFromDate : null,
      compareToDate: value.compare ? value.compareToDate : null,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    const [customers, categories, warehouses] = await Promise.all([
      this.report.customers().catch(() => [] as AgedReceivablesCustomerChoice[]),
      this.report.itemCategories().catch(() => [] as ReceivableInvoiceCategoryChoice[]),
      this.report.warehouses().catch(() => [] as SalesAnalysisWarehouseChoice[]),
    ]);
    this.customers.set(customers);
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
      this.failure.set('You do not have access to the Sales Analysis on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Sales Analysis could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
