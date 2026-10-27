import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgeBasis,
  AgedPayablesDetailsGridRow,
  AgedPayablesDetailsResult,
  AgedPayablesDetailsService,
  AgedPayablesVendorChoice,
  BUCKET_SIZES,
  MSME_AGING_LINK,
  PAYABLE_AGE_BASIS_OPTIONS,
  agedPayablesDetailsRows,
  agedPayablesDetailsSentence,
  bucketOptionLabel,
  detailQueryFrom,
  vendorChoiceLabel,
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
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'asAtDate' | 'ageBy' | 'bucketDays' | 'contactId';

const DEFAULT_LABELS = ['Current', '1-30 days', '31-60 days', '61-90 days', 'Over 90 days'];

/**
 * The Aged Payables Details (TK-234): its own page on its own endpoint. The Aged Payables Summary one
 * document at a time: for each vendor, every bill, debit note, payment and the rest still open on the
 * date, with what it came to, what has been applied to it, what is left, and the amount in the column
 * its age gives. A vendor's subtotal is the summary's row for that vendor, because both read the same
 * open documents.
 *
 * It opens as at today and runs straight away, or on the date, ageing and vendor the summary linked it
 * with. The as-at date is required, so a missing one shows at its field, as does any other refusal; a
 * failure shows in the message box. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-aged-payables-details-page',
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
  templateUrl: './aged-payables-details.page.html',
  styleUrl: './aged-payables-details.page.scss',
})
export class AgedPayablesDetailsPage implements OnInit {
  private readonly report = inject(AgedPayablesDetailsService);
  private readonly formats = inject(FormatSettingsService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each vendor is a card rather than a block of rows. */
  protected readonly narrow = signal(false);

  private readonly start = detailQueryFrom((name) => this.route.snapshot.queryParamMap.get(name));

  protected readonly filters = new FormGroup({
    asAtDate: new FormControl<string | null>(this.start.asAtDate ?? toIsoDate(new Date()), Validators.required),
    ageBy: new FormControl<AgeBasis | null>(this.start.ageBy ?? 'DueDate'),
    bucketDays: new FormControl<number | null>(this.start.bucketDays ?? BUCKET_SIZES[0]),
    contactId: new FormControl<number | null>(this.start.contactId),
  });

  protected readonly ageOptions: BbSelectOption<AgeBasis>[] = PAYABLE_AGE_BASIS_OPTIONS.map((o) => ({ value: o.value, label: o.label }));
  protected readonly bucketOptions: BbSelectOption<number>[] = BUCKET_SIZES.map((days) => ({ value: days, label: bucketOptionLabel(days) }));

  protected readonly vendors = signal<AgedPayablesVendorChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<AgedPayablesDetailsResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly vendorOptions = computed<BbSelectOption<number>[]>(() =>
    this.vendors().map((c) => ({ value: c.contactId, label: vendorChoiceLabel(c) })),
  );

  protected readonly msmeLink = MSME_AGING_LINK;

  protected readonly rows = computed<AgedPayablesDetailsGridRow[]>(() => {
    const result = this.result();
    return result ? agedPayablesDetailsRows(result) : [];
  });

  /** The cards of the phone layout: a vendor's heading, documents and subtotal together; the totals last. */
  protected readonly groups = computed<AgedPayablesDetailsGridRow[][]>(() => {
    const groups: AgedPayablesDetailsGridRow[][] = [];
    for (const row of this.rows()) {
      if (row.kind === 'heading' || row.kind === 'total' || groups.length === 0) {
        groups.push([row]);
      } else {
        groups[groups.length - 1].push(row);
      }
    }
    return groups;
  });

  /** The five headings the server named for the width in use. */
  protected readonly labels = computed<string[]>(() => {
    const labels = this.result()?.bucketLabels;
    return labels && labels.length === DEFAULT_LABELS.length ? labels : DEFAULT_LABELS;
  });

  protected readonly columns = computed<ColumnDef[]>(() => {
    const labels = this.labels();
    return [
      { field: 'document', header: 'Document' },
      { field: 'documentDate', header: 'Date', width: '6.5rem' },
      { field: 'dueDate', header: 'Due', width: '6.5rem' },
      { field: 'daysOverdue', header: 'Days', numeric: true, width: '4rem' },
      { field: 'original', header: 'Original', numeric: true, width: '7rem' },
      { field: 'settled', header: 'Settled', numeric: true, width: '7rem' },
      { field: 'outstanding', header: 'Outstanding', numeric: true, width: '7.5rem' },
      { field: 'current', header: labels[0], numeric: true, width: '7rem' },
      { field: 'bucket1', header: labels[1], numeric: true, width: '7rem' },
      { field: 'bucket2', header: labels[2], numeric: true, width: '7rem' },
      { field: 'bucket3', header: labels[3], numeric: true, width: '7rem' },
      { field: 'bucket4', header: labels[4], numeric: true, width: '7rem' },
    ];
  });

  /** How the report is aged, for the line under the title. */
  protected readonly basisText = computed(() => (this.result()?.ageBy === 'InvoiceDate' ? 'bill date' : 'due date'));

  /** The balance and how much is overdue, in words; a failure above it. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.vendors.length > 0) {
      messages.push({ tone: 'info', text: agedPayablesDetailsSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: AgedPayablesDetailsGridRow): string =>
    row.kind === 'heading' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'total' ? 'row--total' : '';

  ngOnInit(): void {
    if (typeof window !== 'undefined' && window.matchMedia) {
      const phone = window.matchMedia('(max-width: 640px)');
      const follow = (): void => this.narrow.set(phone.matches);
      follow();
      phone.addEventListener('change', follow);
      this.destroyRef.onDestroy(() => phone.removeEventListener('change', follow));
    }

    for (const name of Object.keys(this.filters.controls) as FilterName[]) {
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
      this.failure.set('You do not have access to the Aged Payables Details on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Aged Payables Details could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
