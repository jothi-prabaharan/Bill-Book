import { sameDayLastYear } from '../balance-sheet/balance-sheet-report.models';

/**
 * The Profit & Loss's contract (TK-200), field for field with
 * `ProfitAndLossReportModels.cs`. Every figure is in the branch's base currency.
 */

/** What the report is asked for. Dates are ISO `yyyy-MM-dd`; from and to are required. */
export interface ProfitAndLossReportRequest {
  fromDate: string;
  toDate: string;
  compareFromDate?: string | null;
  compareToDate?: string | null;
  isZeroBalanceShown?: boolean;
}

export type ProfitAndLossSection = 'Revenue' | 'CostOfSales' | 'OtherIncome' | 'Expenses';

export interface ProfitAndLossLine {
  accountId: number;
  accountCode: string;
  accountName: string;
  /** Runs opposite its type, such as sales returns, so its amount reduces the section. */
  isContra: boolean;
  amount: number;
  compareAmount: number | null;
}

export interface ProfitAndLossSectionResult {
  section: ProfitAndLossSection;
  title: string;
  lines: ProfitAndLossLine[];
  total: number;
  compareTotal: number | null;
}

export interface ProfitAndLossTotals {
  revenue: number;
  costOfSales: number;
  /** Revenue less cost of sales. */
  grossProfit: number;
  otherIncome: number;
  expenses: number;
  /** Gross profit plus other income less expenses. */
  netProfit: number;
  /** Percentages of revenue to two places; null without revenue. */
  grossMarginPercent: number | null;
  netMarginPercent: number | null;
  compareRevenue: number | null;
  compareCostOfSales: number | null;
  compareGrossProfit: number | null;
  compareOtherIncome: number | null;
  compareExpenses: number | null;
  compareNetProfit: number | null;
  compareGrossMarginPercent: number | null;
  compareNetMarginPercent: number | null;
}

export interface ProfitAndLossReportResult {
  fromDate: string;
  toDate: string;
  compareFromDate: string | null;
  compareToDate: string | null;
  sections: ProfitAndLossSectionResult[];
  totals: ProfitAndLossTotals;
}

/** The same days a year earlier, for the comparison period. */
export function samePeriodLastYear(fromDate: string, toDate: string): { from: string; to: string } {
  return { from: sameDayLastYear(fromDate), to: sameDayLastYear(toDate) };
}
