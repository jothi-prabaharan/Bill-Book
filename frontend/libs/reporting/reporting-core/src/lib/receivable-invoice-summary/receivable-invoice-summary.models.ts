/**
 * The Receivable Invoice Summary's contract (TK-224), field for field with
 * `ReceivableInvoiceSummaryReportModels.cs`, and the pure steps the page takes from an answer:
 * every posted sales invoice dated in a period with its taxable value, tax and total, what has
 * been received and what is still due. Every amount is in the branch's base currency; a document
 * in another currency also carries its own total. Received and due are as at the To date. A credit
 * note is a negative amount, and is listed only when asked for. Dates are ISO `yyyy-MM-dd`.
 */

import { documentLink } from '../aged-receivables-detail/aged-receivables-detail.models';

/** Where an invoice or credit note stands as at the To date; overdue wins over partly paid. */
export type ReceivableInvoiceStatus = 'AwaitingPayment' | 'PartlyPaid' | 'Overdue' | 'Paid' | 'Void';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface ReceivableInvoiceSummaryRequest {
  fromDate: string;
  toDate: string;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Only this currency; every currency when blank. */
  currencyCode?: string | null;
  /** Only this status; every status but void when blank. */
  status?: ReceivableInvoiceStatus | null;
  includeCreditNotes?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

/** One invoice, or credit note, with its figures in base currency. */
export interface ReceivableInvoiceSummaryRow {
  /** INV or POS for an invoice, CRN for a credit note. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  documentDate: string;
  /** Blank on a credit note and on an invoice with none. */
  dueDate: string | null;
  /** How far past its due date it was on the To date; null unless it is overdue. */
  daysOverdue: number | null;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  /** The total in the document's own currency. */
  totalInCurrency: number;
  taxable: number;
  cgst: number;
  sgst: number;
  igst: number;
  cess: number;
  /** The four taxes added up. */
  tax: number;
  total: number;
  received: number;
  due: number;
  status: ReceivableInvoiceStatus;
}

export interface ReceivableInvoiceSummaryTotals {
  taxable: number;
  tax: number;
  total: number;
  received: number;
  due: number;
}

export interface ReceivableInvoiceSummaryResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of invoices, newest first. */
  invoices: ReceivableInvoiceSummaryRow[];
  /** How many the filters match, all pages. */
  totalInvoices: number;
  pageNumber: number;
  pageSize: number;
  /** How many documents have each status, none left out; void is counted but is not part of All. */
  statusCounts: Record<ReceivableInvoiceStatus, number>;
  /** Added up over every invoice the filters match, not only this page. */
  totals: ReceivableInvoiceSummaryTotals;
}

/** A currency the branch has invoiced in. */
export interface ReceivableInvoiceCurrencyChoice {
  currencyCode: string;
}

