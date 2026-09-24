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
