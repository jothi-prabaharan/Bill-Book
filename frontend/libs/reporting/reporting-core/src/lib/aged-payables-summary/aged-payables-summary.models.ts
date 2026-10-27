/**
 * The Aged Payables Summary's contract (TK-233), field for field with
 * `AgedPayablesSummaryReportModels.cs`, and the pure steps the page takes from an answer: what the
 * branch owed each vendor on a date, spread over Current and four overdue columns. Every figure is in
 * the branch's base currency. A debit note, or a payment or advance not yet applied to a bill, is a
 * negative amount in Current, so a vendor's total is what the branch owes them. It is the Aged
 * Receivables Summary the other way round and shares its age basis and column widths. Dates are ISO
 * `yyyy-MM-dd`.
 */

import { blankZero } from '../journal-report/journal-report.models';
import { AgeBasis, AgedReceivablesSummaryRow, overdueOf } from '../aged-receivables-summary/aged-receivables-summary.models';

/** What the report is asked for. The as-at date is required; the rest narrow or change it. */
export interface AgedPayablesSummaryRequest {
  asAtDate: string;
  /** Due date when blank; invoice date ages a bill from its bill date. */
  ageBy?: AgeBasis | null;
  /** 30 when blank. */
  bucketDays?: number | null;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
  /** Also list vendors with nothing owed. */
  showZeroBalances?: boolean;
}

/** One vendor's balance across the five columns. */
export interface AgedPayablesSummaryRow {
  /** Null on the row for what no vendor claims, such as a journal to Accounts Payable with no contact. */
  contactId: number | null;
  contactCode: string;
  contactName: string;
  /** A micro or small enterprise, whose bills the Sec 43B(h) 45-day rule applies to. */
  isMsme: boolean;
  current: number;
  bucket1: number;
  bucket2: number;
  bucket3: number;
  bucket4: number;
  total: number;
}

export interface AgedPayablesSummaryResult {
  asAtDate: string;
  ageBy: AgeBasis;
  bucketDays: number;
  baseCurrencyCode: string;
  /** The five column headings, Current first. */
  bucketLabels: string[];
  vendors: AgedPayablesSummaryRow[];
  /** Every column added up: the Accounts Payable balance on the date. */
  totals: AgedPayablesSummaryRow;
}

/** A vendor the filter can pick. */
export interface AgedPayablesVendorChoice {
  contactId: number;
  contactCode: string;
  contactName: string;
}

/** What a bill's age counts from, as the filter reads it. */
export const PAYABLE_AGE_BASIS_OPTIONS: readonly { value: AgeBasis; label: string }[] = [
  { value: 'DueDate', label: 'Due date' },
  { value: 'InvoiceDate', label: 'Bill date' },
];

/** Where the Sec 43B(h) view of MSME vendors' bills opens. */
export const MSME_AGING_LINK = '/reports/msme-aging';

export type AgedPayablesRowKind = 'vendor' | 'unassigned' | 'total';

/** One row of the grid. A nought is null so its cell reads blank; the totals row keeps its noughts. */
export interface AgedPayablesGridRow {
  kind: AgedPayablesRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The vendor, so a row can open the detail for it; null on the totals and on what no vendor claims. */
  contactId: number | null;
  name: string;
  code: string;
  isMsme: boolean;
  current: number | null;
  bucket1: number | null;
  bucket2: number | null;
  bucket3: number | null;
  bucket4: number | null;
  total: number;
}

/**
 * The grid's rows: a row for each vendor as the server ordered them, the row for what no vendor claims
 * among them, then the totals when there is anything to total. Nothing is worked out here beyond the
 * layout; the figures are the server's.
 */
export function agedPayablesRows(result: AgedPayablesSummaryResult): AgedPayablesGridRow[] {
  const rows: AgedPayablesGridRow[] = result.vendors.map((v) => ({
    kind: v.contactId === null ? 'unassigned' : 'vendor',
    key: v.contactId === null ? 'u' : `v:${v.contactId}`,
    contactId: v.contactId,
    name: v.contactName,
    code: v.contactCode,
    isMsme: v.isMsme,
    current: blankZero(v.current),
    bucket1: blankZero(v.bucket1),
    bucket2: blankZero(v.bucket2),
    bucket3: blankZero(v.bucket3),
    bucket4: blankZero(v.bucket4),
    total: v.total,
  }));

  if (rows.length > 0) {
    const t = result.totals;
    rows.push({
      kind: 'total',
      key: 't',
      contactId: null,
      name: 'Total',
      code: '',
      isMsme: false,
      current: t.current,
      bucket1: t.bucket1,
      bucket2: t.bucket2,
      bucket3: t.bucket3,
      bucket4: t.bucket4,
      total: t.total,
    });
  }

  return rows;
}

/** How many of the vendors listed are MSMEs. */
export function msmeCount(result: Pick<AgedPayablesSummaryResult, 'vendors'>): number {
  return result.vendors.filter((v) => v.isMsme).length;
}

/**
 * What the report came to, in a sentence: what the branch owes and how much of it is past due. Debits
 * not yet applied are in Current, so what is overdue can exceed the balance. Money is formatted by the
 * caller so the sentence reads in the branch's own currency.
 */
export function agedPayablesSentence(
  result: Pick<AgedPayablesSummaryResult, 'totals' | 'vendors'>,
  money: (value: number) => string,
): string {
  const count = result.vendors.filter((v) => v.contactId !== null).length;
  const who = count === 1 ? '1 vendor' : `${count} vendors`;
  const overdue = overdueOf(result.totals as Pick<AgedReceivablesSummaryRow, 'bucket1' | 'bucket2' | 'bucket3' | 'bucket4'>);

  return overdue > 0
    ? `${money(result.totals.total)} is owed to ${who}; ${money(overdue)} of bills is overdue.`
    : `${money(result.totals.total)} is owed to ${who}; nothing is overdue.`;
}

/** A vendor's label on the filter: the code, then the name. */
export function vendorChoiceLabel(choice: AgedPayablesVendorChoice): string {
  return `${choice.contactCode} — ${choice.contactName}`;
}
