/**
 * The Reconciliation Report's contract (TK-202), field for field with
 * `BankReconciliationReportModels.cs`. Every figure is in the bank account's
 * own currency.
 */

/** What the report is asked for: one bank account at the end of one day. The date is ISO `yyyy-MM-dd`. */
export interface BankReconciliationReportRequest {
  bankAccountId: number;
  asOfDate: string;
}

export interface BankReconciliationAccount {
  bankAccountId: number;
  accountName: string;
  accountNumber: string;
  currencyCode: string;
  isActive: boolean;
}

export type BankReconciliationGroupKind = 'OutstandingPayments' | 'OutstandingReceipts' | 'StatementLinesNotInBooks';

/** An outstanding document, or a statement line not in the books. */
export interface BankReconciliationItem {
  /** `SPM`, `RCM` or `TRM` for a document; null for a statement line. */
  transactionTypeCode: string | null;
  transactionId: number | null;
  bankStatementLineId: number | null;
  transactionDate: string;
  transactionNo: string | null;
  reference: string | null;
  description: string | null;
  amountIn: number;
  amountOut: number;
}

export interface BankReconciliationGroup {
  group: BankReconciliationGroupKind;
  title: string;
  items: BankReconciliationItem[];
  /** What the group adds to the books to reach the statement. */
  adjustment: number;
}

export interface BankReconciliationReportResult {
  asOfDate: string;
  bankAccountId: number;
  accountName: string;
  accountNumber: string;
  currencyCode: string;
  bookBalance: number;
  bookBalanceBase: number;
  groups: BankReconciliationGroup[];
  expectedStatementBalance: number;
  /** Null when no imported statement gives a balance on or before the day. */
  statementBalance: number | null;
  statementBalanceDate: string | null;
  difference: number | null;
  isReconciled: boolean;
}

/** How a bank account reads in the account filter: name, number, currency, and whether it is closed. */
export function bankAccountLabel(account: BankReconciliationAccount): string {
  const number = account.accountNumber && account.accountNumber !== '-' ? ` · ${account.accountNumber}` : '';
  return `${account.accountName}${number} (${account.currencyCode})${account.isActive ? '' : ' · inactive'}`;
}
