/**
 * The Invoice/DN Payment Collection Report's contract (TK-227), field for field with
 * `InvoiceDnPaymentCollectionReportModels.cs`, and the pure steps the page takes from an answer:
 * the receipts posted in a period, newest first, each with the documents it settled and what it
 * did not. A receipt and what was settled are in the receipt's own currency; the totals and the
 * customers' totals are in the branch's base currency. Dates are ISO `yyyy-MM-dd`.
 */

import { documentLink } from '../aged-receivables-detail/aged-receivables-detail.models';

/** How a receipt was taken. */
export type CollectionPaymentMethod = 'Cash' | 'Cheque' | 'BankTransfer' | 'Upi' | 'Card' | 'DemandDraft' | 'Wallet' | 'Other';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface InvoiceDnPaymentCollectionRequest {
  fromDate: string;
  toDate: string;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Only receipts paid into this bank account; every account when blank. */
  bankAccountId?: number | null;
  /** Only receipts taken this way; every method when blank. */
  paymentMethod?: CollectionPaymentMethod | null;
  pageNumber?: number;
  pageSize?: number;
}

/** A document a receipt settled, with what it settled of it, in the receipt's own currency. */
export interface SettledDocument {
  /** INV or POS for an invoice; the code the settlement names otherwise. */
  typeCode: string;
  documentId: number;
  /** The invoice's number; for a document with no number of its own, its code and id. */
  documentNo: string;
  documentDate: string | null;
  /** The day the settlement took effect. */
  allocationDate: string;
  amount: number;
}

/** One posted receipt with the documents it settled. */
export interface ReceiptCollection {
  receiveMoneyId: number;
  receiptNo: string;
  receiptDate: string;
  contactId: number;
  contactCode: string;
  contactName: string;
  bankAccountId: number;
  bankAccountName: string;
  paymentMethod: CollectionPaymentMethod;
  referenceNo: string | null;
  currencyCode: string;
  /** What reached the bank, in the receipt's own currency. */
  amount: number;
  /** The same amount in the branch's base currency. */
  amountBase: number;
  /** What the customer withheld as TDS, in the receipt's own currency. */
  tdsAmount: number;
  /** What went to the documents, in the receipt's own currency. */
  settled: number;
  /** What was received (with the TDS) and settled nothing, such as an advance. */
  unallocated: number;
  documents: SettledDocument[];
}

export interface InvoiceDnPaymentCollectionTotals {
  receipts: number;
  collected: number;
  unallocated: number;
}

/** What one customer paid in the period, in base currency. */
export interface CustomerCollection {
  contactId: number;
  contactCode: string;
  contactName: string;
  receipts: number;
  collected: number;
}

export interface InvoiceDnPaymentCollectionResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of receipts, newest first. */
  receipts: ReceiptCollection[];
  /** How many receipts the filters match, all pages. */
  totalReceipts: number;
  pageNumber: number;
  pageSize: number;
  /** Added up over every receipt the filters match, not only this page. */
  totals: InvoiceDnPaymentCollectionTotals;
  /** The customers who paid most, largest first, at most ten. */
  topCustomers: CustomerCollection[];
  /** How many more customers paid in the period than are listed. */
  otherCustomers: number;
  /** What those other customers paid, in base currency. */
  otherCustomersCollected: number;
}

/** A bank account the filter can pick. */
export interface CollectionBankAccountChoice {
  bankAccountId: number;
  accountName: string;
  currencyCode: string;
}

/** The payment methods in the order the filter offers them. */
export const COLLECTION_PAYMENT_METHODS: readonly { value: CollectionPaymentMethod; label: string }[] = [
  { value: 'Cash', label: 'Cash' },
  { value: 'Cheque', label: 'Cheque' },
  { value: 'BankTransfer', label: 'Bank transfer' },
  { value: 'Upi', label: 'UPI' },
  { value: 'Card', label: 'Card' },
  { value: 'DemandDraft', label: 'Demand draft' },
  { value: 'Wallet', label: 'Wallet' },
  { value: 'Other', label: 'Other' },
];

/** How a payment method reads on the page. */
export function paymentMethodLabel(method: CollectionPaymentMethod): string {
  return COLLECTION_PAYMENT_METHODS.find((m) => m.value === method)?.label ?? method;
}

