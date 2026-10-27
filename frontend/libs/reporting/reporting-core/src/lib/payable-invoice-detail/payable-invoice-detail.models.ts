/**
 * The Payable Invoice Detail's contract (TK-236), field for field with
 * `PayableInvoiceDetailReportModels.cs`, and the pure steps the page takes from an answer: the
 * Payable Invoice Summary line by line, so each vendor bill lists the items, quantities, prices,
 * discounts and taxes it is made of, and marks a line bought as a fixed asset. A line and a bill's own totals are in the bill's own
 * currency; the totals of every bill the filters match are in the branch's base currency. An
 * item or an item category narrows the lines as well as the bills. Dates are ISO `yyyy-MM-dd`.
 */

import { PayableInvoiceStatus, payableBillLink } from '../payable-invoice-summary/payable-invoice-summary.models';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface PayableInvoiceDetailRequest {
  fromDate: string;
  toDate: string;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
  /** Only bills with a line for this item, and only those lines. */
  itemId?: number | null;
  /** Only bills with a line for an item in this category, and only those lines. */
  itemCategoryId?: number | null;
  /** Only this status; every status but void when blank. */
  status?: PayableInvoiceStatus | null;
  pageNumber?: number;
  pageSize?: number;
}

/** One line of a bill, in the bill's own currency. */
export interface PayableInvoiceDetailLine {
  lineNumber: number;
  itemId: number | null;
  /** Blank on a line that is not for an item. */
  itemCode: string | null;
  /** The item's name, else the line's own description. */
  itemName: string;
  /** The line's own description, when it says more than the name. */
  description: string | null;
  /** A fixed asset bought on the bill, which also goes into the asset register. */
  isCapital: boolean;
  /** The fixed-asset category of a capital line. */
  assetCategoryName: string | null;
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

/** A bill with its lines. */
export interface PayableInvoiceDetailBill {
  /** BIL. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  /** The vendor's own number for the bill. */
  vendorBillNo: string | null;
  documentDate: string;
  dueDate: string | null;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  status: PayableInvoiceStatus;
  /** The tax is the branch's own to pay, not the vendor's to be paid. */
  reverseCharge: boolean;
  /** The bill's total in its own currency, round-off included. */
  totalInCurrency: number;
  /** The same total in base currency. */
  total: number;
  /** What the rounding added; null when an item or category narrows the lines. */
  roundOff: number | null;
  lines: PayableInvoiceDetailLine[];
  /** The lines shown added up, in the bill's own currency. */
  linesTaxable: number;
  linesTax: number;
  linesTotal: number;
}

/** The lines shown, in every bill the filters match, in base currency. */
export interface PayableInvoiceDetailTotals {
  taxable: number;
  tax: number;
  total: number;
}

export interface PayableInvoiceDetailResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** True when an item or category narrows the lines, so a bill's lines are not the whole of it. */
  linesFiltered: boolean;
  /** This page of bills, newest first. */
  bills: PayableInvoiceDetailBill[];
  /** How many bills the filters match, all pages. */
  totalBills: number;
  pageNumber: number;
  pageSize: number;
  /** How many bills have each status, none left out; void is counted but is not part of All. */
  statusCounts: Record<PayableInvoiceStatus, number>;
  /** Added up over every bill the filters match, not only this page. */
  totals: PayableInvoiceDetailTotals;
}

/** An item the filter can pick. */
export interface PayableInvoiceItemChoice {
  itemId: number;
  itemCode: string;
  itemName: string;
}

/** An item category the filter can pick. */
export interface PayableInvoiceCategoryChoice {
  itemCategoryId: number;
  categoryCode: string;
  categoryName: string;
}

export type PayableDetailRowKind = 'heading' | 'line' | 'roundoff' | 'subtotal' | 'total';

/** One row of the grid: a bill's heading, one of its lines, its round-off or subtotal, or the grand total. */
export interface PayableDetailGridRow {
  kind: PayableDetailRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The bill's number on a heading, else the label of a round-off, subtotal or total row. */
  name: string;
  /** Where the bill number opens; null off a heading and where the document has no page. */
  link: string | null;
  /** On a heading: the vendor's own number for the bill. */
  vendorBillNo: string | null;
  documentDate: string | null;
  dueDate: string | null;
  contactCode: string;
  contactName: string;
  status: PayableInvoiceStatus | null;
  /** On a heading: the tax is the branch's own to pay, not the vendor's. */
  reverseCharge: boolean;
  /** The bill's currency when it is not the branch's, so its amounts name it; else null. */
  currency: string | null;
  itemCode: string | null;
  itemName: string;
  description: string | null;
  /** On a line: a fixed asset bought on the bill. */
  isCapital: boolean;
  assetCategory: string | null;
  quantity: number | null;
  unitCode: string | null;
  unitPrice: number | null;
  /** The discount on the line, as an amount and, when it was entered as one, a percentage. */
  discount: number | null;
  discountPercent: number | null;
  taxable: number | null;
  taxRate: number | null;
  tax: number | null;
  /** A line's total, a round-off, a bill's total or the grand total. */
  total: number | null;
  /** On a subtotal row of a foreign bill: the bill's total in base currency. */
  baseTotal: number | null;
}

