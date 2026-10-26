/**
 * The Sales Order Track Report's contract (TK-230), field for field with
 * `SalesOrderTrackReportModels.cs`, and the pure steps the page takes from an answer: each sales order
 * dated in a period with how far it has got, its lines with what was ordered, delivered and invoiced of
 * each, and what is still to deliver or bill. Quantities are in each line's own unit; values are in the
 * branch's base currency, before tax, at the order's own price per unit, so ordered less delivered is
 * what is to deliver and ordered less invoiced what is to bill, on every line. Dates are ISO `yyyy-MM-dd`.
 */

/** How far an order has got, worked out from every line; cancelled is a voided or cancelled order. */
export type SalesOrderProgress = 'Open' | 'PartlyDelivered' | 'Delivered' | 'Invoiced' | 'ClosedShort' | 'Cancelled';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface SalesOrderTrackRequest {
  fromDate: string;
  toDate: string;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Only orders at this progress; every progress but cancelled when blank. */
  progress?: SalesOrderProgress | null;
  /** Only orders with something still to deliver or bill. */
  outstandingOnly?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

/** One line of an order. */
export interface SalesOrderTrackLine {
  lineNumber: number;
  itemId: number | null;
  /** Blank on a line that is not for an item. */
  itemCode: string | null;
  /** The item's name, else the line's own description. */
  itemName: string;
  unitCode: string | null;
  ordered: number;
  delivered: number;
  invoiced: number;
  /** Ordered less delivered. */
  toDeliver: number;
  /** Ordered less invoiced. */
  toBill: number;
  orderedValue: number;
  deliveredValue: number;
  invoicedValue: number;
  toDeliverValue: number;
  toBillValue: number;
}

/** An order with its lines and what has been raised against it. */
export interface SalesOrderTrackOrder {
  salesOrderId: number;
  documentNo: string;
  documentDate: string;
  /** The date the customer was promised it, when there is one. */
  deliveryDate: string | null;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  progress: SalesOrderProgress;
  lines: SalesOrderTrackLine[];
  orderedValue: number;
  deliveredValue: number;
  invoicedValue: number;
  toDeliverValue: number;
  toBillValue: number;
  /** How many posted invoices have been raised from the order. */
  invoicesRaised: number;
  /** What those invoices came to before tax, in base currency, at the prices they were raised at. */
  billed: number;
}

export interface SalesOrderTrackTotals {
  orderedValue: number;
  deliveredValue: number;
  invoicedValue: number;
  toDeliverValue: number;
  toBillValue: number;
}

export interface SalesOrderTrackResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of orders, newest first. */
  orders: SalesOrderTrackOrder[];
  /** How many orders the filters match, all pages. */
  totalOrders: number;
  pageNumber: number;
  pageSize: number;
  /** How many orders are at each progress, none left out; cancelled is counted but is not part of All. */
  progressCounts: Record<SalesOrderProgress, number>;
  /** Added up over every order the filters match, not only this page. */
  totals: SalesOrderTrackTotals;
}

/** The progresses in the order the tabs show them: an order's life, start to end. */
export const SALES_ORDER_PROGRESSES: readonly { value: SalesOrderProgress; label: string }[] = [
  { value: 'Open', label: 'Open' },
  { value: 'PartlyDelivered', label: 'Partly delivered' },
  { value: 'Delivered', label: 'Delivered' },
  { value: 'Invoiced', label: 'Invoiced' },
  { value: 'ClosedShort', label: 'Closed short' },
  { value: 'Cancelled', label: 'Cancelled' },
];

/** How a progress reads on the page. */
export function salesOrderProgressLabel(progress: SalesOrderProgress): string {
  return SALES_ORDER_PROGRESSES.find((p) => p.value === progress)?.label ?? progress;
}

/** The colour of a progress's tag: invoiced reads as done, closed short and cancelled as set aside. */
export function salesOrderProgressTag(progress: SalesOrderProgress): string {
  switch (progress) {
    case 'Invoiced':
      return 'tag-success';
    case 'Delivered':
      return 'tag-info';
    case 'PartlyDelivered':
      return 'tag-warning';
    case 'Open':
      return 'tag-outline';
    default:
      return 'tag-neutral';
  }
}

/** A tab above the grid: a progress ('' is All), its label and how many orders are at it. */
export interface SalesOrderTrackTab {
  value: string;
  label: string;
  count: number;
}

/**
 * The tabs: All, then every progress, a nought included so a tab can say nothing is in it. The counts
 * are the server's, because a page cannot count the pages it was not sent. All is every progress but
 * cancelled, the list a person gets without choosing one.
 */
export function salesOrderTrackTabs(counts: Readonly<Partial<Record<SalesOrderProgress, number>>> | undefined): SalesOrderTrackTab[] {
  const count = (progress: SalesOrderProgress): number => counts?.[progress] ?? 0;
  const all = SALES_ORDER_PROGRESSES.filter((p) => p.value !== 'Cancelled').reduce((sum, p) => sum + count(p.value), 0);

  return [{ value: '', label: 'All', count: all }, ...SALES_ORDER_PROGRESSES.map((p) => ({ ...p, count: count(p.value) }))];
}

