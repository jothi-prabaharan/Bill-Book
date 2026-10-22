import { describe, expect, it } from 'vitest';
import { SWITCHABLE_PLANS, canSwitchPlan, isCurrentPlan, planToOffer, switchPlanName } from './customers.page';

describe('switching a customer\'s plan (TK-211)', () => {
  it('SWITCHUI-01: offers the two paid plans and nothing else', () => {
    expect(SWITCHABLE_PLANS.map((plan) => [plan.value, plan.label])).toEqual([
      ['Pro', 'Professional'],
      ['Elite', 'Elite'],
    ]);
    expect(switchPlanName('Pro')).toBe('Professional');
    expect(switchPlanName('Elite')).toBe('Elite');
  });

  it('SWITCHUI-02: an operator moves a paying customer that has a licence, and no other', () => {
    expect(canSwitchPlan({ kind: 'Standard', planName: 'Professional' })).toBe(true);
    expect(canSwitchPlan({ kind: 'Standard', planName: 'Trial' })).toBe(true);

    // Internal and Demo customers are never billed; one with no licence has no plan to move.
    expect(canSwitchPlan({ kind: 'Internal', planName: 'Elite' })).toBe(false);
    expect(canSwitchPlan({ kind: 'Demo', planName: 'Elite' })).toBe(false);
    expect(canSwitchPlan({ kind: 'Standard', planName: null })).toBe(false);
  });

  it('SWITCHUI-03: starts on the plan the customer asked for, else the next one up', () => {
    expect(planToOffer({ planName: 'Trial', upgradeRequestedTo: 'Professional' })).toBe('Pro');
    expect(planToOffer({ planName: 'Trial', upgradeRequestedTo: 'Elite' })).toBe('Elite');
    expect(planToOffer({ planName: 'Professional', upgradeRequestedTo: 'Elite' })).toBe('Elite');

    // No request: Elite for a Professional customer, Professional for anyone else.
    expect(planToOffer({ planName: 'Professional', upgradeRequestedTo: null })).toBe('Elite');
    expect(planToOffer({ planName: 'Trial', upgradeRequestedTo: null })).toBe('Pro');
    expect(planToOffer({ planName: 'Elite', upgradeRequestedTo: null })).toBe('Pro');
  });

  it('SWITCHUI-04: choosing the plan they are on changes nothing, so it is not offered', () => {
    expect(isCurrentPlan({ planName: 'Professional' }, 'Pro')).toBe(true);
    expect(isCurrentPlan({ planName: 'Elite' }, 'Elite')).toBe(true);
    expect(isCurrentPlan({ planName: 'Professional' }, 'Elite')).toBe(false);
    expect(isCurrentPlan({ planName: 'Trial' }, 'Pro')).toBe(false);
    expect(isCurrentPlan({ planName: null }, 'Pro')).toBe(false);
  });
});
