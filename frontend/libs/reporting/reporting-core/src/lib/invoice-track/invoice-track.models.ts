/**
 * The Invoice Track Report's contract (TK-226), field for field with
 * `InvoiceTrackReportModels.cs`, and the pure steps the page takes from an answer: where each sales
 * invoice dated in a period is in its life, with the dates of its steps and what is still due.
 * Amounts are in the branch's base currency; a document in another currency also carries its own
 * total. Received and due are as at the To date. A draft is not in the books, so it owes nothing and
 * is left out of the totals. Dates are ISO `yyyy-MM-dd`; the posting moment is an ISO timestamp.
 */

import { documentLink } from '../aged-receivables-detail/aged-receivables-detail.models';

/** Where an invoice stands as at the To date; overdue wins over partly paid. */
export type InvoiceTrackStatus = 'Draft' | 'ReadyToPost' | 'AwaitingPayment' | 'PartlyPaid' | 'Overdue' | 'Paid' | 'Void';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface InvoiceTrackRequest {
  fromDate: string;
  toDate: string;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Only this status; every status but void when blank. */
  status?: InvoiceTrackStatus | null;
  pageNumber?: number;
  pageSize?: number;
}

/** One invoice with the dates of its life. */
export interface InvoiceTrackRow {
  /** INV, or POS for a sale rung up at the till. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  documentDate: string;
  /** When it was posted; null for a draft and a ready-to-post invoice. */
  postedAt: string | null;
  dueDate: string | null;
  /** The latest settlement of it on or before the To date; null when nothing was settled. */
  lastPaymentDate: string | null;
  /** How far past its due date it was on the To date; null unless it is overdue. */
  daysOverdue: number | null;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  /** The total in the invoice's own currency. */
  totalInCurrency: number;
  total: number;
  received: number;
  due: number;
  status: InvoiceTrackStatus;
}

/** The posted invoices the filters match that still stand, added up: not a draft, not a voided one. */
export interface InvoiceTrackTotals {
  total: number;
  received: number;
  due: number;
}

export interface InvoiceTrackResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of invoices, newest first. */
  invoices: InvoiceTrackRow[];
  /** How many the filters match, all pages. */
  totalInvoices: number;
  pageNumber: number;
  pageSize: number;
  /** How many invoices have each status, none left out; void is counted but is not part of All. */
  statusCounts: Record<InvoiceTrackStatus, number>;
  /** Added up over every posted invoice the filters match, not only this page. */
  totals: InvoiceTrackTotals;
}

/** The statuses in the order the tabs show them: the life of an invoice, start to end. */
export const INVOICE_TRACK_STATUSES: readonly { value: InvoiceTrackStatus; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'ReadyToPost', label: 'Ready to post' },
  { value: 'AwaitingPayment', label: 'Awaiting payment' },
  { value: 'PartlyPaid', label: 'Partly paid' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Void', label: 'Void' },
];

/** How a status reads on the page. */
export function trackStatusLabel(status: InvoiceTrackStatus): string {
  return INVOICE_TRACK_STATUSES.find((s) => s.value === status)?.label ?? status;
}

/** The colour of a status's tag: paid reads as done, overdue as a problem, and what is not yet in the books as outlined. */
export function trackStatusTag(status: InvoiceTrackStatus): string {
  switch (status) {
    case 'Paid':
      return 'tag-success';
    case 'Overdue':
      return 'tag-danger';
    case 'PartlyPaid':
      return 'tag-warning';
    case 'AwaitingPayment':
      return 'tag-info';
    case 'Draft':
    case 'ReadyToPost':
      return 'tag-outline';
    default:
      return 'tag-neutral';
  }
}

/** A tab above the grid: a status ('' is All), its label and how many invoices have it. */
export interface InvoiceTrackTab {
  value: string;
  label: string;
  count: number;
}

/**
 * The tabs: All, then every status, a nought included so a tab can say nothing is in it. The counts
 * are the server's, because a page cannot count the pages it was not sent. All is every status but
 * void, the list a person gets without choosing one.
 */
export function invoiceTrackTabs(counts: Readonly<Partial<Record<InvoiceTrackStatus, number>>> | undefined): InvoiceTrackTab[] {
  const count = (status: InvoiceTrackStatus): number => counts?.[status] ?? 0;
  const all = INVOICE_TRACK_STATUSES.filter((s) => s.value !== 'Void').reduce((sum, s) => sum + count(s.value), 0);

  return [{ value: '', label: 'All', count: all }, ...INVOICE_TRACK_STATUSES.map((s) => ({ ...s, count: count(s.value) }))];
}

