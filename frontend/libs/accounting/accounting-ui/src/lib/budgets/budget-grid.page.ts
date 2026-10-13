import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionContextService } from '@bill-book/auth';
import { readApiFailure } from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DataGridFooterDirective,
  DataGridRowClass,
  FileInputComponent,
  MoneyInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { ProjectListItem, loadProjects } from '../projects/project-options';
import { BudgetAccount, BudgetLineView, BudgetView } from './budget.models';
import { MONTHS, columnTotals, monthLabels, rowTotal, spreadByShape, spreadEvenly, yearLabel } from './budget-math';

interface GridRow {
  key: string;
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
  projectId: number | null;
  projectCode: string | null;
  amounts: number[];
}

/**
 * One row of the grid as drawn: a section's heading, one of its budget lines,
 * or its subtotal. The grid is one table, so the sections are flattened into
 * it and told apart by `kind`.
 */
type GridLine =
  | { kind: 'section'; label: string }
  | { kind: 'line'; item: { row: GridRow; index: number } }
  | { kind: 'subtotal'; label: string; months: number[]; total: number };

interface GridSection {
  label: string;
  rows: { row: GridRow; index: number }[];
  months: number[];
  total: number;
}

const TYPE_LABELS: Record<number, string> = { 1: 'Assets', 2: 'Liabilities', 3: 'Equity', 4: 'Income', 5: 'Expenses' };
const TYPE_ORDER = [4, 5, 1, 2, 3];

function keyOf(accountId: number, projectId: number | null): string {
  return `${accountId}:${projectId ?? ''}`;
}