/** Whether a receipt is in a currency other than the branch's, so its amounts name it. */
export function isForeignReceipt(receipt: Pick<ReceiptCollection, 'currencyCode'>, baseCurrency: string): boolean {
  return receipt.currencyCode !== '' && baseCurrency !== '' && receipt.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

export type CollectionRowKind = 'receipt' | 'document' | 'unallocated' | 'total';

/** One row of the grid: a receipt, a document it settled, what it did not settle, or the grand total. */
export interface CollectionGridRow {
  kind: CollectionRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The receipt's or document's number, or the label of an unallocated or total row. */
  name: string;
  /** Where the number opens; null where there is no page. */
  link: string | null;
  /** A receipt's date, or a document's own date. */
  date: string | null;
  /** The day a settlement took effect, on a document row. */
  settledOn: string | null;
  contactCode: string;
  contactName: string;
  bankAccountName: string;
  /** How the receipt was taken, with its reference when it has one. */
  method: string;
  /** The receipt's currency when it is not the branch's, so its amounts name it; else null. */
  currency: string | null;
  /** The figure in the amount column, in the receipt's own currency (the total's is in base). */
  amount: number;
  /** On a foreign receipt's row: the same amount in base currency; else null. */
  amountBase: number | null;
  /** On a receipt that had TDS withheld: how much; else null. */
  tds: number | null;
}

/**
 * The grid's rows: for each receipt a heading, the documents it settled, and a row for what it did
 * not settle when that is something; then the total collected, which is the server's and not the
 * loaded rows' sum, so it stays right until the last page is loaded. Nothing is worked out here beyond
 * the layout.
 */
export function collectionRows(result: InvoiceDnPaymentCollectionResult): CollectionGridRow[] {
  const rows: CollectionGridRow[] = [];

  for (const receipt of result.receipts) {
    const id = `RCM:${receipt.receiveMoneyId}`;
    const foreign = isForeignReceipt(receipt, result.baseCurrencyCode);
    const currency = foreign ? receipt.currencyCode : null;

    rows.push({
      kind: 'receipt',
      key: id,
      name: receipt.receiptNo,
      link: `/banking/receive-money/${receipt.receiveMoneyId}`,
      date: receipt.receiptDate,
      settledOn: null,
      contactCode: receipt.contactCode,
      contactName: receipt.contactName,
      bankAccountName: receipt.bankAccountName,
      method: receipt.referenceNo ? `${paymentMethodLabel(receipt.paymentMethod)} (${receipt.referenceNo})` : paymentMethodLabel(receipt.paymentMethod),
      currency,
      amount: receipt.amount,
      amountBase: foreign ? receipt.amountBase : null,
      tds: receipt.tdsAmount > 0 ? receipt.tdsAmount : null,
    });

    for (const document of receipt.documents) {
      rows.push({
        kind: 'document',
        key: `${id}:${document.typeCode}:${document.documentId}`,
        name: document.documentNo,
        link: documentLink(document),
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

    if (receipt.unallocated !== 0) {
      rows.push({
        kind: 'unallocated',
        key: `${id}:u`,
        name: 'Not settled against a document',
        link: null,
        date: null,
        settledOn: null,
        contactCode: '',
        contactName: '',
        bankAccountName: '',
        method: '',
        currency,
        amount: receipt.unallocated,
        amountBase: null,
        tds: null,
      });
    }
  }

  if (result.receipts.length > 0) {
    rows.push({
      kind: 'total',
      key: 't',
      name: result.receipts.length < result.totalReceipts ? `Total collected, all ${result.totalReceipts} receipts` : 'Total collected',
      link: null,
      date: null,
      settledOn: null,
      contactCode: '',
      contactName: '',
      bankAccountName: '',
      method: '',
      currency: null,
      amount: result.totals.collected,
      amountBase: null,
      tds: null,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the totals and customers are the latest answer's. */
export function appendCollectionPage(loaded: InvoiceDnPaymentCollectionResult, next: InvoiceDnPaymentCollectionResult): InvoiceDnPaymentCollectionResult {
  return { ...next, receipts: [...loaded.receipts, ...next.receipts] };
}

/** Whether there are receipts the filters match that are not loaded yet. */
export function hasMoreCollection(result: Pick<InvoiceDnPaymentCollectionResult, 'totalReceipts' | 'receipts'>): boolean {
  return result.receipts.length < result.totalReceipts;
}

/** How a bank account reads in the filter: its name, then its currency when it is not the branch's. */
export function bankAccountChoiceLabel(account: CollectionBankAccountChoice, baseCurrency: string): string {
  const foreign = account.currencyCode !== '' && baseCurrency !== '' && account.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
  return foreign ? `${account.accountName} (${account.currencyCode})` : account.accountName;
}

/**
 * What the period came to, in a sentence: how many receipts, what was collected and how much of it
 * settled nothing. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function collectionSentence(
  result: Pick<InvoiceDnPaymentCollectionResult, 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totals.receipts;
  const what = n === 1 ? '1 receipt' : `${n} receipts`;
  const t = result.totals;

  return t.unallocated !== 0
    ? `${what} collected ${money(t.collected)}, of which ${money(t.unallocated)} was not settled against a document.`
    : `${what} collected ${money(t.collected)}, all settled against documents.`;
}
