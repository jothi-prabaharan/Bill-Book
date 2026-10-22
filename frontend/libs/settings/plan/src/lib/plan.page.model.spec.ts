import { MonthlyUsage, PlanPrices, PlanSummary, PlanTier } from '@bill-book/api-client';
import { describe, expect, it } from 'vitest';
import { buildPlanCards, chosenCycle, cycleOptions, planMeters, requestedPlan, userMeterNote } from './plan.page.model';

const professionalTier: PlanTier = {
  plan: 'Professional',
  licenseType: 'Pro',
  maxUsers: 5,
  monthlySalesTransactionLimit: 1000,
  monthlyPurchaseTransactionLimit: 1000,
};

const eliteTier: PlanTier = {
  plan: 'Elite',
  licenseType: 'Elite',
  maxUsers: null,
  monthlySalesTransactionLimit: null,
  monthlyPurchaseTransactionLimit: null,
};

const prices: PlanPrices = {
  businessType: 'Retail',
  currencyCode: 'INR',
  tiers: [professionalTier, eliteTier],
  cycles: [
    { cycle: 'Quarterly', months: 3, professional: 4050, elite: 12150 },
    { cycle: 'BiAnnual', months: 6, professional: 7650, elite: 22950 },
  ],
};

const professional: PlanSummary = {
  plan: 'Professional',
  licenseType: 'Pro',
  maxUsers: 5,
  usersUsed: 4,
  monthlySalesTransactionLimit: 1000,
  monthlyPurchaseTransactionLimit: 1000,
  userLimitMessage: null,
  canUpgrade: true,
  upgradeRequested: false,
};

const elite: PlanSummary = {
  ...professional,
  plan: 'Elite',
  licenseType: 'Elite',
  maxUsers: null,
  monthlySalesTransactionLimit: null,
  monthlyPurchaseTransactionLimit: null,
  canUpgrade: false,
};

const salesUsage = (used: number, limit: number | null = 1000): MonthlyUsage => ({
  kind: 'SalesTransactions',
  plan: 'Professional',
  periodStart: '2026-10-01',
  used,
  limit,
  remaining: limit === null ? null : Math.max(0, limit - used),
});

describe('The Plan screen model', () => {
  it('PLANPG-01: draws both plans with the chosen cycle\'s price, a month of it, and Elite raised', () => {
    const [pro, top] = buildPlanCards(professional, prices, 'Quarterly');

    expect(pro).toMatchObject({
      state: 'current',
      featured: false,
      price: '₹4,050',
      period: 'for 3 months',
      monthly: 'About ₹1,350 a month',
      action: null,
    });
    expect(pro.features).toHaveLength(3);

    expect(top).toMatchObject({
      state: 'upgrade',
      featured: true,
      price: '₹12,150',
      period: 'for 3 months',
      monthly: 'About ₹4,050 a month',
      action: 'Request upgrade to Elite',
    });
  });

  it('PLANPG-02: the other cycle changes the price and the months, not the plan', () => {
    const [pro, top] = buildPlanCards(professional, prices, 'BiAnnual');

    expect(pro.price).toBe('₹7,650');
    expect(pro.monthly).toBe('About ₹1,275 a month');
    expect(top.price).toBe('₹22,950');
    expect(top.period).toBe('for 6 months');
  });

  it('PLANPG-03: a trial may ask for either plan, and Elite may ask for nothing', () => {
    const trial: PlanSummary = { ...professional, plan: 'Trial', licenseType: 'Trial', maxUsers: 3 };

    expect(buildPlanCards(trial, prices, 'Quarterly').map((card) => card.action)).toEqual([
      'Request upgrade to Professional',
      'Request upgrade to Elite',
    ]);
    expect(buildPlanCards(elite, prices, 'Quarterly').map((card) => [card.state, card.action])).toEqual([
      ['none', null],
      ['current', null],
    ]);
  });

  it('PLANPG-04: a trade with no price still lists what each plan allows, and says the price is on request', () => {
    const unpriced: PlanPrices = { ...prices, cycles: [] };
    const [pro, top] = buildPlanCards(professional, unpriced, 'Quarterly');

    expect(pro.price).toBeNull();
    expect(pro.period).toBeNull();
    expect(pro.monthly).toBeNull();
    expect(top.action).toBe('Request upgrade to Elite');
    expect(top.features).toContain('Unlimited Sales documents');
  });

  it('PLANPG-05: offers the cycles a trade is priced in, and falls back to the first it has', () => {
    expect(cycleOptions(prices).map((option) => [option.value, option.label, option.description])).toEqual([
      ['Quarterly', 'Quarterly', 'Paid every 3 months'],
      ['BiAnnual', 'Bi-annual', 'Paid every 6 months'],
    ]);
    expect(cycleOptions(null)).toEqual([]);

    const quarterlyOnly: PlanPrices = { ...prices, cycles: [prices.cycles[0]] };
    expect(chosenCycle(quarterlyOnly, 'BiAnnual')?.cycle).toBe('Quarterly');
    expect(chosenCycle({ ...prices, cycles: [] }, 'Quarterly')).toBeNull();
    expect(chosenCycle(null, 'Quarterly')).toBeNull();
  });

  it('PLANPG-06: a card asks for its own plan', () => {
    const [pro, top] = buildPlanCards({ ...professional, licenseType: 'Trial' }, prices, 'Quarterly');

    expect(requestedPlan(pro)).toBe('Pro');
    expect(requestedPlan(top)).toBe('Elite');
  });

  it('PLANPG-07: meters the users and each document count the branch can be read for', () => {
    expect(planMeters(professional, salesUsage(412), { ...salesUsage(40), kind: 'PurchaseTransactions' })).toEqual([
      { label: 'Users', used: 4, limit: 5, note: 'Deactivated users keep their place.' },
      { label: 'Sales this month', used: 412, limit: 1000, note: '' },
      { label: 'Purchases this month', used: 40, limit: 1000, note: '' },
    ]);

    // A branch that cannot give a count goes without that meter.
    expect(planMeters(professional, null, null).map((meter) => meter.label)).toEqual(['Users']);

    // No limit: the figures are shown without a bar to fill.
    expect(planMeters(elite, salesUsage(12, null), null)[1]).toEqual({
      label: 'Sales this month',
      used: 12,
      limit: null,
      note: '',
    });
  });

  it('PLANPG-08: a full branch shows the server\'s sentence under the users meter', () => {
    const message =
      'Professional plan user limit reached (maximum 5 users including inactive accounts). Upgrade to the Elite Plan to add more users.';

    expect(userMeterNote({ ...professional, usersUsed: 5, userLimitMessage: message })).toBe(message);
    expect(userMeterNote(professional)).toBe('Deactivated users keep their place.');
    expect(userMeterNote(elite)).toBe('');
  });
});
