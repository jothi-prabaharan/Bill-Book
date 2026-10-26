import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedReceivablesCustomerChoice,
  ReceivableInvoiceCurrencyChoice,
  ReceivableInvoiceGridRow,
  ReceivableInvoiceStatus,
  ReceivableInvoiceSummaryResult,
  ReceivableInvoiceSummaryService,
  appendInvoicePage,
  customerChoiceLabel,
  hasMoreInvoices,
  receivableInvoiceRows,
  receivableInvoiceSentence,
  receivableStatusLabel,
  receivableStatusTabs,
  receivableStatusTag,
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
 * The Receivable Invoice Summary (TK-224): its own page on its own endpoint. Every posted sales
 * invoice dated in a period, newest first, with its taxable value, tax and total, what has been
 * received and what is still due, in the branch's base currency, with the status tabs above it
 * counting every status and the totals of every invoice the filters match under it. A page of
 * invoices loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a
 * missing or out-of-order one shows at its field, as does any other refusal; a failure shows in the
 * message box. Received and due are as at the To date, so a past period reads the same when it is
 * run again. Credit notes are listed, as negatives, only when the tick is on. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-receivable-invoice-summary-page',
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
  templateUrl: './receivable-invoice-summary.page.html',
  styleUrl: './receivable-invoice-summary.page.scss',
})
export class ReceivableInvoiceSummaryPage implements OnInit {
  private readonly report = inject(ReceivableInvoiceSummaryService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each invoice is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    currencyCode: new FormControl<string | null>(null),
    includeCreditNotes: new FormControl<boolean>(false, { nonNullable: true }),
  });

  /** The status tab chosen; empty is All. */
  protected readonly status = signal<ReceivableInvoiceStatus | ''>('');

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly currencies = signal<ReceivableInvoiceCurrencyChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<ReceivableInvoiceSummaryResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly currencyOptions = computed<BbSelectOption<string>[]>(() =>
    this.currencies().map((c) => ({ value: c.currencyCode, label: c.currencyCode })),
  );

  protected readonly tabs = computed(() => receivableStatusTabs(this.result()?.statusCounts));

  protected readonly rows = computed<ReceivableInvoiceGridRow[]>(() => {
    const result = this.result();
    return result ? receivableInvoiceRows(result) : [];
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreInvoices(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'document', header: 'Invoice', width: '9rem' },
    { field: 'documentDate', header: 'Date', width: '6.5rem' },
    { field: 'dueDate', header: 'Due', width: '6.5rem' },
    { field: 'customer', header: 'Customer' },
    { field: 'currencyCode', header: 'Currency', width: '5rem' },
    { field: 'taxable', header: 'Taxable', numeric: true, width: '7.5rem' },
    { field: 'tax', header: 'Tax', numeric: true, width: '7rem' },
    { field: 'total', header: 'Total', numeric: true, width: '8rem' },
    { field: 'received', header: 'Received', numeric: true, width: '7.5rem' },
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
      messages.push({ tone: 'info', text: receivableInvoiceSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: ReceivableInvoiceGridRow): string => (row.kind === 'total' ? 'row--total' : '');

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
    if (!loaded || this.busy() || !hasMoreInvoices(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendInvoicePage(loaded, next));
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
      includeCreditNotes: value.includeCreditNotes,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    const [customers, currencies] = await Promise.all([
      this.report.customers().catch(() => [] as AgedReceivablesCustomerChoice[]),
      this.report.currencies().catch(() => [] as ReceivableInvoiceCurrencyChoice[]),
    ]);
    this.customers.set(customers);
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
      this.failure.set('You do not have access to the Receivable Invoice Summary on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Receivable Invoice Summary could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
