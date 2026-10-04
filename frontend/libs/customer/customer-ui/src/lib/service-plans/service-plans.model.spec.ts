import { describe, expect, it } from 'vitest';
import { cadence, dueLabel, emptyPlan, planProblem, planRequest } from './service-plans.model';

describe('service plans (TK-140)', () => {
  it('says how often and how soon in words', () => {
    expect(cadence('Monthly', 6)).toBe('Every 6 months');
    expect(cadence('Yearly', 1)).toBe('Every year');
    expect(dueLabel(0)).toBe('Due today');
    expect(dueLabel(5)).toBe('Due in 5 days');
    expect(dueLabel(-3)).toBe('3 days late');
  });

  it('refuses a plan with no customer or a next service before it starts', () => {
    const d = { ...emptyPlan('2026-10-01'), name: 'AC service' };
    expect(planProblem(d)).toBe('Choose the customer.');

    d.contactId = 7;
    d.nextDueDate = '2026-09-01';
    expect(planProblem(d)).toBe('The next service cannot be before the plan starts.');

    d.nextDueDate = null;
    expect(planProblem(d)).toBeNull();
    expect(planRequest(d)).toMatchObject({ name: 'AC service', contactId: 7, interval: 'Monthly', every: 6, nextDueDate: null, notes: null });
  });
});
