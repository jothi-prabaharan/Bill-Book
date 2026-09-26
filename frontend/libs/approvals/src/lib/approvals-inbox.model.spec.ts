import { describe, expect, it } from 'vitest';
import { InboxItem, actionUrl, documentRoute, kindLabel, mergeInbox } from './approvals-inbox.model';

const item = (overrides: Partial<InboxItem>): InboxItem => ({
  service: 'purchase',
  document: 'purchase-orders',
  requestKind: 'PurchaseOrder',
  requestId: 12,
  documentNo: 'POR-12',
  documentDate: '2026-09-01',
  amount: 1000,
  label: 'Accountant',
  ...overrides,
});

describe('approvals inbox model', () => {
  it('acts on each service at the route its approval panel uses', () => {
    expect(actionUrl(item({}))).toBe('/api/purchase/purchase-orders/12/approval');
    expect(actionUrl(item({ service: 'accounting', document: 'journals', requestKind: 'ManualJournal' }))).toBe(
      '/api/journals/12/approval',
    );
    expect(actionUrl(item({ service: 'inventory', document: 'stock-adjustments', requestKind: 'StockAdjustment' }))).toBe(
      '/api/stock-adjustments/12/approval',
    );
    expect(actionUrl(item({ service: 'sales', document: 'credit-notes', requestKind: 'CreditNote' }))).toBe(
      '/api/sales/credit-notes/12/approval',
    );
    expect(actionUrl(item({ service: 'sales', document: 'invoices', requestKind: 'CreditLimitOverride' }))).toBe(
      '/api/sales/invoices/12/overrides/credit-limit/approval',
    );
    expect(actionUrl(item({ service: 'sales', document: 'sales-orders', requestKind: 'SalesDiscountOverride' }))).toBe(
      '/api/sales/sales-orders/12/overrides/discount/approval',
    );
  });

  it('opens each document on its own screen', () => {
    expect(documentRoute(item({}))).toBe('/purchase/purchase-orders/12');
    expect(documentRoute(item({ service: 'sales', document: 'invoices' }))).toBe('/sales/invoices/12');
    expect(documentRoute(item({ service: 'accounting', document: 'journals' }))).toBe('/accounting/journals/12');
    expect(documentRoute(item({ service: 'accounting', document: 'spend-money' }))).toBe('/banking/spend-money');
  });

  it('names each kind, and passes an unknown one through', () => {
    expect(kindLabel('CreditLimitOverride')).toBe('Credit limit override');
    expect(kindLabel('Leave')).toBe('Leave');
  });

  it('merges every service oldest first', () => {
    const merged = mergeInbox([
      [item({ requestId: 1, documentDate: '2026-09-05' })],
      [item({ service: 'sales', requestId: 2, documentDate: '2026-09-02' })],
    ]);
    expect(merged.map((i) => i.requestId)).toEqual([2, 1]);
  });
});
