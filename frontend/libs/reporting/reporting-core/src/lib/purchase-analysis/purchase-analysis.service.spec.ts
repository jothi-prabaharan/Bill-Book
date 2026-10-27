import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  PURCHASE_ANALYSIS_GROUPINGS,
  PurchaseAnalysisGroup,
  PurchaseAnalysisResult,
  appendPurchaseAnalysisPage,
  hasMorePurchaseAnalysis,
  purchaseAnalysisRows,
  purchaseAnalysisSentence,
  purchaseGroupingHeading,
} from './purchase-analysis.models';
import { PurchaseAnalysisService } from './purchase-analysis.service';

/** The Purchase Analysis's calls and the helpers its page uses (TK-239). */

function setup(answer: unknown = { groups: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [PurchaseAnalysisService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(PurchaseAnalysisService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const group = (over: Partial<PurchaseAnalysisGroup> = {}): PurchaseAnalysisGroup => ({
  key: 9612,
  code: 'BOLT-BOX',
  name: 'Bolt Box',
  quantity: 21,
  gross: 24000,
  discount: 1000,
  net: 23000,
  averageCost: 1095.238095,
  sharePercent: 28.75,
  compareNet: null,
  changePercent: null,
  ...over,
});

const result = (groups: PurchaseAnalysisGroup[], over: Partial<PurchaseAnalysisResult> = {}): PurchaseAnalysisResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  compareFromDate: null,
  compareToDate: null,
  baseCurrencyCode: 'INR',
  groupBy: 'Item',
  includesDebitNotes: true,
  includesFixedAssets: false,
  groups,
  totalGroups: groups.length,
  pageNumber: 1,
  pageSize: 50,
  totals: {
    quantity: groups.reduce((t, g) => t + g.quantity, 0),
    gross: groups.reduce((t, g) => t + g.gross, 0),
    discount: groups.reduce((t, g) => t + g.discount, 0),
    net: groups.reduce((t, g) => t + g.net, 0),
    compareNet: null,
    changePercent: null,
  },
  ...over,
});

describe('PurchaseAnalysisService', () => {
  it('PA-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/purchase-analysis');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['groupBy', 'contactId', 'itemCategoryId', 'includeDebitNotes', 'includeFixedAssets', 'compareFromDate', 'compareToDate', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('PA-02: sends the grouping, vendor, category, comparison period and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      groupBy: 'Vendor',
      contactId: 9111,
      itemCategoryId: 9712,
      compareFromDate: '2026-09-01',
      compareToDate: '2026-09-30',
      pageNumber: 2,
      pageSize: 50,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('groupBy')).toBe('Vendor');
    expect(params.get('contactId')).toBe('9111');
    expect(params.get('itemCategoryId')).toBe('9712');
    expect(params.get('compareFromDate')).toBe('2026-09-01');
    expect(params.get('compareToDate')).toBe('2026-09-30');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('50');
  });

  it('PA-03: sends debit notes only when turned off, and fixed assets only when asked for', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', includeDebitNotes: false, includeFixedAssets: true });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('includeDebitNotes')).toBe('false');
    expect(params.get('includeFixedAssets')).toBe('true');
  });

  it('PA-04: gives back what the endpoint answered', async () => {
    const answer = result([group()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('PA-05: reads the vendors and item categories the filters offer from the report’s own lookups', async () => {
    const vendors = [{ contactId: 9111, contactCode: 'STEELCO', contactName: 'Steelco Metals' }];
    const first = setup(vendors);
    expect(await first.service.vendors()).toEqual(vendors);
    expect(first.get.mock.calls[0][0]).toBe('/api/statements/purchase-analysis/vendors');

    TestBed.resetTestingModule();
    const categories = [{ itemCategoryId: 9712, categoryCode: 'FS', categoryName: 'Fasteners' }];
    const second = setup(categories);
    expect(await second.service.itemCategories()).toEqual(categories);
    expect(second.get.mock.calls[0][0]).toBe('/api/statements/purchase-analysis/item-categories');
  });
});

describe('purchase analysis helpers', () => {
  it('PA-06: the groupings are offered in order and each has a column heading', () => {
    expect(PURCHASE_ANALYSIS_GROUPINGS.map((g) => g.value)).toEqual(['Item', 'Category', 'Vendor']);
    expect(purchaseGroupingHeading('Item')).toBe('Item');
    expect(purchaseGroupingHeading('Category')).toBe('Item category');
    expect(purchaseGroupingHeading('Vendor')).toBe('Vendor');
  });

  it('PA-07: the grid has a row for each group and then the totals of everything the filters match', () => {
    const rows = purchaseAnalysisRows(result([group(), group({ key: 9611, code: 'STEEL-ROD', name: 'Steel Rod', net: 51000, sharePercent: 63.75 })]));

    expect(rows.map((r) => r.kind)).toEqual(['group', 'group', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
    expect(rows[2]).toMatchObject({ name: 'Total', net: 74000, averageCost: null, sharePercent: 100 });
  });

  it('PA-08: when not every group is loaded the totals row says it is of all of them', () => {
    const rows = purchaseAnalysisRows(result([group()], { totalGroups: 120 }));

    expect(rows[rows.length - 1].name).toBe('Total of all 120');
  });

  it('PA-09: a group row carries its figures as the server sent them, its average cost and its share', () => {
    const [row] = purchaseAnalysisRows(result([group({ compareNet: 20000, changePercent: 15 })]));

    expect(row).toMatchObject({
      kind: 'group',
      name: 'Bolt Box',
      code: 'BOLT-BOX',
      quantity: 21,
      gross: 24000,
      discount: 1000,
      net: 23000,
      averageCost: 1095.238095,
      sharePercent: 28.75,
      compareNet: 20000,
      changePercent: 15,
    });
  });

  it('PA-10: when the totals are nought the totals row has no share', () => {
    const rows = purchaseAnalysisRows(result([group({ net: 0 })], { totals: { quantity: 0, gross: 0, discount: 0, net: 0, compareNet: null, changePercent: null } }));

    expect(rows[1].sharePercent).toBeNull();
  });

  it('PA-11: with no groups there are no rows, not even the totals', () => {
    expect(purchaseAnalysisRows(result([]))).toEqual([]);
  });

  it('PA-12: the next page is added to the ones loaded and the totals are the latest answer’s', () => {
    const first = result([group({ key: 1 })], { totalGroups: 3, pageNumber: 1 });
    const next = result([group({ key: 2 })], { totalGroups: 3, pageNumber: 2, totals: { quantity: 9, gross: 9, discount: 0, net: 9, compareNet: null, changePercent: null } });

    const merged = appendPurchaseAnalysisPage(first, next);

    expect(merged.groups.map((g) => g.key)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.net).toBe(9);
  });

  it('PA-13: there is more while fewer groups are loaded than match', () => {
    expect(hasMorePurchaseAnalysis({ groups: [group()], totalGroups: 2 })).toBe(true);
    expect(hasMorePurchaseAnalysis({ groups: [group(), group({ key: 2 })], totalGroups: 2 })).toBe(false);
  });

  it('PA-14: the sentence says how many groups, and what was bought in all, by what it was added up by', () => {
    const answer = result([group(), group({ key: 2 })], { totals: { quantity: 42, gross: 81000, discount: 1000, net: 80000, compareNet: null, changePercent: null } });

    expect(purchaseAnalysisSentence(answer, money)).toBe('2 items: net purchases ₹80000.00 for 42 units, ₹81000.00 gross less ₹1000.00 discount.');
    expect(purchaseAnalysisSentence({ ...answer, groupBy: 'Vendor', totalGroups: 1 }, money)).toMatch(/^1 vendor: /);
    expect(purchaseAnalysisSentence({ ...answer, groupBy: 'Category', totalGroups: 3 }, money)).toMatch(/^3 categories: /);
  });
});
