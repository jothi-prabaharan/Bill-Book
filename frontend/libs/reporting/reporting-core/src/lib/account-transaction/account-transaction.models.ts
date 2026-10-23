/**
 * The Account Transaction report's contract (TK-218), field for field with
 * `AccountTransactionReportModels.cs`, and the pure steps the page takes from an
 * answer: every posting in a period, account by account, with its contact,
 * currency, tax rate and a running balance on the account's normal side.
 * Dates are ISO `yyyy-MM-dd`.
 */

/** Where an account grows, and the side its balances here are on. */
export type AccountSide = 'Debit' | 'Credit';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface AccountTransactionRequest {
  fromDate: string;
  toDate: string;
  /** The accounts wanted; every account that has a posting when empty. */
  accountIds?: readonly number[];
  contactId?: number | null;
  /** The tax rate's group id: every revision of it. */
  taxGroupId?: number | null;
  /** The document type that posted it, such as INV or BIL; every source when blank. */
  source?: string | null;
  /** The page of postings wanted, from 1. */
  pageNumber?: number;
  pageSize?: number;
}

/** One posting to an account. */
export interface AccountTransactionRow {
  ledgerId: number;
  date: string;
  documentNo: string;
  sourceCode: string;
  transactionId: number;
  journalId: number | null;
  relatedAccount: string | null;
  contactId: number | null;
  contactCode: string | null;
  contactName: string | null;
  description: string | null;
  reference: string | null;
  taxRate: number | null;
  taxName: string | null;
  permitNo: string | null;
  currencyCode: string;
  /** The rate the document was booked at; never looked up again. */
  exchangeRate: number;
  debitSource: number;
  creditSource: number;
  debit: number;
  credit: number;
  /** The account's balance after this posting, on its normal side. */
  runningBalance: number;
}

/** One account: its balances for the whole period, and the postings of the pages loaded. */
export interface AccountTransactionAccount {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
  isContra: boolean;
  normalSide: AccountSide;
  openingBalance: number;
  closingBalance: number;
  totalDebit: number;
  totalCredit: number;
  /** How many postings the account has in the period, on every page. */
  postingCount: number;
  /** The account's earlier postings are on a page before the first one loaded for it. */
  hasEarlierRows: boolean;
  /** The account's later postings are on a page not yet loaded. */
  hasLaterRows: boolean;
  rows: AccountTransactionRow[];
}

export interface AccountTransactionResult {
  fromDate: string;
  toDate: string;
  financialYearStart: string;
  pageNumber: number;
  pageSize: number;
  /** Every posting the filters match, not only those loaded. */
  totalPostings: number;
  totalAccounts: number;
  totalDebit: number;
  totalCredit: number;
  accounts: AccountTransactionAccount[];
}

/** One tax rate for the filter: the group every revision of it shares, and its name. */
export interface TaxRateOption {
  taxGroupId: number;
  label: string;
}

/** The columns a posting can show, with the ones shown until the reader chooses. */
export interface TransactionColumn {
  key: string;
  header: string;
  /** Shown before the reader has chosen. */
  isDefault: boolean;
}

export const TRANSACTION_COLUMNS: readonly TransactionColumn[] = [
  { key: 'date', header: 'Date', isDefault: true },
  { key: 'documentNo', header: 'Document', isDefault: true },
  { key: 'source', header: 'Source', isDefault: false },
  { key: 'relatedAccount', header: 'Related account', isDefault: false },
  { key: 'contact', header: 'Contact', isDefault: true },
  { key: 'description', header: 'Description', isDefault: true },
  { key: 'reference', header: 'Reference', isDefault: false },
  { key: 'tax', header: 'Tax rate', isDefault: false },
  { key: 'permitNo', header: 'Permit no.', isDefault: false },
  { key: 'foreign', header: 'Foreign amount', isDefault: false },
  { key: 'debit', header: 'Debit', isDefault: true },
  { key: 'credit', header: 'Credit', isDefault: true },
  { key: 'balance', header: 'Balance', isDefault: true },
];

/** The keys shown before the reader chooses, in column order. */
export const DEFAULT_TRANSACTION_COLUMNS: readonly string[] = TRANSACTION_COLUMNS.filter((c) => c.isDefault).map((c) => c.key);

/** "Dr" or "Cr": the letter after a balance. */
export function sideOf(side: AccountSide): 'Dr' | 'Cr' {
  return side === 'Debit' ? 'Dr' : 'Cr';
}

/**
 * A posting's amount in its own currency with the rate it was booked at, such as
 * "USD 10.00 @ 83.5"; empty in the base currency.
 */
export function foreignAmount(
  row: Pick<AccountTransactionRow, 'currencyCode' | 'exchangeRate' | 'debitSource' | 'creditSource'>,
): string {
  if (row.exchangeRate === 1) {
    return '';
  }

  const amount = row.debitSource !== 0 ? row.debitSource : row.creditSource;
  return `${row.currencyCode} ${amount.toFixed(2)} @ ${row.exchangeRate}`;
}

/** A tax leg's rate as the tax master names it, such as "GST 18% (18%)" trimmed to "GST 18%" when the name already says it; empty on any other leg. */
export function taxLabel(row: Pick<AccountTransactionRow, 'taxName' | 'taxRate'>): string {
  if (row.taxRate === null && !row.taxName) {
    return '';
  }

  const rate = row.taxRate === null ? '' : `${Number(row.taxRate.toFixed(2))}%`;
  const name = row.taxName ?? '';

  if (!name) {
    return rate;
  }

  return rate === '' || name.includes(rate) ? name : `${name} (${rate})`;
}

