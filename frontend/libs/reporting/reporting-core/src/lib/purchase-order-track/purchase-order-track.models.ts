/**
 * The Purchase Order Track Report's contract (TK-240), field for field with
 * `PurchaseOrderTrackReportModels.cs`, and the pure steps the page takes from an answer: each purchase order
 * dated in a period with how far it has got, its lines with what was ordered, received and billed of
 * each, and what is still to receive or bill. Quantities are in each line's own unit; values are in the
 * branch's base currency, before tax, at the order's own price per unit, so ordered less received is
 * what is to receive and ordered less billed what is to bill, on every line. Dates are ISO `yyyy-MM-dd`.
 */

/** How far an order has got, worked out from every line; cancelled is a voided or cancelled order. */
export type PurchaseOrderProgress = 'Open' | 'PartlyReceived' | 'Received' | 'Billed' | 'ClosedShort' | 'Cancelled';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface PurchaseOrderTrackRequest {
  fromDate: string;
  toDate: string;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
  /** Only orders at this progress; every progress but cancelled when blank. */
  progress?: PurchaseOrderProgress | null;
  /** Only orders with something still to receive or bill. */
  outstandingOnly?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

/** One line of an order. */
export interface PurchaseOrderTrackLine {
  lineNumber: number;
  itemId: number | null;
  /** Blank on a line that is not for an item. */
  itemCode: string | null;
  /** The item's name, else the line's own description. */
  itemName: string;
  unitCode: string | null;
  ordered: number;
  received: number;
  billed: number;
  /** Ordered less received. */
  toReceive: number;
  /** Ordered less billed. */
  toBill: number;
  orderedValue: number;
  receivedValue: number;
  billedValue: number;
  toReceiveValue: number;
  toBillValue: number;
}

/** An order with its lines and what has been raised against it. */
export interface PurchaseOrderTrackOrder {
  purchaseOrderId: number;
  documentNo: string;
  documentDate: string;
  /** The date the vendor was expected it, when there is one. */
  expectedDate: string | null;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  progress: PurchaseOrderProgress;
  lines: PurchaseOrderTrackLine[];
  orderedValue: number;
  receivedValue: number;
  billedValue: number;
  toReceiveValue: number;
  toBillValue: number;
  /** How many posted bills have been raised from the order. */
  billsRaised: number;
  /** What those bills came to before tax, in base currency, at the prices they were raised at. */
  billTotal: number;
}

export interface PurchaseOrderTrackTotals {
  orderedValue: number;
  receivedValue: number;
  billedValue: number;
  toReceiveValue: number;
  toBillValue: number;
}

export interface PurchaseOrderTrackResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of orders, newest first. */
  orders: PurchaseOrderTrackOrder[];
  /** How many orders the filters match, all pages. */
  totalOrders: number;
  pageNumber: number;
  pageSize: number;
  /** How many orders are at each progress, none left out; cancelled is counted but is not part of All. */
  progressCounts: Record<PurchaseOrderProgress, number>;
  /** Added up over every order the filters match, not only this page. */
  totals: PurchaseOrderTrackTotals;
}

/** The progresses in the order the tabs show them: an order's life, start to end. */
export const PURCHASE_ORDER_PROGRESSES: readonly { value: PurchaseOrderProgress; label: string }[] = [
  { value: 'Open', label: 'Open' },
  { value: 'PartlyReceived', label: 'Partly received' },
  { value: 'Received', label: 'Received' },
  { value: 'Billed', label: 'Billed' },
  { value: 'ClosedShort', label: 'Closed short' },
  { value: 'Cancelled', label: 'Cancelled' },
];

/** How a progress reads on the page. */
export function purchaseOrderProgressLabel(progress: PurchaseOrderProgress): string {
  return PURCHASE_ORDER_PROGRESSES.find((p) => p.value === progress)?.label ?? progress;
}

/** The colour of a progress's tag: billed reads as done, closed short and cancelled as set aside. */
export function purchaseOrderProgressTag(progress: PurchaseOrderProgress): string {
  switch (progress) {
    case 'Billed':
      return 'tag-success';
    case 'Received':
      return 'tag-info';
    case 'PartlyReceived':
      return 'tag-warning';
    case 'Open':
      return 'tag-outline';
    default:
      return 'tag-neutral';
  }
}

/** A tab above the grid: a progress ('' is All), its label and how many orders are at it. */
export interface PurchaseOrderTrackTab {
  value: string;
  label: string;
  count: number;
}

/**
 * The tabs: All, then every progress, a nought included so a tab can say nothing is in it. The counts
 * are the server's, because a page cannot count the pages it was not sent. All is every progress but
 * cancelled, the list a person gets without choosing one.
 */
export function purchaseOrderTrackTabs(counts: Readonly<Partial<Record<PurchaseOrderProgress, number>>> | undefined): PurchaseOrderTrackTab[] {
  const count = (progress: PurchaseOrderProgress): number => counts?.[progress] ?? 0;
  const all = PURCHASE_ORDER_PROGRESSES.filter((p) => p.value !== 'Cancelled').reduce((sum, p) => sum + count(p.value), 0);

  return [{ value: '', label: 'All', count: all }, ...PURCHASE_ORDER_PROGRESSES.map((p) => ({ ...p, count: count(p.value) }))];
}

