import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  InventoryItemDetailGroup,
  InventoryItemDetailResult,
  appendInventoryItemDetailPage,
  hasMoreInventoryItemDetail,
  inventoryItemDetailSentence,
  itemDetailRows,
} from './inventory-item-detail-report.models';
import { InventoryItemDetailService } from './inventory-item-detail-report.service';

/** The Inventory Item Detail's calls and the helpers its page uses (TK-244). */

function setup(answer: unknown = { items: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [InventoryItemDetailService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(InventoryItemDetailService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const itemGroup = (over: Partial<InventoryItemDetailGroup> = {}): InventoryItemDetailGroup => ({
  itemId: 9631,
  itemCode: 'STK-A',
  itemName: 'Steel bar',
  categoryName: 'Hardware',
  costingType: 'WeightedAverage',
  unitCode: 'PCS',
  openingQuantity: 80,
  openingValue: 4000,
  inQuantity: 42,
  inValue: 2305,
  outQuantity: 38,
  outValue: 2000,
  closingQuantity: 84,
  closingValue: 4305,
  costPending: false,
  movements: [
    {
      stockMovementId: 101,
      date: '2026-10-03',
      documentNo: 'GRN-001',
      source: 'Goods Receipt',
      sourceType: 'GRN',
      sourceId: 501,
      contactCode: 'VND-01',
      contactName: 'Steel Corp',
      warehouseName: 'Main WH',
      direction: 'In',
      quantityIn: 40,
      quantityOut: null,
      quantitySigned: 40,
      unitCost: 55,
      value: 2200,
      unitSalePrice: null,
      margin: null,
      marginPercent: null,
      runningQuantity: 120,
      runningValue: 6200,
      costPending: false,
      notes: null,
    },
    {
      stockMovementId: 102,
      date: '2026-10-10',
      documentNo: 'INV-001',
      source: 'Invoice',
      sourceType: 'INV',
      sourceId: 601,
      contactCode: 'CUS-01',
      contactName: 'Apex Builders',
      warehouseName: 'Main WH',
      direction: 'Out',
      quantityIn: null,
      quantityOut: 30,
      quantitySigned: -30,
      unitCost: 52.5,
      value: -1575,
      unitSalePrice: 80,
      margin: 27.5,
      marginPercent: 34.38,
      runningQuantity: 90,
      runningValue: 4625,
      costPending: false,
      notes: null,
    },
  ],
  ...over,
});

const result = (items: InventoryItemDetailGroup[], over: Partial<InventoryItemDetailResult> = {}): InventoryItemDetailResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  items,
  totalItems: items.length,
  totalMovements: items.reduce((t, i) => t + i.movements.length, 0),
  pageNumber: 1,
  pageSize: 25,
  totals: {
    openingValue: items.reduce((t, i) => t + i.openingValue, 0),
    inValue: items.reduce((t, i) => t + i.inValue, 0),
    outValue: items.reduce((t, i) => t + i.outValue, 0),
    closingValue: items.reduce((t, i) => t + i.closingValue, 0),
    totalMovements: items.reduce((t, i) => t + i.movements.length, 0),
    costPending: items.some((i) => i.costPending),
  },
  ...over,
});

describe('InventoryItemDetailService', () => {
  it('ID-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/inventory-item-detail');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['itemCategoryId', 'itemId', 'warehouseId', 'source', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('ID-02: sends the itemId, category, warehouse, source, and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      itemId: 9631,
      itemCategoryId: 9701,
      warehouseId: 50,
      source: 'Receipt',
      pageNumber: 2,
      pageSize: 25,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('itemId')).toBe('9631');
    expect(params.get('itemCategoryId')).toBe('9701');
    expect(params.get('warehouseId')).toBe('50');
    expect(params.get('source')).toBe('Receipt');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('ID-03: fetches items from .../items', async () => {
    const { service, get } = setup([{ itemId: 9631, itemCode: 'STK-A', itemName: 'Steel bar' }]);

    const items = await service.items();

    expect(get.mock.calls[0][0]).toBe('/api/statements/inventory-item-detail/items');
    expect(items).toEqual([{ itemId: 9631, itemCode: 'STK-A', itemName: 'Steel bar' }]);
  });

  it('ID-04: fetches item categories from .../item-categories', async () => {
    const { service, get } = setup([{ itemCategoryId: 9701, categoryName: 'Hardware' }]);

    const categories = await service.itemCategories();

    expect(get.mock.calls[0][0]).toBe('/api/statements/inventory-item-detail/item-categories');
    expect(categories).toEqual([{ itemCategoryId: 9701, categoryName: 'Hardware' }]);
  });

  it('ID-05: fetches warehouses from .../warehouses', async () => {
    const { service, get } = setup([{ warehouseId: 50, warehouseCode: 'WH-1', warehouseName: 'Main WH' }]);

    const warehouses = await service.warehouses();

    expect(get.mock.calls[0][0]).toBe('/api/statements/inventory-item-detail/warehouses');
    expect(warehouses).toEqual([{ warehouseId: 50, warehouseCode: 'WH-1', warehouseName: 'Main WH' }]);
  });

  it('ID-06: itemDetailRows builds opening row, movement rows, and closing row', () => {
    const group = itemGroup();
    const rows = itemDetailRows(group, '2026-10-01', '2026-10-31');

    expect(rows.length).toBe(4); // 1 opening + 2 movements + 1 closing
    expect(rows[0].kind).toBe('opening');
    expect(rows[0].source).toBe('Opening Balance');
    expect(rows[0].runningQuantity).toBe(80);
    expect(rows[0].runningValue).toBe(4000);

    expect(rows[1].kind).toBe('movement');
    expect(rows[1].documentNo).toBe('GRN-001');
    expect(rows[1].quantityIn).toBe(40);
    expect(rows[1].runningQuantity).toBe(120);

    expect(rows[2].kind).toBe('movement');
    expect(rows[2].documentNo).toBe('INV-001');
    expect(rows[2].quantityOut).toBe(30);
    expect(rows[2].runningQuantity).toBe(90);

    expect(rows[3].kind).toBe('closing');
    expect(rows[3].source).toBe('Closing Balance');
    expect(rows[3].runningQuantity).toBe(84);
    expect(rows[3].runningValue).toBe(4305);
  });

  it('ID-07: hasMoreInventoryItemDetail returns true when items < totalItems', () => {
    const res = result([itemGroup()], { totalItems: 10 });
    expect(hasMoreInventoryItemDetail(res)).toBe(true);

    const full = result([itemGroup()], { totalItems: 1 });
    expect(hasMoreInventoryItemDetail(full)).toBe(false);
  });

  it('ID-08: appendInventoryItemDetailPage appends non-duplicate items', () => {
    const current = result([itemGroup({ itemId: 1 })], { totalItems: 2 });
    const next = result([itemGroup({ itemId: 2 })], { totalItems: 2, pageNumber: 2 });

    const combined = appendInventoryItemDetailPage(current, next);
    expect(combined.items.map((i) => i.itemId)).toEqual([1, 2]);
    expect(combined.pageNumber).toBe(2);
  });

  it('ID-09: inventoryItemDetailSentence formats summary sentence properly', () => {
    const res = result([itemGroup()], { totalItems: 1, totalMovements: 2 });
    const sentence = inventoryItemDetailSentence(res, money);
    expect(sentence).toContain('Showing 2 stock movements across 1 item');
    expect(sentence).toContain('closing at a total value of ₹4305.00 INR');
  });
});
