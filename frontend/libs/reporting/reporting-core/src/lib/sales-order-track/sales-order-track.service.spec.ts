import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  SALES_ORDER_PROGRESSES,
  SalesOrderProgress,
  SalesOrderTrackLine,
  SalesOrderTrackOrder,
  SalesOrderTrackResult,
  appendSalesOrderTrackPage,
  hasMoreSalesOrderTrack,
  isForeignOrder,
  salesOrderProgressLabel,
  salesOrderProgressTag,
  salesOrderTrackRows,
  salesOrderTrackSentence,
  salesOrderTrackTabs,
} from './sales-order-track.models';
import { SalesOrderTrackService } from './sales-order-track.service';

/** The Sales Order Track Report's calls and the helpers its page uses (TK-230). */

function setup(answer: unknown = { orders: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [SalesOrderTrackService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(SalesOrderTrackService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const line = (over: Partial<SalesOrderTrackLine> = {}): SalesOrderTrackLine => ({
  lineNumber: 1,
  itemId: 9601,
  itemCode: 'WIDGET-A',
  itemName: 'Widget A',
  unitCode: 'PCS',
  ordered: 10,
  delivered: 4,
  invoiced: 0,
  toDeliver: 6,
  toBill: 10,
  orderedValue: 6000,
  deliveredValue: 2400,
  invoicedValue: 0,
  toDeliverValue: 3600,
  toBillValue: 6000,
  ...over,
});

const order = (over: Partial<SalesOrderTrackOrder> = {}): SalesOrderTrackOrder => ({
  salesOrderId: 9502,
  documentNo: 'SOR-9502',
  documentDate: '2026-10-08',
  deliveryDate: null,
  contactId: 9103,
  contactCode: 'ZENITH',
  contactName: 'Zenith Traders',
  currencyCode: 'INR',
  progress: 'PartlyDelivered',
  lines: [line()],
  orderedValue: 6000,
  deliveredValue: 2400,
  invoicedValue: 0,
  toDeliverValue: 3600,
  toBillValue: 6000,
  invoicesRaised: 0,
  billed: 0,
  ...over,
});

const result = (orders: SalesOrderTrackOrder[], over: Partial<SalesOrderTrackResult> = {}): SalesOrderTrackResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  orders,
  totalOrders: orders.length,
  pageNumber: 1,
  pageSize: 25,
  progressCounts: { Open: 0, PartlyDelivered: orders.length, Delivered: 0, Invoiced: 0, ClosedShort: 0, Cancelled: 0 },
  totals: {
    orderedValue: orders.reduce((t, o) => t + o.orderedValue, 0),
    deliveredValue: orders.reduce((t, o) => t + o.deliveredValue, 0),
    invoicedValue: orders.reduce((t, o) => t + o.invoicedValue, 0),
    toDeliverValue: orders.reduce((t, o) => t + o.toDeliverValue, 0),
    toBillValue: orders.reduce((t, o) => t + o.toBillValue, 0),
  },
  ...over,
});

describe('SalesOrderTrackService', () => {
  it('SO-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/sales-order-track-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'progress', 'outstandingOnly', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('SO-02: sends the customer, progress, outstanding tick and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: 9103, progress: 'Delivered', outstandingOnly: true, pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('9103');
    expect(params.get('progress')).toBe('Delivered');
    expect(params.get('outstandingOnly')).toBe('true');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('SO-03: leaves the outstanding tick out of the request unless it is on', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', outstandingOnly: false });

    expect(paramsOf(get.mock.calls[0]).has('outstandingOnly')).toBe(false);
  });

  it('SO-04: gives back what the endpoint answered', async () => {
    const answer = result([order()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('SO-05: reads the customers the filter offers from the report’s own lookup', async () => {
    const customers = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const { service, get } = setup(customers);

    expect(await service.customers()).toEqual(customers);
    expect(get.mock.calls[0][0]).toBe('/api/statements/sales-order-track-report/customers');
  });
});

describe('sales order track helpers', () => {
  it('SO-06: the progresses run along an order’s life and read as words', () => {
    expect(SALES_ORDER_PROGRESSES.map((p) => p.value)).toEqual(['Open', 'PartlyDelivered', 'Delivered', 'Invoiced', 'ClosedShort', 'Cancelled']);
    expect(salesOrderProgressLabel('PartlyDelivered')).toBe('Partly delivered');
    expect(salesOrderProgressLabel('ClosedShort')).toBe('Closed short');
  });

  it('SO-07: invoiced reads as done, closed short and cancelled as set aside', () => {
    expect(salesOrderProgressTag('Invoiced')).toBe('tag-success');
    expect(salesOrderProgressTag('Delivered')).toBe('tag-info');
    expect(salesOrderProgressTag('PartlyDelivered')).toBe('tag-warning');
    expect(salesOrderProgressTag('Open')).toBe('tag-outline');
    expect(salesOrderProgressTag('ClosedShort')).toBe('tag-neutral');
    expect(salesOrderProgressTag('Cancelled')).toBe('tag-neutral');
  });

  it('SO-08: the tabs are All and then every progress, with a nought kept', () => {
    const tabs = salesOrderTrackTabs({ Open: 1, PartlyDelivered: 1, Delivered: 1, Invoiced: 2, ClosedShort: 1, Cancelled: 1 });

    expect(tabs.map((t) => t.label)).toEqual(['All', 'Open', 'Partly delivered', 'Delivered', 'Invoiced', 'Closed short', 'Cancelled']);
    expect(tabs.map((t) => t.value)).toEqual(['', 'Open', 'PartlyDelivered', 'Delivered', 'Invoiced', 'ClosedShort', 'Cancelled']);
    expect(tabs.map((t) => t.count)).toEqual([6, 1, 1, 1, 2, 1, 1]);
  });

  it('SO-09: All leaves the cancelled ones out, as the list does without a progress', () => {
    const counts: Record<SalesOrderProgress, number> = { Open: 1, PartlyDelivered: 0, Delivered: 0, Invoiced: 3, ClosedShort: 0, Cancelled: 4 };

    expect(salesOrderTrackTabs(counts)[0].count).toBe(4);
  });

  it('SO-10: with no counts yet every tab reads nought', () => {
    expect(salesOrderTrackTabs(undefined).map((t) => t.count)).toEqual([0, 0, 0, 0, 0, 0, 0]);
  });

  it('SO-11: an order is foreign when its currency is not the branch’s, whatever the case', () => {
    expect(isForeignOrder({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignOrder({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignOrder({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('SO-12: an order is a heading, its lines and its value, and then the total closes the grid', () => {
    const rows = salesOrderTrackRows(result([order({ lines: [line(), line({ lineNumber: 2, itemName: 'Gadget B', itemCode: 'GADGET-B' })] })]));

    expect(rows.map((r) => r.kind)).toEqual(['order', 'line', 'line', 'subtotal', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(5);
    expect(rows[0]).toMatchObject({ name: 'SOR-9502', link: '/sales/sales-orders/9502', progress: 'PartlyDelivered', contactName: 'Zenith Traders' });
    expect(rows[3]).toMatchObject({ name: 'Order value', orderedValue: 6000, toDeliverValue: 3600, toBillValue: 6000 });
    expect(rows[4].name).toBe('Total');
  });

  it('SO-13: a line carries its item, unit, quantities and values as sent', () => {
    const [, row] = salesOrderTrackRows(result([order()]));

    expect(row).toMatchObject({
      kind: 'line',
      name: 'Widget A',
      code: 'WIDGET-A',
      unitCode: 'PCS',
      ordered: 10,
      delivered: 4,
      invoiced: 0,
      toDeliver: 6,
      toBill: 10,
      orderedValue: 6000,
      toDeliverValue: 3600,
      toBillValue: 6000,
    });
  });

  it('SO-14: ordered less delivered and ordered less invoiced hold on a line as laid out', () => {
    const [, row] = salesOrderTrackRows(result([order()]));

    expect((row.ordered ?? 0) - (row.delivered ?? 0)).toBe(row.toDeliver);
    expect((row.ordered ?? 0) - (row.invoiced ?? 0)).toBe(row.toBill);
  });

  it('SO-15: a heading carries the promised date, the invoices raised and what they came to', () => {
    const [heading] = salesOrderTrackRows(result([order({ deliveryDate: '2026-10-20', invoicesRaised: 2, billed: 5000 })]));

    expect([heading.deliveryDate, heading.invoicesRaised, heading.billed]).toEqual(['2026-10-20', 2, 5000]);
  });

  it('SO-16: an order in another currency names it on its heading, and one in the branch’s does not', () => {
    expect(salesOrderTrackRows(result([order({ currencyCode: 'USD' })]))[0].currency).toBe('USD');
    expect(salesOrderTrackRows(result([order()]))[0].currency).toBeNull();
  });

  it('SO-17: the total row is the server’s figures, and says it is of all when not every order is loaded', () => {
    const rows = salesOrderTrackRows(
      result([order()], { totalOrders: 120, totals: { orderedValue: 99, deliveredValue: 1, invoicedValue: 2, toDeliverValue: 98, toBillValue: 97 } }),
    );

    expect(rows[rows.length - 1]).toMatchObject({ name: 'Total of all 120 orders', orderedValue: 99, toDeliverValue: 98, toBillValue: 97 });
  });

  it('SO-18: with no orders there are no rows, not even the total', () => {
    expect(salesOrderTrackRows(result([]))).toEqual([]);
  });

  it('SO-19: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([order({ salesOrderId: 1 })], { totalOrders: 3, pageNumber: 1 });
    const next = result([order({ salesOrderId: 2 })], { totalOrders: 3, pageNumber: 2 });
    next.totals.orderedValue = 9;

    const merged = appendSalesOrderTrackPage(first, next);

    expect(merged.orders.map((o) => o.salesOrderId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.orderedValue).toBe(9);
  });

  it('SO-20: there is more while fewer orders are loaded than match', () => {
    expect(hasMoreSalesOrderTrack({ orders: [order()], totalOrders: 2 })).toBe(true);
    expect(hasMoreSalesOrderTrack({ orders: [order(), order({ salesOrderId: 2 })], totalOrders: 2 })).toBe(false);
  });

  it('SO-21: the sentence says how many orders, what they are for and what is still to deliver and bill', () => {
    const answer = result([order()], { totals: { orderedValue: 80000, deliveredValue: 63000, invoicedValue: 55600, toDeliverValue: 17000, toBillValue: 24400 }, totalOrders: 6 });

    expect(salesOrderTrackSentence(answer, money)).toBe('6 orders for ₹80000.00: ₹17000.00 still to deliver and ₹24400.00 still to bill.');
    expect(salesOrderTrackSentence(result([order()]), money)).toMatch(/^1 order for /);
  });
});
