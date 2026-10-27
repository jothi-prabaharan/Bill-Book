/**
 * The Receive Order Track Report's contract (TK-242), field for field with
 * `ReceiveOrderTrackReportModels.cs`, and the pure steps the page takes from an answer: each posted goods
 * receipt dated in a period with what its accepted goods are worth, how much of that posted bills have
 * billed, what is still to bill and its status. Values are in the branch's base currency, at the cost the
 * receipt itself stocked the goods at, so received less billed is what is to bill, on every receipt; a
 * receipt in another currency also carries its own value. Dates are ISO `yyyy-MM-dd`.
 */

import { isForeignGoodsReceipt } from '../purchase-receive-order-details/purchase-receive-order-details.models';

/** Where a receipt stands with its bills, worked out from every line with goods accepted. */
export type ReceiveOrderTrackStatus = 'NotBilled' | 'PartlyBilled' | 'Billed' | 'NothingToBill';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface ReceiveOrderTrackRequest {
  fromDate: string;
  toDate: string;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
  /** Only receipts with this status; every status when blank. */
  status?: ReceiveOrderTrackStatus | null;
  /** Only receipts with something still to bill. */
  notYetBilledOnly?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

/** A receipt with its value and what has been billed of it. */
export interface ReceiveOrderTrackReceipt {
  goodsReceiptId: number;
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  /** The purchase order the receipt was raised against; null for goods that came with none. */
  purchaseOrderId: number | null;
  purchaseOrderNo: string | null;
  /** The vendor's own delivery note. */
  vendorDeliveryNoteNo: string | null;
  status: ReceiveOrderTrackStatus;
  /** What the accepted goods are worth, in the receipt's own currency. */
  valueInCurrency: number;
  value: number;
  /** The share of the value that posted bills name. */
  billedValue: number;
  /** Value less billed value. */
  toBillValue: number;
}

export interface ReceiveOrderTrackTotals {
  value: number;
  billedValue: number;
  toBillValue: number;
}

export interface ReceiveOrderTrackResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of receipts, newest first. */
  receipts: ReceiveOrderTrackReceipt[];
  /** How many receipts the filters match, all pages. */
  totalReceipts: number;
  pageNumber: number;
  pageSize: number;
  /** How many receipts have each status, none left out. */
  statusCounts: Record<ReceiveOrderTrackStatus, number>;
  /** Added up over every receipt the filters match, not only this page. */
  totals: ReceiveOrderTrackTotals;
}

/** The statuses in the order the tabs show them: from nothing billed to nothing owed. */
export const RECEIVE_ORDER_STATUSES: readonly { value: ReceiveOrderTrackStatus; label: string }[] = [
  { value: 'NotBilled', label: 'To bill' },
  { value: 'PartlyBilled', label: 'Partly billed' },
  { value: 'Billed', label: 'Billed' },
  { value: 'NothingToBill', label: 'Nothing to bill' },
];

/** How a status reads on the page. */
export function receiveOrderStatusLabel(status: ReceiveOrderTrackStatus): string {
  return RECEIVE_ORDER_STATUSES.find((s) => s.value === status)?.label ?? status;
}

/** The colour of a status's tag: billed reads as done, partly billed as in progress, nothing to bill as set aside. */
export function receiveOrderStatusTag(status: ReceiveOrderTrackStatus): string {
  switch (status) {
    case 'Billed':
      return 'tag-success';
    case 'PartlyBilled':
      return 'tag-warning';
    case 'NotBilled':
      return 'tag-info';
    default:
      return 'tag-neutral';
  }
}

/** A tab above the grid: a status ('' is All), its label and how many receipts have it. */
export interface ReceiveOrderTrackTab {
  value: string;
  label: string;
  count: number;
}

/**
 * The tabs: All, then every status, a nought included so a tab can say nothing is in it. The counts are the
 * server's, because a page cannot count the pages it was not sent.
 */
