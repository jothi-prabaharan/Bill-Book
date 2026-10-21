/**
 * Business Performance's contract (TK-203), field for field with
 * `BusinessPerformanceReportModels.cs`. Money is in the branch's base currency.
 */

/** What the report is asked for. Dates are ISO `yyyy-MM-dd`; from and to are required. */
export interface BusinessPerformanceReportRequest {
  fromDate: string;
  toDate: string;
  compareFromDate?: string | null;
  compareToDate?: string | null;
}

export type BusinessPerformanceMetric =
  | 'GrossProfitMargin'
  | 'NetProfitMargin'
  | 'ReturnOnInvestment'
  | 'AverageDaysCustomersTakeToPay'
  | 'AverageDaysToPaySuppliers'
  | 'CurrentAssetsToLiabilities'
  | 'TermAssetsToLiabilities'
  | 'TotalCashBalance';

export type BusinessPerformanceGroup = 'Profitability' | 'Efficiency' | 'FinancialPosition' | 'Cash';

export type BusinessPerformanceUnit = 'Percent' | 'Days' | 'Times' | 'Amount';

export interface BusinessPerformanceRatio {
  metric: BusinessPerformanceMetric;
  group: BusinessPerformanceGroup;
  title: string;
  unit: BusinessPerformanceUnit;
  /** How the ratio is worked out, in words. */
  calculation: string;
  /** Null when there is nothing to divide by, or no books to read. */
  value: number | null;
  numerator: number | null;
  /** Null for the cash balance, which is an amount rather than a ratio. */
  denominator: number | null;
  compareValue: number | null;
  compareNumerator: number | null;
  compareDenominator: number | null;
  /** The value less the comparison's, in the ratio's unit. */
  change: number | null;
}

export interface BusinessPerformanceReportResult {
  fromDate: string;
  toDate: string;
  days: number;
  compareFromDate: string | null;
  compareToDate: string | null;
  compareDays: number | null;
  ratios: BusinessPerformanceRatio[];
}

/** The headings the page shows, in order. */
export const BUSINESS_PERFORMANCE_GROUPS: readonly { group: BusinessPerformanceGroup; title: string }[] = [
  { group: 'Profitability', title: 'Profitability' },
  { group: 'Efficiency', title: 'Efficiency' },
  { group: 'FinancialPosition', title: 'Financial position' },
  { group: 'Cash', title: 'Cash' },
];

/**
 * A ratio's value in its unit: a percentage, days, times, or money through the
 * given formatter. A dash when there is no value.
 */
export function ratioValueText(value: number | null, unit: BusinessPerformanceUnit, money: (amount: number) => string): string {
  if (value === null || value === undefined) {
    return '—';
  }
  switch (unit) {
    case 'Percent':
      return `${value.toFixed(2)}%`;
    case 'Days':
      return `${value.toFixed(2)} days`;
    case 'Times':
      return `${value.toFixed(2)} times`;
    default:
      return money(value);
  }
}

/** A change in a ratio's unit, signed: percentage points, days, times or money. Empty when there is none. */
export function ratioChangeText(change: number | null, unit: BusinessPerformanceUnit, money: (amount: number) => string): string {
  if (change === null || change === undefined) {
    return '';
  }
  const sign = change > 0 ? '+' : change < 0 ? '−' : '';
  const size = Math.abs(change);
  switch (unit) {
    case 'Percent':
      return `${sign}${size.toFixed(2)} pts`;
    case 'Days':
      return `${sign}${size.toFixed(2)} days`;
    case 'Times':
      return `${sign}${size.toFixed(2)}`;
    default:
      return `${sign}${money(size)}`;
  }
}

/** The period of the same length that ends the day before this one starts. */
export function previousPeriod(fromDate: string, toDate: string): { from: string; to: string } {
  const day = 24 * 60 * 60 * 1000;
  const start = Date.UTC(...ymd(fromDate));
  const end = Date.UTC(...ymd(toDate));
  const length = Math.round((end - start) / day) + 1;
  const previousEnd = new Date(start - day);
  const previousStart = new Date(start - length * day);
  return { from: iso(previousStart), to: iso(previousEnd) };
}

function ymd(value: string): [number, number, number] {
  const [year, month, day] = value.split('-').map(Number);
  return [year, month - 1, day];
}

function iso(date: Date): string {
  return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;
}
