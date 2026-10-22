/**
 * The Trial Balance's contract (TK-215), field for field with
 * `TrialBalanceReportModels.cs`, and the pure steps from an answer to the grid's
 * rows. Every figure is in the branch's base currency. Dates are ISO
 * `yyyy-MM-dd`.
 */

/** What the report is asked for. From and to are required; the rest are optional. */
export interface TrialBalanceReportRequest {
  from: string;
  to: string;
  compareFrom?: string | null;
  compareTo?: string | null;
  isZeroBalanceShown?: boolean;
}

/** One account's opening balance, movement and closing balance; each balance is in one column. */
export interface TrialBalanceReportLine {
  /** Zero for a worked-out line, which is not an account. */
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
  isContra: boolean;
  /** The profit of earlier years carried into equity so the columns agree. Not an account. */
  isComputed: boolean;
  openingDebit: number;
  openingCredit: number;
  movementDebit: number;
  movementCredit: number;
  closingDebit: number;
  closingCredit: number;
  compareClosingDebit: number | null;
  compareClosingCredit: number | null;
}

/** Columns added up, for an account type or the whole report. */
export interface TrialBalanceReportTotals {
  openingDebit: number;
  openingCredit: number;
  movementDebit: number;
  movementCredit: number;
  closingDebit: number;
  closingCredit: number;
  compareClosingDebit: number | null;
  compareClosingCredit: number | null;
}

export interface TrialBalanceReportGroup {
  accountTypeId: number;
  lines: TrialBalanceReportLine[];
  totals: TrialBalanceReportTotals;
}

export interface TrialBalanceReportResult {
  from: string;
  to: string;
  compareFrom: string | null;
  compareTo: string | null;
  financialYearStart: string;
  compareFinancialYearStart: string | null;
  groups: TrialBalanceReportGroup[];
  totals: TrialBalanceReportTotals;
  /** Debits less credits of each pair of columns; zero when the books are sound. */
  openingDifference: number;
  movementDifference: number;
  closingDifference: number;
  compareClosingDifference: number | null;
  isBalanced: boolean;
}

/**
 * The five account types, named as a trial balance reads them. The ids are
 * `mst.AccountTypes`' and are fixed, so the names are not fetched.
 */
const TYPE_TITLES: Readonly<Record<number, string>> = {
  1: 'Assets',
  2: 'Liabilities',
  3: 'Equity',
  4: 'Income',
  5: 'Expenses',
};

export function accountTypeTitle(accountTypeId: number): string {
  return TYPE_TITLES[accountTypeId] ?? `Account type ${accountTypeId}`;
}

export type TrialBalanceRowKind = 'group' | 'line' | 'subtotal' | 'total';

/** One row of the grid: a heading, an account, a type's subtotal, or the totals. */
export interface TrialBalanceRow {
  kind: TrialBalanceRowKind;
  accountId: number;
  accountCode: string;
  accountName: string;
  isContra: boolean;
  isComputed: boolean;
  openingDebit: number | null;
  openingCredit: number | null;
  movementDebit: number | null;
  movementCredit: number | null;
  closingDebit: number | null;
  closingCredit: number | null;
  compareDebit: number | null;
  compareCredit: number | null;
}

/** An empty side of an account reads blank rather than as a nought. */
function blankZero(value: number | null): number | null {
  return value === null || value === 0 ? null : value;
}

function blank(kind: TrialBalanceRowKind, name: string): TrialBalanceRow {
  return {
    kind,
    accountId: 0,
    accountCode: '',
    accountName: name,
    isContra: false,
    isComputed: false,
    openingDebit: null,
    openingCredit: null,
    movementDebit: null,
    movementCredit: null,
    closingDebit: null,
    closingCredit: null,
    compareDebit: null,
    compareCredit: null,
  };
}

