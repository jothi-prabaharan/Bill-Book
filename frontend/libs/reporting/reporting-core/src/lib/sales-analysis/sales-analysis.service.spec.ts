import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  SALES_ANALYSIS_GROUPINGS,
  SalesAnalysisGroup,
  SalesAnalysisResult,
  appendSalesAnalysisPage,
  costPendingText,
  groupingHeading,
  hasMoreSalesAnalysis,
  salesAnalysisRows,
  salesAnalysisSentence,
  warehouseChoiceLabel,
} from './sales-analysis.models';
import { SalesAnalysisService } from './sales-analysis.service';

/** The Sales Analysis's calls and the helpers its page uses (TK-228). */

function setup(answer: unknown = { groups: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [SalesAnalysisService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(SalesAnalysisService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const group = (over: Partial<SalesAnalysisGroup> = {}): SalesAnalysisGroup => ({
  key: 9602,
  code: 'GADGET-B',
  name: 'Gadget B',
  quantity: 21,
  gross: 24000,
  discount: 1000,
  net: 23000,
  cost: 10500,
  costPendingLines: 0,
  margin: 12500,
  marginPercent: 54.35,
  compareNet: null,
  changePercent: null,
  ...over,
});

const result = (groups: SalesAnalysisGroup[], over: Partial<SalesAnalysisResult> = {}): SalesAnalysisResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  compareFromDate: null,
  compareToDate: null,
  baseCurrencyCode: 'INR',
  groupBy: 'Item',
  includesCreditNotes: true,
  groups,
  totalGroups: groups.length,
  pageNumber: 1,
  pageSize: 50,
  totals: {
    quantity: groups.reduce((t, g) => t + g.quantity, 0),
    gross: groups.reduce((t, g) => t + g.gross, 0),
    discount: groups.reduce((t, g) => t + g.discount, 0),
    net: groups.reduce((t, g) => t + g.net, 0),
    cost: groups.reduce((t, g) => t + g.cost, 0),
    costPendingLines: groups.reduce((t, g) => t + g.costPendingLines, 0),
    margin: 12500,
    marginPercent: 54.35,
    compareNet: null,
    changePercent: null,
  },
  ...over,
});

describe('SalesAnalysisService', () => {
  it('SA-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/sales-analysis');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['groupBy', 'contactId', 'itemCategoryId', 'warehouseId', 'includeCreditNotes', 'compareFromDate', 'compareToDate', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('SA-02: sends the grouping, filters, comparison period and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      groupBy: 'Customer',
      contactId: 9103,
      itemCategoryId: 9702,
      warehouseId: 9201,
      compareFromDate: '2026-09-01',
      compareToDate: '2026-09-30',
      pageNumber: 2,
      pageSize: 25,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('groupBy')).toBe('Customer');
    expect(params.get('contactId')).toBe('9103');
    expect(params.get('itemCategoryId')).toBe('9702');
    expect(params.get('warehouseId')).toBe('9201');
    expect(params.get('compareFromDate')).toBe('2026-09-01');
    expect(params.get('compareToDate')).toBe('2026-09-30');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('SA-03: credit notes are left to the server’s default when on, and sent only when turned off', async () => {
    const on = setup();
    await on.service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', includeCreditNotes: true });
    expect(paramsOf(on.get.mock.calls[0]).has('includeCreditNotes')).toBe(false);

    TestBed.resetTestingModule();
    const off = setup();
    await off.service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', includeCreditNotes: false });
    expect(paramsOf(off.get.mock.calls[0]).get('includeCreditNotes')).toBe('false');
  });

  it('SA-04: gives back what the endpoint answered', async () => {
    const answer = result([group()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('SA-05: reads the customers, categories and warehouses the filters offer from the report’s own lookups', async () => {
    const customers = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const first = setup(customers);
    expect(await first.service.customers()).toEqual(customers);
    expect(first.get.mock.calls[0][0]).toBe('/api/statements/sales-analysis/customers');

    TestBed.resetTestingModule();
    const categories = [{ itemCategoryId: 9701, categoryCode: 'HW', categoryName: 'Hardware' }];
    const second = setup(categories);
    expect(await second.service.itemCategories()).toEqual(categories);
    expect(second.get.mock.calls[0][0]).toBe('/api/statements/sales-analysis/item-categories');

    TestBed.resetTestingModule();
    const warehouses = [{ warehouseId: 9201, warehouseCode: 'MAIN', warehouseName: 'Main warehouse' }];
    const third = setup(warehouses);
    expect(await third.service.warehouses()).toEqual(warehouses);
    expect(third.get.mock.calls[0][0]).toBe('/api/statements/sales-analysis/warehouses');
  });
});

describe('sales analysis helpers', () => {
  it('SA-06: the groupings are item, category and customer, and head the first column', () => {
    expect(SALES_ANALYSIS_GROUPINGS.map((g) => g.value)).toEqual(['Item', 'Category', 'Customer']);
    expect(groupingHeading('Category')).toBe('Item category');
    expect(groupingHeading('Customer')).toBe('Customer');
  });

  it('SA-07: a warehouse reads by name and code', () => {
    expect(warehouseChoiceLabel({ warehouseId: 1, warehouseCode: 'MAIN', warehouseName: 'Main warehouse' })).toBe('Main warehouse (MAIN)');
    expect(warehouseChoiceLabel({ warehouseId: 1, warehouseCode: '', warehouseName: 'Main warehouse' })).toBe('Main warehouse');
  });

  it('SA-08: the grid has a row for each group and then the totals of everything matched', () => {
    const rows = salesAnalysisRows(result([group(), group({ key: 9601, code: 'WIDGET-A', name: 'Widget A' })]));

    expect(rows.map((r) => r.kind)).toEqual(['group', 'group', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
    expect(rows[2]).toMatchObject({ name: 'Total', net: 46000, code: '' });
  });

  it('SA-09: a group row carries its figures as the server sent them', () => {
    const [row] = salesAnalysisRows(result([group()]));

    expect(row).toMatchObject({
      name: 'Gadget B',
      code: 'GADGET-B',
      quantity: 21,
      gross: 24000,
      discount: 1000,
      net: 23000,
      cost: 10500,
      margin: 12500,
      marginPercent: 54.35,
    });
  });

  it('SA-10: a group with a cost still pending has no margin and says how many lines are waiting', () => {
    const [row] = salesAnalysisRows(result([group({ costPendingLines: 2, margin: null, marginPercent: null })]));

    expect([row.margin, row.marginPercent, row.costPendingLines]).toEqual([null, null, 2]);
    expect(costPendingText(1)).toBe('1 line not costed yet');
    expect(costPendingText(3)).toBe('3 lines not costed yet');
  });

  it('SA-11: the totals row is the server’s figures, and says it is of all when not every group is loaded', () => {
    const rows = salesAnalysisRows(result([group()], { totalGroups: 120 }));

    expect(rows[rows.length - 1].name).toBe('Total of all 120');
  });

  it('SA-12: a comparison period gives each row its net then and its change', () => {
    const [row] = salesAnalysisRows(result([group({ compareNet: 20000, changePercent: 15 })]));

    expect([row.compareNet, row.changePercent]).toEqual([20000, 15]);
  });

  it('SA-13: with no groups there are no rows, not even the totals', () => {
    expect(salesAnalysisRows(result([]))).toEqual([]);
  });

  it('SA-14: the next page is added to the ones loaded and the totals are the latest answer’s', () => {
    const first = result([group({ key: 1 })], { totalGroups: 3, pageNumber: 1 });
    const next = result([group({ key: 2 })], { totalGroups: 3, pageNumber: 2 });
    next.totals.net = 99;

    const merged = appendSalesAnalysisPage(first, next);

    expect(merged.groups.map((g) => g.key)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.net).toBe(99);
  });

  it('SA-15: there is more while fewer groups are loaded than match', () => {
    expect(hasMoreSalesAnalysis({ groups: [group()], totalGroups: 2 })).toBe(true);
    expect(hasMoreSalesAnalysis({ groups: [group(), group({ key: 2 })], totalGroups: 2 })).toBe(false);
  });

  it('SA-16: the sentence gives net sales, cost and margin when every cost is settled', () => {
    const answer = result([group()], { totalGroups: 1 });

    expect(salesAnalysisSentence(answer, money)).toBe('1 item: net sales ₹23000.00, cost ₹10500.00, margin ₹12500.00 (54.35%).');
    expect(salesAnalysisSentence({ ...answer, groupBy: 'Customer', totalGroups: 2 }, money)).toMatch(/^2 customers: /);
    expect(salesAnalysisSentence({ ...answer, groupBy: 'Category', totalGroups: 2 }, money)).toMatch(/^2 categories: /);
  });

  it('SA-17: the sentence says so, and gives no margin, while any cost is pending', () => {
    const answer = result([group({ costPendingLines: 1 })], {
      totalGroups: 1,
      totals: { quantity: 1, gross: 1, discount: 0, net: 1000, cost: 0, costPendingLines: 1, margin: null, marginPercent: null, compareNet: null, changePercent: null },
    });

    expect(salesAnalysisSentence(answer, money)).toBe('1 item: net sales ₹1000.00. 1 line not costed yet, so the margin is not known yet.');
  });
});
