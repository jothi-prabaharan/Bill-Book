import {
  BillingCycle,
  MonthlyUsage,
  PlanCardState,
  PlanCyclePrice,
  PlanPrices,
  PlanSummary,
  PlanTier,
  cycleLabel,
  cyclePriceOf,
  formatPlanPrice,
  perMonth,
  planCardState,
  tierFeatures,
} from '@bill-book/api-client';
import type { BbRadioOption } from '@bill-book/ui-components';

/** One paid plan as the Plan screen draws it. */
export interface PlanCard {
  tier: PlanTier;
  state: PlanCardState;
  /** Elite is drawn raised. */
  featured: boolean;
  /** What the plan allows, one line each. */
  features: string[];
  /** The price for the chosen cycle, or null when none is set for the branch's trade. */
  price: string | null;
  /** "for 3 months". */
  period: string | null;
  /** "About ₹1,350 a month". */
  monthly: string | null;
  /** The words on the card's button, when it offers one. */
  action: string | null;
}

/** One count against its limit, as the usage meters draw it. */
export interface PlanMeter {
  label: string;
  used: number;
  /** Null is no limit. */
  limit: number | null;
  note: string;
}

/** The billing cycles a trade is priced in, as radio options. */
export function cycleOptions(prices: PlanPrices | null): BbRadioOption<BillingCycle>[] {
  return (prices?.cycles ?? []).map((price) => ({
    value: price.cycle,
    label: cycleLabel(price.cycle),
    description: `Paid every ${price.months} months`,
  }));
}

/** The cycle chosen, or the first the trade has when the choice is not one of its own. */
export function chosenCycle(prices: PlanPrices | null, cycle: BillingCycle): PlanCyclePrice | null {
  const cycles = prices?.cycles ?? [];
  return cycles.find((price) => price.cycle === cycle) ?? cycles[0] ?? null;
}

/** The two paid plans, each with what it allows and what it costs for the chosen cycle. */
export function buildPlanCards(summary: PlanSummary, prices: PlanPrices, cycle: BillingCycle): PlanCard[] {
  const chosen = chosenCycle(prices, cycle);

  return prices.tiers.map((tier) => {
    const state = planCardState(summary, tier);
    const total = chosen ? cyclePriceOf(chosen, tier) : null;

    return {
      tier,
      state,
      featured: tier.licenseType === 'Elite',
      features: tierFeatures(tier),
      price: total === null ? null : formatPlanPrice(total, prices.currencyCode),
      period: chosen ? `for ${chosen.months} months` : null,
      monthly:
        chosen && total !== null
          ? `About ${formatPlanPrice(perMonth(total, chosen.months), prices.currencyCode)} a month`
          : null,
      action: state === 'upgrade' ? `Request upgrade to ${tier.plan}` : null,
    };
  });
}

/** The plan a card's button asks for. */
export function requestedPlan(card: PlanCard): 'Pro' | 'Elite' {
  return card.tier.licenseType === 'Elite' ? 'Elite' : 'Pro';
}

/**
 * What goes under the Users meter: the server's own sentence once the branch is
 * full, otherwise the part nobody expects, that people who were deactivated
 * still hold their place.
 */
export function userMeterNote(summary: PlanSummary): string {
  if (summary.userLimitMessage) {
    return summary.userLimitMessage;
  }

  return summary.maxUsers === null ? '' : 'Deactivated users keep their place.';
}

/**
 * The meters of the plan the branch is on. The two document counts belong to
 * Sales and Purchase, and a branch that cannot be read from either simply has
 * no meter for it.
 */
export function planMeters(
  summary: PlanSummary,
  sales: MonthlyUsage | null,
  purchase: MonthlyUsage | null,
): PlanMeter[] {
  const meters: PlanMeter[] = [
    { label: 'Users', used: summary.usersUsed, limit: summary.maxUsers, note: userMeterNote(summary) },
  ];

  if (sales) {
    meters.push({ label: 'Sales this month', used: sales.used, limit: sales.limit, note: '' });
  }

  if (purchase) {
    meters.push({ label: 'Purchases this month', used: purchase.used, limit: purchase.limit, note: '' });
  }

  return meters;
}
