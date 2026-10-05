/**
 * Loyalty points (TK-166): the shapes Customer's `api/loyalty` returns and
 * the pure rules the bill page reads. `pointsFor` mirrors
 * `Shared.Kernel.Restaurant.LoyaltyRules.PointsFor`; the server decides.
 */

export const LOYALTY_API = '/api/loyalty';

export type LoyaltyTransactionKind = 'Earned' | 'Reversed' | 'Redeemed' | 'Adjusted';

export interface LoyaltyTransaction {
  loyaltyTransactionId: number;
  kind: LoyaltyTransactionKind;
  points: number;
  source: 'Invoice' | null;
  sourceId: number | null;
  documentNo: string | null;
  occurredAt: string;
  note: string | null;
}

export interface LoyaltyAccount {
  contactId: number;
  pointsBalance: number;
  transactions: LoyaltyTransaction[];
}

export interface BillLoyalty {
  restaurantBillId: number;
  points: number;
  balance: number;
  alreadyRecorded: boolean;
}

/** Whole points a bill earns at the branch's rate per ₹100, rounded down. */
export function pointsFor(billTotal: number, ratePer100: number): number {
  if (!(ratePer100 > 0) || !(billTotal > 0)) return 0;
  return Math.floor((billTotal * ratePer100) / 100);
}

/** "1 point", "12 points", "−12 points". */
export function pointsLabel(points: number): string {
  const sign = points < 0 ? '−' : '';
  const n = Math.abs(points);
  return `${sign}${n} point${n === 1 ? '' : 's'}`;
}

export function loyaltyKindLabel(kind: LoyaltyTransactionKind): string {
  switch (kind) {
    case 'Earned':
      return 'Earned';
    case 'Reversed':
      return 'Taken back (bill voided)';
    case 'Redeemed':
      return 'Redeemed';
    default:
      return 'Adjusted';
  }
}
