import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  DELIVERY_CHALLAN_PROGRESSES,
  DELIVERY_CHALLAN_TYPES,
  DeliveryChallanProgress,
  DeliveryOrderTrackChallan,
  DeliveryOrderTrackResult,
  appendDeliveryOrderTrackPage,
  challanInvoiceLink,
  challanLink,
  deliveryChallanProgressLabel,
  deliveryChallanProgressTag,
  deliveryChallanTypeLabel,
  deliveryOrderTrackRows,
  deliveryOrderTrackSentence,
  deliveryOrderTrackTabs,
  hasMoreDeliveryOrderTrack,
  isForeignChallan,
} from './delivery-order-track.models';
import { DeliveryOrderTrackService } from './delivery-order-track.service';

/** The Delivery Order Track Report's calls and the helpers its page uses (TK-232). */

function setup(answer: unknown = { challans: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [DeliveryOrderTrackService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(DeliveryOrderTrackService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const challan = (over: Partial<DeliveryOrderTrackChallan> = {}): DeliveryOrderTrackChallan => ({
  deliveryChallanId: 1,
  documentNo: 'DLC-0001',
  documentDate: '2026-10-02',
  dispatchDate: '2026-10-03',
  contactId: 101,
  contactCode: 'ZENITH',
  contactName: 'Zenith Traders',
  currencyCode: 'INR',
  challanType: 'Sale',
  progress: 'PartlyInvoiced',
  salesOrderId: 7,
  salesOrderNo: 'SOR-0007',
  lines: [
    {
      lineNumber: 1,
      itemId: 5,
      itemCode: 'WIDGET-A',
      itemName: 'Widget A',
      unitCode: 'PCS',
      delivered: 10,
      invoiced: 6,
      toBill: 4,
      deliveredValue: 6000,
      invoicedValue: 3600,
      toBillValue: 2400,
    },
  ],
  deliveredValue: 6000,
  invoicedValue: 3600,
  toBillValue: 2400,
  invoices: [{ typeCode: 'INV', documentId: 9, documentNo: 'INV-0009', documentDate: '2026-10-10' }],
  ...over,
});

const result = (challans: DeliveryOrderTrackChallan[], over: Partial<DeliveryOrderTrackResult> = {}): DeliveryOrderTrackResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  challans,
  totalChallans: challans.length,
  pageNumber: 1,
  pageSize: 25,
  progressCounts: { NotInvoiced: 0, PartlyInvoiced: challans.length, Invoiced: 0, Void: 0 },
  totals: {
    deliveredValue: challans.reduce((t, c) => t + c.deliveredValue, 0),
    invoicedValue: challans.reduce((t, c) => t + c.invoicedValue, 0),
    toBillValue: challans.reduce((t, c) => t + c.toBillValue, 0),
  },
  ...over,
});

describe('DeliveryOrderTrackService', () => {
  it('DT-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/delivery-order-track-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'progress', 'challanType', 'notInvoicedOnly', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('DT-02: sends the customer, status, type, tick and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      contactId: 102,
      progress: 'PartlyInvoiced',
      challanType: 'JobWork',
      notInvoicedOnly: true,
      pageNumber: 2,
      pageSize: 25,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('102');
    expect(params.get('progress')).toBe('PartlyInvoiced');
    expect(params.get('challanType')).toBe('JobWork');
    expect(params.get('notInvoicedOnly')).toBe('true');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('DT-03: gives back what the endpoint answered', async () => {
    const answer = result([challan()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('DT-04: reads the customers the filter offers from the report’s own lookup', async () => {
    const customers = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const { service, get } = setup(customers);

    expect(await service.customers()).toEqual(customers);
    expect(get.mock.calls[0][0]).toBe('/api/statements/delivery-order-track-report/customers');
  });
});

describe('delivery order track helpers', () => {
  it('DT-05: the statuses run from not invoiced to void and read as words', () => {
    expect(DELIVERY_CHALLAN_PROGRESSES.map((p) => p.value)).toEqual(['NotInvoiced', 'PartlyInvoiced', 'Invoiced', 'Void']);
    expect(deliveryChallanProgressLabel('NotInvoiced')).toBe('Not invoiced');
    expect(deliveryChallanProgressLabel('PartlyInvoiced')).toBe('Partly invoiced');
  });

  it('DT-06: the challan types read as words', () => {
    expect(DELIVERY_CHALLAN_TYPES.map((t) => t.value)).toEqual(['Sale', 'JobWork', 'Approval', 'BranchTransfer', 'Sample']);
    expect(deliveryChallanTypeLabel('JobWork')).toBe('Job work');
    expect(deliveryChallanTypeLabel('BranchTransfer')).toBe('Branch transfer');
  });

  it('DT-07: invoiced reads as done, partly invoiced as a warning, not invoiced as outlined and void as set aside', () => {
    expect(deliveryChallanProgressTag('Invoiced')).toBe('tag-success');
    expect(deliveryChallanProgressTag('PartlyInvoiced')).toBe('tag-warning');
    expect(deliveryChallanProgressTag('NotInvoiced')).toBe('tag-outline');
    expect(deliveryChallanProgressTag('Void')).toBe('tag-neutral');
  });

  it('DT-08: the tabs are All and then every status, with a nought kept', () => {
    const tabs = deliveryOrderTrackTabs({ NotInvoiced: 3, PartlyInvoiced: 2, Invoiced: 2, Void: 1 });

    expect(tabs.map((t) => t.label)).toEqual(['All', 'Not invoiced', 'Partly invoiced', 'Invoiced', 'Void']);
    expect(tabs.map((t) => t.value)).toEqual(['', 'NotInvoiced', 'PartlyInvoiced', 'Invoiced', 'Void']);
    expect(tabs.map((t) => t.count)).toEqual([7, 3, 2, 2, 1]);
  });

  it('DT-09: All leaves the void ones out, as the list does without a status', () => {
    const counts: Record<DeliveryChallanProgress, number> = { NotInvoiced: 1, PartlyInvoiced: 0, Invoiced: 3, Void: 4 };

    expect(deliveryOrderTrackTabs(counts)[0].count).toBe(4);
  });

  it('DT-10: with no counts yet every tab reads nought', () => {
    expect(deliveryOrderTrackTabs(undefined).map((t) => t.count)).toEqual([0, 0, 0, 0, 0]);
  });

  it('DT-11: a foreign challan is told by a currency other than the branch’s, whatever the case', () => {
    expect(isForeignChallan({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignChallan({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignChallan({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('DT-12: a challan opens at its page and an invoice raised from it at its own', () => {
    expect(challanLink(4)).toBe('/sales/delivery-challans/4');
    expect(challanInvoiceLink({ documentId: 9 })).toBe('/sales/invoices/9');
  });

  it('DT-13: the grid has a heading, the lines and a value row for each challan, then the total', () => {
    const rows = deliveryOrderTrackRows(result([challan({ deliveryChallanId: 1 }), challan({ deliveryChallanId: 2, documentNo: 'DLC-0002' })]));

    expect(rows.map((r) => r.kind)).toEqual(['challan', 'line', 'subtotal', 'challan', 'line', 'subtotal', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(7);
    expect(rows[6].name).toBe('Total');
    expect(rows[6].deliveredValue).toBe(12000);
    expect(rows[6].toBillValue).toBe(4800);
  });

  it('DT-14: when not every challan is loaded the total row says it is of all of them and carries the server’s figures', () => {
    const rows = deliveryOrderTrackRows(
      result([challan()], { totalChallans: 120, totals: { deliveredValue: 99, invoicedValue: 40, toBillValue: 59 } }),
    );

    expect(rows[rows.length - 1].name).toBe('Total of all 120 challans');
    expect(rows[rows.length - 1].toBillValue).toBe(59);
  });

  it('DT-15: a heading links to the challan, the order it was delivered against and the invoices raised from it', () => {
    const [heading] = deliveryOrderTrackRows(result([challan({ deliveryChallanId: 4 })]));

    expect(heading.link).toBe('/sales/delivery-challans/4');
    expect([heading.orderNo, heading.orderLink]).toEqual(['SOR-0007', '/sales/sales-orders/7']);
    expect(heading.invoices).toEqual([{ no: 'INV-0009', link: '/sales/invoices/9' }]);
    expect([heading.challanType, heading.progress, heading.dispatchDate]).toEqual(['Sale', 'PartlyInvoiced', '2026-10-03']);
  });

  it('DT-16: a challan against no order links to none, and one with no invoice lists none', () => {
    const [heading] = deliveryOrderTrackRows(result([challan({ salesOrderId: null, salesOrderNo: null, invoices: [] })]));

    expect([heading.orderNo, heading.orderLink]).toEqual([null, null]);
    expect(heading.invoices).toEqual([]);
  });

  it('DT-17: a line carries what was delivered, invoiced and is to bill, and the value row the challan’s', () => {
    const rows = deliveryOrderTrackRows(result([challan()]));
    const [, line, value] = rows;

    expect([line.name, line.code, line.unitCode, line.delivered, line.invoiced, line.toBill]).toEqual(['Widget A', 'WIDGET-A', 'PCS', 10, 6, 4]);
    expect([line.deliveredValue, line.invoicedValue, line.toBillValue]).toEqual([6000, 3600, 2400]);
    expect([value.name, value.deliveredValue, value.invoicedValue, value.toBillValue]).toEqual(['Challan value', 6000, 3600, 2400]);
  });

  it('DT-18: a challan in another currency names it, and one in the branch’s does not', () => {
    const rows = deliveryOrderTrackRows(result([challan({ currencyCode: 'USD' }), challan({ deliveryChallanId: 2, currencyCode: 'INR' })]));

    expect(rows[0].currency).toBe('USD');
    expect(rows[3].currency).toBeNull();
  });

  it('DT-19: with no challans there are no rows, not even the total', () => {
    expect(deliveryOrderTrackRows(result([]))).toEqual([]);
  });

  it('DT-20: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([challan({ deliveryChallanId: 1 })], { totalChallans: 3, pageNumber: 1 });
    const next = result([challan({ deliveryChallanId: 2 })], { totalChallans: 3, pageNumber: 2, totals: { deliveredValue: 9, invoicedValue: 4, toBillValue: 5 } });

    const merged = appendDeliveryOrderTrackPage(first, next);

    expect(merged.challans.map((c) => c.deliveryChallanId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.toBillValue).toBe(5);
  });

  it('DT-21: there is more while fewer challans are loaded than match', () => {
    expect(hasMoreDeliveryOrderTrack({ challans: [challan()], totalChallans: 2 })).toBe(true);
    expect(hasMoreDeliveryOrderTrack({ challans: [challan(), challan({ deliveryChallanId: 2 })], totalChallans: 2 })).toBe(false);
  });

  it('DT-22: the sentence says how many challans, what they delivered, and what is invoiced and still to bill', () => {
    const answer = result([challan({ deliveredValue: 100, invoicedValue: 40, toBillValue: 60 }), challan({ deliveryChallanId: 2, deliveredValue: 200, invoicedValue: 0, toBillValue: 200 })]);

    expect(deliveryOrderTrackSentence(answer, money)).toBe('2 challans delivered ₹300.00: ₹40.00 invoiced and ₹260.00 still to bill.');
    expect(deliveryOrderTrackSentence(result([challan()]), money)).toMatch(/^1 challan delivered /);
  });
});
