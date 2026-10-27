import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  PURCHASE_ORDER_PROGRESSES,
  PurchaseOrderProgress,
  PurchaseOrderTrackLine,
  PurchaseOrderTrackOrder,
  PurchaseOrderTrackResult,
  appendPurchaseOrderTrackPage,
  hasMorePurchaseOrderTrack,
  isForeignPurchaseOrder,
  purchaseOrderProgressLabel,
  purchaseOrderProgressTag,
  purchaseOrderTrackRows,
  purchaseOrderTrackSentence,
  purchaseOrderTrackTabs,
} from './purchase-order-track.models';
import { PurchaseOrderTrackService } from './purchase-order-track.service';

/** The Purchase Order Track Report's calls and the helpers its page uses (TK-240). */

function setup(answer: unknown = { orders: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [PurchaseOrderTrackService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(PurchaseOrderTrackService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const line = (over: Partial<PurchaseOrderTrackLine> = {}): PurchaseOrderTrackLine => ({
  lineNumber: 1,
  itemId: 9601,
  itemCode: 'WIDGET-A',
  itemName: 'Widget A',
  unitCode: 'PCS',
  ordered: 10,
  received: 4,
  billed: 0,
  toReceive: 6,
  toBill: 10,
  orderedValue: 6000,
  receivedValue: 2400,
  billedValue: 0,
  toReceiveValue: 3600,
  toBillValue: 6000,
  ...over,
});

const order = (over: Partial<PurchaseOrderTrackOrder> = {}): PurchaseOrderTrackOrder => ({
  purchaseOrderId: 9502,
  documentNo: 'POR-9502',
  documentDate: '2026-10-08',
  expectedDate: null,
  contactId: 9103,
  contactCode: 'ZENITH',
  contactName: 'Zenith Components',
  currencyCode: 'INR',
  progress: 'PartlyReceived',
  lines: [line()],
  orderedValue: 6000,
  receivedValue: 2400,
  billedValue: 0,
  toReceiveValue: 3600,
  toBillValue: 6000,
  billsRaised: 0,
  billTotal: 0,
  ...over,
});

const result = (orders: PurchaseOrderTrackOrder[], over: Partial<PurchaseOrderTrackResult> = {}): PurchaseOrderTrackResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  orders,
  totalOrders: orders.length,
  pageNumber: 1,
  pageSize: 25,
  progressCounts: { Open: 0, PartlyReceived: orders.length, Received: 0, Billed: 0, ClosedShort: 0, Cancelled: 0 },
  totals: {
    orderedValue: orders.reduce((t, o) => t + o.orderedValue, 0),
    receivedValue: orders.reduce((t, o) => t + o.receivedValue, 0),
    billedValue: orders.reduce((t, o) => t + o.billedValue, 0),
    toReceiveValue: orders.reduce((t, o) => t + o.toReceiveValue, 0),
    toBillValue: orders.reduce((t, o) => t + o.toBillValue, 0),
  },
  ...over,
});

describe('PurchaseOrderTrackService', () => {
  it('PO-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/purchase-order-track-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'progress', 'outstandingOnly', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('PO-02: sends the vendor, progress, outstanding tick and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: 9103, progress: 'Received', outstandingOnly: true, pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('9103');
    expect(params.get('progress')).toBe('Received');
    expect(params.get('outstandingOnly')).toBe('true');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('PO-03: leaves the outstanding tick out of the request unless it is on', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', outstandingOnly: false });

    expect(paramsOf(get.mock.calls[0]).has('outstandingOnly')).toBe(false);
  });

  it('PO-04: gives back what the endpoint answered', async () => {
    const answer = result([order()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('PO-05: reads the vendors the filter offers from the report’s own lookup', async () => {
    const vendors = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Supplies' }];
    const { service, get } = setup(vendors);

    expect(await service.vendors()).toEqual(vendors);
    expect(get.mock.calls[0][0]).toBe('/api/statements/purchase-order-track-report/vendors');
  });
});

describe('purchase order track helpers', () => {
  it('PO-06: the progresses run along an order’s life and read as words', () => {
    expect(PURCHASE_ORDER_PROGRESSES.map((p) => p.value)).toEqual(['Open', 'PartlyReceived', 'Received', 'Billed', 'ClosedShort', 'Cancelled']);
    expect(purchaseOrderProgressLabel('PartlyReceived')).toBe('Partly received');
    expect(purchaseOrderProgressLabel('ClosedShort')).toBe('Closed short');
  });

  it('PO-07: billed reads as done, closed short and cancelled as set aside', () => {
    expect(purchaseOrderProgressTag('Billed')).toBe('tag-success');
    expect(purchaseOrderProgressTag('Received')).toBe('tag-info');
    expect(purchaseOrderProgressTag('PartlyReceived')).toBe('tag-warning');
    expect(purchaseOrderProgressTag('Open')).toBe('tag-outline');
    expect(purchaseOrderProgressTag('ClosedShort')).toBe('tag-neutral');
    expect(purchaseOrderProgressTag('Cancelled')).toBe('tag-neutral');
  });

  it('PO-08: the tabs are All and then every progress, with a nought kept', () => {
    const tabs = purchaseOrderTrackTabs({ Open: 1, PartlyReceived: 1, Received: 1, Billed: 2, ClosedShort: 1, Cancelled: 1 });

    expect(tabs.map((t) => t.label)).toEqual(['All', 'Open', 'Partly received', 'Received', 'Billed', 'Closed short', 'Cancelled']);
    expect(tabs.map((t) => t.value)).toEqual(['', 'Open', 'PartlyReceived', 'Received', 'Billed', 'ClosedShort', 'Cancelled']);
    expect(tabs.map((t) => t.count)).toEqual([6, 1, 1, 1, 2, 1, 1]);
  });

  it('PO-09: All leaves the cancelled ones out, as the list does without a progress', () => {
    const counts: Record<PurchaseOrderProgress, number> = { Open: 1, PartlyReceived: 0, Received: 0, Billed: 3, ClosedShort: 0, Cancelled: 4 };

    expect(purchaseOrderTrackTabs(counts)[0].count).toBe(4);
  });

  it('PO-10: with no counts yet every tab reads nought', () => {
    expect(purchaseOrderTrackTabs(undefined).map((t) => t.count)).toEqual([0, 0, 0, 0, 0, 0, 0]);
  });

  it('PO-11: an order is foreign when its currency is not the branch’s, whatever the case', () => {
    expect(isForeignPurchaseOrder({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignPurchaseOrder({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignPurchaseOrder({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('PO-12: an order is a heading, its lines and its value, and then the total closes the grid', () => {
    const rows = purchaseOrderTrackRows(result([order({ lines: [line(), line({ lineNumber: 2, itemName: 'Gadget B', itemCode: 'GADGET-B' })] })]));

    expect(rows.map((r) => r.kind)).toEqual(['order', 'line', 'line', 'subtotal', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(5);
    expect(rows[0]).toMatchObject({ name: 'POR-9502', link: '/purchase/purchase-orders/9502', progress: 'PartlyReceived', contactName: 'Zenith Components' });
    expect(rows[3]).toMatchObject({ name: 'Order value', orderedValue: 6000, toReceiveValue: 3600, toBillValue: 6000 });
    expect(rows[4].name).toBe('Total');
  });

  it('PO-13: a line carries its item, unit, quantities and values as sent', () => {
    const [, row] = purchaseOrderTrackRows(result([order()]));

    expect(row).toMatchObject({
      kind: 'line',
      name: 'Widget A',
      code: 'WIDGET-A',
      unitCode: 'PCS',
      ordered: 10,
      received: 4,
      billed: 0,
      toReceive: 6,
      toBill: 10,
      orderedValue: 6000,
      toReceiveValue: 3600,
      toBillValue: 6000,
    });
  });

  it('PO-14: ordered less received and ordered less billed hold on a line as laid out', () => {
    const [, row] = purchaseOrderTrackRows(result([order()]));

    expect((row.ordered ?? 0) - (row.received ?? 0)).toBe(row.toReceive);
    expect((row.ordered ?? 0) - (row.billed ?? 0)).toBe(row.toBill);
  });

  it('PO-15: a heading carries the expected date, the bills raised and what they came to', () => {
    const [heading] = purchaseOrderTrackRows(result([order({ expectedDate: '2026-10-20', billsRaised: 2, billTotal: 5000 })]));

    expect([heading.expectedDate, heading.billsRaised, heading.billTotal]).toEqual(['2026-10-20', 2, 5000]);
  });

  it('PO-16: an order in another currency names it on its heading, and one in the branch’s does not', () => {
    expect(purchaseOrderTrackRows(result([order({ currencyCode: 'USD' })]))[0].currency).toBe('USD');
    expect(purchaseOrderTrackRows(result([order()]))[0].currency).toBeNull();
  });

  it('PO-17: the total row is the server’s figures, and says it is of all when not every order is loaded', () => {
    const rows = purchaseOrderTrackRows(
      result([order()], { totalOrders: 120, totals: { orderedValue: 99, receivedValue: 1, billedValue: 2, toReceiveValue: 98, toBillValue: 97 } }),
    );

    expect(rows[rows.length - 1]).toMatchObject({ name: 'Total of all 120 orders', orderedValue: 99, toReceiveValue: 98, toBillValue: 97 });
  });

  it('PO-18: with no orders there are no rows, not even the total', () => {
    expect(purchaseOrderTrackRows(result([]))).toEqual([]);
  });

  it('PO-19: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([order({ purchaseOrderId: 1 })], { totalOrders: 3, pageNumber: 1 });
    const next = result([order({ purchaseOrderId: 2 })], { totalOrders: 3, pageNumber: 2 });
    next.totals.orderedValue = 9;

    const merged = appendPurchaseOrderTrackPage(first, next);

    expect(merged.orders.map((o) => o.purchaseOrderId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.orderedValue).toBe(9);
  });

  it('PO-20: there is more while fewer orders are loaded than match', () => {
    expect(hasMorePurchaseOrderTrack({ orders: [order()], totalOrders: 2 })).toBe(true);
    expect(hasMorePurchaseOrderTrack({ orders: [order(), order({ purchaseOrderId: 2 })], totalOrders: 2 })).toBe(false);
  });

  it('PO-21: the sentence says how many orders, what they are for and what is still to receive and bill', () => {
    const answer = result([order()], { totals: { orderedValue: 80000, receivedValue: 63000, billedValue: 55600, toReceiveValue: 17000, toBillValue: 24400 }, totalOrders: 6 });

    expect(purchaseOrderTrackSentence(answer, money)).toBe('6 orders for ₹80000.00: ₹17000.00 still to receive and ₹24400.00 still to bill.');
    expect(purchaseOrderTrackSentence(result([order()]), money)).toMatch(/^1 order for /);
  });
});
