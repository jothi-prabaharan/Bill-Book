/**
 * Jewellery savings schemes (TK-141): the shapes Accounting's
 * `api/savings-schemes` returns, and the pure helpers the page uses.
 */

export type BonusType = 'None' | 'FixedAmount' | 'Instalments' | 'Percent';

export type EnrolmentStatus = 'Active' | 'Matured' | 'Redeemed' | 'Lapsed' | 'Refunded';

export interface SavingsScheme {
  savingsSchemeId: number;
  code: string;
  name: string;
  instalmentAmount: number;
  months: number;
  bonusType: BonusType;
  bonusValue: number;
  bonusAmount: number;
  graceDays: number;
  isActive: boolean;
  activeEnrolments: number;
}

export interface SchemeDraft {
  code: string;
  name: string;
  instalmentAmount: number | null;
  months: number | null;
  bonusType: BonusType;
  bonusValue: number | null;
  graceDays: number | null;
  isActive: boolean;
}

export interface SchemeInstalment {
  schemeInstalmentId: number;
  instalmentNumber: number;
  dueDate: string;
  amount: number;
  receiveMoneyId: number | null;
  paidOn: string | null;
}

export interface SchemeEnrolment {
  schemeEnrolmentId: number;
  enrolmentNo: string | null;
  savingsSchemeId: number;
  schemeName: string;
  contactId: number;
  startDate: string;
  maturityDate: string;
  instalmentAmount: number;
  months: number;
  instalmentsPaid: number;
  paidPrincipal: number;
  bonusAmount: number;
  nextDueDate: string | null;
  status: EnrolmentStatus;
  principalAvailable?: number;
  principalRedeemed?: number;
  bonusRedeemed?: number;
  redeemedInvoiceId?: number | null;
  instalments?: SchemeInstalment[];
}

export interface BankAccountOption {
  bankAccountId: number;
  accountName: string;
  isDefault: boolean;
}

export interface InvoiceOption {
  invoiceId: number;
  documentNo: string | null;
  documentDate: string;
  outstanding: number;
}

export const BONUS_OPTIONS: readonly { value: BonusType; label: string }[] = [
  { value: 'Instalments', label: 'Free instalments' },
  { value: 'FixedAmount', label: 'Fixed amount' },
  { value: 'Percent', label: 'Percentage of what is paid' },
  { value: 'None', label: 'No bonus' },
];

export const STATUS_OPTIONS: readonly { value: EnrolmentStatus | ''; label: string }[] = [
  { value: '', label: 'Every status' },
  { value: 'Active', label: 'Paying' },
  { value: 'Matured', label: 'Matured' },
  { value: 'Lapsed', label: 'Lapsed' },
  { value: 'Redeemed', label: 'Redeemed' },
  { value: 'Refunded', label: 'Refunded' },
];

export function emptyScheme(): SchemeDraft {
  return { code: '', name: '', instalmentAmount: null, months: 11, bonusType: 'Instalments', bonusValue: 1, graceDays: 30, isActive: true };
}

export function schemeDraftOf(s: SavingsScheme): SchemeDraft {
  return {
    code: s.code,
    name: s.name,
    instalmentAmount: s.instalmentAmount,
    months: s.months,
    bonusType: s.bonusType,
    bonusValue: s.bonusValue,
    graceDays: s.graceDays,
    isActive: s.isActive,
  };
}

/**
 * The bonus a draft would pay, by the same rule as the server's
 * `SavingsSchemeRulesRepository.Bonus`, so the form shows it before saving.
 */
export function bonusOf(type: BonusType, value: number | null, instalment: number | null, months: number | null): number {
  const v = value ?? 0;
  const i = instalment ?? 0;
  const m = months ?? 0;
  const raw = type === 'FixedAmount' ? v : type === 'Instalments' ? v * i : type === 'Percent' ? (i * m * v) / 100 : 0;
  return Math.round(raw * 100) / 100;
}

export function schemeProblem(d: SchemeDraft): string | null {
  if (!d.code.trim()) return 'Give the scheme a code.';
  if (!d.name.trim()) return 'Name the scheme.';
  if (!d.instalmentAmount || d.instalmentAmount < 1) return 'Enter the monthly instalment.';
  if (!d.months || d.months < 1 || d.months > 60) return 'A scheme runs 1 to 60 months.';
  if (d.bonusType === 'Percent' && (d.bonusValue ?? 0) > 100) return 'A percentage bonus cannot exceed 100.';
  if ((d.bonusValue ?? 0) < 0) return 'The bonus cannot be negative.';
  return null;
}

export function schemeRequest(d: SchemeDraft): Record<string, unknown> {
  return {
    code: d.code.trim().toUpperCase(),
    name: d.name.trim(),
    instalmentAmount: d.instalmentAmount,
    months: d.months,
    bonusType: d.bonusType,
    bonusValue: d.bonusType === 'None' ? 0 : (d.bonusValue ?? 0),
    graceDays: d.graceDays ?? 30,
    isActive: d.isActive,
  };
}

/** What the customer would get on an invoice now: bonus only once matured. */
export function redeemableNow(e: SchemeEnrolment): number {
  const paid = e.principalAvailable ?? e.paidPrincipal;
  return e.status === 'Matured' ? paid + e.bonusAmount : paid;
}

export function statusLabel(status: EnrolmentStatus): string {
  return status === 'Active' ? 'Paying' : status;
}

export function statusTag(status: EnrolmentStatus): string {
  switch (status) {
    case 'Active':
      return 'tag tag-neutral';
    case 'Matured':
      return 'tag tag-accent';
    case 'Lapsed':
      return 'tag tag-warning';
    default:
      return 'tag tag-outline';
  }
}

/** Which actions an enrolment allows. */
export function actionsFor(e: SchemeEnrolment): { pay: boolean; redeem: boolean; closeEarly: boolean; refund: boolean } {
  const open = e.status === 'Active' || e.status === 'Matured' || e.status === 'Lapsed';
  return {
    pay: e.status === 'Active',
    redeem: e.status === 'Matured' || e.status === 'Lapsed',
    closeEarly: e.status === 'Active' && e.instalmentsPaid > 0,
    refund: open && (e.principalAvailable ?? e.paidPrincipal) > 0,
  };
}
