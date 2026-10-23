/**
 * The Bank Summary's contract (TK-219), field for field with
 * `BankSummaryReportModels.cs`, and the pure steps the page takes from an answer:
 * for each bank and cash account, what it held when a period began, what came in,
 * what went out, any revaluation, and what it holds at the end. Every figure is in
 * the branch's base currency; a foreign currency account also has its own.
 * Dates are ISO `yyyy-MM-dd`.
 */

/** What kind of account a bank account is, as Accounting names it. */
export type BankAccountKind = 'Savings' | 'Current' | 'OverDraft' | 'CashCredit' | 'CreditCard' | 'Wallet' | 'Cash' | 'EEFC';

/** What the report is asked for. Both dates are required; the bank account narrows it. */
export interface BankSummaryRequest {
  fromDate: string;
  toDate: string;
  /** Only this bank account; every one when blank. */
  bankAccountId?: number | null;
}

/** One bank or cash account over the period. */
export interface BankSummaryAccount {
  bankAccountId: number;
  accountName: string;
  accountNumber: string;
  accountType: BankAccountKind;
  currencyCode: string;
  isActive: boolean;
  /** Kept in a currency other than the branch's, so it has its own-currency figures too. */
  isForeign: boolean;
  openingBalance: number;
  /** Debits in the period, revaluation excluded. */
  received: number;
  /** Credits in the period, revaluation excluded. */
  spent: number;
  /** The net of the period's postings on documents that also post to Unrealized FX Gain/Loss. */
  revaluation: number;
  closingBalance: number;
  ownOpeningBalance: number | null;
  ownReceived: number | null;
  ownSpent: number | null;
  ownClosingBalance: number | null;
}

export interface BankSummaryResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  accounts: BankSummaryAccount[];
  totalOpeningBalance: number;
  totalReceived: number;
  totalSpent: number;
  totalRevaluation: number;
  totalClosingBalance: number;
}

const KIND_LABELS: Record<BankAccountKind, string> = {
  Savings: 'Savings',
  Current: 'Current',
  OverDraft: 'Overdraft',
  CashCredit: 'Cash credit',
  CreditCard: 'Credit card',
  Wallet: 'Wallet',
  Cash: 'Cash',
  EEFC: 'EEFC',
};

/** How a kind of account reads on the page. */
export function bankKindLabel(kind: BankAccountKind): string {
  return KIND_LABELS[kind] ?? kind;
}

export type BankSummaryRowKind = 'account' | 'total';

/** One row of the grid: an account, or the totals. */
export interface BankSummaryGridRow {
  kind: BankSummaryRowKind;
  bankAccountId: number;
  name: string;
  /** The account number and kind, such as "Current · 0123456789"; empty on the totals. */
  detail: string;
  currencyCode: string;
  isForeign: boolean;
  isActive: boolean;
  openingBalance: number;
  received: number;
  spent: number;
  /** Null when there is none, so the cell reads blank rather than as a nought. */
  revaluation: number | null;
  closingBalance: number;
  ownOpeningBalance: number | null;
  ownReceived: number | null;
  ownSpent: number | null;
  ownClosingBalance: number | null;
}

/** A kind and number joined for a second line, leaving out a number that is only a dash. */
export function accountDetail(account: Pick<BankSummaryAccount, 'accountType' | 'accountNumber'>): string {
  const number = account.accountNumber && account.accountNumber !== '-' ? ` · ${account.accountNumber}` : '';
  return `${bankKindLabel(account.accountType)}${number}`;
}

/**
 * The grid's rows: one for each account, then the totals when there is more than one
 * account to add up. Nothing is worked out here beyond the layout; the figures are the
 * server's.
 */
export function bankSummaryRows(result: BankSummaryResult): BankSummaryGridRow[] {
  const rows = result.accounts.map(
    (a): BankSummaryGridRow => ({
      kind: 'account',
      bankAccountId: a.bankAccountId,
      name: a.accountName,
      detail: accountDetail(a),
      currencyCode: a.currencyCode,
      isForeign: a.isForeign,
      isActive: a.isActive,
      openingBalance: a.openingBalance,
      received: a.received,
      spent: a.spent,
      revaluation: a.revaluation === 0 ? null : a.revaluation,
      closingBalance: a.closingBalance,
      ownOpeningBalance: a.ownOpeningBalance,
      ownReceived: a.ownReceived,
      ownSpent: a.ownSpent,
      ownClosingBalance: a.ownClosingBalance,
    }),
  );

  if (result.accounts.length < 2) {
    return rows;
  }

  return [
    ...rows,
    {
      kind: 'total',
      bankAccountId: 0,
      name: 'Total',
      detail: '',
      currencyCode: result.baseCurrencyCode,
      isForeign: false,
      isActive: true,
      openingBalance: result.totalOpeningBalance,
      received: result.totalReceived,
      spent: result.totalSpent,
      revaluation: result.totalRevaluation === 0 ? null : result.totalRevaluation,
      closingBalance: result.totalClosingBalance,
      ownOpeningBalance: null,
      ownReceived: null,
      ownSpent: null,
      ownClosingBalance: null,
    },
  ];
}

/** Whether any account is kept in another currency, which is when the page explains its own-currency figures. */
export function hasForeignAccounts(result: BankSummaryResult): boolean {
  return result.accounts.some((a) => a.isForeign);
}

/**
 * What the period came to, in a sentence: the opening balance, what came in and went
 * out, the revaluation when there is one, and the closing balance. Money is formatted
 * by the caller so the sentence reads in the branch's own currency.
 */
export function bankSummarySentence(result: BankSummaryResult, money: (value: number) => string): string {
  const count = `${result.accounts.length} ${result.accounts.length === 1 ? 'account' : 'accounts'}`;
  const revaluation = result.totalRevaluation === 0 ? '' : ` ${result.totalRevaluation < 0 ? '−' : '+'} revaluation ${money(Math.abs(result.totalRevaluation))}`;

  return `${count}: opening ${money(result.totalOpeningBalance)} + received ${money(result.totalReceived)} − spent ${money(result.totalSpent)}${revaluation} = closing ${money(result.totalClosingBalance)}.`;
}
