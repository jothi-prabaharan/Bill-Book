/**
 * Paying for a till sale (TK-40), with the same rules the server applies
 * (TK-39, `PosSaleService.CheckTenders`), so the till refuses what the server
 * would refuse before it sends anything. Amounts are in paise.
 */

export type TenderMode = 'Cash' | 'Card' | 'Upi';

export interface TenderLine {
  mode: TenderMode;
  /** Paise. */
  amount: number;
  bankAccountId: number;
  reference?: string;
}

export interface TenderSummary {
  paid: number;
  /** Still to pay, never negative. */
  remaining: number;
  /** Cash to hand back, never negative. */
  change: number;
  /** Why the sale cannot complete yet, or null when it can. */
  problem: string | null;
}

/**
 * The total a till sale is paid to: rounded to the rupee, as the invoice's
 * round-off line does on the server (owner's decision, 24 September 2026).
 */
export function roundToRupee(paise: number): number {
  return Math.round(paise / 100) * 100;
}

export function tenderSummary(total: number, tenders: readonly TenderLine[]): TenderSummary {
  const paid = tenders.reduce((sum, t) => sum + t.amount, 0);
  const nonCash = tenders.filter((t) => t.mode !== 'Cash').reduce((sum, t) => sum + t.amount, 0);
  const remaining = Math.max(0, total - paid);
  const change = Math.max(0, paid - total);

  let problem: string | null = null;
  if (tenders.length === 0) {
    problem = 'Add a tender.';
  } else if (tenders.some((t) => t.amount <= 0)) {
    problem = 'Every tender must be more than zero.';
  } else if (nonCash > total) {
    problem = 'Card and UPI can pay at most the total. Only cash gives change.';
  } else if (paid < total) {
    problem = 'The tenders do not cover the total yet.';
  }

  return { paid, remaining, change, problem };
}

// ---- The accounts a tender goes into (TK-268) ---------------------------------

/**
 * Whether an account can take a tender made in this mode: cash goes into cash in
 * hand, and card and UPI into any other account the till may use. The Into list
 * and the account a mode starts on follow this one rule.
 */
export function accountFitsMode(mode: TenderMode, account: { accountType: string }): boolean {
  return (mode === 'Cash') === (account.accountType === 'Cash');
}

/** The kinds of account the till can take money into, as the server names them. */
export type TenderAccountKind = 'Cash' | 'Current' | 'Savings' | 'Wallet' | 'EEFC';

/**
 * The kinds the Add account popup offers for a mode: the ones `accountFitsMode`
 * accepts, and never an overdraft, cash credit or credit card, which the server
 * leaves out of the till's list, so an account of those kinds would be saved and
 * then not appear.
 */
export function tenderAccountKinds(mode: TenderMode): readonly TenderAccountKind[] {
  return mode === 'Cash' ? ['Cash'] : ['Current', 'Savings', 'Wallet', 'EEFC'];
}

/** What the Add account popup starts with: cash in hand for cash, a current account otherwise. */
export function newAccountPreset(mode: TenderMode): {
  accountType: TenderAccountKind;
  accountName: string;
  accountNumber: string;
} {
  return mode === 'Cash'
    ? { accountType: 'Cash', accountName: 'Cash in hand', accountNumber: 'CASH' }
    : { accountType: 'Current', accountName: '', accountNumber: '' };
}

/** The popup's heading for a mode. */
export function newAccountHeading(mode: TenderMode): string {
  return mode === 'Cash' ? 'Add a cash account' : 'Add a bank account';
}

/**
 * What the till says when no account fits the mode, so the cashier is told what
 * is missing and who can supply it instead of meeting an empty list.
 */
export function tenderAccountNotice(mode: TenderMode, canAdd: boolean): string {
  const missing =
    mode === 'Cash' ? 'There is no cash account yet.' : `There is no bank account for ${mode === 'Upi' ? 'UPI' : 'card'} payments yet.`;

  return canAdd
    ? `${missing} Use + beside Into to add one.`
    : `${missing} Ask the owner to add one under Banking › Bank accounts.`;
}
