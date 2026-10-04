/**
 * Service plans (TK-140): the shapes Customer's `api/service-plans` returns,
 * and the pure helpers the page and the dashboard card use.
 */

export type ServicePlanInterval = 'Daily' | 'Weekly' | 'Monthly' | 'Yearly';

export type ServicePlanStatus = 'Active' | 'Paused' | 'Ended';

export interface ServicePlan {
  servicePlanId: number;
  name: string;
  contactId: number;
  itemId: number | null;
  assetId: number | null;
  interval: ServicePlanInterval;
  every: number;
  startDate: string;
  endDate: string | null;
  leadDays: number;
  nextDueDate: string;
  lastServicedOn: string | null;
  lastTicketId: number | null;
  lastTicketStatus: string | null;
  notes: string | null;
  status: ServicePlanStatus;
  daysToDue: number;
}

export interface ServicePlanDraft {
  name: string;
  contactId: number | null;
  interval: ServicePlanInterval;
  every: number;
  startDate: string | null;
  endDate: string | null;
  leadDays: number;
  nextDueDate: string | null;
  notes: string;
}

export const SERVICE_PLANS_API = '/api/service-plans';

export const INTERVAL_OPTIONS: readonly { value: ServicePlanInterval; label: string }[] = [
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Yearly', label: 'Yearly' },
  { value: 'Weekly', label: 'Weekly' },
  { value: 'Daily', label: 'Daily' },
];

const UNIT: Record<ServicePlanInterval, [string, string]> = {
  Daily: ['day', 'days'],
  Weekly: ['week', 'weeks'],
  Monthly: ['month', 'months'],
  Yearly: ['year', 'years'],
};

export function cadence(interval: ServicePlanInterval, every: number): string {
  const [one, many] = UNIT[interval] ?? ['period', 'periods'];
  return every <= 1 ? `Every ${one}` : `Every ${every} ${many}`;
}

/** "Due in 3 days", "Due today", "4 days late". */
export function dueLabel(daysToDue: number): string {
  if (daysToDue === 0) return 'Due today';
  if (daysToDue === 1) return 'Due tomorrow';
  if (daysToDue > 1) return `Due in ${daysToDue} days`;
  return daysToDue === -1 ? '1 day late' : `${-daysToDue} days late`;
}

export function dueTone(daysToDue: number): string {
  return daysToDue < 0 ? 'tag tag-warning' : daysToDue <= 7 ? 'tag tag-accent' : 'tag tag-neutral';
}

export function emptyPlan(today: string): ServicePlanDraft {
  return { name: '', contactId: null, interval: 'Monthly', every: 6, startDate: today, endDate: null, leadDays: 7, nextDueDate: null, notes: '' };
}

export function planDraftOf(p: ServicePlan): ServicePlanDraft {
  return {
    name: p.name,
    contactId: p.contactId,
    interval: p.interval,
    every: p.every,
    startDate: p.startDate,
    endDate: p.endDate,
    leadDays: p.leadDays,
    nextDueDate: p.nextDueDate,
    notes: p.notes ?? '',
  };
}

export function planProblem(d: ServicePlanDraft): string | null {
  if (!d.name.trim()) return 'Name the plan.';
  if (!d.contactId) return 'Choose the customer.';
  if (!d.startDate) return 'Choose the start date.';
  if (!Number.isInteger(d.every) || d.every < 1 || d.every > 24) return 'Repeat every 1 to 24 periods.';
  if (d.endDate && d.endDate < d.startDate) return 'The end date cannot be before the start date.';
  if (d.nextDueDate && d.nextDueDate < d.startDate) return 'The next service cannot be before the plan starts.';
  if (d.leadDays < 0 || d.leadDays > 90) return 'Remind 0 to 90 days ahead.';
  return null;
}

export function planRequest(d: ServicePlanDraft): Record<string, unknown> {
  return {
    name: d.name.trim(),
    contactId: d.contactId,
    interval: d.interval,
    every: d.every,
    startDate: d.startDate,
    endDate: d.endDate || null,
    leadDays: d.leadDays,
    nextDueDate: d.nextDueDate || null,
    notes: d.notes.trim() || null,
  };
}
