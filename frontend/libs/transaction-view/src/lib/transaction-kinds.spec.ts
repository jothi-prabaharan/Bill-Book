import { describe, expect, it } from 'vitest';
import {
  TRANSACTION_KINDS,
  TransactionKind,
  transactionDocumentRoute,
  transactionKindInfo,
  transactionKindOfTypeName,
  transactionListRoute,
  transactionViewApiPath,
  transactionViewRoute,
  transactionViewRouteOfRow,
} from './transaction-kinds';
import { transactionViewRoutes } from './transaction-view.routes';

describe('transaction kinds', () => {
  it('TXV-01: there is one entry for each of the twelve transaction types, each with its own code', () => {
    const codes = TRANSACTION_KINDS.map((info) => info.typeCode);

    expect(TRANSACTION_KINDS).toHaveLength(12);
    expect(new Set(codes).size).toBe(12);
    expect(codes).toEqual(['QTE', 'SOR', 'DLC', 'INV', 'CRN', 'POR', 'GRN', 'BIL', 'DBN', 'RCM', 'SPM', 'JRN']);
  });

  it('TXV-02: every kind resolves by name and an unknown or empty one does not', () => {
    for (const info of TRANSACTION_KINDS) {
      expect(transactionKindInfo(info.kind)).toBe(info);
    }

    expect(transactionKindInfo('estimates')).toBeNull();
    expect(transactionKindInfo('')).toBeNull();
    expect(transactionKindInfo(undefined)).toBeNull();
  });

  it.each<[TransactionKind, string]>([
    ['quotes', '/api/sales/quotes/7/view'],
    ['sales-orders', '/api/sales/sales-orders/7/view'],
    ['delivery-challans', '/api/sales/delivery-challans/7/view'],
    ['invoices', '/api/sales/invoices/7/view'],
    ['credit-notes', '/api/sales/credit-notes/7/view'],
    ['purchase-orders', '/api/purchase/purchase-orders/7/view'],
    ['goods-receipts', '/api/purchase/goods-receipts/7/view'],
    ['bills', '/api/purchase/bills/7/view'],
    ['debit-notes', '/api/purchase/debit-notes/7/view'],
    ['receive-money', '/api/receive-money/7/view'],
    ['spend-money', '/api/spend-money/7/view'],
    ['journals', '/api/journals/7/view'],
  ])('TXV-03: %s is read at %s', (kind, url) => {
    expect(transactionViewApiPath(kind, 7)).toBe(url);
  });

  it('TXV-04: the view page, the document and the register are three routes to the same place', () => {
    expect(transactionViewRoute('bills', 12)).toEqual(['/purchase', 'bills', '12', 'view']);
    expect(transactionDocumentRoute('bills', 12)).toEqual(['/purchase', 'bills', '12']);
    expect(transactionListRoute('bills')).toEqual(['/purchase', 'bills']);

    expect(transactionViewRoute('receive-money', 3)).toEqual(['/banking', 'receive-money', '3', 'view']);
    expect(transactionViewRoute('journals', 3)).toEqual(['/accounting', 'journals', '3', 'view']);
  });

  it('TXV-05: a register row finds its view by the type name it is listed under', () => {
    expect(transactionKindOfTypeName('SalesOrder')).toBe('sales-orders');
    expect(transactionKindOfTypeName('GoodsReceipt')).toBe('goods-receipts');
    expect(transactionKindOfTypeName('DeliveryChallan')).toBe('delivery-challans');
    expect(transactionKindOfTypeName('Loan')).toBeNull();

    expect(transactionViewRouteOfRow('CreditNote', 9)).toEqual(['/sales', 'credit-notes', '9', 'view']);
  });

  it('TXV-06: a row of no known type, or with no id, has no view link rather than a broken one', () => {
    expect(transactionViewRouteOfRow('Loan', 9)).toBeNull();
    expect(transactionViewRouteOfRow(undefined, 9)).toBeNull();
    expect(transactionViewRouteOfRow('Bill', 0)).toBeNull();
    expect(transactionViewRouteOfRow('Bill', null)).toBeNull();
  });

  it('TXV-07: an unknown kind is refused instead of building a wrong address', () => {
    expect(() => transactionViewApiPath('estimates' as TransactionKind, 1)).toThrow();
    expect(transactionViewRoute('estimates' as TransactionKind, 1)).toEqual([]);
  });

  it('TXV-08: the app mounts one route per kind, at the module view permission, with the kind in its data', () => {
    expect(transactionViewRoutes).toHaveLength(TRANSACTION_KINDS.length);

    const quotes = transactionViewRoutes.find((r) => r.data?.['viewKind'] === 'quotes');
    expect(quotes?.path).toBe('sales/quotes/:id/view');
    expect(quotes?.data?.['access']).toEqual({ permission: 'sales.view' });

    const spend = transactionViewRoutes.find((r) => r.data?.['viewKind'] === 'spend-money');
    expect(spend?.path).toBe('banking/spend-money/:id/view');
    expect(spend?.data?.['access']).toEqual({ permission: 'banking.view' });

    const journals = transactionViewRoutes.find((r) => r.data?.['viewKind'] === 'journals');
    expect(journals?.path).toBe('accounting/journals/:id/view');
    expect(journals?.data?.['access']).toEqual({ permission: 'accounting.view' });
  });

  it('TXV-09: no two routes answer the same address', () => {
    const paths = transactionViewRoutes.map((r) => r.path);

    expect(new Set(paths).size).toBe(paths.length);
  });
});
