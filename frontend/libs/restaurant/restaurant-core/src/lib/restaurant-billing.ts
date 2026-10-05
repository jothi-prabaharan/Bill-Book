/**
 * Billing restaurant orders (TK-148): the shapes Sales's `api/restaurant/bills`
 * takes and returns, and the pure checks the billing page makes before it
 * asks. `tenderProblem` mirrors `PosSaleRepository.CheckTenders`: card and UPI
 * may pay at most the bill, everything together at least the bill, and only
 * cash gives change.
 */

export const RESTAURANT_BILLS_API = '/api/restaurant/bills';

export type TenderMode = 'Cash' | 'Card' | 'Upi';

export const TENDER_MODES: readonly { value: TenderMode; label: string }[] = [
  { value: 'Cash', label: 'Cash' },
  { value: 'Card', label: 'Card' },
  { value: 'Upi', label: 'UPI' },
];

export interface TenderAccount {
  bankAccountId: number;
  accountName: string;
  maskedNumber: string;
  isCash: boolean;
}

export interface TenderDraft {
  mode: TenderMode;
  amount: number | null;
  bankAccountId: number | null;
  reference: string;
}

export interface BillPreview {
  subTotal: number;
  serviceChargePercent: number;
  serviceChargeAmount: number;
  taxAmount: number;
  roundOff: number;
  total: number;
  tipAmount: number;
  amountToPay: number;
  lineIds: number[];
  problem: string | null;
}

export interface RestaurantBill {
  restaurantBillId: number;
  invoiceId: number;
  invoiceNo: string;
  total: number;
  serviceChargeAmount: number;
  tipAmount: number;
  change: number;
  billedAt: string;
  closedOrderIds: number[];
  /** Points the bill earned its customer (TK-166); 0 for none. */
  loyaltyPoints: number;
  /** The customer's balance after it, when it was recorded. */
  loyaltyBalance: number | null;
  /** Set when points were due but could not be recorded yet. */
  loyaltyNote: string | null;
}

export interface BillDraft {
  orderIds: number[];
  lineIds: number[];
  contactId: number | null;
  applyServiceCharge: boolean;
  tipAmount: number | null;
  tipBankAccountId: number | null;
  tenders: TenderDraft[];
}

/** A tender's default account: the cash account for cash, else the first bank account. */
export function defaultAccount(mode: TenderMode, accounts: TenderAccount[]): number | null {
  const match = accounts.find((a) => (mode === 'Cash' ? a.isCash : !a.isCash)) ?? accounts[0];
  return match?.bankAccountId ?? null;
}

export function tendered(tenders: TenderDraft[]): number {
  return round(tenders.reduce((sum, t) => sum + (t.amount ?? 0), 0));
}

/** The change due from cash, or null while the tenders do not cover the bill. */
export function changeDue(total: number, tenders: TenderDraft[]): number | null {
  const paid = tendered(tenders);
  return paid < total ? null : round(paid - total);
}

/** Why the tenders cannot pay the bill, worded as the server words it, or null. Empty tenders bill on account. */
export function tenderProblem(total: number, tenders: TenderDraft[]): string | null {
  if (!tenders.length) return null;
  if (tenders.some((t) => !t.amount || t.amount <= 0)) return 'Every tender must be more than zero.';
  if (tenders.some((t) => !t.bankAccountId)) return 'Choose the account each payment went into.';
  const nonCash = round(tenders.filter((t) => t.mode !== 'Cash').reduce((sum, t) => sum + (t.amount ?? 0), 0));
  if (nonCash > total) return `Card and UPI can pay at most the total of ${total.toFixed(2)}. Only cash gives change.`;
  const paid = tendered(tenders);
  if (paid < total) return `The payments cover ${paid.toFixed(2)} of ${total.toFixed(2)}. ${round(total - paid).toFixed(2)} is still to pay.`;
  return null;
}

/** Why the tip cannot be taken, or null. */
export function tipProblem(tipsEnabled: boolean, tip: number | null, bankAccountId: number | null): string | null {
  if (!tip) return null;
  if (tip < 0) return 'A tip cannot be negative.';
  if (!tipsEnabled) return 'Tips are switched off for this branch.';
  return bankAccountId ? null : 'Choose where the tip was paid in.';
}

/** The body for preview and billing. An empty line list bills every dish not yet billed. */
export function billRequest(draft: BillDraft, allLineIds: number[]) {
  const splitting = draft.lineIds.length > 0 && draft.lineIds.length < allLineIds.length;
  return {
    orderIds: draft.orderIds,
    lineIds: splitting ? draft.lineIds : [],
    contactId: draft.contactId,
    applyServiceCharge: draft.applyServiceCharge,
    tipAmount: draft.tipAmount ?? 0,
    tipBankAccountId: draft.tipAmount ? draft.tipBankAccountId : null,
    tenders: draft.tenders.map((t) => ({ mode: t.mode, amount: t.amount ?? 0, bankAccountId: t.bankAccountId, reference: t.reference.trim() || null })),
  };
}

function round(value: number): number {
  return Math.round(value * 100) / 100;
}
