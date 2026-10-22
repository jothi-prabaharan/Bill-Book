import { BudgetVsActualBudget, BudgetVsActualSection } from '../budget-vs-actual/budget-vs-actual-report.models';

/**
 * Budget Monthly Trend's contract (TK-214), field for field with
 * `BudgetMonthlyTrendReportModels.cs`, and the helpers its page uses to lay the
 * answer out. Money is in the branch's base currency.
 */

/** What the report is asked for. Both are required; the date is ISO `yyyy-MM-dd`, within the budget's year. */
export interface BudgetMonthlyTrendReportRequest {
  budgetId: number;
  asOfDate: string;
}

/** A budget against what was posted. A month after the as-at month has its budget only. */
export interface BudgetMonthlyTrendCell {
  budget: number;
  actual: number | null;
  /** Positive when favourable. */
  variance: number | null;
  variancePercent: number | null;
}

export interface BudgetMonthlyTrendMonth {
  /** The budget's period, 1 to 12. */
  period: number;
  fromDate: string;
  toDate: string;
  isFuture: boolean;
}

export interface BudgetMonthlyTrendLine {
  accountId: number;
  accountCode: string;
  accountName: string;
  isContra: boolean;
  months: BudgetMonthlyTrendCell[];
  yearToDate: BudgetMonthlyTrendCell;
  yearBudget: number;
}

export interface BudgetMonthlyTrendSection {
  section: BudgetVsActualSection;
  title: string;
  lines: BudgetMonthlyTrendLine[];
  months: BudgetMonthlyTrendCell[];
  yearToDate: BudgetMonthlyTrendCell;
  yearBudget: number;
}

export interface BudgetMonthlyTrendProfit {
  months: BudgetMonthlyTrendCell[];
  yearToDate: BudgetMonthlyTrendCell;
  yearBudget: number;
}

export interface BudgetMonthlyTrendReportResult {
  budget: BudgetVsActualBudget;
  asOfDate: string;
  /** The budget's period holding the as-at date: the last month with an actual. */
  asOfPeriod: number;
  fromDate: string;
  /** Where the year to date ends: the last day of the as-at date's month. */
  throughDate: string;
  months: BudgetMonthlyTrendMonth[];
  sections: BudgetMonthlyTrendSection[];
  grossProfit: BudgetMonthlyTrendProfit;
  netProfit: BudgetMonthlyTrendProfit;
}

/** One of the three figures a cell holds. */
export type TrendMeasure = 'Budget' | 'Actual' | 'Variance';

/** What the page shows: all three, or one. */
export type TrendShow = 'All' | TrendMeasure;

export const TREND_SHOW_OPTIONS: readonly { value: TrendShow; label: string }[] = [
  { value: 'All', label: 'Budget, actual and variance' },
  { value: 'Budget', label: 'Budget' },
  { value: 'Actual', label: 'Actual' },
  { value: 'Variance', label: 'Variance' },
];

/** The measures a choice shows, in the order they are drawn. */
export function trendMeasuresOf(show: TrendShow): TrendMeasure[] {
  return show === 'All' ? ['Budget', 'Actual', 'Variance'] : [show];
}

/** A cell's figure for a measure; null for a month with no actual yet. */
export function cellValue(cell: BudgetMonthlyTrendCell, measure: TrendMeasure): number | null {
  switch (measure) {
    case 'Budget':
      return cell.budget;
    case 'Actual':
      return cell.actual;
    default:
      return cell.variance;
  }
}

const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'] as const;

/** A month's column heading from the first day, ISO: `Apr 26`. Not the branch's date pattern, which has no month-only form. */
export function monthHeader(fromDate: string): string {
  const [year, month] = fromDate.split('-').map(Number);
  return `${MONTH_NAMES[month - 1]} ${String(year % 100).padStart(2, '0')}`;
}

/** What a grid row is. */
export type TrendRowKind = 'section' | 'account' | 'subtotal' | 'profit';

/** One grid row: twelve months, the year to date and the year's budget. A heading has no figures. */
export interface TrendRow {
  key: string;
  kind: TrendRowKind;
  accountCode: string;
  accountName: string;
  /** Null for a heading. */
  measure: TrendMeasure | null;
  /** True for the first row of an account's or total's group: the name is drawn in full there. */
  first: boolean;
  months: (number | null)[];
  yearToDate: number | null;
  /** The year to date's variance as a percentage of its budget: on a variance row only. */
  yearToDatePercent: number | null;
  /** The whole year's budget: on a budget row only. */
  yearBudget: number | null;
  /** Which months are after the as-at month, for the grid to grey. */
  future: boolean[];
}