/**
 * One budget (TK-108): accounts down, twelve months across, a year total that
 * spreads when typed, import and export in the same layout, and approval.
 * Under 640px the grid becomes one month at a time.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-budget-grid-page',
  standalone: true,
  imports: [
    FormsModule,
    TextInputComponent,
    SelectComponent,
    CheckboxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    FileInputComponent,
    MoneyInputComponent,
    DataGridFooterDirective,
  ],
  templateUrl: './budget-grid.page.html',
  styleUrl: './budget-grid.page.scss',
})
export class BudgetGridPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly budget = signal<BudgetView | null>(null);
  protected readonly rows = signal<GridRow[]>([]);
  protected readonly accounts = signal<BudgetAccount[]>([]);
  protected readonly projects = signal<ProjectListItem[]>([]);
  protected readonly busy = signal(false);
  protected readonly dirty = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly unknownCodes = signal<string[]>([]);
  protected readonly spreadMode = signal<'even' | 'shape'>('even');

  protected readonly spreadOptions = [
    { value: 'even', label: 'evenly' },
    { value: 'shape', label: "by last year's shape" },
  ];
  protected readonly month = signal(0);
  protected readonly lastYear = signal<Map<string, number[]> | null>(null);

  protected readonly editable = computed(() => this.budget()?.status === 'Draft' && this.session.has('accounting.edit'));
  protected readonly canApprove = computed(() => this.session.has('accounting.approve'));
  protected readonly labels = computed(() => monthLabels(this.budget()?.financialYearStart ?? '2026-04-01'));
  protected readonly title = computed(() => {
    const b = this.budget();
    return b ? `${yearLabel(b.financialYearStart)} · ${b.name}` : 'Budget';
  });
  protected readonly monthOptions = computed<BbSelectOption<number>[]>(() =>
    this.labels().map((label, i) => ({ value: i, label })),
  );

  protected readonly sections = computed<GridSection[]>(() => {
    const indexed = this.rows().map((row, index) => ({ row, index }));
    return TYPE_ORDER.map((type) => {
      const rows = indexed.filter((r) => r.row.accountTypeId === type);
      const totals = columnTotals(rows.map((r) => r.row.amounts));
      return { label: TYPE_LABELS[type], rows, months: totals.months, total: totals.total };
    }).filter((s) => s.rows.length > 0);
  });

  protected readonly gridRows = computed<GridLine[]>(() =>
    this.sections().flatMap((s): GridLine[] => [
      { kind: 'section', label: s.label },
      ...s.rows.map((item): GridLine => ({ kind: 'line', item })),
      { kind: 'subtotal', label: s.label, months: s.months, total: s.total },
    ]),
  );

  protected readonly gridColumns = computed<ColumnDef[]>(() => [
    { field: 'account', header: 'Account' },
    ...this.labels().map((label, m) => ({ field: `month${m}`, header: label, numeric: true })),
    { field: 'year', header: 'Year', numeric: true },
    ...(this.editable() ? [{ field: 'remove', header: '' }] : []),
  ]);

  protected readonly gridRowClass: DataGridRowClass = (line: GridLine) =>
    line.kind === 'section' ? 'row--group' : line.kind === 'subtotal' ? 'row--subtotal' : null;

  /** Income less expenses, month by month: the budgeted profit. */
  protected readonly profit = computed(() => {
    const income = columnTotals(this.rows().filter((r) => r.accountTypeId === 4).map((r) => r.amounts));
    const expense = columnTotals(this.rows().filter((r) => r.accountTypeId === 5).map((r) => r.amounts));
    const months = income.months.map((v, i) => Math.round((v - expense.months[i]) * 100) / 100);
    return { months, total: Math.round((income.total - expense.total) * 100) / 100 };
  });

  /** Leaf accounts not yet on the grid, for "Add a line". */
  protected readonly addableAccounts = computed<BbSelectOption<number>[]>(() => {
    const present = new Set(this.rows().filter((r) => r.projectId === null).map((r) => r.accountId));
    const byProject = this.budget()?.byProject ?? false;
    return this.leafAccounts()
      .filter((a) => byProject || !present.has(a.accountId))
      .map((a) => ({ value: a.accountId, label: `${a.accountCode} · ${a.accountName}` }));
  });

  protected readonly projectOptions = computed<BbSelectOption<number>[]>(() =>
    this.projects().map((p) => ({ value: p.projectId, label: `${p.projectCode} · ${p.projectName}` })),
  );

  protected name = '';
  protected makeDefault = true;
  protected reviseReason = '';
  protected addAccountId: number | null = null;
  protected addProjectId: number | null = null;
  private budgetId = 0;

  ngOnInit(): void {
    this.budgetId = Number(this.route.snapshot.paramMap.get('budgetId'));
    void this.load();
  }

  protected amountText(value: number): string {
    return this.formats.formatMoney(value);
  }

  protected rowTotalOf(row: GridRow): number {
    return rowTotal(row.amounts);
  }

  protected setCell(index: number, month: number, raw: string | number | null): void {
    const value = Math.max(0, Math.round(Number(raw || 0) * 100) / 100);
    this.rows.update((rows) =>
      rows.map((r, i) => (i === index ? { ...r, amounts: r.amounts.map((a, m) => (m === month ? value : a)) } : r)),
    );
    this.dirty.set(true);
  }

  /** A typed year total, spread evenly or by last year's shape. */
  protected async setTotal(index: number, raw: string | number | null): Promise<void> {
    const total = Math.max(0, Number(raw || 0));
    let shape: number[] | undefined;

    if (this.spreadMode() === 'shape') {
      const lastYear = await this.loadLastYear();
      const row = this.rows()[index];
      shape = lastYear.get(keyOf(row.accountId, row.projectId));
    }

    const amounts = shape ? spreadByShape(total, shape) : spreadEvenly(total);
    this.rows.update((rows) => rows.map((r, i) => (i === index ? { ...r, amounts } : r)));
    this.dirty.set(true);
  }

  protected addLine(): void {
    const account = this.accounts().find((a) => a.accountId === this.addAccountId);
    if (!account) {
      return;
    }

    const projectId = this.budget()?.byProject ? this.addProjectId : null;
    const key = keyOf(account.accountId, projectId);
    if (this.rows().some((r) => r.key === key)) {
      this.fail('That line is already on the budget.');
      return;
    }

    const project = this.projects().find((p) => p.projectId === projectId);
    this.rows.update((rows) =>
      [
        ...rows,
        {
          key,
          accountId: account.accountId,
          accountCode: account.accountCode,
          accountName: account.accountName,
          accountTypeId: account.accountTypeId,
          projectId,
          projectCode: project?.projectCode ?? null,
          amounts: new Array(MONTHS).fill(0),
        },
      ].sort(this.byCode),
    );
    this.addAccountId = null;
    this.addProjectId = null;
  }

  protected removeLine(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
    this.dirty.set(true);
  }

  protected async save(): Promise<void> {
    const lines = this.rows()
      .filter((r) => r.amounts.some((a) => a > 0))
      .map((r) => ({ accountId: r.accountId, projectId: r.projectId, amounts: r.amounts }));

    await this.act(async () => {
      await firstValueFrom(this.http.put(`/api/budgets/${this.budgetId}`, { name: this.name.trim(), lines }));
      this.ok('Budget saved.');
      await this.load();
    });
  }

  /** bb-file-input clears its own element, so the same file can be picked twice. */
  protected async importFile(files: readonly File[]): Promise<void> {
    const file = files[0];
    if (!file) {
      return;
    }

    const body = new FormData();
    body.append('file', file);
    this.unknownCodes.set([]);

    await this.act(async () => {
      await firstValueFrom(this.http.post(`/api/budgets/${this.budgetId}/import`, body));
      this.ok('Budget imported.');
      await this.load();
    });
  }

  protected async exportAs(format: 'csv' | 'xlsx'): Promise<void> {
    await this.act(async () => {
      const blob = await firstValueFrom(
        this.http.get(`/api/budgets/${this.budgetId}/export?format=${format}`, { responseType: 'blob' }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${this.budget()?.name ?? 'budget'}.${format}`;
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }

  protected async approve(): Promise<void> {
    await this.act(async () => {
      await firstValueFrom(this.http.post(`/api/budgets/${this.budgetId}/approve`, { makeDefault: this.makeDefault }));
      this.ok('Budget approved. It is locked now; revise it to change it.');
      await this.load();
    });
  }

  protected async setDefault(): Promise<void> {
    await this.act(async () => {
      await firstValueFrom(this.http.post(`/api/budgets/${this.budgetId}/make-default`, {}));
      this.ok("This is now the branch's budget for the year.");
      await this.load();
    });
  }

  protected async revise(): Promise<void> {
    if (!this.reviseReason.trim()) {
      this.fail('Say why the budget is being revised.');
      return;
    }

    await this.act(async () => {
      const revised = await firstValueFrom(
        this.http.post<{ budgetId: number }>(`/api/budgets/${this.budgetId}/revise`, { reason: this.reviseReason.trim() }),
      );
      this.reviseReason = '';
      await this.router.navigate(['/accounting/budgets', revised.budgetId]);
      this.budgetId = revised.budgetId;
      this.ok('Revision created as a draft. Approving it replaces the budget it revises.');
      await this.load();
    });
  }

  protected async remove(): Promise<void> {
    await this.act(async () => {
      await firstValueFrom(this.http.delete(`/api/budgets/${this.budgetId}`));
      await this.router.navigate(['/accounting/budgets']);
    });
  }

  protected setMonth(value: number | null): void {
    this.month.set(value ?? 0);
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      const [budget, accounts] = await Promise.all([
        firstValueFrom(this.http.get<BudgetView>(`/api/budgets/${this.budgetId}`)),
        this.accounts().length > 0
          ? Promise.resolve(this.accounts())
          : firstValueFrom(this.http.get<BudgetAccount[]>('/api/accounts')),
      ]);
      this.accounts.set(accounts);
      if (budget.byProject && this.projects().length === 0) {
        this.projects.set(await loadProjects(this.http));
      }

      this.budget.set(budget);
      this.name = budget.name;
      this.rows.set(this.buildRows(budget));
      this.lastYear.set(null);
      this.dirty.set(false);
    } catch (err) {
      this.fail(readApiFailure(err).text);
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * The grid's rows: every line the budget holds and, on a budget that is not
   * by project, every income and expense leaf account too, so the usual lines
   * are there to type into without adding them one by one.
   */
  private buildRows(budget: BudgetView): GridRow[] {
    const rows = new Map<string, GridRow>();

    if (!budget.byProject && budget.status === 'Draft') {
      for (const account of this.leafAccounts().filter((a) => a.accountTypeId === 4 || a.accountTypeId === 5)) {
        rows.set(keyOf(account.accountId, null), {
          key: keyOf(account.accountId, null),
          accountId: account.accountId,
          accountCode: account.accountCode,
          accountName: account.accountName,
          accountTypeId: account.accountTypeId,
          projectId: null,
          projectCode: null,
          amounts: new Array(MONTHS).fill(0),
        });
      }
    }

    for (const line of budget.lines) {
      rows.set(keyOf(line.accountId, line.projectId), this.rowOf(line));
    }

    return [...rows.values()].sort(this.byCode);
  }

  private rowOf(line: BudgetLineView): GridRow {
    return {
      key: keyOf(line.accountId, line.projectId),
      accountId: line.accountId,
      accountCode: line.accountCode,
      accountName: line.accountName,
      accountTypeId: line.accountTypeId,
      projectId: line.projectId,
      projectCode: line.projectCode,
      amounts: [...line.amounts],
    };
  }

  private leafAccounts(): BudgetAccount[] {
    const accounts = this.accounts().filter((a) => a.isActive);
    const parents = new Set(this.accounts().map((a) => a.parentAccountId).filter((id): id is number => id !== null));
    return accounts.filter((a) => !parents.has(a.accountId)).sort((a, b) => a.accountCode.localeCompare(b.accountCode));
  }

  private readonly byCode = (a: GridRow, b: GridRow): number =>
    a.accountCode.localeCompare(b.accountCode) || (a.projectCode ?? '').localeCompare(b.projectCode ?? '');

  private async loadLastYear(): Promise<Map<string, number[]>> {
    const cached = this.lastYear();
    if (cached) {
      return cached;
    }

    const lines = await firstValueFrom(
      this.http.get<BudgetLineView[]>(`/api/budgets/${this.budgetId}/last-year`),
    ).catch(() => [] as BudgetLineView[]);
    const map = new Map(lines.map((l) => [keyOf(l.accountId, l.projectId), l.amounts]));
    this.lastYear.set(map);
    return map;
  }

  private async act(work: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      await work();
    } catch (err) {
      const unknown = err instanceof HttpErrorResponse ? (err.error as { unknownCodes?: string[] } | null)?.unknownCodes : undefined;
      this.unknownCodes.set(unknown ?? []);
      this.fail(readApiFailure(err).text);
    } finally {
      this.busy.set(false);
    }
  }

  private ok(text: string): void {
    this.message.set(text);
    this.messageIsError.set(false);
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }
}
