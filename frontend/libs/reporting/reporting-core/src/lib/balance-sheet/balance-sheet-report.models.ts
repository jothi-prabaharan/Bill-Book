/**
 * The Balance Sheet's contract (TK-199), field for field with
 * `BalanceSheetReportModels.cs`. Every figure is in the branch's base currency.
 */

/** What the report is asked for. Dates are ISO `yyyy-MM-dd`. */
export interface BalanceSheetReportRequest {
  asOfDate: string;
  compareAsOfDate?: string | null;
  isZeroBalanceShown?: boolean;
}

export type BalanceSheetSection = 'Assets' | 'Liabilities' | 'Equity';

export interface BalanceSheetLine {
  /** Null for a worked-out line: the year's profit, or earlier years'. */
  accountId: number | null;
  accountCode: string | null;
  accountName: string;
  /** Runs opposite its type, so its balance is negative within the section. */
  isContra: boolean;
  /** Worked out from the income and expense accounts, not an account's own balance. */
  isComputed: boolean;
  balance: number;
  compareBalance: number | null;
}

export interface BalanceSheetSectionResult {
  section: BalanceSheetSection;
  title: string;
  lines: BalanceSheetLine[];
  total: number;
  compareTotal: number | null;
}

export interface BalanceSheetTotals {
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
  totalLiabilitiesAndEquity: number;
  /** Assets less liabilities and equity: zero when the ledger balances. */
  difference: number;
  compareTotalAssets: number | null;
  compareTotalLiabilities: number | null;
  compareTotalEquity: number | null;
  compareTotalLiabilitiesAndEquity: number | null;
  compareDifference: number | null;
}

export interface BalanceSheetReportResult {
  asOfDate: string;
  compareAsOfDate: string | null;
  financialYearStart: string;
  compareFinancialYearStart: string | null;
  sections: BalanceSheetSectionResult[];
  totals: BalanceSheetTotals;
  isBalanced: boolean;
}

/**
 * The same day a year earlier, for the comparison date. 29 February becomes
 * 28 February, the last day that month has in a common year.
 */
export function sameDayLastYear(iso: string): string {
  const [year, month, day] = iso.split('-').map(Number);
  const lastDay = new Date(Date.UTC(year - 1, month, 0)).getUTCDate();
  return `${year - 1}-${String(month).padStart(2, '0')}-${String(Math.min(day, lastDay)).padStart(2, '0')}`;
}
