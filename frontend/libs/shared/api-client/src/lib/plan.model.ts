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
