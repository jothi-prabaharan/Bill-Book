import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedPayablesVendorChoice,
  PayableInvoiceCurrencyChoice,
  PayableInvoiceGridRow,
  PayableInvoiceStatus,
  PayableInvoiceSummaryResult,
  PayableInvoiceSummaryService,
  appendPayablePage,
  hasMorePayable,
  payableInvoiceRows,
  payableInvoiceSentence,
  payableStatusLabel,
  payableStatusTabs,
  payableStatusTag,
  vendorChoiceLabel,
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
  StatusTabsComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'contactId' | 'currencyCode';

/**
 * The Payable Invoice Summary (TK-235): its own page on its own endpoint. Every posted vendor bill
 * dated in a period, newest first, with its taxable value, tax and total, what has been paid and what
 * is still due, in the branch's base currency, with the status tabs above it counting every status and
 * the totals of every bill the filters match under it. A page of bills loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a missing
 * or out-of-order one shows at its field, as does any other refusal; a failure shows in the message box.
 * Paid and due are as at the To date, so a past period reads the same when it is run again. What the
 * vendor is owed is the total less TDS withheld and, on a reverse-charge bill, the tax, and the page says
 * so where it differs. Debit notes are listed, as negatives, only when the tick is on. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-payable-invoice-summary-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BranchDateInputComponent,
    SelectComponent,
    CheckboxComponent,
    StatusTabsComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './payable-invoice-summary.page.html',
  styleUrl: './payable-invoice-summary.page.scss',
})
export class PayableInvoiceSummaryPage implements OnInit {
  private readonly report = inject(PayableInvoiceSummaryService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each bill is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    currencyCode: new FormControl<string | null>(null),
    includeDebitNotes: new FormControl<boolean>(false, { nonNullable: true }),
  });

  /** The status tab chosen; empty is All. */
  protected readonly status = signal<PayableInvoiceStatus | ''>('');

  protected readonly vendors = signal<AgedPayablesVendorChoice[]>([]);
  protected readonly currencies = signal<PayableInvoiceCurrencyChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<PayableInvoiceSummaryResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly vendorOptions = computed<BbSelectOption<number>[]>(() =>
    this.vendors().map((c) => ({ value: c.contactId, label: vendorChoiceLabel(c) })),
  );

  protected readonly currencyOptions = computed<BbSelectOption<string>[]>(() =>
    this.currencies().map((c) => ({ value: c.currencyCode, label: c.currencyCode })),
  );

  protected readonly tabs = computed(() => payableStatusTabs(this.result()?.statusCounts));

  protected readonly rows = computed<PayableInvoiceGridRow[]>(() => {
    const result = this.result();
    return result ? payableInvoiceRows(result) : [];
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMorePayable(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'document', header: 'Bill', width: '9rem' },
    { field: 'documentDate', header: 'Date', width: '6.5rem' },
    { field: 'dueDate', header: 'Due date', width: '6.5rem' },
    { field: 'vendor', header: 'Vendor' },
    { field: 'currencyCode', header: 'Currency', width: '5rem' },
    { field: 'taxable', header: 'Taxable', numeric: true, width: '7.5rem' },
    { field: 'tax', header: 'Tax', numeric: true, width: '7rem' },
    { field: 'total', header: 'Total', numeric: true, width: '8.5rem' },
    { field: 'paid', header: 'Paid', numeric: true, width: '7.5rem' },
    { field: 'due', header: 'Due', numeric: true, width: '7.5rem' },
    { field: 'status', header: 'Status', width: '9rem' },
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
      messages.push({ tone: 'info', text: payableInvoiceSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: PayableInvoiceGridRow): string => (row.kind === 'total' ? 'row--total' : '');

  protected readonly statusLabel = payableStatusLabel;
  protected readonly statusTag = payableStatusTag;

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
    this.status.set(value as PayableInvoiceStatus | '');
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
      // Bills from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of bills to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMorePayable(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendPayablePage(loaded, next));
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
      currencyCode: value.currencyCode,
      status: this.status() || null,
      includeDebitNotes: value.includeDebitNotes,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    const [vendors, currencies] = await Promise.all([
      this.report.vendors().catch(() => [] as AgedPayablesVendorChoice[]),
      this.report.currencies().catch(() => [] as PayableInvoiceCurrencyChoice[]),
    ]);
    this.vendors.set(vendors);
    this.currencies.set(currencies);
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Payable Invoice Summary on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Payable Invoice Summary could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
