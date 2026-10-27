/**
 * The Bill/DN Payment Report's contract (TK-238), field for field with
 * `BillDnPaymentReportModels.cs`, and the pure steps the page takes from an answer:
 * the payments to vendors posted in a period, newest first, each with the bills it settled and what
 * it did not. A payment and what was settled are in the payment's own currency; the totals and the
 * vendors' totals are in the branch's base currency. A payment's method and the bank accounts the
 * filter offers are the receipts report's, since both come from the money documents. Dates are ISO
 * `yyyy-MM-dd`.
 */

import { CollectionPaymentMethod, paymentMethodLabel } from '../invoice-dn-payment-collection/invoice-dn-payment-collection.models';
import { payableBillLink } from '../payable-invoice-summary/payable-invoice-summary.models';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface BillDnPaymentRequest {
  fromDate: string;
  toDate: string;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
  /** Only payments made from this bank account; every account when blank. */
  bankAccountId?: number | null;
  /** Only payments made this way; every method when blank. */
  paymentMethod?: CollectionPaymentMethod | null;
  pageNumber?: number;
  pageSize?: number;
}

/** A document a payment settled, with what it settled of it, in the payment's own currency. */
export interface PaidDocument {
  /** BIL for a bill; the code the settlement names otherwise. */
  typeCode: string;
  documentId: number;
  /** The bill's number; for a document with no number of its own, its code and id. */
  documentNo: string;
  /** The vendor's own number for a bill; null for any other document. */
  vendorBillNo: string | null;
  documentDate: string | null;
  /** The day the settlement took effect. */
  allocationDate: string;
  amount: number;
}

/** One posted payment with the documents it settled. */
export interface VendorPayment {
  spendMoneyId: number;
  paymentNo: string;
  paymentDate: string;
  contactId: number;
  contactCode: string;
  contactName: string;
  bankAccountId: number;
  bankAccountName: string;
  paymentMethod: CollectionPaymentMethod;
  referenceNo: string | null;
  currencyCode: string;
  /** What left the bank, in the payment's own currency. */
  amount: number;
  /** The same amount in the branch's base currency. */
  amountBase: number;
  /** What was withheld from the vendor as TDS, in the payment's own currency. */
  tdsAmount: number;
  /** What went to the documents, in the payment's own currency. */
  settled: number;
  /** What was paid (with the TDS) and settled nothing, such as an advance. */
  unallocated: number;
  documents: PaidDocument[];
}

export interface BillDnPaymentTotals {
  payments: number;
  paid: number;
  unallocated: number;
}

/** What one vendor was paid in the period, in base currency. */
export interface VendorPaid {
  contactId: number;
  contactCode: string;
  contactName: string;
  payments: number;
  paid: number;
}

export interface BillDnPaymentResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of payments, newest first. */
  payments: VendorPayment[];
  /** How many payments the filters match, all pages. */
  totalPayments: number;
  pageNumber: number;
  pageSize: number;
  /** Added up over every payment the filters match, not only this page. */
  totals: BillDnPaymentTotals;
  /** The vendors paid most, largest first, at most ten. */
  topVendors: VendorPaid[];
  /** How many more vendors were paid in the period than are listed. */
  otherVendors: number;
  /** What those other vendors were paid, in base currency. */
  otherVendorsPaid: number;
}

