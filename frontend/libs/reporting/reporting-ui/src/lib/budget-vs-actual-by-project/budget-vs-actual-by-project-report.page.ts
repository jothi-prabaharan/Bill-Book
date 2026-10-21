import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  BUDGET_PERIODS,
  BudgetPeriod,
  BudgetVsActualBudget,
  BudgetVsActualByProjectReportResult,
  BudgetVsActualByProjectReportService,
  BudgetVsActualProfit,
  BudgetVsActualProjectOption,
  budgetLabel,
  dateWithinBudgetYear,
  defaultBudget,
  hasGrossProfit,
  projectLabel,
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

type FilterName = 'budgetId' | 'asOfDate' | 'period' | 'projectId';

/** What a grid row is: a project's heading, an account, a section's total, gross profit, or net profit. */
type RowKind = 'heading' | 'account' | 'subtotal' | 'gross' | 'net';

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
 * Budget vs Actual by Project (TK-207): its own page on its own endpoints. A
 * budget kept by project against the postings that carry each project, for
 * whole months of its year, each project laid out as Budget vs Actual lays out
 * the branch: its accounts by section with their totals, its gross profit when
 * it has revenue or a cost of sales, and its net profit; then the profit across
 * the projects. One grid holds the lot, so every figure sits in its column and
 * each row is a card on a phone.
 *
 * It opens on the branch's approved budget kept by project for this year and
 * every project, and runs straight away. A missing date, a date outside the
 * budget's year or a budget not kept by project shows at its field; a refusal
 * or a failure shows in the message box.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-budget-vs-actual-by-project-report-page',
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
  templateUrl: './budget-vs-actual-by-project-report.page.html',
  styleUrl: './budget-vs-actual-by-project-report.page.scss',
})
export class BudgetVsActualByProjectReportPage implements OnInit {
  private readonly report = inject(BudgetVsActualByProjectReportService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each account becomes a card and the amounts take no set width. */
  private readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    budgetId: new FormControl<number | null>(null, Validators.required),
    asOfDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    period: new FormControl<BudgetPeriod | null>('YearToDate', Validators.required),
    projectId: new FormControl<number | null>(null),
  });

  protected readonly budgets = signal<BudgetVsActualBudget[]>([]);
  protected readonly projects = signal<BudgetVsActualProjectOption[]>([]);
  protected readonly loaded = signal(false);
  protected readonly result = signal<BudgetVsActualByProjectReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly percent = variancePercentText;
  protected readonly tone = varianceTone;

  protected readonly budgetOptions = computed<BbSelectOption<number>[]>(() =>
    this.budgets().map((b) => ({ value: b.budgetId, label: budgetLabel(b) })),
  );

  protected readonly projectOptions = computed<BbSelectOption<number>[]>(() =>
    this.projects().map((p) => ({ value: p.projectId, label: projectLabel(p) })),
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

  /** The failure, else no budget kept by project, else nothing in the months covered. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    if (failure) {
      return [{ tone: 'error', text: failure }];
    }
    if (this.loaded() && this.budgets().length === 0) {
      return [{
        tone: 'info',
        text: 'This branch has no budget kept by project. Enter one under Accounting › Budgets with Budget by project ticked, or run Budget vs Actual for the whole branch.',
      }];
    }
    if (this.result()?.projects.length === 0) {
      return [{ tone: 'info', text: 'Nothing was budgeted or posted with a project in these months.' }];
    }
    return [];
  });

  protected readonly rowClass = (row: ReportRow): string =>
    row.kind === 'heading' ? 'row--group'
      : row.kind === 'subtotal' || row.kind === 'gross' ? 'row--subtotal'
        : row.kind === 'net' ? 'row--total'
          : '';

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

    void this.load();
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
          projectId: value.projectId,
        }),
      );
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** The budgets and projects for the filters; the budget the page opens on is chosen and run. */
  private async load(): Promise<void> {
    try {
      const [budgets, projects] = await Promise.all([this.report.budgets(), this.report.projects()]);
      this.budgets.set(budgets);
      this.projects.set(projects);
      const chosen = defaultBudget(budgets, toIsoDate(new Date()));
      if (chosen) {
        this.filters.controls.budgetId.setValue(chosen.budgetId);
        await this.run();
      }
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.loaded.set(true);
    }
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to Budget vs Actual by Project on this branch.');
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

    this.failure.set(failure?.error?.message || 'Budget vs Actual by Project could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}

/** A row with no figures. */
function heading(key: string, name: string): ReportRow {
  return { key, kind: 'heading', accountCode: '', accountName: name, budget: null, actual: null, variance: null, variancePercent: null };
}

/** A gross or net profit row. */
function profit(key: string, kind: 'gross' | 'net', name: string, p: BudgetVsActualProfit): ReportRow {
  return { key, kind, accountCode: '', accountName: name, ...p };
}

/**
 * The report as rows: for each project its heading, its accounts section by
 * section with each section's total, its gross profit when it has revenue or a
 * cost of sales, and its net profit; then, when there is more than one project,
 * the profit across them.
 */
function rowsOf(report: BudgetVsActualByProjectReportResult): ReportRow[] {
  const rows: ReportRow[] = [];

  for (const project of report.projects) {
    const p = `p${project.projectId}`;
    rows.push(heading(`${p}-heading`, projectLabel(project)));

    // Gross profit follows the last of revenue and cost of sales that has lines.
    const grossAfter = hasGrossProfit(project)
      ? [...project.sections].reverse().find((s) => (s.section === 'Revenue' || s.section === 'CostOfSales') && s.lines.length > 0)?.section
      : undefined;

    for (const section of project.sections) {
      if (section.lines.length === 0) {
        continue;
      }
      for (const line of section.lines) {
        rows.push({ ...line, key: `${p}-a${line.accountId}`, kind: 'account' });
      }
      rows.push({
        key: `${p}-${section.section}`,
        kind: 'subtotal',
        accountCode: '',
        accountName: `Total ${section.title.toLowerCase()}`,
        budget: section.budget,
        actual: section.actual,
        variance: section.variance,
        variancePercent: section.variancePercent,
      });
      if (section.section === grossAfter) {
        rows.push(profit(`${p}-gross`, 'gross', 'Gross profit', project.grossProfit));
      }
    }

    rows.push(profit(`${p}-net`, 'net', 'Net profit', project.netProfit));
  }

  if (report.projects.length > 1) {
    rows.push(heading('all-heading', 'All projects'));
    rows.push(profit('all-gross', 'gross', 'Gross profit', report.grossProfit));
    rows.push(profit('all-net', 'net', 'Net profit', report.netProfit));
  }

  return rows;
}
