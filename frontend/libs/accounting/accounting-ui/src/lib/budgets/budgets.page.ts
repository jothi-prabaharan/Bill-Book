import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionContextService } from '@bill-book/auth';
import { readApiFailure } from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BbSelectOption,
  CheckboxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { BudgetListItem } from './budget.models';
import { yearLabel } from './budget-math';

interface CreateForm {
  name: string;
  year: number | null;
  byProject: boolean;
  source: 'Blank' | 'LastYearActuals' | 'OtherBudget';
  sourceBudgetId: number | null;
  adjustPercent: number | null;
  incomePercent: number | null;
  expensePercent: number | null;
}

/**
 * Accounting › Budgets (TK-108). The branch's budgets, newest year first, and
 * a form to start one: blank, from last year's actuals with a percentage per
 * account type, or as a copy of another budget.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-budgets-page',
  standalone: true,
  imports: [FormsModule, TextInputComponent, SelectComponent, NumberInputComponent, CheckboxComponent],
  templateUrl: './budgets.page.html',
  styleUrl: './budgets.page.scss',
})
export class BudgetsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionContextService);
  private readonly router = inject(Router);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly sourceOptions: BbSelectOption<string>[] = [
    { value: 'Blank', label: 'A blank budget' },
    { value: 'LastYearActuals', label: "Last year's actuals" },
    { value: 'OtherBudget', label: 'A copy of another budget' },
  ];

  protected readonly canEdit = computed(() => this.session.has('accounting.edit'));
  protected readonly rows = signal<BudgetListItem[]>([]);
  protected readonly budgetOptions = computed<BbSelectOption<number>[]>(() =>
    this.rows().map((b) => ({ value: b.budgetId, label: `${yearLabel(b.financialYearStart)} · ${b.name}` })),
  );
  protected readonly creating = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  protected form: CreateForm = this.blankForm();

  ngOnInit(): void {
    void this.load();
  }

  protected yearOf(budget: BudgetListItem): string {
    return yearLabel(budget.financialYearStart);
  }

  protected open(budget: BudgetListItem): void {
    void this.router.navigate(['/accounting/budgets', budget.budgetId]);
  }

  protected startCreate(): void {
    this.form = this.blankForm();
    this.message.set(null);
    this.creating.set(true);
  }

  protected cancel(): void {
    this.creating.set(false);
  }

  protected async create(): Promise<void> {
    const f = this.form;
    if (!f.name.trim() || !f.year) {
      this.fail('Give the budget a name and a year.');
      return;
    }
    if (f.source === 'OtherBudget' && !f.sourceBudgetId) {
      this.fail('Choose the budget to copy.');
      return;
    }

    const typeAdjustments = [
      ...(f.incomePercent !== null ? [{ accountTypeId: 4, percent: f.incomePercent }] : []),
      ...(f.expensePercent !== null ? [{ accountTypeId: 5, percent: f.expensePercent }] : []),
    ];

    this.busy.set(true);
    try {
      const created = await firstValueFrom(
        this.http.post<{ budgetId: number }>('/api/budgets', {
          name: f.name.trim(),
          year: f.year,
          byProject: f.byProject,
          source: f.source,
          sourceBudgetId: f.source === 'OtherBudget' ? f.sourceBudgetId : null,
          adjustPercent: f.adjustPercent ?? 0,
          typeAdjustments,
        }),
      );
      await this.router.navigate(['/accounting/budgets', created.budgetId]);
    } catch (err) {
      this.fail(readApiFailure(err).text);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await firstValueFrom(this.http.get<BudgetListItem[]>('/api/budgets')));
    } catch (err) {
      this.fail(readApiFailure(err).text);
    } finally {
      this.busy.set(false);
    }
  }

  private blankForm(): CreateForm {
    const now = new Date();
    // Before April the current financial year started last calendar year, for the usual April start.
    const year = now.getMonth() < 3 ? now.getFullYear() - 1 : now.getFullYear();
    return {
      name: '',
      year: year + 1,
      byProject: false,
      source: 'Blank',
      sourceBudgetId: null,
      adjustPercent: null,
      incomePercent: null,
      expensePercent: null,
    };
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }
}