/** Whether an invoice is in the books: posted or voided, as against a draft or one ready to post. */
export function isInBooks(status: InvoiceTrackStatus): boolean {
  return status !== 'Draft' && status !== 'ReadyToPost';
}

/** Whether an invoice is in a currency other than the branch's, so its own total is worth showing. */
export function isForeignTrack(row: Pick<InvoiceTrackRow, 'currencyCode'>, baseCurrency: string): boolean {
  return row.currencyCode !== '' && baseCurrency !== '' && row.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

/**
 * The day a moment fell on in the viewer's own time, as `yyyy-MM-dd`: the server sends the moment an
 * invoice was posted, and an evening posting in India is the next day in UTC. A value that is not a
 * date gives null.
 */
export function localIsoDate(moment: string | null | undefined): string | null {
  if (!moment) {
    return null;
  }

  const date = new Date(moment);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export type InvoiceTrackRowKind = 'invoice' | 'total';

/** One row of the grid: an invoice, or the totals of every posted invoice the filters match that still stands. */
export interface InvoiceTrackGridRow {
  kind: InvoiceTrackRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The invoice's number, or the label on the totals row. */
  name: string;
  /** Where the number opens; null on the totals row. */
  link: string | null;
  documentDate: string | null;
  /** The day it was posted, in the viewer's time; null while it is not in the books. */
  postedOn: string | null;
  dueDate: string | null;
  lastPaymentDate: string | null;
  daysOverdue: number | null;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  /** The invoice's own total when it is in another currency, else null. */
  ownTotal: number | null;
  total: number;
  received: number;
  due: number;
  /** A draft and a ready-to-post invoice owe nothing yet, so their received and due are left blank. */
  inBooks: boolean;
  status: InvoiceTrackStatus | null;
}

/**
 * The grid's rows: each invoice as the server sent it, then the totals of every posted invoice the
 * filters match, which are the server's and not the loaded rows' sum, so they stay right until the
 * last page is loaded. Nothing is worked out here beyond the layout and the day of the posting.
 */
export function invoiceTrackRows(result: InvoiceTrackResult): InvoiceTrackGridRow[] {
  const rows: InvoiceTrackGridRow[] = result.invoices.map((r) => ({
    kind: 'invoice',
    key: `${r.typeCode}:${r.documentId}`,
    name: r.documentNo,
    link: documentLink(r),
    documentDate: r.documentDate,
    postedOn: localIsoDate(r.postedAt),
    dueDate: r.dueDate,
    lastPaymentDate: r.lastPaymentDate,
    daysOverdue: r.daysOverdue,
    contactCode: r.contactCode,
    contactName: r.contactName,
    currencyCode: r.currencyCode,
    ownTotal: isForeignTrack(r, result.baseCurrencyCode) ? r.totalInCurrency : null,
    total: r.total,
    received: r.received,
    due: r.due,
    inBooks: isInBooks(r.status),
    status: r.status,
  }));

  if (result.invoices.length > 0) {
    const t = result.totals;
    rows.push({
      kind: 'total',
      key: 't',
      name: result.invoices.length < result.totalInvoices ? `Total of posted, all ${result.totalInvoices}` : 'Total of posted',
      link: null,
      documentDate: null,
      postedOn: null,
      dueDate: null,
      lastPaymentDate: null,
      daysOverdue: null,
      contactCode: '',
      contactName: '',
      currencyCode: '',
      ownTotal: null,
      total: t.total,
      received: t.received,
      due: t.due,
      inBooks: true,
      status: null,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendTrackPage(loaded: InvoiceTrackResult, next: InvoiceTrackResult): InvoiceTrackResult {
  return { ...next, invoices: [...loaded.invoices, ...next.invoices] };
}

/** Whether there are invoices the filters match that are not loaded yet. */
export function hasMoreTrack(result: Pick<InvoiceTrackResult, 'totalInvoices' | 'invoices'>): boolean {
  return result.invoices.length < result.totalInvoices;
}

/**
 * What the period came to, in a sentence: how many invoices, and what the posted ones are for and how
 * much of it is received and due. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function invoiceTrackSentence(
  result: Pick<InvoiceTrackResult, 'totalInvoices' | 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totalInvoices;
  const what = n === 1 ? '1 invoice' : `${n} invoices`;
  const t = result.totals;

  return `${what}. Posted: ${money(t.total)}, of which ${money(t.received)} is received and ${money(t.due)} is due.`;
}
