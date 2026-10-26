import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  JOURNAL_STATUS_CHOICES,
  JournalReportEntry,
  JournalReportLine,
  JournalReportResult,
  JournalReportService,
  JournalSourceOption,
  JournalStatus,
  appendPage,
  auditNote,
  blankZero,
  hasMore,
  journalVerdict,
  reversalNote,
  sourceName,
  sourceNote,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DataGridFooterDirective,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  SelectComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'source' | 'status' | 'accountId';

/**
 * The Journal Report (TK-217): its own page on its own endpoint. Every journal
 * posted in a period with its lines, who created and posted it and its status:
 * the audit view of what was posted by hand and by the system. A journal is a
 * document, so a sales invoice's posting is listed beside a manual journal, each
 * under its own number and the document type that posted it.
 *
 * It opens on the financial year to date and runs straight away. Both dates are
 * required, so a missing or out-of-order one shows at its field; a refusal or a
 * failure shows in the message box. The journals come a page at a time, and the
 * totals above them are of every journal the filters match.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-journal-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    DataGridFooterDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './journal-report.page.html',
  styleUrl: './journal-report.page.scss',
})
export class JournalReportPage implements OnInit {
  private readonly report = inject(JournalReportService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each line is a small row of a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    source: new FormControl<string | null>(null),
    status: new FormControl<JournalStatus | null>(null),
    accountId: new FormControl<number | null>(null),
  });

  protected readonly sources = signal<JournalSourceOption[]>([]);
  protected readonly accountOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<JournalReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly sourceOptions = computed<BbSelectOption<string>[]>(() =>
    this.sources().map((s) => ({ value: s.code, label: s.name })),
  );

  protected readonly statusOptions: BbSelectOption<JournalStatus>[] = JOURNAL_STATUS_CHOICES.map((c) => ({ value: c.value, label: c.label }));

  protected readonly journals = computed<JournalReportEntry[]>(() => this.result()?.journals ?? []);
  protected readonly moreToLoad = computed(() => {
    const report = this.result();
    return report ? hasMore(report) : false;
  });

  /** The columns of a journal's lines; the description takes whatever width is left. */
  protected readonly columns: ColumnDef[] = [
    { field: 'account', header: 'Account', width: '15rem' },
    { field: 'contactName', header: 'Contact', width: '12rem' },
    { field: 'description', header: 'Description' },
    { field: 'debit', header: 'Debit', numeric: true, width: '7.5rem' },
    { field: 'credit', header: 'Credit', numeric: true, width: '7.5rem' },
  ];

  /** The period's totals said in words, as a message: success when they agree, an error when they do not. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const report = this.result();
    if (report && report.totalJournals > 0) {
      const verdict = journalVerdict(report, (value) => this.formats.formatMoney(value));
      messages.push({ tone: verdict.isBalanced ? 'success' : 'error', text: verdict.text });
    }

    return messages;
  });

  protected readonly blankZero = blankZero;
  protected readonly reversalNote = reversalNote;
  protected readonly sourceNote = (line: JournalReportLine): string => sourceNote(line, (value) => this.formats.formatAmount(value));

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

    return control.errors['required'] ? `${label} is required.` : `Check the ${label.toLowerCase()}.`;
  }

  /** What a journal's document type is called, from Master's transaction types. */
  protected sourceLabel(code: string): string {
    return sourceName(code, this.sources());
  }

  protected audit(entry: JournalReportEntry): string {
    // The same date format as the journal's own date above it, so one card does not carry two.
    return auditNote(entry, (value) => this.formats.formatDate(value));
  }

  /** The line's account, as the grid and the phone list both name it. */
  protected account(line: JournalReportLine): string {
    return `${line.accountCode} ${line.accountName}`;
  }

  protected trackEntry(entry: JournalReportEntry): string {
    return `${entry.sourceCode}:${entry.transactionId}`;
  }

  /** Runs from the first page for the filters now chosen. */
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
      // Journals from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of journals under those already shown, for the filters they were run with. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMore(loaded)) {
      return;
    }

    this.busy.set(true);
    this.failure.set(null);

    try {
      // The filters the loaded pages were run with, not any edit made since.
      const next = await this.report.run({ ...this.request(loaded.pageNumber + 1), fromDate: loaded.fromDate, toDate: loaded.toDate });
      this.result.set(appendPage(loaded, next));
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
      source: value.source,
      status: value.status,
      accountId: value.accountId,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    const [sources, accounts] = await Promise.all([
      this.report.sources().catch(() => [] as JournalSourceOption[]),
      this.report.accounts().catch(() => [] as BbSelectOption<number>[]),
    ]);
    this.sources.set(sources);
    this.accountOptions.set(accounts);
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Journal Report on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Journal Report could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
