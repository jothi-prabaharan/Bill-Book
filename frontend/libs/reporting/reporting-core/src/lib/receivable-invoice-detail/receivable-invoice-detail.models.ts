/**
 * The Receivable Invoice Detail's contract (TK-225), field for field with
 * `ReceivableInvoiceDetailReportModels.cs`, and the pure steps the page takes from an answer: the
 * Receivable Invoice Summary line by line, so each invoice lists the items, quantities, prices,
 * discounts and taxes it is made of. A line and an invoice's own totals are in the invoice's own
 * currency; the totals of every invoice the filters match are in the branch's base currency. An
 * item or an item category narrows the lines as well as the invoices. Dates are ISO `yyyy-MM-dd`.
 */

import { documentLink } from '../aged-receivables-detail/aged-receivables-detail.models';
import { ReceivableInvoiceStatus } from '../receivable-invoice-summary/receivable-invoice-summary.models';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface ReceivableInvoiceDetailRequest {
  fromDate: string;
  toDate: string;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Only invoices with a line for this item, and only those lines. */
  itemId?: number | null;
  /** Only invoices with a line for an item in this category, and only those lines. */
  itemCategoryId?: number | null;
  /** Only this status; every status but void when blank. */
  status?: ReceivableInvoiceStatus | null;
  pageNumber?: number;
  pageSize?: number;
}

/** One line of an invoice, in the invoice's own currency. */
export interface ReceivableInvoiceDetailLine {
  lineNumber: number;
  itemId: number | null;
  /** Blank on a line that is not for an item. */
  itemCode: string | null;
  /** The item's name, else the line's own description. */
  itemName: string;
  /** The line's own description, when it says more than the name. */
  description: string | null;
  quantity: number;
  unitCode: string | null;
  unitPrice: number;
  discountAmount: number;
  discountPercent: number | null;
  taxable: number;
  /** The rates of the line's tax rows added up; nought when it has none. */
  taxRate: number;
  taxAmount: number;
  lineTotal: number;
}

/** An invoice with its lines. */
export interface ReceivableInvoiceDetailInvoice {
  /** INV, or POS for a sale rung up at the till. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  documentDate: string;
  dueDate: string | null;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  status: ReceivableInvoiceStatus;
  /** The invoice's total in its own currency, round-off included. */
  totalInCurrency: number;
  /** The same total in base currency. */
  total: number;
  /** What the rounding added; null when an item or category narrows the lines. */
  roundOff: number | null;
  lines: ReceivableInvoiceDetailLine[];
  /** The lines shown added up, in the invoice's own currency. */
  linesTaxable: number;
  linesTax: number;
  linesTotal: number;
}

/** The lines shown, in every invoice the filters match, in base currency. */
export interface ReceivableInvoiceDetailTotals {
  taxable: number;
  tax: number;
  total: number;
}

export interface ReceivableInvoiceDetailResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** True when an item or category narrows the lines, so an invoice's lines are not the whole of it. */
  linesFiltered: boolean;
  /** This page of invoices, newest first. */
  invoices: ReceivableInvoiceDetailInvoice[];
  /** How many invoices the filters match, all pages. */
  totalInvoices: number;
  pageNumber: number;
  pageSize: number;
  /** How many invoices have each status, none left out; void is counted but is not part of All. */
  statusCounts: Record<ReceivableInvoiceStatus, number>;
  /** Added up over every invoice the filters match, not only this page. */
  totals: ReceivableInvoiceDetailTotals;
}

/** An item the filter can pick. */
export interface ReceivableInvoiceItemChoice {
  itemId: number;
  itemCode: string;
  itemName: string;
}

/** An item category the filter can pick. */
export interface ReceivableInvoiceCategoryChoice {
  itemCategoryId: number;
  categoryCode: string;
  categoryName: string;
}

export type ReceivableDetailRowKind = 'heading' | 'line' | 'roundoff' | 'subtotal' | 'total';

/** One row of the grid: an invoice's heading, one of its lines, its round-off or subtotal, or the grand total. */
export interface ReceivableDetailGridRow {
  kind: ReceivableDetailRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The invoice's number on a heading, else the label of a round-off, subtotal or total row. */
  name: string;
  /** Where the invoice number opens; null off a heading and where the document has no page. */
  link: string | null;
  documentDate: string | null;
  dueDate: string | null;
  contactCode: string;
  contactName: string;
  status: ReceivableInvoiceStatus | null;
  /** The invoice's currency when it is not the branch's, so its amounts name it; else null. */
  currency: string | null;
  itemCode: string | null;
  itemName: string;
  description: string | null;
  quantity: number | null;
  unitCode: string | null;
  unitPrice: number | null;
  /** The discount on the line, as an amount and, when it was entered as one, a percentage. */
  discount: number | null;
  discountPercent: number | null;
  taxable: number | null;
  taxRate: number | null;
  tax: number | null;
  /** A line's total, a round-off, an invoice's total or the grand total. */
  total: number | null;
  /** On a subtotal row of a foreign invoice: the invoice's total in base currency. */
  baseTotal: number | null;
}

