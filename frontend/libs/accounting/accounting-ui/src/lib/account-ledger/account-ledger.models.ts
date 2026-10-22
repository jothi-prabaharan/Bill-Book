/**
 * The General ledger's contract (TK-216), field for field with
 * `GeneralLedgerReportModels.cs`, and the pure steps from an answer to the grid's
 * rows. Every figure is in the branch's base currency, and every balance is on
 * the account's normal side. Dates are ISO `yyyy-MM-dd`.
 */

/** Where an account grows, and the side its balances are shown on. */
export type LedgerSide = 'Debit' | 'Credit';

/** One posting, with everything needed to get back to the document that wrote it. */
export interface GeneralLedgerReportRow {
  ledgerId: number;
  ledgerDate: string;
  /** The document's own number; for an old row, its type and id joined, such as INV-42. */
  documentNo: string;
  /** The document type: INV, BIL, JRN, STA. */
  transactionTypeCode: string;
  transactionId: number;
  transactionDetailId: number;
  journalId: number | null;
  journalNo: string | null;
  subAccountName: string | null;
  contactId: number | null;
  contactName: string | null;
  projectId: number | null;
  projectName: string | null;
  description: string | null;
  debitAmount: number;
  creditAmount: number;
  debitAmountBase: number;
  creditAmountBase: number;
  currencyCode: string;
  exchangeRate: number;
  runningBalance: number;
}

/** One account over a period. */
export interface GeneralLedgerReportResult {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
  isContra: boolean;
  normalSide: LedgerSide;
  from: string;
  to: string;
  financialYearStart: string;
  contactId: number | null;
  projectId: number | null;
  openingBalance: number;
  closingBalance: number;
  totalDebit: number;
  totalCredit: number;
  rows: GeneralLedgerReportRow[];
}

/** One account's opening balance, the period's debits and credits, and its closing balance. */
export interface GeneralLedgerSummaryLine {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
  isContra: boolean;
  normalSide: LedgerSide;
  openingBalance: number;
  totalDebit: number;
  totalCredit: number;
  closingBalance: number;
}

export interface GeneralLedgerSummaryResult {
  from: string;
  to: string;
  financialYearStart: string;
  lines: GeneralLedgerSummaryLine[];
  totalDebit: number;
  totalCredit: number;
}

/** An income or expense account, whose balance starts again with each financial year. */
export function isProfitAndLoss(accountTypeId: number): boolean {
  return accountTypeId === 4 || accountTypeId === 5;
}

/** "Dr" or "Cr": the letter after a balance whose side is not the same on every row. */
export function sideLetter(side: LedgerSide): 'Dr' | 'Cr' {
  return side === 'Debit' ? 'Dr' : 'Cr';
}

export type LedgerRowKind = 'opening' | 'posting' | 'closing';

/** One row of the ledger grid: the opening balance, a posting, or the closing balance with the period's totals. */
export interface LedgerGridRow {
  kind: LedgerRowKind;
  ledgerId: number;
  /** Null on the opening and closing rows. */
  ledgerDate: string | null;
  documentNo: string;
  sourceCode: string;
  journalId: number | null;
  contactName: string | null;
  projectName: string | null;
  subAccountName: string | null;
  /** The posting's description, or "Opening balance" and "Closing balance" on theirs. */
  description: string;
  /** A posting in another currency, as "USD 100.00 at 83.5"; null in the base currency. */
  foreignNote: string | null;
  debit: number | null;
  credit: number | null;
  balance: number;
}

/** An empty side of a posting reads blank rather than as a nought. */
function blankZero(value: number): number | null {
  return value === 0 ? null : value;
}

/**
 * A posting's amount in its own currency when that is not the base one: the code,
 * the amount and the rate it was booked at. The rate is the document's own and is
 * never looked up again.
 */
export function foreignNote(row: Pick<GeneralLedgerReportRow, 'currencyCode' | 'exchangeRate' | 'debitAmount' | 'creditAmount'>): string | null {
  if (!row.currencyCode || row.exchangeRate === 1) {
    return null;
  }

  const amount = row.debitAmount !== 0 ? row.debitAmount : row.creditAmount;
  return `${row.currencyCode} ${amount.toFixed(2)} at ${row.exchangeRate}`;
}

