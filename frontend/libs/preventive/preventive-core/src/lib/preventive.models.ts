/** The preventive service's shapes (S7, TK-67), as the API sends them: enums by name. */

export type Frequency = 'Daily' | 'Weekly' | 'Monthly' | 'Quarterly' | 'HalfYearly' | 'Yearly';
export type OccurrenceStatus = 'Scheduled' | 'Raised' | 'Done' | 'Skipped';

export interface PreventivePlan {
  preventivePlanId: number;
  name: string;
  facilityAssetId: number | null;
  spaceId: number | null;
  frequency: Frequency;
  interval: number;
  startDate: string;
  endDate: string | null;
  nextDueDate: string;
  leadDays: number;
  defaultAssigneeEmployeeId: number | null;
  isActive: boolean;
}

export type SavePlan = Omit<PreventivePlan, 'preventivePlanId' | 'nextDueDate'>;

export interface Occurrence {
  preventiveOccurrenceId: number;
  preventivePlanId: number;
  planName: string;
  dueDate: string;
  workOrderId: number | null;
  workOrderNo: string | null;
  occurrenceStatus: OccurrenceStatus;
}

export interface GenerationResult {
  generated: number;
  raised: number;
  failed: number;
}

export const FREQUENCIES: readonly { value: Frequency; label: string; unit: string }[] = [
  { value: 'Daily', label: 'Daily', unit: 'day' },
  { value: 'Weekly', label: 'Weekly', unit: 'week' },
  { value: 'Monthly', label: 'Monthly', unit: 'month' },
  { value: 'Quarterly', label: 'Quarterly', unit: 'quarter' },
  { value: 'HalfYearly', label: 'Half-yearly', unit: 'half-year' },
  { value: 'Yearly', label: 'Yearly', unit: 'year' },
];

export const OCCURRENCE_STATUSES: readonly { value: OccurrenceStatus; label: string }[] = [
  { value: 'Scheduled', label: 'Scheduled' },
  { value: 'Raised', label: 'Work order raised' },
  { value: 'Done', label: 'Done' },
  { value: 'Skipped', label: 'Skipped' },
];

/** How a plan's recurrence reads: "Every month", "Every 2 weeks". */
export function everyLabel(frequency: Frequency, interval: number): string {
  const unit = FREQUENCIES.find((f) => f.value === frequency)?.unit ?? frequency.toLowerCase();
  return interval <= 1 ? `Every ${unit}` : `Every ${interval} ${unit}s`;
}
