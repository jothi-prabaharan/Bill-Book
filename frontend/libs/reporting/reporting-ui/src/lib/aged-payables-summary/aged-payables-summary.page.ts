import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgeBasis,
  AgedPayablesGridRow,
  AgedPayablesSummaryResult,
  AgedPayablesSummaryService,
  AgedPayablesVendorChoice,
  BUCKET_SIZES,
  MSME_AGING_LINK,
  PAYABLE_AGE_BASIS_OPTIONS,
  agedPayablesRows,
  agedPayablesSentence,
  bucketOptionLabel,
  msmeCount,
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
  UiMessage,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'asAtDate' | 'ageBy' | 'bucketDays' | 'contactId';

const DEFAULT_LABELS = ['Current', '1-30 days', '31-60 days', '61-90 days', 'Over 90 days'];

/**
 * The Aged Payables Summary (TK-233): its own page on its own endpoint. What the branch owed
 * each vendor on a date and how overdue it was: Current and four overdue columns, a row for
 * each vendor and the totals, in the branch's base currency. A debit note, or a payment not
 * yet applied to a bill, is a negative amount in Current, so a vendor's total is what the
 * branch owes them and the grand total is the Accounts Payable balance on the date. A vendor
 * that is a micro or small enterprise is flagged, with a link to the Sec 43B(h) view.
 *
 * It opens as at today and runs straight away. The as-at date is required, so a missing one
 * shows at its field, as does any other refusal; a failure shows in the message box. It can
 * age by the due date or the bill date, in columns of 30, 15 or 7 days, for one vendor or
 * all, with the vendors owed nothing listed when asked. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-aged-payables-summary-page',
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
  templateUrl: './aged-payables-summary.page.html',
  styleUrl: './aged-payables-summary.page.scss',
})
export class AgedPayablesSummaryPage implements OnInit {
  private readonly report = inject(AgedPayablesSummaryService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each vendor is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    asAtDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    ageBy: new FormControl<AgeBasis | null>('DueDate'),
    bucketDays: new FormControl<number | null>(BUCKET_SIZES[0]),
    contactId: new FormControl<number | null>(null),
    showZeroBalances: new FormControl<boolean>(false, { nonNullable: true }),
  });

  protected readonly ageOptions: BbSelectOption<AgeBasis>[] = PAYABLE_AGE_BASIS_OPTIONS.map((o) => ({ value: o.value, label: o.label }));
  protected readonly bucketOptions: BbSelectOption<number>[] = BUCKET_SIZES.map((days) => ({ value: days, label: bucketOptionLabel(days) }));

  protected readonly vendors = signal<AgedPayablesVendorChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<AgedPayablesSummaryResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly vendorOptions = computed<BbSelectOption<number>[]>(() =>
    this.vendors().map((c) => ({ value: c.contactId, label: vendorChoiceLabel(c) })),
  );

  protected readonly rows = computed<AgedPayablesGridRow[]>(() => {
    const result = this.result();
    return result ? agedPayablesRows(result) : [];
  });

  /** How many of the vendors listed are MSMEs, for the note that explains the flag. */
  protected readonly msmes = computed(() => {
    const result = this.result();
    return result ? msmeCount(result) : 0;
  });

  protected readonly msmeLink = MSME_AGING_LINK;

  /** The five headings the server named for the width in use. */
  protected readonly labels = computed<string[]>(() => {
    const labels = this.result()?.bucketLabels;
    return labels && labels.length === DEFAULT_LABELS.length ? labels : DEFAULT_LABELS;
  });

  protected readonly columns = computed<ColumnDef[]>(() => {
    const labels = this.labels();
    return [
      { field: 'vendor', header: 'Vendor' },
      { field: 'current', header: labels[0], numeric: true, width: '7.5rem' },
      { field: 'bucket1', header: labels[1], numeric: true, width: '7.5rem' },
      { field: 'bucket2', header: labels[2], numeric: true, width: '7.5rem' },
      { field: 'bucket3', header: labels[3], numeric: true, width: '7.5rem' },
      { field: 'bucket4', header: labels[4], numeric: true, width: '7.5rem' },
      { field: 'total', header: 'Total', numeric: true, width: '8rem' },
    ];
  });

  /** How the report is aged, for the line under the title. */
  protected readonly basisText = computed(() => (this.result()?.ageBy === 'InvoiceDate' ? 'bill date' : 'due date'));

  /** The balance and how much of it is overdue, in words; a failure above it. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.vendors.length > 0) {
      messages.push({ tone: 'info', text: agedPayablesSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: AgedPayablesGridRow): string => (row.kind === 'total' ? 'row--total' : '');

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

  /** Runs for the date, ageing, column width and vendor now chosen. */
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
    this.vendors.set(await this.report.vendors().catch(() => [] as AgedPayablesVendorChoice[]));
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Aged Payables Summary on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Aged Payables Summary could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