export function receiveOrderTrackTabs(counts: Readonly<Partial<Record<ReceiveOrderTrackStatus, number>>> | undefined): ReceiveOrderTrackTab[] {
  const count = (status: ReceiveOrderTrackStatus): number => counts?.[status] ?? 0;
  const all = RECEIVE_ORDER_STATUSES.reduce((sum, s) => sum + count(s.value), 0);

  return [{ value: '', label: 'All', count: all }, ...RECEIVE_ORDER_STATUSES.map((s) => ({ ...s, count: count(s.value) }))];
}

export type ReceiveOrderTrackRowKind = 'receipt' | 'total';

/** One row of the grid: a receipt, or the grand total. */
export interface ReceiveOrderTrackGridRow {
  kind: ReceiveOrderTrackRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** A receipt's number, or the label of the total. */
  name: string;
  /** Where a receipt number opens; null on the total. */
  link: string | null;
  date: string | null;
  contactCode: string;
  contactName: string;
  /** The purchase order the receipt was raised against, and where it opens. */
  orderNo: string | null;
  orderLink: string | null;
  vendorDeliveryNoteNo: string | null;
  status: ReceiveOrderTrackStatus | null;
  /** The receipt's currency and its value in it when it is not the branch's; else null. */
  currency: string | null;
  ownValue: number | null;
  value: number;
  billedValue: number;
  toBillValue: number;
}

/**
 * The grid's rows: a row for each receipt, then the total of every receipt the filters match, which is the
 * server's and not the loaded receipts' sum, so it stays right until the last page is loaded. Nothing is
 * worked out here beyond the layout.
 */
export function receiveOrderTrackRows(result: ReceiveOrderTrackResult): ReceiveOrderTrackGridRow[] {
  const rows: ReceiveOrderTrackGridRow[] = result.receipts.map((receipt) => {
    const foreign = isForeignGoodsReceipt(receipt, result.baseCurrencyCode);

    return {
      kind: 'receipt',
      key: `r:${receipt.goodsReceiptId}`,
      name: receipt.documentNo,
      link: `/purchase/goods-receipts/${receipt.goodsReceiptId}`,
      date: receipt.documentDate,
      contactCode: receipt.contactCode,
      contactName: receipt.contactName,
      orderNo: receipt.purchaseOrderNo,
      orderLink: receipt.purchaseOrderId ? `/purchase/purchase-orders/${receipt.purchaseOrderId}` : null,
      vendorDeliveryNoteNo: receipt.vendorDeliveryNoteNo,
      status: receipt.status,
      currency: foreign ? receipt.currencyCode : null,
      ownValue: foreign ? receipt.valueInCurrency : null,
      value: receipt.value,
      billedValue: receipt.billedValue,
      toBillValue: receipt.toBillValue,
    };
  });

  if (result.receipts.length > 0) {
    rows.push({
      kind: 'total',
      key: 't',
      name: result.receipts.length < result.totalReceipts ? `Total of all ${result.totalReceipts} receipts` : 'Total',
      link: null,
      date: null,
      contactCode: '',
      contactName: '',
      orderNo: null,
      orderLink: null,
      vendorDeliveryNoteNo: null,
      status: null,
      currency: null,
      ownValue: null,
      value: result.totals.value,
      billedValue: result.totals.billedValue,
      toBillValue: result.totals.toBillValue,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendReceiveOrderTrackPage(loaded: ReceiveOrderTrackResult, next: ReceiveOrderTrackResult): ReceiveOrderTrackResult {
  return { ...next, receipts: [...loaded.receipts, ...next.receipts] };
}

/** Whether there are receipts the filters match that are not loaded yet. */
export function hasMoreReceiveOrderTrack(result: Pick<ReceiveOrderTrackResult, 'totalReceipts' | 'receipts'>): boolean {
  return result.receipts.length < result.totalReceipts;
}

/**
 * What the period came to, in a sentence: how many receipts, what they were worth and how much of it is
 * still to bill. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function receiveOrderTrackSentence(
  result: Pick<ReceiveOrderTrackResult, 'totalReceipts' | 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totalReceipts;
  const what = n === 1 ? '1 receipt' : `${n} receipts`;

  return `${what} for ${money(result.totals.value)}: ${money(result.totals.billedValue)} billed and ${money(result.totals.toBillValue)} still to bill.`;
}
