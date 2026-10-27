/**
 * The Purchase Receive Order Details' contract (TK-241), field for field with
 * `PurchaseReceiveOrderDetailsReportModels.cs`, and the pure steps the page takes from an answer: every
 * posted goods receipt dated in a period, newest first, with its lines: what was delivered, rejected,
 * accepted and billed, what a unit cost and what the accepted goods are worth, into which warehouse, and
 * the batch or serial numbers the receipt created. A line is in its receipt's own currency; the totals of
 * every receipt the filters match are in the branch's base currency. Dates are ISO `yyyy-MM-dd`.
 */

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface PurchaseReceiveOrderDetailsRequest {
  fromDate: string;
  toDate: string;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
  /** Only receipts with a line for this item, and only those lines. */
  itemId?: number | null;
  /** Only receipts with a line taken into this warehouse, and only those lines. */
  warehouseId?: number | null;
  /** Only receipts against this purchase order. */
  purchaseOrderId?: number | null;
  pageNumber?: number;
  pageSize?: number;
}

/** One line of a receipt, in the receipt's own currency. */
export interface PurchaseReceiveOrderDetailsLine {
  goodsReceiptDetailId: number;
  lineNumber: number;
  itemId: number | null;
  /** Blank on a line that is not for an item. */
  itemCode: string | null;
  /** The item's name, else the line's own description. */
  itemName: string;
  unitCode: string | null;
  warehouseId: number | null;
  warehouseName: string | null;
  /** What the vendor delivered. */
  delivered: number;
  /** What was refused at the door: never taken into stock and not owed for. */
  rejected: number;
  /** What was accepted and went into stock. */
  accepted: number;
  /** What posted bills have billed against the line. */
  billed: number;
  /** What one delivered unit cost, net of discount and before tax. */
  unitCost: number;
  /** The accepted quantity at that cost: what the receipt debits to Inventory. */
  value: number;
  /** The value in the branch's base currency, at the receipt's rate. */
  valueBase: number;
  rejectionReason: string | null;
  batchNumber: string | null;
  batchExpiryDate: string | null;
  /** The serial numbers the receipt created for the line: the first of them when there are very many. */
  serials: string[];
  /** How many serial numbers the receipt created for the line. */
  serialCount: number;
}

/** A receipt with its lines. */
export interface PurchaseReceiveOrderDetailsReceipt {
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
  vendorDeliveryNoteDate: string | null;
  lines: PurchaseReceiveOrderDetailsLine[];
  /** The lines shown added up, in the receipt's own currency. */
  value: number;
  /** The same in the branch's base currency. */
  valueBase: number;
}

export interface PurchaseReceiveOrderDetailsTotals {
  /** How many lines the matching receipts have: the narrowed ones when an item or warehouse is chosen. */
  lines: number;
  value: number;
}

export interface PurchaseReceiveOrderDetailsResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** An item or a warehouse narrows the lines, so a receipt's value is of the lines shown. */
  linesFiltered: boolean;
  /** This page of receipts, newest first. */
  receipts: PurchaseReceiveOrderDetailsReceipt[];
  /** How many receipts the filters match, all pages. */
  totalReceipts: number;
  pageNumber: number;
  pageSize: number;
  /** Added up over every receipt the filters match, not only this page. */
  totals: PurchaseReceiveOrderDetailsTotals;
}

/** A purchase order the filter can pick: one a posted goods receipt was raised against. */
export interface PurchaseReceiveOrderDetailsOrderChoice {
  purchaseOrderId: number;
  documentNo: string;
  documentDate: string;
  contactName: string;
}

/** How a purchase order reads in the filter: its number, then its vendor. */
export function receiveOrderChoiceLabel(order: Pick<PurchaseReceiveOrderDetailsOrderChoice, 'documentNo' | 'contactName'>): string {
  return order.contactName ? `${order.documentNo} · ${order.contactName}` : order.documentNo;
}

