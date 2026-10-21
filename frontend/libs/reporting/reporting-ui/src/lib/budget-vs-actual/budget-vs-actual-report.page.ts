import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  BUDGET_PERIODS,
  BudgetPeriod,
  BudgetVsActualBudget,
  BudgetVsActualProfit,
  BudgetVsActualReportResult,
  BudgetVsActualReportService,
  budgetLabel,
  dateWithinBudgetYear,
  defaultBudget,
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
  MoneyCellComponent,
  RegisterDatePipe,
  SelectComponent,
  UiMessage,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'budgetId' | 'asOfDate' | 'period';

/** What a grid row is: a section's heading, an account, a section's total, or gross or net profit. */
type RowKind = 'section' | 'account' | 'subtotal' | 'profit';

/** A grid row. A heading has no figures. */
interface ReportRow {
  key: string;
  kind: RowKind;
  accountCode: string;
  accountName: string;
  budget: number | null;
  actual: number | null;
  variance: number | null;
  variancePercent: number | null;
}

/**
 * Budget vs Actual (TK-204): its own page on its own endpoints. One budget
 * against the books for whole months of its year (the as-at date's month,
 * its quarter, or the year to date), laid out as the Profit & Loss: revenue
 * and cost of sales to gross profit, other income and expenses to net profit.
 * Each line has its variance, positive and green when favourable, negative and
 * red when adverse. One grid holds the lot, as the budget entry screen does,
 * so every figure sits in its column and each row is a card on a phone.
 *
 * It opens on the branch's approved budget for this year and runs straight
 * away. A missing date, or one outside the budget's year, shows at its field;
 * a refusal or a failure shows in the message box.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-budget-vs-actual-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    MoneyCellComponent,
    RegisterDatePipe,
  ],
  templateUrl: './budget-vs-actual-report.page.html',
  styleUrl: './budget-vs-actual-report.page.scss',
})
export class BudgetVsActualReportPage implements OnInit {
  private readonly report = inject(BudgetVsActualReportService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each account becomes a card and the amounts take no set width. */
  private readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    budgetId: new FormControl<number | null>(null, Validators.required),
    asOfDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    period: new FormControl<BudgetPeriod | null>('YearToDate', Validators.required),
  });

  protected readonly budgets = signal<BudgetVsActualBudget[]>([]);
  protected readonly budgetsLoaded = signal(false);
  protected readonly result = signal<BudgetVsActualReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly percent = variancePercentText;
  protected readonly tone = varianceTone;

  protected readonly budgetOptions = computed<BbSelectOption<number>[]>(() =>
    this.budgets().map((b) => ({ value: b.budgetId, label: budgetLabel(b) })),
  );

  protected readonly periodOptions: BbSelectOption<BudgetPeriod>[] = BUDGET_PERIODS.map((p) => ({ value: p.value, label: p.label }));

  /** Above phone width the amounts have a set width; on a phone each row is a card and needs none. */
  protected readonly columns = computed<ColumnDef[]>(() => {
    const width = this.narrow() ? undefined : '9rem';
    return [
      { field: 'accountName', header: 'Account' },
      { field: 'budget', header: 'Budget', numeric: true, width },
      { field: 'actual', header: 'Actual', numeric: true, width },
      { field: 'variance', header: 'Variance', numeric: true, width },
      { field: 'variancePercent', header: 'Var %', numeric: true, width: this.narrow() ? undefined : '6.5rem' },
    ];
  });

  /** The failure, else no budgets to choose from. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    if (failure) {
      return [{ tone: 'error', text: failure }];
    }
    if (this.budgetsLoaded() && this.budgets().length === 0) {
      return [{ tone: 'info', text: 'This branch has no budgets yet. Enter one under Accounting › Budgets and compare it with the books here.' }];
    }
    return [];
  });

  protected readonly rowClass = (row: ReportRow): string =>
    row.kind === 'section' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'profit' ? 'row--total' : '';

  /** The grid's rows, worked out once per answer. */
  protected readonly rows = computed<ReportRow[]>(() => {
    const report = this.result();
    return report ? rowsOf(report) : [];
  });

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
      this.result.set(
        await this.report.run({
          budgetId: value.budgetId as number,
          asOfDate: value.asOfDate as string,
          period: value.period as BudgetPeriod,
        }),
      );
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** The budgets for the filter; the one the page opens on is chosen and run. */
  private async loadBudgets(): Promise<void> {
    try {
      const budgets = await this.report.budgets();
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
      this.failure.set('You do not have access to Budget vs Actual on this branch.');
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

    this.failure.set(failure?.error?.message || 'Budget vs Actual could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}

/**
 * The report as rows: each section's heading, its accounts and its total;
 * gross profit after cost of sales and net profit at the end.
 */
function rowsOf(report: BudgetVsActualReportResult): ReportRow[] {
  const rows: ReportRow[] = [];
  const profit = (key: string, name: string, p: BudgetVsActualProfit): ReportRow => ({
    key,
    kind: 'profit',
    accountCode: '',
    accountName: name,
    budget: p.budget,
    actual: p.actual,
    variance: p.variance,
    variancePercent: p.variancePercent,
  });

  for (const section of report.sections) {
    rows.push({
      key: `${section.section}-heading`,
      kind: 'section',
      accountCode: '',
      accountName: section.title,
      budget: null,
      actual: null,
      variance: null,
      variancePercent: null,
    });
    for (const line of section.lines) {
      rows.push({ ...line, key: `a${line.accountId}`, kind: 'account' });
    }
    rows.push({
      key: `${section.section}-total`,
      kind: 'subtotal',
      accountCode: '',
      accountName: `Total ${section.title.toLowerCase()}`,
      budget: section.budget,
      actual: section.actual,
      variance: section.variance,
      variancePercent: section.variancePercent,
    });
    if (section.section === 'CostOfSales') {
      rows.push(profit('gross-profit', 'Gross profit', report.grossProfit));
    }
  }

  rows.push(profit('net-profit', 'Net profit', report.netProfit));
  return rows;
}
