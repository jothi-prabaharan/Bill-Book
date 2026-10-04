/**
 * Recurring invoices and bills (TK-139): the shapes the two services return,
 * and the pure helpers the page uses. One page serves both, configured by a
 * {@link RecurringKind}, because the two differ only in names and one field.
 */

export type RecurringKind = 'invoice' | 'bill';

export type RecurrenceFrequency = 'Weekly' | 'Monthly' | 'Quarterly' | 'HalfYearly' | 'Yearly';

export interface RecurringRun {
  occurrenceDate: string;
  invoiceId?: number;
  billId?: number;
  documentNo: string | null;
  posted: boolean;
  postError: string | null;
}

export interface RecurringSchedule {
  /** Either id, named for its document. */
  recurringInvoiceScheduleId?: number;
  recurringBillScheduleId?: number;
  name: string;
  templateInvoiceId?: number;
  templateBillId?: number;
  templateDocumentNo: string | null;
  contactId: number;
  templateTotal: number;
  frequency: RecurrenceFrequency;
  every: number;
  startDate: string;
  endDate: string | null;
  maxOccurrences: number | null;
  nextRunDate: string | null;
  occurrencesCreated: number;
  status: 'Active' | 'Paused' | 'Ended';
  autoPost: boolean;
  vendorBillNoPrefix?: string;
  placeOfSupplyStateCode: string | null;
  lastRunAt: string | null;
  lastError: string | null;
  runs?: RecurringRun[];
}

/** What the form edits. */
export interface RecurringDraft {
  name: string;
  templateId: number | null;
  frequency: RecurrenceFrequency;
  every: number;
  startDate: string | null;
  endDate: string | null;
  maxOccurrences: number | null;
  autoPost: boolean;
  vendorBillNoPrefix: string;
  placeOfSupplyStateCode: string;
}

/** A document that can be picked as the template. */
export interface TemplateOption {
  id: number;
  documentNo: string;
  contactName: string | null;
  totalAmount: number;
  documentDate: string;
}

export interface RecurringKindConfig {
  kind: RecurringKind;
  title: string;
  singular: string;
  documentLabel: string;
  base: string;
  documentRoute: string;
}

export const RECURRING_KINDS: Record<RecurringKind, RecurringKindConfig> = {
  invoice: {
    kind: 'invoice',
    title: 'Recurring invoices',
    singular: 'invoice',
    documentLabel: 'Invoice to repeat',
    base: '/api/sales/recurring-invoices',
    documentRoute: '/sales/invoices',
  },
  bill: {
    kind: 'bill',
    title: 'Recurring bills',
    singular: 'bill',
    documentLabel: 'Bill to repeat',
    base: '/api/purchase/recurring-bills',
    documentRoute: '/purchase/bills',
  },
};

export const FREQUENCY_OPTIONS: readonly { value: RecurrenceFrequency; label: string }[] = [
  { value: 'Weekly', label: 'Weekly' },
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Quarterly', label: 'Quarterly' },
  { value: 'HalfYearly', label: 'Every six months' },
  { value: 'Yearly', label: 'Yearly' },
];

const UNIT: Record<RecurrenceFrequency, [string, string]> = {
  Weekly: ['week', 'weeks'],
  Monthly: ['month', 'months'],
  Quarterly: ['quarter', 'quarters'],
  HalfYearly: ['half-year', 'half-years'],
  Yearly: ['year', 'years'],
};

/** "Every month", "Every 2 weeks". */
export function cadence(frequency: RecurrenceFrequency, every: number): string {
  const [one, many] = UNIT[frequency] ?? ['period', 'periods'];
  return every <= 1 ? `Every ${one}` : `Every ${every} ${many}`;
}

export function scheduleId(s: RecurringSchedule): number {
  return s.recurringInvoiceScheduleId ?? s.recurringBillScheduleId ?? 0;
}

