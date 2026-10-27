import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  RECEIVE_ORDER_STATUSES,
  ReceiveOrderTrackReceipt,
  ReceiveOrderTrackResult,
  ReceiveOrderTrackStatus,
  appendReceiveOrderTrackPage,
  hasMoreReceiveOrderTrack,
  receiveOrderStatusLabel,
  receiveOrderStatusTag,
  receiveOrderTrackRows,
  receiveOrderTrackSentence,
  receiveOrderTrackTabs,
} from './receive-order-track.models';
import { ReceiveOrderTrackService } from './receive-order-track.service';

/** The Receive Order Track Report's calls and the helpers its page uses (TK-242). */

function setup(answer: unknown = { receipts: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [ReceiveOrderTrackService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(ReceiveOrderTrackService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const receipt = (over: Partial<ReceiveOrderTrackReceipt> = {}): ReceiveOrderTrackReceipt => ({
  goodsReceiptId: 9901,
  documentNo: 'GRN-9901',
  documentDate: '2026-10-05',
  contactId: 9203,
  contactCode: 'ZENITH',
  contactName: 'Zenith Components',
  currencyCode: 'INR',
  purchaseOrderId: 9911,
  purchaseOrderNo: 'POR-9911',
  vendorDeliveryNoteNo: 'DN-77',
  status: 'PartlyBilled',
  valueInCurrency: 6700,
  value: 6700,
  billedValue: 2100,
  toBillValue: 4600,
  ...over,
});

const result = (receipts: ReceiveOrderTrackReceipt[], over: Partial<ReceiveOrderTrackResult> = {}): ReceiveOrderTrackResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  receipts,
  totalReceipts: receipts.length,
  pageNumber: 1,
  pageSize: 25,
  statusCounts: { NotBilled: 0, PartlyBilled: receipts.length, Billed: 0, NothingToBill: 0 },
  totals: {
    value: receipts.reduce((t, r) => t + r.value, 0),
    billedValue: receipts.reduce((t, r) => t + r.billedValue, 0),
    toBillValue: receipts.reduce((t, r) => t + r.toBillValue, 0),
  },
  ...over,
});

describe('ReceiveOrderTrackService', () => {
  it('RT-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/receive-order-track-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'status', 'notYetBilledOnly', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('RT-02: sends the vendor, status, not-yet-billed tick and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: 9203, status: 'Billed', notYetBilledOnly: true, pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('9203');
    expect(params.get('status')).toBe('Billed');
    expect(params.get('notYetBilledOnly')).toBe('true');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('RT-03: leaves the not-yet-billed tick out of the request unless it is on', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', notYetBilledOnly: false });

    expect(paramsOf(get.mock.calls[0]).has('notYetBilledOnly')).toBe(false);
  });

  it('RT-04: gives back what the endpoint answered', async () => {
    const answer = result([receipt()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('RT-05: reads the vendors the filter offers from the report’s own lookup', async () => {
    const vendors = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Supplies' }];
    const { service, get } = setup(vendors);

    expect(await service.vendors()).toEqual(vendors);
    expect(get.mock.calls[0][0]).toBe('/api/statements/receive-order-track-report/vendors');
  });
});

describe('receive order track helpers', () => {
  it('RT-06: the statuses run from nothing billed to nothing owed and read as words', () => {
    expect(RECEIVE_ORDER_STATUSES.map((s) => s.value)).toEqual(['NotBilled', 'PartlyBilled', 'Billed', 'NothingToBill']);
    expect(receiveOrderStatusLabel('NotBilled')).toBe('To bill');
    expect(receiveOrderStatusLabel('PartlyBilled')).toBe('Partly billed');
    expect(receiveOrderStatusLabel('NothingToBill')).toBe('Nothing to bill');
  });

  it('RT-07: billed reads as done, partly billed as in progress and nothing to bill as set aside', () => {
    expect(receiveOrderStatusTag('Billed')).toBe('tag-success');
    expect(receiveOrderStatusTag('PartlyBilled')).toBe('tag-warning');
    expect(receiveOrderStatusTag('NotBilled')).toBe('tag-info');
    expect(receiveOrderStatusTag('NothingToBill')).toBe('tag-neutral');
  });

  it('RT-08: the tabs are All and then every status, with a nought kept', () => {
    const tabs = receiveOrderTrackTabs({ NotBilled: 2, PartlyBilled: 1, Billed: 3, NothingToBill: 0 });

    expect(tabs.map((t) => t.label)).toEqual(['All', 'To bill', 'Partly billed', 'Billed', 'Nothing to bill']);
    expect(tabs.map((t) => t.value)).toEqual(['', 'NotBilled', 'PartlyBilled', 'Billed', 'NothingToBill']);
    expect(tabs.map((t) => t.count)).toEqual([6, 2, 1, 3, 0]);
  });

  it('RT-09: with no counts yet every tab reads nought', () => {
    expect(receiveOrderTrackTabs(undefined).map((t) => t.count)).toEqual([0, 0, 0, 0, 0]);
  });

  it('RT-10: a receipt is a row of its own and the total closes the grid', () => {
    const rows = receiveOrderTrackRows(result([receipt(), receipt({ goodsReceiptId: 9902, documentNo: 'GRN-9902', status: 'NotBilled' })]));

    expect(rows.map((r) => r.kind)).toEqual(['receipt', 'receipt', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
    expect(rows[0]).toMatchObject({
      name: 'GRN-9901',
      link: '/purchase/goods-receipts/9901',
      contactName: 'Zenith Components',
      orderNo: 'POR-9911',
      orderLink: '/purchase/purchase-orders/9911',
      vendorDeliveryNoteNo: 'DN-77',
      status: 'PartlyBilled',
      value: 6700,
      billedValue: 2100,
      toBillValue: 4600,
    });
    expect(rows[2].name).toBe('Total');
  });

  it('RT-11: received less billed is to bill on a receipt as laid out', () => {
    const [row] = receiveOrderTrackRows(result([receipt()]));

    expect(row.value - row.billedValue).toBe(row.toBillValue);
  });

  it('RT-12: a receipt with no order has no order link', () => {
    const [row] = receiveOrderTrackRows(result([receipt({ purchaseOrderId: null, purchaseOrderNo: null })]));

    expect([row.orderNo, row.orderLink]).toEqual([null, null]);
  });

  it('RT-13: a receipt in another currency names it with its own value, and one in the branch’s does not', () => {
    const [foreign] = receiveOrderTrackRows(result([receipt({ currencyCode: 'USD', valueInCurrency: 800, value: 64000 })]));
    expect([foreign.currency, foreign.ownValue, foreign.value]).toEqual(['USD', 800, 64000]);

    const [home] = receiveOrderTrackRows(result([receipt()]));
    expect([home.currency, home.ownValue]).toEqual([null, null]);
  });

  it('RT-14: the total row is the server’s figures, and says it is of all when not every receipt is loaded', () => {
    const rows = receiveOrderTrackRows(result([receipt()], { totalReceipts: 120, totals: { value: 99, billedValue: 1, toBillValue: 98 } }));

    expect(rows[rows.length - 1]).toMatchObject({ name: 'Total of all 120 receipts', value: 99, billedValue: 1, toBillValue: 98 });
  });

  it('RT-15: with no receipts there are no rows, not even the total', () => {
    expect(receiveOrderTrackRows(result([]))).toEqual([]);
  });

  it('RT-16: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([receipt({ goodsReceiptId: 1 })], { totalReceipts: 3, pageNumber: 1 });
    const next = result([receipt({ goodsReceiptId: 2 })], { totalReceipts: 3, pageNumber: 2 });
    next.totals.value = 9;

    const merged = appendReceiveOrderTrackPage(first, next);

    expect(merged.receipts.map((r) => r.goodsReceiptId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.value).toBe(9);
  });

  it('RT-17: there is more while fewer receipts are loaded than match', () => {
    expect(hasMoreReceiveOrderTrack({ receipts: [receipt()], totalReceipts: 2 })).toBe(true);
    expect(hasMoreReceiveOrderTrack({ receipts: [receipt(), receipt({ goodsReceiptId: 2 })], totalReceipts: 2 })).toBe(false);
  });

  it('RT-18: the sentence says how many receipts, what they came to and what is billed and still to bill', () => {
    const answer = result([receipt()], { totals: { value: 76166.67, billedValue: 7566.67, toBillValue: 68600 }, totalReceipts: 6 });

    expect(receiveOrderTrackSentence(answer, money)).toBe('6 receipts for ₹76166.67: ₹7566.67 billed and ₹68600.00 still to bill.');
    expect(receiveOrderTrackSentence(result([receipt()]), money)).toMatch(/^1 receipt for /);
  });

  it('RT-19: every status the server can send has a label', () => {
    const statuses: ReceiveOrderTrackStatus[] = ['NotBilled', 'PartlyBilled', 'Billed', 'NothingToBill'];

    expect(statuses.map(receiveOrderStatusLabel).every((label) => label.length > 0)).toBe(true);
  });
});