export type TransactionRowKind = 'opening' | 'posting' | 'closing';

/** One row of an account's grid: its opening balance, a posting, or its closing balance with the period's totals. */
export interface TransactionGridRow {
  kind: TransactionRowKind;
  ledgerId: number;
  date: string | null;
  documentNo: string;
  sourceCode: string;
  relatedAccount: string | null;
  contactName: string | null;
  description: string;
  reference: string | null;
  tax: string;
  permitNo: string | null;
  foreign: string;
  debit: number | null;
  credit: number | null;
  balance: number;
}

/** An empty side of a posting reads blank rather than as a nought. */
function blankZero(value: number): number | null {
  return value === 0 ? null : value;
}

/**
 * An account's rows for its grid: the opening balance unless the postings before
 * it are on a page not loaded, each posting, and the closing balance with the
 * period's debit and credit totals unless more postings are still to come. The
 * figures are the server's; nothing here is worked out beyond the layout.
 */
export function transactionRows(account: AccountTransactionAccount): TransactionGridRow[] {
  const blank = {
    ledgerId: 0,
    date: null,
    documentNo: '',
    sourceCode: '',
    relatedAccount: null,
    contactName: null,
    reference: null,
    tax: '',
    permitNo: null,
    foreign: '',
  };

  const rows: TransactionGridRow[] = [];

  if (!account.hasEarlierRows) {
    rows.push({ ...blank, kind: 'opening', description: 'Opening balance', debit: null, credit: null, balance: account.openingBalance });
  }

  for (const row of account.rows) {
    rows.push({
      kind: 'posting',
      ledgerId: row.ledgerId,
      date: row.date,
      documentNo: row.documentNo,
      sourceCode: row.sourceCode,
      relatedAccount: row.relatedAccount,
      contactName: row.contactName,
      description: row.description ?? '',
      reference: row.reference,
      tax: taxLabel(row),
      permitNo: row.permitNo,
      foreign: foreignAmount(row),
      debit: blankZero(row.debit),
      credit: blankZero(row.credit),
      balance: row.runningBalance,
    });
  }

  if (!account.hasLaterRows) {
    rows.push({
      ...blank,
      kind: 'closing',
      description: 'Closing balance',
      debit: blankZero(account.totalDebit),
      credit: blankZero(account.totalCredit),
      balance: account.closingBalance,
    });
  }

  return rows;
}

/** How many postings the pages loaded so far hold. */
export function loadedPostings(result: AccountTransactionResult): number {
  return result.accounts.reduce((total, account) => total + account.rows.length, 0);
}

/** Whether postings are still to be loaded. */
export function hasMoreTransactions(result: AccountTransactionResult): boolean {
  return loadedPostings(result) < result.totalPostings;
}

/**
 * The pages loaded so far with the next one added. An account that runs over a page
 * boundary is one account: the postings of the second page go under those of the
 * first, and it keeps the first's opening and the second's closing.
 */
export function appendTransactionPage(loaded: AccountTransactionResult, next: AccountTransactionResult): AccountTransactionResult {
  const accounts = loaded.accounts.map((account) => ({ ...account, rows: [...account.rows] }));
  const last = accounts[accounts.length - 1];

  for (const incoming of next.accounts) {
    if (last && incoming.accountId === last.accountId && accounts[accounts.length - 1] === last && incoming.hasEarlierRows) {
      last.rows.push(...incoming.rows);
      last.hasLaterRows = incoming.hasLaterRows;
      continue;
    }

    accounts.push({ ...incoming, rows: [...incoming.rows] });
  }

  return { ...next, accounts };
}

/**
 * What the period came to, in a sentence: how many postings to how many accounts,
 * and the debits and credits added up. Money is formatted by the caller so the
 * sentence reads in the branch's own currency.
 */
export function transactionSentence(result: AccountTransactionResult, money: (value: number) => string): string {
  const postings = `${result.totalPostings} ${result.totalPostings === 1 ? 'posting' : 'postings'}`;
  const accounts = `${result.totalAccounts} ${result.totalAccounts === 1 ? 'account' : 'accounts'}`;

  return `${postings} to ${accounts}: debits ${money(result.totalDebit)}, credits ${money(result.totalCredit)}.`;
}

/** The tax rates for the filter, one for each group, named as its latest revision is. */
export function taxRateOptions(
  rates: readonly { taxGroupId: number; taxMasterId: number; taxName: string; totalRate: number; effectiveFrom: string; effectiveTo: string | null }[],
): TaxRateOption[] {
  const latest = new Map<number, (typeof rates)[number]>();

  for (const rate of rates) {
    const group = rate.taxGroupId || rate.taxMasterId;
    const held = latest.get(group);

    // The revision still in force wins, else the one that began last.
    const better =
      !held ||
      (rate.effectiveTo === null && held.effectiveTo !== null) ||
      ((rate.effectiveTo === null) === (held.effectiveTo === null) && rate.effectiveFrom > held.effectiveFrom);

    if (better) {
      latest.set(group, rate);
    }
  }

  return [...latest.entries()]
    .map(([taxGroupId, rate]) => ({ taxGroupId, label: taxLabel({ taxName: rate.taxName, taxRate: rate.totalRate }) }))
    .sort((a, b) => a.label.localeCompare(b.label));
}
