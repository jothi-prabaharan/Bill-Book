import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AgedReceivablesCustomerChoice,
  QuoteOutcome,
  QuotationTrackGridRow,
  QuotationTrackResult,
  QuotationTrackService,
  appendQuotationTrackPage,
  conversionRateLabel,
  customerChoiceLabel,
  daysToConvertLabel,
  hasMoreQuotationTrack,
  quoteOutcomeLabel,
  quoteOutcomeTag,
  quotationTrackRows,
  quotationTrackTabs,
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
 * The Quotation Track Report (TK-231): its own page on its own endpoint. What became of each quote
 * dated in a period (still open, expired, accepted, converted to an order or an invoice, or declined),
 * newest first, with the day it lapses, the order or invoice it became and how many days that took, the
 * outcome tabs above it counting every outcome, and a strip of how many quotes were raised, how many
 * converted and the conversion rate by count and by value. A page of quotes loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a missing
 * or out-of-order one shows at its field, as does any other refusal; a failure shows in the message box.
 * Outcomes are as they stand today, whatever the period: a conversion after the period counts, and a quote
 * past its valid-until date is expired. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-quotation-track-page',
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
  templateUrl: './quotation-track.page.html',
  styleUrl: './quotation-track.page.scss',
})
export class QuotationTrackPage implements OnInit {
  private readonly report = inject(QuotationTrackService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each quote is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
  });

  /** The outcome tab chosen; empty is All. */
  protected readonly outcome = signal<QuoteOutcome | ''>('');

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<QuotationTrackResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly tabs = computed(() => quotationTrackTabs(this.result()?.outcomeCounts));

  protected readonly rows = computed<QuotationTrackGridRow[]>(() => {
    const result = this.result();
    return result ? quotationTrackRows(result) : [];
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreQuotationTrack(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'quote', header: 'Quote', width: '8.5rem' },
    { field: 'customer', header: 'Customer' },
    { field: 'validUntil', header: 'Valid until', width: '6.5rem' },
    { field: 'outcome', header: 'Status', width: '10.5rem' },
    { field: 'convertedTo', header: 'Converted to', width: '9rem' },
    { field: 'days', header: 'Days to convert', numeric: true, width: '6.5rem' },
    { field: 'total', header: 'Value', numeric: true, width: '8.5rem' },
  ];

  /** A failure above the list. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    return failure ? [{ tone: 'error', text: failure }] : [];
  });

  protected readonly rowClass = (row: QuotationTrackGridRow): string => (row.kind === 'total' ? 'row--total' : '');

  protected readonly outcomeLabel = quoteOutcomeLabel;
  protected readonly outcomeTag = quoteOutcomeTag;
  protected readonly rate = conversionRateLabel;
  protected readonly daysLabel = daysToConvertLabel;

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

  /** Chooses an outcome tab and runs again from the first page. */
  protected chooseOutcome(value: string): void {
    this.outcome.set(value as QuoteOutcome | '');
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
      // Quotes from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of quotes to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreQuotationTrack(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendQuotationTrackPage(loaded, next));
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
      outcome: this.outcome() || null,
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
      this.failure.set('You do not have access to the Quotation Track Report on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Quotation Track Report could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