export function templateIdOf(s: RecurringSchedule): number {
  return s.templateInvoiceId ?? s.templateBillId ?? 0;
}

export function runDocumentId(r: RecurringRun): number {
  return r.invoiceId ?? r.billId ?? 0;
}

export function emptyDraft(today: string): RecurringDraft {
  return {
    name: '',
    templateId: null,
    frequency: 'Monthly',
    every: 1,
    startDate: today,
    endDate: null,
    maxOccurrences: null,
    autoPost: false,
    vendorBillNoPrefix: 'REC-',
    placeOfSupplyStateCode: '',
  };
}

export function draftOf(s: RecurringSchedule): RecurringDraft {
  return {
    name: s.name,
    templateId: templateIdOf(s),
    frequency: s.frequency,
    every: s.every,
    startDate: s.startDate,
    endDate: s.endDate,
    maxOccurrences: s.maxOccurrences,
    autoPost: s.autoPost,
    vendorBillNoPrefix: s.vendorBillNoPrefix ?? 'REC-',
    placeOfSupplyStateCode: s.placeOfSupplyStateCode ?? '',
  };
}

/** What a field is missing, in the order the form shows them, or null when it can be sent. */
export function draftProblem(d: RecurringDraft, kind: RecurringKind): string | null {
  if (!d.name.trim()) return 'Name the schedule.';
  if (!d.templateId) return `Choose the ${RECURRING_KINDS[kind].singular} to repeat.`;
  if (!d.startDate) return 'Choose the start date.';
  if (!Number.isInteger(d.every) || d.every < 1 || d.every > 12) return 'Repeat every 1 to 12 periods.';
  if (d.endDate && d.endDate < d.startDate) return 'The end date cannot be before the start date.';
  if (d.maxOccurrences !== null && (d.maxOccurrences < 1 || d.maxOccurrences > 1000)) return 'Stop after 1 to 1000 occurrences.';
  if (d.placeOfSupplyStateCode && !/^\d{2}$/.test(d.placeOfSupplyStateCode)) return 'Place of supply must be a 2-digit state code.';
  return null;
}

/** The request body, named for the document the service expects. */
export function requestOf(d: RecurringDraft, kind: RecurringKind): Record<string, unknown> {
  const body: Record<string, unknown> = {
    name: d.name.trim(),
    frequency: d.frequency,
    every: d.every,
    startDate: d.startDate,
    endDate: d.endDate || null,
    maxOccurrences: d.maxOccurrences ?? null,
    autoPost: d.autoPost,
    placeOfSupplyStateCode: d.placeOfSupplyStateCode.trim() || null,
  };

  if (kind === 'invoice') {
    body['templateInvoiceId'] = d.templateId;
  } else {
    body['templateBillId'] = d.templateId;
    body['vendorBillNoPrefix'] = d.vendorBillNoPrefix.trim() || 'REC-';
  }

  return body;
}

/** The template choices from either list endpoint: Sales pages, Purchase answers with an array. */
export function templateOptions(response: unknown, kind: RecurringKind): TemplateOption[] {
  const rows = Array.isArray(response)
    ? response
    : ((response as { rows?: unknown[] } | null)?.rows ?? []);

  return (rows as Record<string, unknown>[])
    .filter((r) => r['status'] !== 'Void')
    .map((r) => ({
      id: Number(kind === 'invoice' ? r['invoiceId'] : r['billId']),
      documentNo: String(r['documentNo'] ?? ''),
      contactName: (r['contactName'] as string | null) ?? null,
      totalAmount: Number(r['totalAmount'] ?? 0),
      documentDate: String(r['documentDate'] ?? ''),
    }))
    .filter((o) => o.id > 0);
}

export function statusTag(status: RecurringSchedule['status']): string {
  switch (status) {
    case 'Active':
      return 'tag tag-accent';
    case 'Ended':
      return 'tag tag-outline';
    default:
      return 'tag tag-neutral';
  }
}
