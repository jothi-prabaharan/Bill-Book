/**
 * The Aged Receivables Summary's contract (TK-222), field for field with
 * `AgedReceivablesSummaryReportModels.cs`, and the pure steps the page takes from an
 * answer: what each customer owed on a date, spread over Current and four overdue
 * columns. Every figure is in the branch's base currency. A credit note, or a receipt
 * or advance not yet applied to an invoice, is a negative amount in Current, so a
 * customer's total is their receivable balance. Dates are ISO `yyyy-MM-dd`.
 */

import { blankZero } from '../journal-report/journal-report.models';

/** What an invoice's age counts from. */
export type AgeBasis = 'DueDate' | 'InvoiceDate';

/** The widths an overdue column may have, in days; the first is the default. */
export const BUCKET_SIZES: readonly number[] = [30, 15, 7];

/** What the report is asked for. The as-at date is required; the rest narrow or change it. */
export interface AgedReceivablesSummaryRequest {
  asAtDate: string;
  /** Due date when blank. */
  ageBy?: AgeBasis | null;
  /** 30 when blank. */
  bucketDays?: number | null;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Also list customers with nothing owed. */
  showZeroBalances?: boolean;
}

/** One customer's balance across the five columns. */
export interface AgedReceivablesSummaryRow {
  /** Null on the row for what no customer claims, such as a journal to Accounts Receivable with no contact. */
  contactId: number | null;
  contactCode: string;
  contactName: string;
  current: number;
  bucket1: number;
  bucket2: number;
  bucket3: number;
  bucket4: number;
  total: number;
}

export interface AgedReceivablesSummaryResult {
  asAtDate: string;
  ageBy: AgeBasis;
  bucketDays: number;
  baseCurrencyCode: string;
  /** The five column headings, Current first. */
  bucketLabels: string[];
  customers: AgedReceivablesSummaryRow[];
  /** Every column added up: the Accounts Receivable balance on the date. */
  totals: AgedReceivablesSummaryRow;
}

/** A customer the filter can pick. */
export interface AgedReceivablesCustomerChoice {
  contactId: number;
  contactCode: string;
  contactName: string;
}

export const AGE_BASIS_OPTIONS: readonly { value: AgeBasis; label: string }[] = [
  { value: 'DueDate', label: 'Due date' },
  { value: 'InvoiceDate', label: 'Invoice date' },
];

/** A column width as the filter reads it. */
export function bucketOptionLabel(days: number): string {
  return `${days} days`;
}

export type AgedReceivablesRowKind = 'customer' | 'unassigned' | 'total';

/** One row of the grid. A nought is null so its cell reads blank; the totals row keeps its noughts. */
export interface AgedReceivablesGridRow {
  kind: AgedReceivablesRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  name: string;
  code: string;
  current: number | null;
  bucket1: number | null;
  bucket2: number | null;
  bucket3: number | null;
  bucket4: number | null;
  total: number;
}

/** The five columns of a row in order, Current first. */
export function columnAmounts(row: Pick<AgedReceivablesSummaryRow, 'current' | 'bucket1' | 'bucket2' | 'bucket3' | 'bucket4'>): number[] {
  return [row.current, row.bucket1, row.bucket2, row.bucket3, row.bucket4];
}

/** What is past due: the four overdue columns added up. A credit sitting in Current is not taken off it. */
export function overdueOf(row: Pick<AgedReceivablesSummaryRow, 'bucket1' | 'bucket2' | 'bucket3' | 'bucket4'>): number {
  return row.bucket1 + row.bucket2 + row.bucket3 + row.bucket4;
}

/**
 * The grid's rows: a row for each customer as the server ordered them, the row for what
 * no customer claims among them, then the totals when there is anything to total. Nothing
 * is worked out here beyond the layout; the figures are the server's.
 */
export function agedReceivablesRows(result: AgedReceivablesSummaryResult): AgedReceivablesGridRow[] {
  const rows: AgedReceivablesGridRow[] = result.customers.map((c) => ({
    kind: c.contactId === null ? 'unassigned' : 'customer',
    key: c.contactId === null ? 'u' : `c:${c.contactId}`,
    name: c.contactName,
    code: c.contactCode,
    current: blankZero(c.current),
    bucket1: blankZero(c.bucket1),
    bucket2: blankZero(c.bucket2),
    bucket3: blankZero(c.bucket3),
    bucket4: blankZero(c.bucket4),
    total: c.total,
  }));

  if (rows.length > 0) {
    const t = result.totals;
    rows.push({
      kind: 'total',
      key: 't',
      name: 'Total',
      code: '',
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

/**
 * What the report came to, in a sentence: the receivable balance and how much is past due.
 * Credits not yet applied are in Current, so what is overdue can exceed the balance. Money is
 * formatted by the caller so the sentence reads in the branch's own currency.
 */
export function agedReceivablesSentence(
  result: Pick<AgedReceivablesSummaryResult, 'totals' | 'customers'>,
  money: (value: number) => string,
): string {
  const count = result.customers.filter((c) => c.contactId !== null).length;
  const who = count === 1 ? '1 customer' : `${count} customers`;
  const overdue = overdueOf(result.totals);

  return overdue > 0
    ? `${money(result.totals.total)} is owed by ${who}; ${money(overdue)} of invoices is overdue.`
    : `${money(result.totals.total)} is owed by ${who}; nothing is overdue.`;
}

/** A customer's label on the filter: the code, then the name. */
export function customerChoiceLabel(choice: AgedReceivablesCustomerChoice): string {
  return `${choice.contactCode} — ${choice.contactName}`;
}