/** Whether an order is in a currency other than the branch's, so its own currency is worth naming. */
export function isForeignOrder(order: Pick<SalesOrderTrackOrder, 'currencyCode'>, baseCurrency: string): boolean {
  return order.currencyCode !== '' && baseCurrency !== '' && order.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

export type SalesOrderTrackRowKind = 'order' | 'line' | 'subtotal' | 'total';

/** One row of the grid: an order's heading, one of its lines, its value, or the grand total. */
export interface SalesOrderTrackGridRow {
  kind: SalesOrderTrackRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** An order's number, an item's name, or the label of a value or total row. */
  name: string;
  /** Where an order number opens; null off a heading. */
  link: string | null;
  /** An item's code. */
  code: string | null;
  date: string | null;
  deliveryDate: string | null;
  contactName: string;
  /** The order's currency when it is not the branch's; else null. */
  currency: string | null;
  progress: SalesOrderProgress | null;
  unitCode: string | null;
  ordered: number | null;
  delivered: number | null;
  invoiced: number | null;
  toDeliver: number | null;
  toBill: number | null;
  orderedValue: number | null;
  toDeliverValue: number | null;
  toBillValue: number | null;
  /** How many invoices were raised from the order, and what they came to; on a heading. */
  invoicesRaised: number;
  billed: number;
}

const blank = (kind: SalesOrderTrackRowKind, key: string, name: string): SalesOrderTrackGridRow => ({
  kind,
  key,
  name,
  link: null,
  code: null,
  date: null,
  deliveryDate: null,
  contactName: '',
  currency: null,
  progress: null,
  unitCode: null,
  ordered: null,
  delivered: null,
  invoiced: null,
  toDeliver: null,
  toBill: null,
  orderedValue: null,
  toDeliverValue: null,
  toBillValue: null,
  invoicesRaised: 0,
  billed: 0,
});

/**
 * The grid's rows: for each order a heading, its lines and its value; then the total of every order the
 * filters match, which is the server's and not the loaded orders' sum, so it stays right until the last
 * page is loaded. Nothing is worked out here beyond the layout.
 */
export function salesOrderTrackRows(result: SalesOrderTrackResult): SalesOrderTrackGridRow[] {
  const rows: SalesOrderTrackGridRow[] = [];

  for (const order of result.orders) {
    const id = `o:${order.salesOrderId}`;

    rows.push({
      ...blank('order', id, order.documentNo),
      link: `/sales/sales-orders/${order.salesOrderId}`,
      date: order.documentDate,
      deliveryDate: order.deliveryDate,
      contactName: order.contactName,
      currency: isForeignOrder(order, result.baseCurrencyCode) ? order.currencyCode : null,
      progress: order.progress,
      invoicesRaised: order.invoicesRaised,
      billed: order.billed,
    });

    for (const line of order.lines) {
      rows.push({
        ...blank('line', `${id}:${line.lineNumber}`, line.itemName),
        code: line.itemCode,
        unitCode: line.unitCode,
        ordered: line.ordered,
        delivered: line.delivered,
        invoiced: line.invoiced,
        toDeliver: line.toDeliver,
        toBill: line.toBill,
        orderedValue: line.orderedValue,
        toDeliverValue: line.toDeliverValue,
        toBillValue: line.toBillValue,
      });
    }

    rows.push({
      ...blank('subtotal', `${id}:v`, 'Order value'),
      orderedValue: order.orderedValue,
      toDeliverValue: order.toDeliverValue,
      toBillValue: order.toBillValue,
    });
  }

  if (result.orders.length > 0) {
    const t = result.totals;
    rows.push({
      ...blank('total', 't', result.orders.length < result.totalOrders ? `Total of all ${result.totalOrders} orders` : 'Total'),
      orderedValue: t.orderedValue,
      toDeliverValue: t.toDeliverValue,
      toBillValue: t.toBillValue,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendSalesOrderTrackPage(loaded: SalesOrderTrackResult, next: SalesOrderTrackResult): SalesOrderTrackResult {
  return { ...next, orders: [...loaded.orders, ...next.orders] };
}

/** Whether there are orders the filters match that are not loaded yet. */
export function hasMoreSalesOrderTrack(result: Pick<SalesOrderTrackResult, 'totalOrders' | 'orders'>): boolean {
  return result.orders.length < result.totalOrders;
}

/**
 * What the period came to, in a sentence: how many orders, what they were for, and how much of it is
 * still to deliver and to bill. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function salesOrderTrackSentence(
  result: Pick<SalesOrderTrackResult, 'totalOrders' | 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totalOrders;
  const what = n === 1 ? '1 order' : `${n} orders`;
  const t = result.totals;

  return `${what} for ${money(t.orderedValue)}: ${money(t.toDeliverValue)} still to deliver and ${money(t.toBillValue)} still to bill.`;
}
