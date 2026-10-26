import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  SalesAnalysisDetailGroup,
  SalesAnalysisDetailLine,
  SalesAnalysisDetailResult,
  appendSalesAnalysisDetailPage,
  detailDocumentLink,
  hasMoreSalesAnalysisDetail,
  loadedDetailLines,
  salesAnalysisDetailRows,
  salesAnalysisDetailSentence,
} from './sales-analysis-detail.models';
import { SalesAnalysisDetailService } from './sales-analysis-detail.service';

/** The Sales Analysis Detail Report's calls and the helpers its page uses (TK-229). */

function setup(answer: unknown = { groups: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [SalesAnalysisDetailService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(SalesAnalysisDetailService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const line = (over: Partial<SalesAnalysisDetailLine> = {}): SalesAnalysisDetailLine => ({
  lineKey: 9802,
  typeCode: 'INV',
  documentId: 9401,
  documentNo: 'INV-9401',
  documentDate: '2026-10-02',
  lineNumber: 2,
  contactId: 9103,
  contactName: 'Zenith Traders',
  itemId: 9602,
  itemCode: 'GADGET-B',
  itemName: 'Gadget B',
  quantity: 4,
  unitPrice: 1000,
  gross: 4000,
  discount: 0,
  net: 4000,
  cost: 2000,
  costPending: false,
  margin: 2000,
  ...over,
});

const group = (lines: SalesAnalysisDetailLine[], over: Partial<SalesAnalysisDetailGroup> = {}): SalesAnalysisDetailGroup => ({
  key: 9602,
  code: 'GADGET-B',
  name: 'Gadget B',
  lines,
  lineCount: lines.length,
  subtotal: {
    key: 9602,
    code: 'GADGET-B',
    name: 'Gadget B',
    quantity: lines.reduce((t, l) => t + l.quantity, 0),
    gross: lines.reduce((t, l) => t + l.gross, 0),
    discount: lines.reduce((t, l) => t + l.discount, 0),
    net: lines.reduce((t, l) => t + l.net, 0),
    cost: lines.reduce((t, l) => t + l.cost, 0),
    costPendingLines: lines.filter((l) => l.costPending).length,
    margin: 2000,
    marginPercent: 50,
    compareNet: null,
    changePercent: null,
  },
  ...over,
});

const result = (groups: SalesAnalysisDetailGroup[], over: Partial<SalesAnalysisDetailResult> = {}): SalesAnalysisDetailResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  groupBy: 'Item',
  includesCreditNotes: true,
  groups,
  totalLines: groups.reduce((t, g) => t + g.lineCount, 0),
  pageNumber: 1,
  pageSize: 50,
  totals: {
    quantity: 4,
    gross: 4000,
    discount: 0,
    net: 4000,
    cost: 2000,
    costPendingLines: 0,
    margin: 2000,
    marginPercent: 50,
    compareNet: null,
    changePercent: null,
  },
  ...over,
});

describe('SalesAnalysisDetailService', () => {
  it('SD-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/sales-analysis-detail-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['groupBy', 'itemId', 'itemCategoryId', 'contactId', 'warehouseId', 'includeCreditNotes', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('SD-02: sends the grouping, filters and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      groupBy: 'Customer',
      itemId: 9601,
      itemCategoryId: 9701,
      contactId: 9103,
      warehouseId: 9201,
      includeCreditNotes: false,
      pageNumber: 2,
      pageSize: 25,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('groupBy')).toBe('Customer');
    expect(params.get('itemId')).toBe('9601');
    expect(params.get('itemCategoryId')).toBe('9701');
    expect(params.get('contactId')).toBe('9103');
    expect(params.get('warehouseId')).toBe('9201');
    expect(params.get('includeCreditNotes')).toBe('false');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('SD-03: gives back what the endpoint answered', async () => {
    const answer = result([group([line()])]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('SD-04: reads the customers, items, categories and warehouses the filters offer from the report’s own lookups', async () => {
    for (const [method, path] of [
      ['customers', 'customers'],
      ['items', 'items'],
      ['itemCategories', 'item-categories'],
      ['warehouses', 'warehouses'],
    ] as const) {
      TestBed.resetTestingModule();
      const answer = [{ id: 1 }];
      const { service, get } = setup(answer);

      expect(await service[method]()).toEqual(answer as never);
      expect(get.mock.calls[0][0]).toBe(`/api/statements/sales-analysis-detail-report/${path}`);
    }
  });
});

describe('sales analysis detail helpers', () => {
  it('SD-05: a line links to its invoice or its credit note', () => {
    expect(detailDocumentLink({ typeCode: 'INV', documentId: 7 })).toBe('/sales/invoices/7');
    expect(detailDocumentLink({ typeCode: 'POS', documentId: 8 })).toBe('/sales/invoices/8');
    expect(detailDocumentLink({ typeCode: 'CRN', documentId: 9 })).toBe('/sales/credit-notes/9');
    expect(detailDocumentLink({ typeCode: 'XYZ', documentId: 1 })).toBeNull();
  });

  it('SD-06: a group is a heading, its lines and its subtotal, and then the total closes the grid', () => {
    const rows = salesAnalysisDetailRows(result([group([line(), line({ lineKey: 9803, documentNo: 'INV-9402' })])]));

    expect(rows.map((r) => r.kind)).toEqual(['group', 'line', 'line', 'subtotal', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(5);
    expect(rows[0]).toMatchObject({ name: 'Gadget B', code: 'GADGET-B', lineCount: 2 });
    expect(rows[3].name).toBe('Subtotal, 2 lines');
    expect(rows[4]).toMatchObject({ name: 'Total', net: 4000 });
  });

  it('SD-07: a line carries its document, date, figures and link', () => {
    const [, row] = salesAnalysisDetailRows(result([group([line()])]));

    expect(row).toMatchObject({
      kind: 'line',
      name: 'INV-9401',
      link: '/sales/invoices/9401',
      date: '2026-10-02',
      quantity: 4,
      unitPrice: 1000,
      discount: 0,
      net: 4000,
      cost: 2000,
      margin: 2000,
      costPending: false,
    });
  });

  it('SD-08: a line shows the customer, or the item when the lines are grouped by customer', () => {
    expect(salesAnalysisDetailRows(result([group([line()])]))[1].who).toBe('Zenith Traders');
    expect(salesAnalysisDetailRows(result([group([line()])], { groupBy: 'Customer' }))[1].who).toBe('Gadget B');
  });

  it('SD-09: a line whose cost is pending says so and has no margin, and so does its subtotal', () => {
    const pending = line({ costPending: true, margin: null, cost: 0 });
    const rows = salesAnalysisDetailRows(result([group([pending], { subtotal: { ...group([pending]).subtotal, margin: null, marginPercent: null } })]));

    expect([rows[1].costPending, rows[1].margin]).toEqual([true, null]);
    expect([rows[2].costPending, rows[2].margin]).toEqual([true, null]);
  });

  it('SD-10: a credit note line is negative and links to the credit note', () => {
    const [, row] = salesAnalysisDetailRows(
      result([group([line({ lineKey: -9901, typeCode: 'CRN', documentId: 9421, documentNo: 'CRN-9421', quantity: -1, net: -1000 })])]),
    );

    expect([row.link, row.quantity, row.net]).toEqual(['/sales/credit-notes/9421', -1, -1000]);
  });

  it('SD-11: a subtotal says how many lines its group has in all, even when not all are loaded', () => {
    const partial = group([line()], { lineCount: 5 });
    const rows = salesAnalysisDetailRows(result([partial], { totalLines: 5 }));

    expect(rows[2]).toMatchObject({ name: 'Subtotal, 5 lines', lineCount: 5 });
    expect(salesAnalysisDetailRows(result([group([line()])]))[2].name).toBe('Subtotal, 1 line');
  });

  it('SD-12: with no groups there are no rows, not even the total', () => {
    expect(salesAnalysisDetailRows(result([]))).toEqual([]);
  });

  it('SD-13: the next page joins a group that ran over the break and adds the others after', () => {
    const first = result([group([line({ lineKey: 1 })], { lineCount: 3 })], { totalLines: 5, pageNumber: 1 });
    const next = result(
      [group([line({ lineKey: 2 }), line({ lineKey: 3 })], { lineCount: 3 }), group([line({ lineKey: 4 })], { key: 9601, name: 'Widget A', lineCount: 2 })],
      { totalLines: 5, pageNumber: 2 },
    );

    const merged = appendSalesAnalysisDetailPage(first, next);

    expect(merged.groups.map((g) => [g.key, g.lines.map((l) => l.lineKey)])).toEqual([
      [9602, [1, 2, 3]],
      [9601, [4]],
    ]);
    expect(merged.pageNumber).toBe(2);
  });

  it('SD-14: a group that does not run over the break is added as it is', () => {
    const first = result([group([line({ lineKey: 1 })])], { totalLines: 2 });
    const next = result([group([line({ lineKey: 2 })], { key: 9601, name: 'Widget A' })], { totalLines: 2, pageNumber: 2 });

    expect(appendSalesAnalysisDetailPage(first, next).groups.map((g) => g.key)).toEqual([9602, 9601]);
  });

  it('SD-15: there is more while fewer lines are loaded than match', () => {
    const two = result([group([line({ lineKey: 1 }), line({ lineKey: 2 })])]);

    expect(loadedDetailLines(two)).toBe(2);
    expect(hasMoreSalesAnalysisDetail({ groups: two.groups, totalLines: 5 })).toBe(true);
    expect(hasMoreSalesAnalysisDetail({ groups: two.groups, totalLines: 2 })).toBe(false);
  });

  it('SD-16: the sentence gives net sales, cost and margin when every cost is settled', () => {
    expect(salesAnalysisDetailSentence(result([group([line()])]), money)).toBe('1 line: net sales ₹4000.00, cost ₹2000.00, margin ₹2000.00.');
  });

  it('SD-17: the sentence says so, and gives no margin, while any cost is pending', () => {
    const answer = result([group([line()])], {
      totalLines: 3,
      totals: { quantity: 1, gross: 1, discount: 0, net: 1000, cost: 0, costPendingLines: 2, margin: null, marginPercent: null, compareNet: null, changePercent: null },
    });

    expect(salesAnalysisDetailSentence(answer, money)).toBe('3 lines: net sales ₹1000.00. 2 lines are not costed yet, so the margin is not known yet.');
  });
});
