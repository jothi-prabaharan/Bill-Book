import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedReceivablesCustomerChoice,
  InvoiceTrackGridRow,
  InvoiceTrackResult,
  InvoiceTrackService,
  InvoiceTrackStatus,
  appendTrackPage,
  customerChoiceLabel,
  hasMoreTrack,
  invoiceTrackRows,
  invoiceTrackSentence,
  invoiceTrackTabs,
  trackStatusLabel,
  trackStatusTag,
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

type FilterName = 'fromDate' | 'toDate' | 'contactId';

/**
 * The Invoice Track Report (TK-226): its own page on its own endpoint. Where each sales invoice
 * dated in a period is in its life (a draft, ready to post, awaiting payment, partly paid, overdue,
 * paid or void), newest first, with the day it was posted, its due date, the last settlement of it
 * and what is still due, the status tabs above it counting every status and the totals of every
 * posted invoice the filters match under it. A page of invoices loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a
 * missing or out-of-order one shows at its field, as does any other refusal; a failure shows in the
 * message box. Received and due are as at the To date, so a past period reads the same when it is run
 * again. A draft is not in the books: it owes nothing and is left out of the totals. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-invoice-track-page',
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
  templateUrl: './invoice-track.page.html',
  styleUrl: './invoice-track.page.scss',
})
export class InvoiceTrackPage implements OnInit {
  private readonly report = inject(InvoiceTrackService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each invoice is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
  });

  /** The status tab chosen; empty is All. */
  protected readonly status = signal<InvoiceTrackStatus | ''>('');

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<InvoiceTrackResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly tabs = computed(() => invoiceTrackTabs(this.result()?.statusCounts));

  protected readonly rows = computed<InvoiceTrackGridRow[]>(() => {
    const result = this.result();
    return result ? invoiceTrackRows(result) : [];
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreTrack(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'document', header: 'Invoice', width: '8.5rem' },
    { field: 'customer', header: 'Customer' },
    { field: 'status', header: 'Status', width: '8.5rem' },
    { field: 'postedOn', header: 'Posted', width: '6.5rem' },
    { field: 'dueDate', header: 'Due date', width: '6.5rem' },
    { field: 'lastPaymentDate', header: 'Last payment', width: '6.5rem' },
    { field: 'total', header: 'Total', numeric: true, width: '8rem' },
    { field: 'due', header: 'Due', numeric: true, width: '7.5rem' },
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
      messages.push({ tone: 'info', text: invoiceTrackSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: InvoiceTrackGridRow): string => (row.kind === 'total' ? 'row--total' : '');

  protected readonly statusLabel = trackStatusLabel;
  protected readonly statusTag = trackStatusTag;

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
    this.status.set(value as InvoiceTrackStatus | '');
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
    if (!loaded || this.busy() || !hasMoreTrack(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendTrackPage(loaded, next));
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
      status: this.status() || null,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    this.customers.set(await this.report.customers().catch(() => [] as AgedReceivablesCustomerChoice[]));
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Invoice Track Report on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Invoice Track Report could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
