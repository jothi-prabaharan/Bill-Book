/**
 * The Payable Invoice Summary's contract (TK-235), field for field with
 * `PayableInvoiceSummaryReportModels.cs`, and the pure steps the page takes from an answer: every posted
 * vendor bill dated in a period with its taxable value, tax and total, what has been paid and what is still
 * due. Every amount is in the branch's base currency; a document in another currency also carries its own
 * total. Paid and due are as at the To date. A debit note is a negative amount, and is listed only when
 * asked for. Dates are ISO `yyyy-MM-dd`.
 *
 * What the vendor is owed (`payable`) is the total less any TDS withheld and, on a reverse-charge bill, the
 * tax the branch pays itself, so payable less paid is due on every row, and is the total less paid where
 * there is neither.
 */

import { AgedPayablesVendorChoice } from '../aged-payables-summary/aged-payables-summary.models';

/** Where a bill or debit note stands as at the To date; overdue wins over partly paid. */
export type PayableInvoiceStatus = 'AwaitingPayment' | 'PartlyPaid' | 'Overdue' | 'Paid' | 'Void';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface PayableInvoiceSummaryRequest {
  fromDate: string;
  toDate: string;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
  /** Only this currency; every currency when blank. */
  currencyCode?: string | null;
  /** Only this status; every status but void when blank. */
  status?: PayableInvoiceStatus | null;
  includeDebitNotes?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

/** One bill, or debit note, with its figures in base currency. */
export interface PayableInvoiceSummaryRow {
  /** BIL for a bill, DBN for a debit note. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  /** The vendor's own number for the bill; null on a debit note. */
  vendorBillNo: string | null;
  documentDate: string;
  /** Blank on a debit note. */
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
  /** The tax is paid by the branch itself, not to the vendor. */
  reverseCharge: boolean;
  /** TDS withheld from the vendor. */
  tds: number;
  total: number;
  /** What the vendor is owed: the total less TDS and, on a reverse-charge bill, the tax. */
  payable: number;
  paid: number;
  due: number;
  status: PayableInvoiceStatus;
}

export interface PayableInvoiceSummaryTotals {
  taxable: number;
  tax: number;
  tds: number;
  total: number;
  payable: number;
  paid: number;
  due: number;
}

export interface PayableInvoiceSummaryResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of bills, newest first. */
  invoices: PayableInvoiceSummaryRow[];
  /** How many the filters match, all pages. */
  totalInvoices: number;
  pageNumber: number;
  pageSize: number;
  /** How many documents have each status, none left out; void is counted but is not part of All. */
  statusCounts: Record<PayableInvoiceStatus, number>;
  /** Added up over every bill the filters match, not only this page. */
  totals: PayableInvoiceSummaryTotals;
}

/** A currency the branch has been billed in. */
export interface PayableInvoiceCurrencyChoice {
  currencyCode: string;
}