function lineRow(line: TrialBalanceReportLine): TrialBalanceRow {
  return {
    ...blank('line', line.accountName),
    accountId: line.accountId,
    accountCode: line.accountCode,
    isContra: line.isContra,
    isComputed: line.isComputed,
    openingDebit: blankZero(line.openingDebit),
    openingCredit: blankZero(line.openingCredit),
    movementDebit: blankZero(line.movementDebit),
    movementCredit: blankZero(line.movementCredit),
    closingDebit: blankZero(line.closingDebit),
    closingCredit: blankZero(line.closingCredit),
    compareDebit: blankZero(line.compareClosingDebit),
    compareCredit: blankZero(line.compareClosingCredit),
  };
}

function totalsRow(kind: 'subtotal' | 'total', name: string, totals: TrialBalanceReportTotals): TrialBalanceRow {
  // A subtotal's empty side is blank like an account's; the grand total shows
  // both sides, because two equal totals are the point of the report.
  const side = kind === 'subtotal' ? blankZero : (value: number | null): number | null => value;
  return {
    ...blank(kind, name),
    openingDebit: side(totals.openingDebit),
    openingCredit: side(totals.openingCredit),
    movementDebit: side(totals.movementDebit),
    movementCredit: side(totals.movementCredit),
    closingDebit: side(totals.closingDebit),
    closingCredit: side(totals.closingCredit),
    compareDebit: side(totals.compareClosingDebit),
    compareCredit: side(totals.compareClosingCredit),
  };
}

/**
 * The grid's rows: for each account type its heading, its accounts and its
 * subtotal, then the totals. Nothing is added or worked out here beyond the
 * layout; the figures are the server's.
 */
export function trialBalanceRows(result: TrialBalanceReportResult): TrialBalanceRow[] {
  const rows: TrialBalanceRow[] = [];

  for (const group of result.groups) {
    const title = accountTypeTitle(group.accountTypeId);
    rows.push(blank('group', title));
    rows.push(...group.lines.map(lineRow));
    rows.push(totalsRow('subtotal', `Total ${title.toLowerCase()}`, group.totals));
  }

  if (result.groups.length > 0) {
    rows.push(totalsRow('total', 'Total', result.totals));
  }

  return rows;
}

/** What the totals say about the books, in words as well as in colour. */
export interface TrialBalanceVerdict {
  isBalanced: boolean;
  text: string;
}

/**
 * "In balance" or how far out, spelled out. Money is formatted by the caller
 * so the sentence reads in the branch's own currency and grouping.
 */
export function trialBalanceVerdict(
  result: TrialBalanceReportResult,
  money: (value: number) => string,
): TrialBalanceVerdict {
  if (result.isBalanced) {
    return {
      isBalanced: true,
      text: `The trial balance is in balance: debits and credits both come to ${money(result.totals.closingDebit)} at the close of the period.`,
    };
  }

  const out: string[] = [];
  if (result.openingDifference !== 0) {
    out.push(`the opening balances by ${money(Math.abs(result.openingDifference))}`);
  }
  if (result.movementDifference !== 0) {
    out.push(`the period's postings by ${money(Math.abs(result.movementDifference))}`);
  }
  if (result.closingDifference !== 0) {
    out.push(`the closing balances by ${money(Math.abs(result.closingDifference))}`);
  }
  if ((result.compareClosingDifference ?? 0) !== 0) {
    out.push(`the compared period's closing balances by ${money(Math.abs(result.compareClosingDifference ?? 0))}`);
  }

  return {
    isBalanced: false,
    text: `The trial balance is out of balance: debits and credits disagree in ${out.join(', ')}. Every posting is checked when it is written, so this should not happen; find out why before anything is filed.`,
  };
}

/** The same day a year earlier; 29 February becomes 28 February. */
export function sameDayLastYear(isoDate: string): string {
  const [year, month, day] = isoDate.split('-').map(Number);
  const lastYear = year - 1;
  const lastDay = month === 2 && day === 29 ? 28 : day;
  return `${lastYear}-${`${month}`.padStart(2, '0')}-${`${lastDay}`.padStart(2, '0')}`;
}

/** The same days a year earlier, for the comparison period. */
export function samePeriodLastYear(from: string, to: string): { from: string; to: string } {
  return { from: sameDayLastYear(from), to: sameDayLastYear(to) };
}
