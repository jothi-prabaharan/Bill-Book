import { Allocation, FeeDemand, FeeFrequency } from './fee.models';

/**
 * The fee rules the screens check before sending (S4, TK-64). The server
 * checks the same and its answer wins; these only save a round trip and show
 * the figures as they are typed.
 */

/** Whether a line of this frequency falls due in a month, for a year starting in `firstMonth`. Mirrors `FeeRules.IsDue`. */
export function isDue(frequency: FeeFrequency, firstMonth: number, month: number): boolean {
  const offset = (((month - firstMonth) % 12) + 12) % 12;
  switch (frequency) {
    case 'Monthly':
      return true;
    case 'Quarterly':
      return offset % 3 === 0;
    case 'Termly':
      return offset === 0 || offset === 4 || offset === 8;
    default:
      return offset === 0;
  }
}

/** Oldest open demands first, until the money runs out. Mirrors `FeeRules.AutoAllocate`. */
export function autoAllocate(amount: number, oldestFirst: readonly FeeDemand[]): Allocation[] {
  const result: Allocation[] = [];
  let left = round(amount);
  for (const demand of oldestFirst) {
    if (left <= 0) {
      break;
    }

    const take = round(Math.min(left, demand.openAmount));
    if (take > 0) {
      result.push({ feeDemandId: demand.feeDemandId, amount: take });
      left = round(left - take);
    }
  }

  return result;
}

/** What a receipt leaves as the guardian's advance. */
export function unallocated(amount: number, allocations: readonly Allocation[]): number {
  return round(amount - allocations.reduce((sum, a) => sum + (a.amount || 0), 0));
}

/** Why the allocations cannot stand, or null. */
export function allocationProblem(amount: number, allocations: readonly Allocation[], open: readonly FeeDemand[]): string | null {
  for (const a of allocations) {
    const demand = open.find((d) => d.feeDemandId === a.feeDemandId);
    if (!demand) {
      return 'Allocate only to open demands of this guardian.';
    }

    if (a.amount > demand.openAmount) {
      return `${demand.demandNo ?? 'A demand'} owes only ${demand.openAmount}.`;
    }
  }

  return unallocated(amount, allocations) < 0 ? 'The allocations add up to more than was received.' : null;
}

/** The period code for a date, `2026-06`. */
export function periodOf(isoDate: string): string {
  return isoDate.slice(0, 7);
}

function round(value: number): number {
  return Math.round(value * 100) / 100;
}

export const FREQUENCY_LABELS: Readonly<Record<FeeFrequency, string>> = {
  OneTime: 'One time',
  Monthly: 'Monthly',
  Quarterly: 'Quarterly',
  Termly: 'Termly',
  Annual: 'Annual',
};
