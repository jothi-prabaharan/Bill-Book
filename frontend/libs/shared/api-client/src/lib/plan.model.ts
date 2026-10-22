import { HttpErrorResponse } from '@angular/common/http';

/** The limit of a plan that can stop a request (TK-209, TK-210). */
export type PlanLimitKind = 'Users' | 'SalesTransactions' | 'PurchaseTransactions';

/** The type stored on a licence. Professional is `Pro`; `Standard` is what Pro was called. */
export type LicenseType = 'Trial' | 'Standard' | 'Pro' | 'Elite';

/**
 * What the server answers, with a 409, when a rule of the customer's plan stops
 * a request. The words are the server's and are shown as they are; the rest is
 * what a screen needs to open its upgrade prompt without reading them.
 */
export interface PlanLimit {
  message: string;
  code: 'PlanLimitReached';
  kind: PlanLimitKind;
  /** Trial, Professional or Elite. */
  plan: string;
  limit: number;
  used: number;
}

/** The plan, what it allows the signed-in branch, and how many user places are taken. */
export interface PlanSummary {
  plan: string;
  licenseType: LicenseType;
  /** Null is no limit. */
  maxUsers: number | null;
  /** Everyone ever given access to the branch, deactivated people included. */
  usersUsed: number;
  monthlySalesTransactionLimit: number | null;
  monthlyPurchaseTransactionLimit: number | null;
  /** The server's sentence for a refused invitation, or null while a place is left. */
  userLimitMessage: string | null;
  canUpgrade: boolean;
  upgradeRequested: boolean;
}

export interface UpgradeRequestInput {
  /** Defaults to Elite on the server. */
  toPlan?: 'Pro' | 'Elite';
  /** The limit that sent the person here; leave out when they asked from the Plan screen. */
  reason?: PlanLimitKind;
  note?: string;
}

/**
 * The plan limit a refused request carries, or null when it was refused for any
 * other reason. A 409 alone is not enough: a document that conflicts with
 * another is also a 409, and must not open an upgrade prompt.
 */
export function planLimitOf(error: unknown): PlanLimit | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 409) {
    return null;
  }

  const body = error.error as Partial<PlanLimit> | null;
  return body?.code === 'PlanLimitReached' ? (body as PlanLimit) : null;
}

/** Whether every user place the plan allows is taken. A plan with no limit never is. */
export function seatsFull(summary: PlanSummary): boolean {
  return summary.maxUsers !== null && summary.usersUsed >= summary.maxUsers;
}

/**
 * The limit a branch with no place left is at, in the shape a refused invitation
 * would have carried it, so the upgrade prompt opened from a button says what
 * the one opened from a refusal does. Null while a place is left.
 */
export function userLimitOf(summary: PlanSummary): PlanLimit | null {
  if (summary.maxUsers === null || summary.userLimitMessage === null) {
    return null;
  }

  return {
    message: summary.userLimitMessage,
    code: 'PlanLimitReached',
    kind: 'Users',
    plan: summary.plan,
    limit: summary.maxUsers,
    used: summary.usersUsed,
  };
}

/** What the Users screen says about the places taken, or null when there is no limit to say it about. */
export interface SeatBanner {
  tone: 'info' | 'warning';
  text: string;
}

/**
 * "5 of 5 User Slots Used (Includes Deactivated Users)". It is a warning once
 * the last place is taken, and says "including deactivated" always, because
 * that is the part nobody expects.
 */
export function seatBanner(summary: PlanSummary): SeatBanner | null {
  if (summary.maxUsers === null) {
    return null;
  }

  return {
    tone: seatsFull(summary) ? 'warning' : 'info',
    text: `${summary.usersUsed} of ${summary.maxUsers} User Slots Used (Includes Deactivated Users)`,
  };
}

/** One row of the plan comparison: what the signed-in plan allows against what Elite does. */
export interface PlanComparisonRow {
  label: string;
  current: string;
  elite: string;
}

const COUNT = new Intl.NumberFormat('en-IN');

function figure(limit: number | null): string {
  return limit === null ? 'Unlimited' : COUNT.format(limit);
}

/** The three limits side by side, for the upgrade prompt. */
export function comparePlans(summary: PlanSummary): PlanComparisonRow[] {
  return [
    { label: 'Users in a branch', current: figure(summary.maxUsers), elite: figure(null) },
    { label: 'Sales documents a month', current: figure(summary.monthlySalesTransactionLimit), elite: figure(null) },
    { label: 'Purchase documents a month', current: figure(summary.monthlyPurchaseTransactionLimit), elite: figure(null) },
  ];
}

/** The two counted sides of a plan's monthly limit: documents are counted for Sales and for Purchase, apart (D-69). */
export type TransactionKind = 'SalesTransactions' | 'PurchaseTransactions';

/** What a branch has created of one kind of document this month, against what its plan allows (TK-210). */
export interface MonthlyUsage {
  kind: TransactionKind;
  /** Trial, Professional or Elite; null when the plan could not be read. */
  plan: string | null;
  /** The first day of the month counted, in India Standard Time. */
  periodStart: string;
  used: number;
  /** Null is no limit: Elite, and a branch whose plan could not be read. */
  limit: number | null;
  remaining: number | null;
}

