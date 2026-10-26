import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  BudgetMonthlyTrendReportResult,
  BudgetMonthlyTrendReportService,
  BudgetVsActualBudget,
  BudgetVsActualReportService,
  TREND_SHOW_OPTIONS,
  TrendMonthRow,
  TrendRow,
  TrendShow,
  budgetLabel,
  dateWithinBudgetYear,
  defaultBudget,
  monthHeader,
  trendLineOptions,
  trendMonthRows,
  trendRowsOf,
  variancePercentText,
  varianceTone,
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

type FilterName = 'budgetId' | 'asOfDate' | 'show';

/** The twelve month columns, whose templates the grid finds by these names. */
const MONTH_FIELDS = ['m0', 'm1', 'm2', 'm3', 'm4', 'm5', 'm6', 'm7', 'm8', 'm9', 'm10', 'm11'] as const;

/**
 * Budget Monthly Trend (TK-214): its own page on its own endpoint. A budget
 * against the books month by month across its year, laid out as Budget vs
 * Actual: revenue and cost of sales to gross profit, other income and expenses
 * to net profit. Each account, total and profit line shows its budget, actual
 * and variance for the twelve months, then the year to date and the year's
 * budget. A month after the as-at date's has its budget and no actual yet, and
 * is greyed.
 *
 * Wide, one grid scrolls sideways inside its own frame, never the page. On a
 * phone twelve columns cannot fit, so one line at a time is chosen and shown as
 * a table of months, as the budget entry screen shows one month at a time.
 *
 * It opens on the branch's approved budget for this year and runs straight
 * away. Which of budget, actual and variance to show is the page's choice and
 * changes nothing on the server.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-budget-monthly-trend-report-page',
  standalone: true,
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './budget-monthly-trend-report.page.html',
  styleUrl: './budget-monthly-trend-report.page.scss',
})
export class BudgetMonthlyTrendReportPage implements OnInit {
  private readonly report = inject(BudgetMonthlyTrendReportService);
  private readonly budgetsApi = inject(BudgetVsActualReportService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where the twelve columns give way to one line shown month by month. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    budgetId: new FormControl<number | null>(null, Validators.required),
    asOfDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    show: new FormControl<TrendShow>('All', { nonNullable: true }),
  });

  /** The line the phone view shows. */
  protected readonly lineControl = new FormControl<string>('net', { nonNullable: true });

  protected readonly budgets = signal<BudgetVsActualBudget[]>([]);
  protected readonly budgetsLoaded = signal(false);
  protected readonly result = signal<BudgetMonthlyTrendReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  private readonly show = toSignal(this.filters.controls.show.valueChanges, { initialValue: this.filters.controls.show.value });
  private readonly line = toSignal(this.lineControl.valueChanges, { initialValue: this.lineControl.value });

  /** The phone view always shows all three; the wide grid shows one measure's rows or all three. */
  protected readonly showAll = computed(() => this.show() === 'All');

  protected readonly percent = variancePercentText;
  protected readonly tone = varianceTone;

  protected readonly budgetOptions = computed<BbSelectOption<number>[]>(() =>
    this.budgets().map((b) => ({ value: b.budgetId, label: budgetLabel(b) })),
  );

  protected readonly showOptions: BbSelectOption<TrendShow>[] = TREND_SHOW_OPTIONS.map((o) => ({ value: o.value, label: o.label }));

  protected readonly lineOptions = computed<BbSelectOption<string>[]>(() => {
    const report = this.result();
    return report ? trendLineOptions(report).map((o) => ({ value: o.key, label: o.label })) : [];
  });

  /** The wide grid's rows, one set per measure shown. */
  protected readonly rows = computed<TrendRow[]>(() => {
    const report = this.result();
    return report ? trendRowsOf(report, this.show()) : [];
  });

  /** The phone view's rows: the chosen line's twelve months and its year to date. */
  protected readonly monthRows = computed<TrendMonthRow[]>(() => {
    const report = this.result();
    if (!report) {
      return [];
    }
    const rows = trendMonthRows(report, this.line());
    return rows.length > 0 ? rows : trendMonthRows(report, 'net');
  });

  /**
   * The account, the twelve months headed by their names, the year to date and,
   * when the budget is shown, the year's budget. Above phone width the figures
   * have a set width, so they line up from one row to the next.
   */
  protected readonly columns = computed<ColumnDef[]>(() => {
    const report = this.result();
    const budgetShown = this.show() === 'All' || this.show() === 'Budget';
    return [
      { field: 'accountName', header: 'Account', width: '17rem' },
      ...MONTH_FIELDS.map((field, i) => ({
        field,
        header: report ? monthHeader(report.months[i].fromDate) : `Month ${i + 1}`,
        numeric: true,
        width: '6.75rem',
      })),
      { field: 'yearToDate', header: 'Year to date', numeric: true, width: '9.5rem' },
      ...(budgetShown ? [{ field: 'yearBudget', header: 'Full-year budget', numeric: true, width: '9.5rem' }] : []),
    ];
  });

  /** The phone view's columns: the month, then its budget, actual and variance. */
  protected readonly phoneColumns: ColumnDef[] = [
    { field: 'label', header: 'Month' },
    { field: 'budget', header: 'Budget', numeric: true },
    { field: 'actual', header: 'Actual', numeric: true },
    { field: 'variance', header: 'Variance', numeric: true },
  ];

  /** The failure, else no budgets to choose from, else nothing budgeted or posted. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    if (failure) {
      return [{ tone: 'error', text: failure }];
    }
    if (this.budgetsLoaded() && this.budgets().length === 0) {
      return [{ tone: 'info', text: 'This branch has no budgets yet. Enter one under Accounting › Budgets and follow it month by month here.' }];
    }
    if (this.result() && this.result()!.sections.every((s) => s.lines.length === 0)) {
      return [{ tone: 'info', text: "Nothing was budgeted or posted in this budget's year so far." }];
    }
    return [];
  });

  protected readonly rowClass = (row: TrendRow): string =>
    row.kind === 'section' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'profit' ? 'row--total' : '';

  protected readonly phoneRowClass = (row: TrendMonthRow): string =>
    row.isTotal ? 'row--total' : row.isFuture ? 'row--muted' : '';

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

    // A budget for another year moves the as-at date into that year.
    this.filters.controls.budgetId.valueChanges.subscribe((id) => {
      const budget = this.budgets().find((b) => b.budgetId === id);
      const asOf = this.filters.controls.asOfDate.value;
      if (budget && asOf) {
        const within = dateWithinBudgetYear(asOf, budget);
        if (within !== asOf) {
          this.filters.controls.asOfDate.setValue(within);
        }
      }
    });

    void this.loadBudgets();
  }

  /** The words under a field: the server's refusal, else a missing value once touched. */
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

  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid || this.busy()) {
      return;
    }

    const value = this.filters.getRawValue();
    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});

    try {
      const report = await this.report.run({ budgetId: value.budgetId as number, asOfDate: value.asOfDate as string });
      this.result.set(report);
      if (!trendLineOptions(report).some((o) => o.key === this.lineControl.value)) {
        this.lineControl.setValue('net');
      }
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** The budgets for the filter; the one the page opens on is chosen and run. */
  private async loadBudgets(): Promise<void> {
    try {
      const budgets = await this.budgetsApi.budgets();
      this.budgets.set(budgets);
      const chosen = defaultBudget(budgets, toIsoDate(new Date()));
      if (chosen) {
        this.filters.controls.budgetId.setValue(chosen.budgetId);
        await this.run();
      }
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.budgetsLoaded.set(true);
    }
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to Budget Monthly Trend on this branch.');
      return;
    }

    if (failure?.status === 404) {
      this.serverErrors.set({ budgetId: 'That budget is not on this branch.' });
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

    this.failure.set(failure?.error?.message || 'Budget Monthly Trend could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