interface Totals {
  months: BudgetMonthlyTrendCell[];
  yearToDate: BudgetMonthlyTrendCell;
  yearBudget: number;
}

/**
 * The report as grid rows: for each section that has accounts its heading, each
 * account and its total; gross profit after the last of revenue and cost of
 * sales that has accounts; net profit at the end. Each account, total and
 * profit line is one row per measure shown.
 */
export function trendRowsOf(result: BudgetMonthlyTrendReportResult, show: TrendShow): TrendRow[] {
  const measures = trendMeasuresOf(show);
  const future = result.months.map((m) => m.isFuture);
  const rows: TrendRow[] = [];

  const group = (key: string, kind: TrendRowKind, code: string, name: string, totals: Totals): void => {
    measures.forEach((measure, index) => {
      rows.push({
        key: `${key}-${measure}`,
        kind,
        accountCode: code,
        accountName: name,
        measure,
        first: index === 0,
        months: totals.months.map((c) => cellValue(c, measure)),
        yearToDate: cellValue(totals.yearToDate, measure),
        yearToDatePercent: measure === 'Variance' ? totals.yearToDate.variancePercent : null,
        yearBudget: measure === 'Budget' ? totals.yearBudget : null,
        future,
      });
    });
  };

  const withLines = result.sections.filter((s) => s.lines.length > 0);
  const grossAfter = [...withLines].reverse().find((s) => s.section === 'Revenue' || s.section === 'CostOfSales')?.section;

  for (const section of withLines) {
    rows.push({
      key: `${section.section}-heading`,
      kind: 'section',
      accountCode: '',
      accountName: section.title,
      measure: null,
      first: true,
      months: [],
      yearToDate: null,
      yearToDatePercent: null,
      yearBudget: null,
      future,
    });
    for (const line of section.lines) {
      group(`a${line.accountId}`, 'account', line.accountCode, line.accountName, line);
    }
    group(`${section.section}-total`, 'subtotal', '', `Total ${section.title.toLowerCase()}`, section);
    if (section.section === grossAfter) {
      group('gross', 'profit', '', 'Gross profit', result.grossProfit);
    }
  }

  group('net', 'profit', '', 'Net profit', result.netProfit);
  return rows;
}

/** A line the phone view can show: net or gross profit, a section's total, or an account. */
export interface TrendLineOption {
  key: string;
  label: string;
}

/** The lines to choose from, net profit first, then each section's total and its accounts. */
export function trendLineOptions(result: BudgetMonthlyTrendReportResult): TrendLineOption[] {
  const options: TrendLineOption[] = [
    { key: 'net', label: 'Net profit' },
    { key: 'gross', label: 'Gross profit' },
  ];
  for (const section of result.sections.filter((s) => s.lines.length > 0)) {
    options.push({ key: `section:${section.section}`, label: `Total ${section.title.toLowerCase()}` });
    for (const line of section.lines) {
      options.push({ key: `account:${line.accountId}`, label: `${line.accountCode} · ${line.accountName}` });
    }
  }
  return options;
}

/** The twelve months, the year to date and the year's budget of the line a key names; null when it names none. */
export function trendLine(result: BudgetMonthlyTrendReportResult, key: string): (Totals & { label: string }) | null {
  if (key === 'net') {
    return { label: 'Net profit', ...result.netProfit };
  }
  if (key === 'gross') {
    return { label: 'Gross profit', ...result.grossProfit };
  }
  if (key.startsWith('section:')) {
    const section = result.sections.find((s) => s.section === key.slice('section:'.length));
    return section ? { label: `Total ${section.title.toLowerCase()}`, ...section } : null;
  }
  if (key.startsWith('account:')) {
    const id = Number(key.slice('account:'.length));
    const line = result.sections.flatMap((s) => s.lines).find((l) => l.accountId === id);
    return line ? { label: `${line.accountCode} · ${line.accountName}`, ...line } : null;
  }
  return null;
}

/** A row of the phone view: one month of the chosen line, or its year to date. */
export interface TrendMonthRow {
  key: string;
  label: string;
  isFuture: boolean;
  isTotal: boolean;
  cell: BudgetMonthlyTrendCell;
}

/** The chosen line as a table of months with the year to date at the foot. */
export function trendMonthRows(result: BudgetMonthlyTrendReportResult, key: string): TrendMonthRow[] {
  const line = trendLine(result, key);
  if (!line) {
    return [];
  }
  return [
    ...line.months.map((cell, i) => ({
      key: `m${result.months[i].period}`,
      label: monthHeader(result.months[i].fromDate),
      isFuture: result.months[i].isFuture,
      isTotal: false,
      cell,
    })),
    { key: 'ytd', label: 'Year to date', isFuture: false, isTotal: true, cell: line.yearToDate },
  ];
}
