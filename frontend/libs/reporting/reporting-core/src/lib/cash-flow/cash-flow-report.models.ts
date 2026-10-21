/**
 * The Cash Flow Statement – Direct's contract (TK-201), field for field with
 * `CashFlowReportModels.cs`. Every figure is in the branch's base currency,
 * and an inflow is positive.
 */

/** What the report is asked for. Dates are ISO `yyyy-MM-dd`; from and to are required. */
export interface CashFlowReportRequest {
  fromDate: string;
  toDate: string;
  compareFromDate?: string | null;
  compareToDate?: string | null;
}

export type CashFlowActivity = 'Operating' | 'Investing' | 'Financing';

export type CashFlowLineKind =
  | 'ReceiptsFromCustomers'
  | 'PaymentsToSuppliersAndEmployees'
  | 'GstAndTds'
  | 'OtherOperating'
  | 'FixedAssetPurchases'
  | 'FixedAssetSales'
  | 'CapitalAndDrawings'
  | 'ExchangeRateEffect';

/** The cash one account on the other side of the movements accounts for, within a line. */
export interface CashFlowAccount {
  accountId: number;
  accountCode: string;
  accountName: string;
  amount: number;
  compareAmount: number | null;
}

export interface CashFlowLine {
  line: CashFlowLineKind;
  title: string;
  amount: number;
  compareAmount: number | null;
  accounts: CashFlowAccount[];
}

export interface CashFlowSection {
  activity: CashFlowActivity;
  title: string;
  lines: CashFlowLine[];
  /** Net cash from the activity. */
  total: number;
  compareTotal: number | null;
}

export interface CashFlowTotals {
  netChange: number;
  /** Cash on the day before the period, with opening balances entered within it. */
  openingCash: number;
  closingCash: number;
  /** Closing less opening, the net change and the exchange effect: zero when they reconcile. */
  difference: number;
  compareNetChange: number | null;
  compareOpeningCash: number | null;
  compareClosingCash: number | null;
  compareDifference: number | null;
}

export interface CashFlowReportResult {
  fromDate: string;
  toDate: string;
  compareFromDate: string | null;
  compareToDate: string | null;
  sections: CashFlowSection[];
  exchangeRateEffect: CashFlowLine;
  totals: CashFlowTotals;
  isReconciled: boolean;
}

/** One row of an activity's table: a line, or an account under it. */
export interface CashFlowRow {
  kind: 'line' | 'account';
  code: string;
  label: string;
  amount: number;
  compareAmount: number | null;
}

/** An activity's lines, each followed by the accounts that make it up, as table rows. */
export function cashFlowRows(section: CashFlowSection): CashFlowRow[] {
  return section.lines.flatMap((line) => [
    { kind: 'line' as const, code: '', label: line.title, amount: line.amount, compareAmount: line.compareAmount },
    ...line.accounts.map((account) => ({
      kind: 'account' as const,
      code: account.accountCode,
      label: account.accountName,
      amount: account.amount,
      compareAmount: account.compareAmount,
    })),
  ]);
}