/** Whether a payment is in a currency other than the branch's, so its amounts name it. */
export function isForeignPayment(payment: Pick<VendorPayment, 'currencyCode'>, baseCurrency: string): boolean {
  return payment.currencyCode !== '' && baseCurrency !== '' && payment.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

export type PaymentRowKind = 'payment' | 'document' | 'unallocated' | 'total';

/** One row of the grid: a payment, a document it settled, what it did not settle, or the grand total. */
export interface PaymentGridRow {
  kind: PaymentRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The payment's or document's number, or the label of an unallocated or total row. */
  name: string;
  /** Where the number opens; null where there is no page. */
  link: string | null;
  /** The vendor's own number for a bill, on a document row; else null. */
  vendorBillNo: string | null;
  /** A payment's date, or a document's own date. */
  date: string | null;
  /** The day a settlement took effect, on a document row. */
  settledOn: string | null;
  contactCode: string;
  contactName: string;
  bankAccountName: string;
  /** How the payment was taken, with its reference when it has one. */
  method: string;
  /** The payment's currency when it is not the branch's, so its amounts name it; else null. */
  currency: string | null;
  /** The figure in the amount column, in the payment's own currency (the total's is in base). */
  amount: number;
  /** On a foreign payment's row: the same amount in base currency; else null. */
  amountBase: number | null;
  /** On a payment that had TDS withheld: how much; else null. */
  tds: number | null;
}

/**
 * The grid's rows: for each payment a heading, the documents it settled, and a row for what it did
 * not settle when that is something; then the total paid, which is the server's and not the
 * loaded rows' sum, so it stays right until the last page is loaded. Nothing is worked out here beyond
 * the layout.
 */
export function paymentRows(result: BillDnPaymentResult): PaymentGridRow[] {
  const rows: PaymentGridRow[] = [];

  for (const payment of result.payments) {
    const id = `SPM:${payment.spendMoneyId}`;
    const foreign = isForeignPayment(payment, result.baseCurrencyCode);
    const currency = foreign ? payment.currencyCode : null;

    rows.push({
      kind: 'payment',
      key: id,
      name: payment.paymentNo,
      link: `/banking/spend-money/${payment.spendMoneyId}`,
      vendorBillNo: null,
      date: payment.paymentDate,
      settledOn: null,
      contactCode: payment.contactCode,
      contactName: payment.contactName,
      bankAccountName: payment.bankAccountName,
      method: payment.referenceNo ? `${paymentMethodLabel(payment.paymentMethod)} (${payment.referenceNo})` : paymentMethodLabel(payment.paymentMethod),
      currency,
      amount: payment.amount,
      amountBase: foreign ? payment.amountBase : null,
      tds: payment.tdsAmount > 0 ? payment.tdsAmount : null,
    });

    for (const document of payment.documents) {
      rows.push({
        kind: 'document',
        key: `${id}:${document.typeCode}:${document.documentId}`,
        name: document.documentNo,
        link: payableBillLink(document),
        vendorBillNo: document.vendorBillNo,
        date: document.documentDate,
        settledOn: document.allocationDate,
        contactCode: '',
        contactName: '',
        bankAccountName: '',
        method: '',
        currency,
        amount: document.amount,
        amountBase: null,
        tds: null,
      });
    }

    if (payment.unallocated !== 0) {
      rows.push({
        kind: 'unallocated',
        key: `${id}:u`,
        name: 'Not settled against a document',
        link: null,
        vendorBillNo: null,
        date: null,
        settledOn: null,
        contactCode: '',
        contactName: '',
        bankAccountName: '',
        method: '',
        currency,
        amount: payment.unallocated,
        amountBase: null,
        tds: null,
      });
    }
  }

  if (result.payments.length > 0) {
    rows.push({
      kind: 'total',
      key: 't',
      name: result.payments.length < result.totalPayments ? `Total paid, all ${result.totalPayments} payments` : 'Total paid',
      link: null,
      vendorBillNo: null,
      date: null,
      settledOn: null,
      contactCode: '',
      contactName: '',
      bankAccountName: '',
      method: '',
      currency: null,
      amount: result.totals.paid,
      amountBase: null,
      tds: null,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the totals and vendors are the latest answer's. */
export function appendPaymentPage(loaded: BillDnPaymentResult, next: BillDnPaymentResult): BillDnPaymentResult {
  return { ...next, payments: [...loaded.payments, ...next.payments] };
}

/** Whether there are payments the filters match that are not loaded yet. */
export function hasMorePayments(result: Pick<BillDnPaymentResult, 'totalPayments' | 'payments'>): boolean {
  return result.payments.length < result.totalPayments;
}

/**
 * What the period came to, in a sentence: how many payments, what was paid and how much of it
 * settled nothing. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function paymentSentence(
  result: Pick<BillDnPaymentResult, 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totals.payments;
  const what = n === 1 ? '1 payment' : `${n} payments`;
  const t = result.totals;

  return t.unallocated !== 0
    ? `${what} paid ${money(t.paid)}, of which ${money(t.unallocated)} was not settled against a document.`
    : `${what} paid ${money(t.paid)}, all settled against documents.`;
}
