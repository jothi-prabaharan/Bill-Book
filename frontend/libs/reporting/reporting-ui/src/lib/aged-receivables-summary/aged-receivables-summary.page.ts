import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AGE_BASIS_OPTIONS,
  AgeBasis,
  AgedReceivablesCustomerChoice,
  AgedReceivablesGridRow,
  AgedReceivablesSummaryResult,
  AgedReceivablesSummaryService,
  BUCKET_SIZES,
  agedReceivablesRows,
  agedReceivablesSentence,
  bucketOptionLabel,
  customerChoiceLabel,
  detailQueryParams,
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
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'asAtDate' | 'ageBy' | 'bucketDays' | 'contactId';

const DEFAULT_LABELS = ['Current', '1-30 days', '31-60 days', '61-90 days', 'Over 90 days'];

/**
 * The Aged Receivables Summary (TK-222): its own page on its own endpoint. What each
 * customer owed on a date and how overdue it was: Current and four overdue columns, a
 * row for each customer and the totals, in the branch's base currency. A credit note,
 * or a receipt not yet applied to an invoice, is a negative amount in Current, so a
 * customer's total is their receivable balance and the grand total is the Accounts
 * Receivable balance on the date.
 *
 * It opens as at today and runs straight away. The as-at date is required, so a missing
 * one shows at its field, as does any other refusal; a failure shows in the message box.
 * It can age by the due date or the invoice date, in columns of 30, 15 or 7 days, for one
 * customer or all, with the customers who owe nothing listed when asked. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-aged-receivables-summary-page',
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
  templateUrl: './aged-receivables-summary.page.html',
  styleUrl: './aged-receivables-summary.page.scss',
})
export class AgedReceivablesSummaryPage implements OnInit {
  private readonly report = inject(AgedReceivablesSummaryService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each customer is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    asAtDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    ageBy: new FormControl<AgeBasis | null>('DueDate'),
    bucketDays: new FormControl<number | null>(BUCKET_SIZES[0]),
    contactId: new FormControl<number | null>(null),
    showZeroBalances: new FormControl<boolean>(false, { nonNullable: true }),
  });

  protected readonly ageOptions: BbSelectOption<AgeBasis>[] = AGE_BASIS_OPTIONS.map((o) => ({ value: o.value, label: o.label }));
  protected readonly bucketOptions: BbSelectOption<number>[] = BUCKET_SIZES.map((days) => ({ value: days, label: bucketOptionLabel(days) }));

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<AgedReceivablesSummaryResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly rows = computed<AgedReceivablesGridRow[]>(() => {
    const result = this.result();
    return result ? agedReceivablesRows(result) : [];
  });

  /** The five headings the server named for the width in use. */
  protected readonly labels = computed<string[]>(() => {
    const labels = this.result()?.bucketLabels;
    return labels && labels.length === DEFAULT_LABELS.length ? labels : DEFAULT_LABELS;
  });

  protected readonly columns = computed<ColumnDef[]>(() => {
    const labels = this.labels();
    return [
      { field: 'customer', header: 'Customer' },
      { field: 'current', header: labels[0], numeric: true, width: '7.5rem' },
      { field: 'bucket1', header: labels[1], numeric: true, width: '7.5rem' },
      { field: 'bucket2', header: labels[2], numeric: true, width: '7.5rem' },
      { field: 'bucket3', header: labels[3], numeric: true, width: '7.5rem' },
      { field: 'bucket4', header: labels[4], numeric: true, width: '7.5rem' },
      { field: 'total', header: 'Total', numeric: true, width: '8rem' },
    ];
  });

  /** How the report is aged, for the line under the title. */
  protected readonly basisText = computed(() => (this.result()?.ageBy === 'InvoiceDate' ? 'invoice date' : 'due date'));

  /** The balance and how much of it is overdue, in words; a failure above it. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.customers.length > 0) {
      messages.push({ tone: 'info', text: agedReceivablesSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: AgedReceivablesGridRow): string => (row.kind === 'total' ? 'row--total' : '');

  /** Whether a row's total opens the detail: a customer's does, and so do the totals; what no customer claims has no filter to open it on. */
  protected opensDetail(row: AgedReceivablesGridRow): boolean {
    return row.kind !== 'unassigned';
  }

  /** The detail report for this row on the date, ageing and width the report was run with. */
  protected detailQuery(row: AgedReceivablesGridRow): Record<string, string | number> {
    const result = this.result();
    return result ? detailQueryParams(result, row.contactId) : {};
  }

  ngOnInit(): void {
    if (typeof window !== 'undefined' && window.matchMedia) {
      const phone = window.matchMedia('(max-width: 640px)');
      const follow = (): void => this.narrow.set(phone.matches);
      follow();
      phone.addEventListener('change', follow);
      this.destroyRef.onDestroy(() => phone.removeEventListener('change', follow));
    }

    for (const name of Object.keys(this.filters.controls) as (keyof typeof this.filters.controls)[]) {
      const control: AbstractControl = this.filters.controls[name];
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

  /** Runs for the date, ageing, column width and customer now chosen. */
  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});

    try {
      const value = this.filters.getRawValue();
      this.result.set(
        await this.report.run({
          asAtDate: value.asAtDate as string,
          ageBy: value.ageBy,
          bucketDays: value.bucketDays,
          contactId: value.contactId,
          showZeroBalances: value.showZeroBalances,
        }),
      );
    } catch (err: unknown) {
      // Figures from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
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
      this.failure.set('You do not have access to the Aged Receivables Summary on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Aged Receivables Summary could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