/** The document codes that count, and which side. A delivery challan (`DLC`) is not one of them. */
const COUNTED_CODES: Readonly<Record<string, TransactionKind>> = {
  QTE: 'SalesTransactions',
  SOR: 'SalesTransactions',
  INV: 'SalesTransactions',
  CRN: 'SalesTransactions',
  POR: 'PurchaseTransactions',
  GRN: 'PurchaseTransactions',
  BIL: 'PurchaseTransactions',
  DBN: 'PurchaseTransactions',
};

/** Which side a register's documents are counted on, or null for one that is not counted at all. */
export function quotaKindOf(code: string): TransactionKind | null {
  return COUNTED_CODES[code.trim().toUpperCase()] ?? null;
}

/** The badge a register wears while its branch has a limit: places left, and the tone that says how few. */
export interface QuotaBadge {
  tone: 'info' | 'warning' | 'danger';
  text: string;
  /** The full sentence, for the tooltip and a screen reader. */
  title: string;
}

/**
 * "Sales: 88 of 1,000 left". A warning when a fifth or less is left, and a
 * danger once none is. Null when the branch has no limit, which is nothing to
 * count down.
 */
export function quotaBadge(usage: MonthlyUsage): QuotaBadge | null {
  // Not a number is no limit, whether the server said null or said nothing.
  if (typeof usage.limit !== 'number' || typeof usage.remaining !== 'number') {
    return null;
  }

  const side = usage.kind === 'PurchaseTransactions' ? 'Purchase' : 'Sales';
  const limit = COUNT.format(usage.limit);
  const plan = usage.plan ? `${usage.plan} plan: ` : '';
  const title = `${plan}${COUNT.format(usage.used)} of ${limit} ${side} transactions used this month.`;

  if (usage.remaining === 0) {
    return { tone: 'danger', text: `${side}: limit reached`, title };
  }

  return {
    tone: usage.remaining * 5 <= usage.limit ? 'warning' : 'info',
    text: `${side}: ${COUNT.format(usage.remaining)} of ${limit} left`,
    title,
  };
}

// ---- The Plan screen (TK-211) --------------------------------------------------------------------

/** How often a paid plan is paid for. */
export type BillingCycle = 'Quarterly' | 'BiAnnual';

/** What one paid plan allows, as the server gives a licence it. Null is no limit. */
export interface PlanTier {
  /** Professional or Elite. */
  plan: string;
  licenseType: LicenseType;
  maxUsers: number | null;
  monthlySalesTransactionLimit: number | null;
  monthlyPurchaseTransactionLimit: number | null;
}

/** What each paid plan costs for the whole of one billing cycle. */
export interface PlanCyclePrice {
  cycle: BillingCycle;
  months: number;
  professional: number;
  elite: number;
}

/**
 * What the Plan screen shows beside the plan: what each paid plan allows, and
 * what each costs for the branch's trade. No cycles means no price is set for
 * the trade, and the price is agreed with the platform's team.
 */
export interface PlanPrices {
  businessType: string | null;
  currencyCode: string;
  tiers: PlanTier[];
  cycles: PlanCyclePrice[];
}

/** Where a plan stands: a trial, then Professional (and the older Standard), then Elite. */
export function planRank(type: LicenseType): number {
  switch (type) {
    case 'Trial':
      return 0;
    case 'Standard':
    case 'Pro':
      return 1;
    default:
      return 2;
  }
}

/** What a plan's card offers: it is yours, it is one you can ask for, or it is neither. */
export type PlanCardState = 'current' | 'upgrade' | 'none';

/** The plans below yours, and every plan when nothing can be upgraded, offer nothing. */
export function planCardState(summary: PlanSummary, tier: PlanTier): PlanCardState {
  const mine = planRank(summary.licenseType);
  const theirs = planRank(tier.licenseType);

  if (mine === theirs) {
    return 'current';
  }

  return theirs > mine && summary.canUpgrade ? 'upgrade' : 'none';
}

/** The name a cycle is shown under. */
export function cycleLabel(cycle: BillingCycle): string {
  return cycle === 'BiAnnual' ? 'Bi-annual' : 'Quarterly';
}

/** A platform price, whole currency units, in the currency it was set in and not the branch's. */
export function formatPlanPrice(amount: number, currencyCode: string): string {
  try {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: currencyCode,
      maximumFractionDigits: 0,
    }).format(amount);
  } catch {
    return `${currencyCode} ${COUNT.format(Math.round(amount))}`;
  }
}

/** What a cycle's price comes to in one month. */
export function perMonth(total: number, months: number): number {
  return months > 0 ? total / months : total;
}

/** A plan's price for a cycle. */
export function cyclePriceOf(price: PlanCyclePrice, tier: PlanTier): number {
  return tier.licenseType === 'Elite' ? price.elite : price.professional;
}

/** What a plan allows, one line each, in the words the Plan screen lists. */
export function tierFeatures(tier: PlanTier): string[] {
  return [
    tier.maxUsers === null
      ? 'Unlimited users in every branch'
      : `Up to ${COUNT.format(tier.maxUsers)} users in each branch, deactivated ones included`,
    tier.monthlySalesTransactionLimit === null
      ? 'Unlimited Sales documents'
      : `${COUNT.format(tier.monthlySalesTransactionLimit)} Sales documents a month`,
    tier.monthlyPurchaseTransactionLimit === null
      ? 'Unlimited Purchase documents'
      : `${COUNT.format(tier.monthlyPurchaseTransactionLimit)} Purchase documents a month`,
  ];
}