/** Whether an order is in a currency other than the branch's, so its own currency is worth naming. */
export function isForeignPurchaseOrder(order: Pick<PurchaseOrderTrackOrder, 'currencyCode'>, baseCurrency: string): boolean {
  return order.currencyCode !== '' && baseCurrency !== '' && order.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

export type PurchaseOrderTrackRowKind = 'order' | 'line' | 'subtotal' | 'total';

/** One row of the grid: an order's heading, one of its lines, its value, or the grand total. */
export interface PurchaseOrderTrackGridRow {
  kind: PurchaseOrderTrackRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** An order's number, an item's name, or the label of a value or total row. */
  name: string;
  /** Where an order number opens; null off a heading. */
  link: string | null;
  /** An item's code. */
  code: string | null;
  date: string | null;
  expectedDate: string | null;
  contactName: string;
  /** The order's currency when it is not the branch's; else null. */
  currency: string | null;
  progress: PurchaseOrderProgress | null;
  unitCode: string | null;
  ordered: number | null;
  received: number | null;
  billed: number | null;
  toReceive: number | null;
  toBill: number | null;
  orderedValue: number | null;
  toReceiveValue: number | null;
  toBillValue: number | null;
  /** How many bills were raised from the order, and what they came to; on a heading. */
  billsRaised: number;
  billTotal: number;
}

const blank = (kind: PurchaseOrderTrackRowKind, key: string, name: string): PurchaseOrderTrackGridRow => ({
  kind,
  key,
  name,
  link: null,
  code: null,
  date: null,
  expectedDate: null,
  contactName: '',
  currency: null,
  progress: null,
  unitCode: null,
  ordered: null,
  received: null,
  billed: null,
  toReceive: null,
  toBill: null,
  orderedValue: null,
  toReceiveValue: null,
  toBillValue: null,
  billsRaised: 0,
  billTotal: 0,
});

/**
 * The grid's rows: for each order a heading, its lines and its value; then the total of every order the
 * filters match, which is the server's and not the loaded orders' sum, so it stays right until the last
 * page is loaded. Nothing is worked out here beyond the layout.
 */
export function purchaseOrderTrackRows(result: PurchaseOrderTrackResult): PurchaseOrderTrackGridRow[] {
  const rows: PurchaseOrderTrackGridRow[] = [];

  for (const order of result.orders) {
    const id = `o:${order.purchaseOrderId}`;

    rows.push({
      ...blank('order', id, order.documentNo),
      link: `/purchase/purchase-orders/${order.purchaseOrderId}`,
      date: order.documentDate,
      expectedDate: order.expectedDate,
      contactName: order.contactName,
      currency: isForeignPurchaseOrder(order, result.baseCurrencyCode) ? order.currencyCode : null,
      progress: order.progress,
      billsRaised: order.billsRaised,
      billTotal: order.billTotal,
    });

    for (const line of order.lines) {
      rows.push({
        ...blank('line', `${id}:${line.lineNumber}`, line.itemName),
        code: line.itemCode,
        unitCode: line.unitCode,
        ordered: line.ordered,
        received: line.received,
        billed: line.billed,
        toReceive: line.toReceive,
        toBill: line.toBill,
        orderedValue: line.orderedValue,
        toReceiveValue: line.toReceiveValue,
        toBillValue: line.toBillValue,
      });
    }

    rows.push({
      ...blank('subtotal', `${id}:v`, 'Order value'),
      orderedValue: order.orderedValue,
      toReceiveValue: order.toReceiveValue,
      toBillValue: order.toBillValue,
    });
  }

  if (result.orders.length > 0) {
    const t = result.totals;
    rows.push({
      ...blank('total', 't', result.orders.length < result.totalOrders ? `Total of all ${result.totalOrders} orders` : 'Total'),
      orderedValue: t.orderedValue,
      toReceiveValue: t.toReceiveValue,
      toBillValue: t.toBillValue,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendPurchaseOrderTrackPage(loaded: PurchaseOrderTrackResult, next: PurchaseOrderTrackResult): PurchaseOrderTrackResult {
  return { ...next, orders: [...loaded.orders, ...next.orders] };
}

/** Whether there are orders the filters match that are not loaded yet. */
export function hasMorePurchaseOrderTrack(result: Pick<PurchaseOrderTrackResult, 'totalOrders' | 'orders'>): boolean {
  return result.orders.length < result.totalOrders;
}

/**
 * What the period came to, in a sentence: how many orders, what they were for, and how much of it is
 * still to receive and to bill. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function purchaseOrderTrackSentence(
  result: Pick<PurchaseOrderTrackResult, 'totalOrders' | 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totalOrders;
  const what = n === 1 ? '1 order' : `${n} orders`;
  const t = result.totals;

  return `${what} for ${money(t.orderedValue)}: ${money(t.toReceiveValue)} still to receive and ${money(t.toBillValue)} still to bill.`;
}
