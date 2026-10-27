import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  InventoryItemSummaryItem,
  InventoryItemSummaryResult,
  appendInventoryItemSummaryPage,
  costingMethodLabel,
  hasMoreInventoryItemSummary,
  inventoryItemSummaryRows,
  inventoryItemSummarySentence,
} from './inventory-item-summary.models';
import { InventoryItemSummaryService } from './inventory-item-summary.service';

/** The Inventory Item Summary's calls and the helpers its page uses (TK-243). */

function setup(answer: unknown = { items: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [InventoryItemSummaryService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(InventoryItemSummaryService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const item = (over: Partial<InventoryItemSummaryItem> = {}): InventoryItemSummaryItem => ({
  itemId: 9631,
  itemCode: 'STK-A',
  itemName: 'Steel bar',
  categoryName: 'Hardware',
  costingType: 'WeightedAverage',
  unitCode: 'PCS',
  openingQuantity: 80,
  openingValue: 4000,
  purchasedQuantity: 35,
  purchasedValue: 1925,
  soldQuantity: 28,
  salesValue: 2240,
  cogs: 1470,
  profit: 770,
  adjustedQuantity: -3,
  adjustedValue: -150,
  closingQuantity: 84,
  closingValue: 4305,
  costPending: false,
  ...over,
});

const result = (items: InventoryItemSummaryItem[], over: Partial<InventoryItemSummaryResult> = {}): InventoryItemSummaryResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  items,
  totalItems: items.length,
  pageNumber: 1,
  pageSize: 25,
  totals: {
    openingValue: items.reduce((t, i) => t + i.openingValue, 0),
    purchasedValue: items.reduce((t, i) => t + i.purchasedValue, 0),
    salesValue: items.reduce((t, i) => t + i.salesValue, 0),
    cogs: items.reduce((t, i) => t + i.cogs, 0),
    profit: items.reduce((t, i) => t + (i.profit ?? 0), 0),
    adjustedValue: items.reduce((t, i) => t + i.adjustedValue, 0),
    closingValue: items.reduce((t, i) => t + i.closingValue, 0),
    costPending: items.some((i) => i.costPending),
  },
  ...over,
});

describe('InventoryItemSummaryService', () => {
  it('IS-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/inventory-item-summary');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['itemCategoryId', 'itemId', 'showItemsWithNoMovement', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('IS-02: sends the category, item, tick and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', itemCategoryId: 9701, itemId: 9631, showItemsWithNoMovement: true, pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('itemCategoryId')).toBe('9701');
    expect(params.get('itemId')).toBe('9631');
    expect(params.get('showItemsWithNoMovement')).toBe('true');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('IS-03: leaves the no-movement tick out of the request unless it is on', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', showItemsWithNoMovement: false });

    expect(paramsOf(get.mock.calls[0]).has('showItemsWithNoMovement')).toBe(false);
  });

  it('IS-04: gives back what the endpoint answered', async () => {
    const answer = result([item()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('IS-05: reads the items and categories the filters offer from the report’s own lookups', async () => {
    const { service, get } = setup([]);

    await service.items();
    await service.itemCategories();

    expect(get.mock.calls.map((c) => c[0])).toEqual([
      '/api/statements/inventory-item-summary/items',
      '/api/statements/inventory-item-summary/item-categories',
    ]);
  });
});

describe('inventory item summary helpers', () => {
  it('IS-06: a costing method reads as words', () => {
    expect(costingMethodLabel('WeightedAverage')).toBe('Weighted average');
    expect(costingMethodLabel('Fifo')).toBe('FIFO');
    expect(costingMethodLabel('SpecificIdentification')).toBe('Specific identification');
    expect(costingMethodLabel('None')).toBe('Not costed');
  });

  it('IS-07: an item is a row of its own and the total closes the grid', () => {
    const rows = inventoryItemSummaryRows(result([item(), item({ itemId: 9632, itemName: 'Copper wire', itemCode: 'STK-B' })]));

    expect(rows.map((r) => r.kind)).toEqual(['item', 'item', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
    expect(rows[0]).toMatchObject({ name: 'Steel bar', code: 'STK-A', categoryName: 'Hardware', costing: 'Weighted average', unitCode: 'PCS' });
    expect(rows[2].name).toBe('Total');
  });

  it('IS-08: an item carries its quantities and values as sent', () => {
    const [row] = inventoryItemSummaryRows(result([item()]));

    expect(row).toMatchObject({
      openingQuantity: 80,
      openingValue: 4000,
      purchasedQuantity: 35,
      purchasedValue: 1925,
      soldQuantity: 28,
      salesValue: 2240,
      cogs: 1470,
      profit: 770,
      adjustedQuantity: -3,
      adjustedValue: -150,
      closingQuantity: 84,
      closingValue: 4305,
    });
  });

  it('IS-09: opening plus purchased less sold plus adjusted is the closing quantity and value on an item as laid out', () => {
    const [row] = inventoryItemSummaryRows(result([item()]));

    expect((row.openingQuantity ?? 0) + (row.purchasedQuantity ?? 0) - (row.soldQuantity ?? 0) + (row.adjustedQuantity ?? 0)).toBe(row.closingQuantity);
    expect(row.openingValue + row.purchasedValue - row.cogs + row.adjustedValue).toBe(row.closingValue);
  });

  it('IS-10: an item with a movement not yet costed has no profit and is marked', () => {
    const [row] = inventoryItemSummaryRows(result([item({ profit: null, costPending: true })]));

    expect([row.profit, row.costPending]).toEqual([null, true]);
  });

  it('IS-11: the total row is the server’s figures with no quantities, and says it is of all when not every item is loaded', () => {
    const rows = inventoryItemSummaryRows(
      result([item()], {
        totalItems: 120,
        totals: { openingValue: 1, purchasedValue: 2, salesValue: 3, cogs: 4, profit: null, adjustedValue: 6, closingValue: 7, costPending: true },
      }),
    );

    expect(rows[rows.length - 1]).toMatchObject({
      name: 'Total of all 120 items',
      openingQuantity: null,
      closingQuantity: null,
      openingValue: 1,
      salesValue: 3,
      cogs: 4,
      profit: null,
      closingValue: 7,
      costPending: true,
    });
  });

  it('IS-12: with no items there are no rows, not even the total', () => {
    expect(inventoryItemSummaryRows(result([]))).toEqual([]);
  });

  it('IS-13: the next page is added to the ones loaded and the totals are the latest answer’s', () => {
    const first = result([item({ itemId: 1 })], { totalItems: 3, pageNumber: 1 });
    const next = result([item({ itemId: 2 })], { totalItems: 3, pageNumber: 2 });
    next.totals.closingValue = 9;

    const merged = appendInventoryItemSummaryPage(first, next);

    expect(merged.items.map((i) => i.itemId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.closingValue).toBe(9);
  });

  it('IS-14: there is more while fewer items are loaded than match', () => {
    expect(hasMoreInventoryItemSummary({ items: [item()], totalItems: 2 })).toBe(true);
    expect(hasMoreInventoryItemSummary({ items: [item(), item({ itemId: 2 })], totalItems: 2 })).toBe(false);
  });

  it('IS-15: the sentence says how many items and what the stock was worth at the start and the end, and warns of an uncosted movement', () => {
    const answer = result([item()], {
      totalItems: 4,
      totals: { openingValue: 6000, purchasedValue: 2865, salesValue: 3330, cogs: 1785, profit: null, adjustedValue: -150, closingValue: 6930, costPending: true },
    });

    expect(inventoryItemSummarySentence(answer, money)).toBe(
      '4 items: stock worth ₹6000.00 at the start and ₹6930.00 at the end. Some movements have not been costed yet, so these values are short by what they cost.',
    );
    expect(inventoryItemSummarySentence(result([item()]), money)).toMatch(/^1 item: stock worth/);
    expect(inventoryItemSummarySentence(result([item()]), money)).not.toMatch(/costed/);
  });
});