/** The statuses in the order the tabs show them. */
export const RECEIVABLE_INVOICE_STATUSES: readonly { value: ReceivableInvoiceStatus; label: string }[] = [
  { value: 'AwaitingPayment', label: 'Awaiting payment' },
  { value: 'PartlyPaid', label: 'Partly paid' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Void', label: 'Void' },
];

/** How a status reads on the page. */
export function receivableStatusLabel(status: ReceivableInvoiceStatus): string {
  return RECEIVABLE_INVOICE_STATUSES.find((s) => s.value === status)?.label ?? status;
}

/** The colour of a status's tag: paid reads as done and overdue as a problem. */
export function receivableStatusTag(status: ReceivableInvoiceStatus): string {
  switch (status) {
    case 'Paid':
      return 'tag-success';
    case 'Overdue':
      return 'tag-danger';
    case 'PartlyPaid':
      return 'tag-warning';
    case 'AwaitingPayment':
      return 'tag-info';
    default:
      return 'tag-neutral';
  }
}

/** A tab above the grid: a status ('' is All), its label and how many documents have it. */
export interface ReceivableStatusTab {
  value: string;
  label: string;
  count: number;
}

/**
 * The tabs: All, then every status, a nought included so a tab can say nothing is in it. The counts are
 * the server's, because a page cannot count the pages it was not sent. All is every status but void,
 * the list a person gets without choosing one.
 */
export function receivableStatusTabs(counts: Readonly<Partial<Record<ReceivableInvoiceStatus, number>>> | undefined): ReceivableStatusTab[] {
  const count = (status: ReceivableInvoiceStatus): number => counts?.[status] ?? 0;
  const all = RECEIVABLE_INVOICE_STATUSES.filter((s) => s.value !== 'Void').reduce((sum, s) => sum + count(s.value), 0);

  return [{ value: '', label: 'All', count: all }, ...RECEIVABLE_INVOICE_STATUSES.map((s) => ({ ...s, count: count(s.value) }))];
}

export type ReceivableInvoiceRowKind = 'invoice' | 'total';

/** One row of the grid: an invoice or credit note, or the totals of every one the filters match. */
export interface ReceivableInvoiceGridRow {
  kind: ReceivableInvoiceRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The document's number, or the label on the totals row. */
  name: string;
  /** Where the number opens; null on the totals row and where the document has no page. */
  link: string | null;
  isCreditNote: boolean;
  documentDate: string | null;
  dueDate: string | null;
  daysOverdue: number | null;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  /** The document's own total when it is in another currency, else null. */
  ownTotal: number | null;
  taxable: number;
  tax: number;
  total: number;
  received: number;
  due: number;
  status: ReceivableInvoiceStatus | null;
}

/**
 * The grid's rows: each invoice as the server sent it, then the totals of every match, which are the
 * server's and not the loaded rows' sum, so they stay right until the last page is loaded. Nothing is
 * worked out here beyond the layout.
 */
export function receivableInvoiceRows(result: ReceivableInvoiceSummaryResult): ReceivableInvoiceGridRow[] {
  const rows: ReceivableInvoiceGridRow[] = result.invoices.map((r) => ({
    kind: 'invoice',
    key: `${r.typeCode}:${r.documentId}`,
    name: r.documentNo,
    link: documentLink(r),
    isCreditNote: isCreditNote(r),
    documentDate: r.documentDate,
    dueDate: r.dueDate,
    daysOverdue: r.daysOverdue,
    contactCode: r.contactCode,
    contactName: r.contactName,
    currencyCode: r.currencyCode,
    ownTotal: isForeignRow(r, result.baseCurrencyCode) ? r.totalInCurrency : null,
    taxable: r.taxable,
    tax: r.tax,
    total: r.total,
    received: r.received,
    due: r.due,
    status: r.status,
  }));

  if (result.invoices.length > 0) {
    const t = result.totals;
    rows.push({
      kind: 'total',
      key: 't',
      name: result.invoices.length < result.totalInvoices ? `Total of all ${result.totalInvoices}` : 'Total',
      link: null,
      isCreditNote: false,
      documentDate: null,
      dueDate: null,
      daysOverdue: null,
      contactCode: '',
      contactName: '',
      currencyCode: '',
      ownTotal: null,
      taxable: t.taxable,
      tax: t.tax,
      total: t.total,
      received: t.received,
      due: t.due,
      status: null,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendInvoicePage(loaded: ReceivableInvoiceSummaryResult, next: ReceivableInvoiceSummaryResult): ReceivableInvoiceSummaryResult {
  return { ...next, invoices: [...loaded.invoices, ...next.invoices] };
}

/** Whether there are invoices the filters match that are not loaded yet. */
export function hasMoreInvoices(result: Pick<ReceivableInvoiceSummaryResult, 'totalInvoices' | 'invoices'>): boolean {
  return result.invoices.length < result.totalInvoices;
}

/** Whether a row is a credit note, whose amounts run the other way. */
export function isCreditNote(row: Pick<ReceivableInvoiceSummaryRow, 'typeCode'>): boolean {
  return row.typeCode === 'CRN';
}

/** Whether a row is in a currency other than the branch's, so its own total is worth showing. */
export function isForeignRow(row: Pick<ReceivableInvoiceSummaryRow, 'currencyCode'>, baseCurrency: string): boolean {
  return row.currencyCode !== '' && baseCurrency !== '' && row.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

/**
 * What the period came to, in a sentence: how many invoices, what they were for, what was received
 * and what is still due. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function receivableInvoiceSentence(
  result: Pick<ReceivableInvoiceSummaryResult, 'totalInvoices' | 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totalInvoices;
  const what = n === 1 ? '1 document' : `${n} documents`;

  return `${what} for ${money(result.totals.total)}, of which ${money(result.totals.received)} is received and ${money(result.totals.due)} is due.`;
}
