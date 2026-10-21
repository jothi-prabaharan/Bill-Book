/**
 * Budget vs Actual's contract (TK-204), field for field with
 * `BudgetVsActualReportModels.cs`. Money is in the branch's base currency.
 */

/** Which whole months of the budget's year the as-at date covers. */
export type BudgetPeriod = 'Month' | 'Quarter' | 'YearToDate';

export type BudgetStatus = 'Draft' | 'Approved' | 'Superseded';

/** The Profit & Loss's sections, in its order. */
export type BudgetVsActualSection = 'Revenue' | 'CostOfSales' | 'OtherIncome' | 'Expenses';

/** What the report is asked for. All three are required; the date is ISO `yyyy-MM-dd`. */
export interface BudgetVsActualReportRequest {
  budgetId: number;
  asOfDate: string;
  period: BudgetPeriod;
}

/** A budget the report can be run against. */
export interface BudgetVsActualBudget {
  budgetId: number;
  name: string;
  financialYearStart: string;
  financialYearEnd: string;
  status: BudgetStatus;
  isBranchDefault: boolean;
  byProject: boolean;
}

/** One account: its budget and actual on the Profit & Loss's sign, and the variance, positive when favourable. */
export interface BudgetVsActualLine {
  accountId: number;
  accountCode: string;
  accountName: string;
  isContra: boolean;
  budget: number;
  actual: number;
  variance: number;
  /** The variance as a percentage of the budget; null without a budget. */
  variancePercent: number | null;
}

export interface BudgetVsActualSectionResult {
  section: BudgetVsActualSection;
  title: string;
  lines: BudgetVsActualLine[];
  budget: number;
  actual: number;
  variance: number;
  variancePercent: number | null;
}

/** Gross or net profit: higher is favourable. */
export interface BudgetVsActualProfit {
  budget: number;
  actual: number;
  variance: number;
  variancePercent: number | null;
}

export interface BudgetVsActualReportResult {
  budget: BudgetVsActualBudget;
  asOfDate: string;
  period: BudgetPeriod;
  /** The budget months covered, 1 to 12 of its year. */
  periodFrom: number;
  periodTo: number;
  /** The first and last day of those months. */
  fromDate: string;
  toDate: string;
  sections: BudgetVsActualSectionResult[];
  grossProfit: BudgetVsActualProfit;
  netProfit: BudgetVsActualProfit;
}

/** The periods the filter offers, in order. */
export const BUDGET_PERIODS: readonly { value: BudgetPeriod; label: string }[] = [
  { value: 'Month', label: 'Month' },
  { value: 'Quarter', label: 'Quarter' },
  { value: 'YearToDate', label: 'Year to date' },
];

/**
 * The budget the page opens on: the branch's own approved budget for the year
 * holding today, else any approved one for that year, else the latest year's
 * first. Null when there are none.
 */
export function defaultBudget(budgets: readonly BudgetVsActualBudget[], today: string): BudgetVsActualBudget | null {
  const thisYear = budgets.filter((b) => b.financialYearStart <= today && today <= b.financialYearEnd);
  return (
    thisYear.find((b) => b.isBranchDefault && b.status === 'Approved') ??
    thisYear.find((b) => b.status === 'Approved') ??
    thisYear[0] ??
    budgets[0] ??
    null
  );
}

/** A date moved into the budget's year: its first day when before, its last when after. */
export function dateWithinBudgetYear(date: string, budget: BudgetVsActualBudget): string {
  if (date < budget.financialYearStart) {
    return budget.financialYearStart;
  }
  return date > budget.financialYearEnd ? budget.financialYearEnd : date;
}

/** A budget as the filter names it: its name, and its status unless approved. */
export function budgetLabel(budget: BudgetVsActualBudget): string {
  return budget.status === 'Approved' ? budget.name : `${budget.name} (${budget.status.toLowerCase()})`;
}

/** Favourable, adverse, or neither: the variance is already signed so that positive is favourable. */
export function varianceTone(variance: number | null | undefined): 'favourable' | 'adverse' | null {
  if (variance === null || variance === undefined || variance === 0) {
    return null;
  }
  return variance > 0 ? 'favourable' : 'adverse';
}

/** A variance percentage, signed, to two places; a dash without a budget. */
export function variancePercentText(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return '—';
  }
  const sign = value > 0 ? '+' : value < 0 ? '−' : '';
  return `${sign}${Math.abs(value).toFixed(2)}%`;
}