/** Whether a receipt is in a currency other than the branch's, so its own currency is worth naming. */
export function isForeignGoodsReceipt(receipt: Pick<PurchaseReceiveOrderDetailsReceipt, 'currencyCode'>, baseCurrency: string): boolean {
  return receipt.currencyCode !== '' && baseCurrency !== '' && receipt.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

export type PurchaseReceiveOrderDetailsRowKind = 'heading' | 'line' | 'subtotal' | 'total';

/** One row of the grid: a receipt's heading, one of its lines, its value, or the grand total. */
export interface PurchaseReceiveOrderDetailsGridRow {
  kind: PurchaseReceiveOrderDetailsRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** A receipt's number, an item's name, or the label of a value or total row. */
  name: string;
  /** Where a receipt number opens; null off a heading. */
  link: string | null;
  /** An item's code. */
  code: string | null;
  date: string | null;
  contactCode: string;
  contactName: string;
  /** The receipt's currency when it is not the branch's; else null. */
  currency: string | null;
  /** The purchase order the receipt was raised against, and where it opens. */
  orderNo: string | null;
  orderLink: string | null;
  vendorDeliveryNoteNo: string | null;
  vendorDeliveryNoteDate: string | null;
  warehouse: string | null;
  unitCode: string | null;
  delivered: number | null;
  rejected: number | null;
  accepted: number | null;
  billed: number | null;
  unitCost: number | null;
  value: number | null;
  /** The value in base currency, given on a receipt in another currency; else null. */
  valueBase: number | null;
  rejectionReason: string | null;
  batchNumber: string | null;
  batchExpiryDate: string | null;
  serials: string[];
  serialCount: number;
}

const blank = (kind: PurchaseReceiveOrderDetailsRowKind, key: string, name: string): PurchaseReceiveOrderDetailsGridRow => ({
  kind,
  key,
  name,
  link: null,
  code: null,
  date: null,
  contactCode: '',
  contactName: '',
  currency: null,
  orderNo: null,
  orderLink: null,
  vendorDeliveryNoteNo: null,
  vendorDeliveryNoteDate: null,
  warehouse: null,
  unitCode: null,
  delivered: null,
  rejected: null,
  accepted: null,
  billed: null,
  unitCost: null,
  value: null,
  valueBase: null,
  rejectionReason: null,
  batchNumber: null,
  batchExpiryDate: null,
  serials: [],
  serialCount: 0,
});

/**
 * The grid's rows: for each receipt a heading, its lines and its value (of the lines shown); then the
 * total of every receipt the filters match, which is the server's and not the loaded receipts' sum, so it
 * stays right until the last page is loaded. Nothing is worked out here beyond the layout.
 */
export function purchaseReceiveOrderDetailsRows(result: PurchaseReceiveOrderDetailsResult): PurchaseReceiveOrderDetailsGridRow[] {
  const rows: PurchaseReceiveOrderDetailsGridRow[] = [];

  for (const receipt of result.receipts) {
    const id = `r:${receipt.goodsReceiptId}`;
    const foreign = isForeignGoodsReceipt(receipt, result.baseCurrencyCode);
    const currency = foreign ? receipt.currencyCode : null;

    rows.push({
      ...blank('heading', id, receipt.documentNo),
      link: `/purchase/goods-receipts/${receipt.goodsReceiptId}`,
      date: receipt.documentDate,
      contactCode: receipt.contactCode,
      contactName: receipt.contactName,
      currency,
      orderNo: receipt.purchaseOrderNo,
      orderLink: receipt.purchaseOrderId ? `/purchase/purchase-orders/${receipt.purchaseOrderId}` : null,
      vendorDeliveryNoteNo: receipt.vendorDeliveryNoteNo,
      vendorDeliveryNoteDate: receipt.vendorDeliveryNoteDate,
    });

    for (const line of receipt.lines) {
      rows.push({
        ...blank('line', `${id}:${line.lineNumber}`, line.itemName),
        code: line.itemCode,
        currency,
        warehouse: line.warehouseName,
        unitCode: line.unitCode,
        delivered: line.delivered,
        rejected: line.rejected,
        accepted: line.accepted,
        billed: line.billed,
        unitCost: line.unitCost,
        value: line.value,
        valueBase: foreign ? line.valueBase : null,
        rejectionReason: line.rejectionReason,
        batchNumber: line.batchNumber,
        batchExpiryDate: line.batchExpiryDate,
        serials: line.serials,
        serialCount: line.serialCount,
      });
    }

    rows.push({
      ...blank('subtotal', `${id}:v`, 'Receipt value'),
      currency,
      value: receipt.value,
      valueBase: foreign ? receipt.valueBase : null,
    });
  }

  if (result.receipts.length > 0) {
    rows.push({
      ...blank('total', 't', result.receipts.length < result.totalReceipts ? `Total of all ${result.totalReceipts} receipts` : 'Total'),
      value: result.totals.value,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the totals are the latest answer's. */
export function appendPurchaseReceiveOrderDetailsPage(
  loaded: PurchaseReceiveOrderDetailsResult,
  next: PurchaseReceiveOrderDetailsResult,
): PurchaseReceiveOrderDetailsResult {
  return { ...next, receipts: [...loaded.receipts, ...next.receipts] };
}

/** Whether there are receipts the filters match that are not loaded yet. */
export function hasMorePurchaseReceiveOrderDetails(result: Pick<PurchaseReceiveOrderDetailsResult, 'totalReceipts' | 'receipts'>): boolean {
  return result.receipts.length < result.totalReceipts;
}

/** How many serial numbers a line has beyond the ones it lists, when it has very many. */
export function serialsNotListed(line: Pick<PurchaseReceiveOrderDetailsGridRow, 'serials' | 'serialCount'>): number {
  return Math.max(0, line.serialCount - line.serials.length);
}

/**
 * What the period came to, in a sentence: how many receipts and lines, and what the accepted goods are
 * worth. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function purchaseReceiveOrderDetailsSentence(
  result: Pick<PurchaseReceiveOrderDetailsResult, 'totalReceipts' | 'totals'>,
  money: (value: number) => string,
): string {
  const receipts = result.totalReceipts === 1 ? '1 receipt' : `${result.totalReceipts} receipts`;
  const lines = result.totals.lines === 1 ? '1 line' : `${result.totals.lines} lines`;

  return `${receipts} with ${lines}: the goods accepted into stock are worth ${money(result.totals.value)}.`;
}