/**
 * The grid's rows: the opening balance, each posting in the order the server
 * gives, and the closing balance carrying the period's debit and credit totals.
 * Nothing is worked out here beyond the layout; the figures are the server's.
 */
export function ledgerRows(result: GeneralLedgerReportResult): LedgerGridRow[] {
  const opening: LedgerGridRow = {
    kind: 'opening',
    ledgerId: 0,
    ledgerDate: null,
    documentNo: '',
    sourceCode: '',
    journalId: null,
    contactName: null,
    projectName: null,
    subAccountName: null,
    description: 'Opening balance',
    foreignNote: null,
    debit: null,
    credit: null,
    balance: result.openingBalance,
  };

  const postings = result.rows.map(
    (row): LedgerGridRow => ({
      kind: 'posting',
      ledgerId: row.ledgerId,
      ledgerDate: row.ledgerDate,
      documentNo: row.documentNo,
      sourceCode: row.transactionTypeCode,
      journalId: row.journalId,
      contactName: row.contactName,
      projectName: row.projectName,
      subAccountName: row.subAccountName,
      description: row.description ?? '',
      foreignNote: foreignNote(row),
      debit: blankZero(row.debitAmountBase),
      credit: blankZero(row.creditAmountBase),
      balance: row.runningBalance,
    }),
  );

  const closing: LedgerGridRow = {
    ...opening,
    kind: 'closing',
    description: 'Closing balance',
    debit: blankZero(result.totalDebit),
    credit: blankZero(result.totalCredit),
    balance: result.closingBalance,
  };

  return [opening, ...postings, closing];
}

export type SummaryRowKind = 'line' | 'total';

/** One row of the summary grid: an account, or the totals. */
export interface SummaryGridRow {
  kind: SummaryRowKind;
  accountId: number;
  accountCode: string;
  accountName: string;
  isContra: boolean;
  /** Null on the totals, whose balances have no single side. */
  side: LedgerSide | null;
  openingBalance: number | null;
  debit: number | null;
  credit: number | null;
  closingBalance: number | null;
}

/**
 * The summary's rows: each account that moved or holds a balance, then the totals
 * of the period's debits and credits. The two totals agree on books that balance;
 * the balances are not added, because they stand on different sides.
 */
export function summaryRows(result: GeneralLedgerSummaryResult): SummaryGridRow[] {
  if (result.lines.length === 0) {
    return [];
  }

  const lines = result.lines.map(
    (line): SummaryGridRow => ({
      kind: 'line',
      accountId: line.accountId,
      accountCode: line.accountCode,
      accountName: line.accountName,
      isContra: line.isContra,
      side: line.normalSide,
      openingBalance: line.openingBalance,
      debit: blankZero(line.totalDebit),
      credit: blankZero(line.totalCredit),
      closingBalance: line.closingBalance,
    }),
  );

  const total: SummaryGridRow = {
    kind: 'total',
    accountId: 0,
    accountCode: '',
    accountName: 'Total',
    isContra: false,
    side: null,
    openingBalance: null,
    debit: result.totalDebit,
    credit: result.totalCredit,
    closingBalance: null,
  };

  return [...lines, total];
}

/**
 * What the opening and closing figures say, in a sentence: the opening balance,
 * what grew the account, what reduced it, and where that leaves it. Money is
 * formatted by the caller so the sentence reads in the branch's own currency.
 */
export function ledgerEquation(result: GeneralLedgerReportResult, money: (value: number) => string): string {
  const debitNormal = result.normalSide === 'Debit';
  const grew = debitNormal ? result.totalDebit : result.totalCredit;
  const reduced = debitNormal ? result.totalCredit : result.totalDebit;

  return `Opening ${money(result.openingBalance)} + ${debitNormal ? 'debits' : 'credits'} ${money(grew)} − ${debitNormal ? 'credits' : 'debits'} ${money(reduced)} = closing ${money(result.closingBalance)}.`;
}