/** The statuses in the order the tabs show them. */
export const PAYABLE_INVOICE_STATUSES: readonly { value: PayableInvoiceStatus; label: string }[] = [
  { value: 'AwaitingPayment', label: 'Awaiting payment' },
  { value: 'PartlyPaid', label: 'Partly paid' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Void', label: 'Void' },
];

/** How a status reads on the page. */
export function payableStatusLabel(status: PayableInvoiceStatus): string {
  return PAYABLE_INVOICE_STATUSES.find((s) => s.value === status)?.label ?? status;
}

/** The colour of a status's tag: paid reads as done and overdue as a problem. */
export function payableStatusTag(status: PayableInvoiceStatus): string {
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
export interface PayableStatusTab {
  value: string;
  label: string;
  count: number;
}

/**
 * The tabs: All, then every status, a nought included so a tab can say nothing is in it. The counts are the
 * server's, because a page cannot count the pages it was not sent. All is every status but void, the list a
 * person gets without choosing one.
 */
export function payableStatusTabs(counts: Readonly<Partial<Record<PayableInvoiceStatus, number>>> | undefined): PayableStatusTab[] {
  const count = (status: PayableInvoiceStatus): number => counts?.[status] ?? 0;
  const all = PAYABLE_INVOICE_STATUSES.filter((s) => s.value !== 'Void').reduce((sum, s) => sum + count(s.value), 0);

  return [{ value: '', label: 'All', count: all }, ...PAYABLE_INVOICE_STATUSES.map((s) => ({ ...s, count: count(s.value) }))];
}

export type PayableInvoiceRowKind = 'invoice' | 'total';

/** One row of the grid: a bill or debit note, or the totals of every one the filters match. */
export interface PayableInvoiceGridRow {
  kind: PayableInvoiceRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The document's number, or the label on the totals row. */
  name: string;
  /** Where the number opens; null on the totals row and where the document has no page. */
  link: string | null;
  isDebitNote: boolean;
  /** The vendor's own bill number, shown under ours. */
  vendorBillNo: string | null;
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
  /** TDS withheld, and whether the tax is the branch's own to pay: why what the vendor is owed can differ from the total. */
  tds: number;
  reverseCharge: boolean;
  payable: number;
  /** Whether what the vendor is owed differs from the total, so the page says why. */
  payableDiffers: boolean;
  paid: number;
  due: number;
  status: PayableInvoiceStatus | null;
}

/** Where a bill or debit note opens, or null when the code is neither. */
export function payableBillLink(row: Pick<PayableInvoiceSummaryRow, 'typeCode' | 'documentId'>): string | null {
  switch (row.typeCode) {
    case 'BIL':
      return `/purchase/bills/${row.documentId}`;
    case 'DBN':
      return `/purchase/debit-notes/${row.documentId}`;
    default:
      return null;
  }
}

/** Whether a row is a debit note, whose amounts run the other way. */
export function isDebitNote(row: Pick<PayableInvoiceSummaryRow, 'typeCode'>): boolean {
  return row.typeCode === 'DBN';
}

/** Whether a row is in a currency other than the branch's, so its own total is worth showing. */
export function isForeignPayable(row: Pick<PayableInvoiceSummaryRow, 'currencyCode'>, baseCurrency: string): boolean {
  return row.currencyCode !== '' && baseCurrency !== '' && row.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

/**
 * The grid's rows: each document as the server sent it, then the totals of every match, which are the
 * server's and not the loaded rows' sum, so they stay right until the last page is loaded. Nothing is
 * worked out here beyond the layout.
 */
export function payableInvoiceRows(result: PayableInvoiceSummaryResult): PayableInvoiceGridRow[] {
  const rows: PayableInvoiceGridRow[] = result.invoices.map((r) => ({
    kind: 'invoice',
    key: `${r.typeCode}:${r.documentId}`,
    name: r.documentNo,
    link: payableBillLink(r),
    isDebitNote: isDebitNote(r),
    vendorBillNo: r.vendorBillNo,
    documentDate: r.documentDate,
    dueDate: r.dueDate,
    daysOverdue: r.daysOverdue,
    contactCode: r.contactCode,
    contactName: r.contactName,
    currencyCode: r.currencyCode,
    ownTotal: isForeignPayable(r, result.baseCurrencyCode) ? r.totalInCurrency : null,
    taxable: r.taxable,
    tax: r.tax,
    total: r.total,
    tds: r.tds,
    reverseCharge: r.reverseCharge,
    payable: r.payable,
    payableDiffers: r.payable !== r.total,
    paid: r.paid,
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
      isDebitNote: false,
      vendorBillNo: null,
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
      tds: t.tds,
      reverseCharge: false,
      payable: t.payable,
      payableDiffers: t.payable !== t.total,
      paid: t.paid,
      due: t.due,
      status: null,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendPayablePage(loaded: PayableInvoiceSummaryResult, next: PayableInvoiceSummaryResult): PayableInvoiceSummaryResult {
  return { ...next, invoices: [...loaded.invoices, ...next.invoices] };
}

/** Whether there are bills the filters match that are not loaded yet. */
export function hasMorePayable(result: Pick<PayableInvoiceSummaryResult, 'totalInvoices' | 'invoices'>): boolean {
  return result.invoices.length < result.totalInvoices;
}

/** The vendor list the filter offers is the Aged Payables Summary's. */
export type PayableInvoiceVendorChoice = AgedPayablesVendorChoice;

/**
 * What the period came to, in a sentence: how many documents, what they were for, what was paid and what is
 * still due. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function payableInvoiceSentence(
  result: Pick<PayableInvoiceSummaryResult, 'totalInvoices' | 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totalInvoices;
  const what = n === 1 ? '1 document' : `${n} documents`;

  return `${what} for ${money(result.totals.total)}, of which ${money(result.totals.paid)} is paid and ${money(result.totals.due)} is due.`;
}