const blankRow = (kind: PayableDetailRowKind, key: string, name: string): PayableDetailGridRow => ({
  kind,
  key,
  name,
  link: null,
  vendorBillNo: null,
  documentDate: null,
  dueDate: null,
  contactCode: '',
  contactName: '',
  status: null,
  reverseCharge: false,
  currency: null,
  itemCode: null,
  itemName: '',
  description: null,
  isCapital: false,
  assetCategory: null,
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

/** Whether a bill is in a currency other than the branch's, so its amounts are worth labelling. */
export function isForeignBill(bill: Pick<PayableInvoiceDetailBill, 'currencyCode'>, baseCurrency: string): boolean {
  return bill.currencyCode !== '' && baseCurrency !== '' && bill.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

/**
 * The grid's rows: for each bill a heading, its lines, its round-off when it has one, and its
 * subtotal; then the totals of every match, which are the server's and not the loaded rows' sum, so
 * they stay right until the last page is loaded. Nothing is worked out here beyond the layout.
 *
 * A bill's subtotal is its own total when every line is shown, and the lines' when an item or
 * category narrows them, since then the lines are not the whole bill and no round-off is shown.
 */
export function payableDetailRows(result: PayableInvoiceDetailResult): PayableDetailGridRow[] {
  const rows: PayableDetailGridRow[] = [];

  for (const bill of result.bills) {
    const id = `${bill.typeCode}:${bill.documentId}`;
    const currency = isForeignBill(bill, result.baseCurrencyCode) ? bill.currencyCode : null;

    rows.push({
      ...blankRow('heading', `${id}:h`, bill.documentNo),
      link: payableBillLink(bill),
      vendorBillNo: bill.vendorBillNo,
      documentDate: bill.documentDate,
      dueDate: bill.dueDate,
      contactCode: bill.contactCode,
      contactName: bill.contactName,
      status: bill.status,
      reverseCharge: bill.reverseCharge,
      currency,
    });

    for (const line of bill.lines) {
      rows.push({
        ...blankRow('line', `${id}:${line.lineNumber}`, ''),
        currency,
        itemCode: line.itemCode,
        itemName: line.itemName,
        description: line.description,
        isCapital: line.isCapital,
        assetCategory: line.assetCategoryName,
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

    if (bill.roundOff !== null && bill.roundOff !== 0) {
      rows.push({ ...blankRow('roundoff', `${id}:r`, 'Round off'), currency, total: bill.roundOff });
    }

    rows.push({
      ...blankRow('subtotal', `${id}:s`, 'Bill total'),
      currency,
      taxable: bill.linesTaxable,
      tax: bill.linesTax,
      total: result.linesFiltered ? bill.linesTotal : bill.totalInCurrency,
      baseTotal: currency && !result.linesFiltered ? bill.total : null,
    });
  }

  if (result.bills.length > 0) {
    const t = result.totals;
    rows.push({
      ...blankRow('total', 't', result.bills.length < result.totalBills ? `Total of all ${result.totalBills} bills` : 'Total'),
      taxable: t.taxable,
      tax: t.tax,
      total: t.total,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendBillDetailPage(loaded: PayableInvoiceDetailResult, next: PayableInvoiceDetailResult): PayableInvoiceDetailResult {
  return { ...next, bills: [...loaded.bills, ...next.bills] };
}

/** Whether there are bills the filters match that are not loaded yet. */
export function hasMoreBillDetail(result: Pick<PayableInvoiceDetailResult, 'totalBills' | 'bills'>): boolean {
  return result.bills.length < result.totalBills;
}

/**
 * What the period came to, in a sentence: how many bills and what they came to, in base currency,
 * which is formatted by the caller. Says when an item or category narrowed the lines to those for it.
 */
export function payableDetailSentence(
  result: Pick<PayableInvoiceDetailResult, 'totalBills' | 'totals' | 'linesFiltered'>,
  money: (value: number) => string,
): string {
  const n = result.totalBills;
  const what = n === 1 ? '1 bill' : `${n} bills`;
  const t = result.totals;

  return result.linesFiltered
    ? `${what} with a line for the chosen item or category; those lines come to ${money(t.taxable)} taxable and ${money(t.tax)} tax, ${money(t.total)} in all.`
    : `${what} for ${money(t.total)}: ${money(t.taxable)} taxable and ${money(t.tax)} tax.`;
}