const blankRow = (kind: ReceivableDetailRowKind, key: string, name: string): ReceivableDetailGridRow => ({
  kind,
  key,
  name,
  link: null,
  documentDate: null,
  dueDate: null,
  contactCode: '',
  contactName: '',
  status: null,
  currency: null,
  itemCode: null,
  itemName: '',
  description: null,
  quantity: null,
  unitCode: null,
  unitPrice: null,
  discount: null,
  discountPercent: null,
  taxable: null,
  taxRate: null,
  tax: null,
  total: null,
  baseTotal: null,
});

/** Whether an invoice is in a currency other than the branch's, so its amounts are worth labelling. */
export function isForeignInvoice(invoice: Pick<ReceivableInvoiceDetailInvoice, 'currencyCode'>, baseCurrency: string): boolean {
  return invoice.currencyCode !== '' && baseCurrency !== '' && invoice.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

/**
 * The grid's rows: for each invoice a heading, its lines, its round-off when it has one, and its
 * subtotal; then the totals of every match, which are the server's and not the loaded rows' sum, so
 * they stay right until the last page is loaded. Nothing is worked out here beyond the layout.
 *
 * An invoice's subtotal is its own total when every line is shown, and the lines' when an item or
 * category narrows them, since then the lines are not the whole invoice and no round-off is shown.
 */
export function receivableDetailRows(result: ReceivableInvoiceDetailResult): ReceivableDetailGridRow[] {
  const rows: ReceivableDetailGridRow[] = [];

  for (const invoice of result.invoices) {
    const id = `${invoice.typeCode}:${invoice.documentId}`;
    const currency = isForeignInvoice(invoice, result.baseCurrencyCode) ? invoice.currencyCode : null;

    rows.push({
      ...blankRow('heading', `${id}:h`, invoice.documentNo),
      link: documentLink(invoice),
      documentDate: invoice.documentDate,
      dueDate: invoice.dueDate,
      contactCode: invoice.contactCode,
      contactName: invoice.contactName,
      status: invoice.status,
      currency,
    });

    for (const line of invoice.lines) {
      rows.push({
        ...blankRow('line', `${id}:${line.lineNumber}`, ''),
        currency,
        itemCode: line.itemCode,
        itemName: line.itemName,
        description: line.description,
        quantity: line.quantity,
        unitCode: line.unitCode,
        unitPrice: line.unitPrice,
        discount: line.discountAmount,
        discountPercent: line.discountPercent,
        taxable: line.taxable,
        taxRate: line.taxRate,
        tax: line.taxAmount,
        total: line.lineTotal,
      });
    }

    if (invoice.roundOff !== null && invoice.roundOff !== 0) {
      rows.push({ ...blankRow('roundoff', `${id}:r`, 'Round off'), currency, total: invoice.roundOff });
    }

    rows.push({
      ...blankRow('subtotal', `${id}:s`, 'Invoice total'),
      currency,
      taxable: invoice.linesTaxable,
      tax: invoice.linesTax,
      total: result.linesFiltered ? invoice.linesTotal : invoice.totalInCurrency,
      baseTotal: currency && !result.linesFiltered ? invoice.total : null,
    });
  }

  if (result.invoices.length > 0) {
    const t = result.totals;
    rows.push({
      ...blankRow('total', 't', result.invoices.length < result.totalInvoices ? `Total of all ${result.totalInvoices} invoices` : 'Total'),
      taxable: t.taxable,
      tax: t.tax,
      total: t.total,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendDetailPage(loaded: ReceivableInvoiceDetailResult, next: ReceivableInvoiceDetailResult): ReceivableInvoiceDetailResult {
  return { ...next, invoices: [...loaded.invoices, ...next.invoices] };
}

/** Whether there are invoices the filters match that are not loaded yet. */
export function hasMoreDetail(result: Pick<ReceivableInvoiceDetailResult, 'totalInvoices' | 'invoices'>): boolean {
  return result.invoices.length < result.totalInvoices;
}

/** How an item reads in the filter: its code, then its name. */
export function itemChoiceLabel(item: ReceivableInvoiceItemChoice): string {
  return item.itemCode ? `${item.itemName} (${item.itemCode})` : item.itemName;
}

/** How a category reads in the filter: its name, then its code. */
export function categoryChoiceLabel(category: ReceivableInvoiceCategoryChoice): string {
  return category.categoryCode ? `${category.categoryName} (${category.categoryCode})` : category.categoryName;
}

/**
 * What the period came to, in a sentence: how many invoices and what they came to, in base currency,
 * which is formatted by the caller. Says when an item or category narrowed the lines to those for it.
 */
export function receivableDetailSentence(
  result: Pick<ReceivableInvoiceDetailResult, 'totalInvoices' | 'totals' | 'linesFiltered'>,
  money: (value: number) => string,
): string {
  const n = result.totalInvoices;
  const what = n === 1 ? '1 invoice' : `${n} invoices`;
  const t = result.totals;

  return result.linesFiltered
    ? `${what} with a line for the chosen item or category; those lines come to ${money(t.taxable)} taxable and ${money(t.tax)} tax, ${money(t.total)} in all.`
    : `${what} for ${money(t.total)}: ${money(t.taxable)} taxable and ${money(t.tax)} tax.`;
}
