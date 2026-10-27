import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  PurchaseReceiveOrderDetailsLine,
  PurchaseReceiveOrderDetailsReceipt,
  PurchaseReceiveOrderDetailsResult,
  appendPurchaseReceiveOrderDetailsPage,
  hasMorePurchaseReceiveOrderDetails,
  isForeignGoodsReceipt,
  purchaseReceiveOrderDetailsRows,
  purchaseReceiveOrderDetailsSentence,
  receiveOrderChoiceLabel,
  serialsNotListed,
} from './purchase-receive-order-details.models';
import { PurchaseReceiveOrderDetailsService } from './purchase-receive-order-details.service';

/** The Purchase Receive Order Details' calls and the helpers its page uses (TK-241). */

function setup(answer: unknown = { receipts: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [PurchaseReceiveOrderDetailsService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(PurchaseReceiveOrderDetailsService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const line = (over: Partial<PurchaseReceiveOrderDetailsLine> = {}): PurchaseReceiveOrderDetailsLine => ({
  goodsReceiptDetailId: 98011,
  lineNumber: 1,
  itemId: 9701,
  itemCode: 'WIDGET-A',
  itemName: 'Widget A',
  unitCode: 'PCS',
  warehouseId: 9301,
  warehouseName: 'Main warehouse',
  delivered: 10,
  rejected: 2,
  accepted: 8,
  billed: 0,
  unitCost: 400,
  value: 3200,
  valueBase: 3200,
  rejectionReason: 'Damaged in transit',
  batchNumber: 'BT-1',
  batchExpiryDate: '2027-03-31',
  serials: [],
  serialCount: 0,
  ...over,
});

const receipt = (over: Partial<PurchaseReceiveOrderDetailsReceipt> = {}): PurchaseReceiveOrderDetailsReceipt => ({
  goodsReceiptId: 9801,
  documentNo: 'GRN-9801',
  documentDate: '2026-10-05',
  contactId: 9203,
  contactCode: 'ZENITH',
  contactName: 'Zenith Components',
  currencyCode: 'INR',
  purchaseOrderId: 9811,
  purchaseOrderNo: 'POR-9811',
  vendorDeliveryNoteNo: 'DN-77',
  vendorDeliveryNoteDate: '2026-10-04',
  lines: [line()],
  value: 3200,
  valueBase: 3200,
  ...over,
});

const result = (receipts: PurchaseReceiveOrderDetailsReceipt[], over: Partial<PurchaseReceiveOrderDetailsResult> = {}): PurchaseReceiveOrderDetailsResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  linesFiltered: false,
  receipts,
  totalReceipts: receipts.length,
  pageNumber: 1,
  pageSize: 25,
  totals: { lines: receipts.reduce((t, r) => t + r.lines.length, 0), value: receipts.reduce((t, r) => t + r.valueBase, 0) },
  ...over,
});

describe('PurchaseReceiveOrderDetailsService', () => {
  it('RO-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/purchase-receive-order-details');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'itemId', 'warehouseId', 'purchaseOrderId', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('RO-02: sends the vendor, item, warehouse, purchase order and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: 9203, itemId: 9701, warehouseId: 9301, purchaseOrderId: 9811, pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('9203');
    expect(params.get('itemId')).toBe('9701');
    expect(params.get('warehouseId')).toBe('9301');
    expect(params.get('purchaseOrderId')).toBe('9811');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('RO-03: gives back what the endpoint answered', async () => {
    const answer = result([receipt()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('RO-04: reads each filter list from the report’s own lookup', async () => {
    const { service, get } = setup([]);

    await service.vendors();
    await service.items();
    await service.warehouses();
    await service.purchaseOrders();

    expect(get.mock.calls.map((c) => c[0])).toEqual([
      '/api/statements/purchase-receive-order-details/vendors',
      '/api/statements/purchase-receive-order-details/items',
      '/api/statements/purchase-receive-order-details/warehouses',
      '/api/statements/purchase-receive-order-details/purchase-orders',
    ]);
  });
});

describe('purchase receive order details helpers', () => {
  it('RO-05: a purchase order reads as its number and its vendor', () => {
    expect(receiveOrderChoiceLabel({ documentNo: 'POR-9811', contactName: 'Zenith Components' })).toBe('POR-9811 · Zenith Components');
    expect(receiveOrderChoiceLabel({ documentNo: 'POR-9811', contactName: '' })).toBe('POR-9811');
  });

  it('RO-06: a receipt is foreign when its currency is not the branch’s, whatever the case', () => {
    expect(isForeignGoodsReceipt({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignGoodsReceipt({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignGoodsReceipt({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('RO-07: a receipt is a heading, its lines and its value, and then the total closes the grid', () => {
    const rows = purchaseReceiveOrderDetailsRows(result([receipt({ lines: [line(), line({ lineNumber: 2, itemName: 'Gadget B', itemCode: 'GADGET-B' })] })]));

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'line', 'line', 'subtotal', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(5);
    expect(rows[0]).toMatchObject({
      name: 'GRN-9801',
      link: '/purchase/goods-receipts/9801',
      contactName: 'Zenith Components',
      orderNo: 'POR-9811',
      orderLink: '/purchase/purchase-orders/9811',
      vendorDeliveryNoteNo: 'DN-77',
      vendorDeliveryNoteDate: '2026-10-04',
    });
    expect(rows[3]).toMatchObject({ name: 'Receipt value', value: 3200 });
    expect(rows[4].name).toBe('Total');
  });

  it('RO-08: a line carries its item, unit, warehouse, quantities, cost, value and batch as sent', () => {
    const [, row] = purchaseReceiveOrderDetailsRows(result([receipt()]));

    expect(row).toMatchObject({
      kind: 'line',
      name: 'Widget A',
      code: 'WIDGET-A',
      unitCode: 'PCS',
      warehouse: 'Main warehouse',
      delivered: 10,
      rejected: 2,
      accepted: 8,
      billed: 0,
      unitCost: 400,
      value: 3200,
      rejectionReason: 'Damaged in transit',
      batchNumber: 'BT-1',
      batchExpiryDate: '2027-03-31',
    });
  });

  it('RO-09: delivered less rejected is accepted on a line as laid out', () => {
    const [, row] = purchaseReceiveOrderDetailsRows(result([receipt()]));

    expect((row.delivered ?? 0) - (row.rejected ?? 0)).toBe(row.accepted);
  });

  it('RO-10: a receipt with no order has no order link', () => {
    const [heading] = purchaseReceiveOrderDetailsRows(result([receipt({ purchaseOrderId: null, purchaseOrderNo: null })]));

    expect([heading.orderNo, heading.orderLink]).toEqual([null, null]);
  });

  it('RO-11: a receipt in another currency names it and gives its values in base too, and one in the branch’s does not', () => {
    const foreign = purchaseReceiveOrderDetailsRows(
      result([receipt({ currencyCode: 'USD', value: 800, valueBase: 64000, lines: [line({ value: 500, valueBase: 40000 })] })]),
    );

    expect(foreign[0].currency).toBe('USD');
    expect(foreign[1]).toMatchObject({ currency: 'USD', value: 500, valueBase: 40000 });
    expect(foreign[2]).toMatchObject({ value: 800, valueBase: 64000 });

    const home = purchaseReceiveOrderDetailsRows(result([receipt()]));
    expect(home[0].currency).toBeNull();
    expect(home[1].valueBase).toBeNull();
  });

  it('RO-12: the total row is the server’s figure, and says it is of all when not every receipt is loaded', () => {
    const rows = purchaseReceiveOrderDetailsRows(result([receipt()], { totalReceipts: 120, totals: { lines: 300, value: 99 } }));

    expect(rows[rows.length - 1]).toMatchObject({ name: 'Total of all 120 receipts', value: 99 });
  });

  it('RO-13: with no receipts there are no rows, not even the total', () => {
    expect(purchaseReceiveOrderDetailsRows(result([]))).toEqual([]);
  });

  it('RO-14: the next page is added to the ones loaded and the totals are the latest answer’s', () => {
    const first = result([receipt({ goodsReceiptId: 1 })], { totalReceipts: 3, pageNumber: 1 });
    const next = result([receipt({ goodsReceiptId: 2 })], { totalReceipts: 3, pageNumber: 2 });
    next.totals.value = 9;

    const merged = appendPurchaseReceiveOrderDetailsPage(first, next);

    expect(merged.receipts.map((r) => r.goodsReceiptId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.value).toBe(9);
  });

  it('RO-15: there is more while fewer receipts are loaded than match', () => {
    expect(hasMorePurchaseReceiveOrderDetails({ receipts: [receipt()], totalReceipts: 2 })).toBe(true);
    expect(hasMorePurchaseReceiveOrderDetails({ receipts: [receipt(), receipt({ goodsReceiptId: 2 })], totalReceipts: 2 })).toBe(false);
  });

  it('RO-16: a line with very many serials lists the first of them and says how many are not listed', () => {
    const [, row] = purchaseReceiveOrderDetailsRows(result([receipt({ lines: [line({ serials: ['S-001', 'S-002'], serialCount: 60 })] })]));

    expect(serialsNotListed(row)).toBe(58);
    expect(serialsNotListed({ serials: ['S-1', 'S-2'], serialCount: 2 })).toBe(0);
  });

  it('RO-17: the sentence says how many receipts and lines and what the accepted goods are worth', () => {
    const answer = result([receipt()], { totals: { lines: 7, value: 114166.67 }, totalReceipts: 5 });

    expect(purchaseReceiveOrderDetailsSentence(answer, money)).toBe('5 receipts with 7 lines: the goods accepted into stock are worth ₹114166.67.');
    expect(purchaseReceiveOrderDetailsSentence(result([receipt()]), money)).toMatch(/^1 receipt with 1 line:/);
  });
});
