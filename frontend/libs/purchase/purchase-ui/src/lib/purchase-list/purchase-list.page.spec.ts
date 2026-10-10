import { describe, expect, it } from 'vitest';
import { PurchaseTransactionListItem } from '@bill-book/purchase-core';
import { countStatusTabs } from '@bill-book/ui-components';
import { PURCHASE_REGISTERS, purchaseListStatus } from './purchase-list.page';

/** The purchase register's codes and statuses (TK-187). */
describe('Purchase register rules', () => {
  const row = (over: Partial<PurchaseTransactionListItem>): PurchaseTransactionListItem => ({
    transactionId: 1,
    transactionType: 'PurchaseOrder',
    documentNo: 'POR-000001',
    documentDate: '2026-08-11',
    contactId: 1,
    contactName: 'Coimbatore Yarns',
    totalAmount: 400000,
    status: 'Posted',
    ...over,
  });

  it('PREG-01: a confirmed order shows its fulfilment, a voided one Cancelled, a draft Draft', () => {
    expect(purchaseListStatus(row({ fulfilmentStatus: 'Open' }))).toBe('Open');
    expect(purchaseListStatus(row({ fulfilmentStatus: 'PartlyReceived' }))).toBe('PartlyReceived');
    expect(purchaseListStatus(row({ status: 'Void', fulfilmentStatus: 'Cancelled' }))).toBe('Cancelled');
    expect(purchaseListStatus(row({ status: 'Draft', fulfilmentStatus: 'Open' }))).toBe('Draft');
  });

  it('PREG-02: a bill, receipt or debit note shows its document status', () => {
    expect(purchaseListStatus(row({ transactionType: 'Bill', status: 'Posted' }))).toBe('Posted');
    expect(purchaseListStatus(row({ transactionType: 'GoodsReceipt', status: 'ReadyToPost' }))).toBe('ReadyToPost');
  });

  it('PREG-03: the order register counts the screenshot\'s tabs, Cancelled ( 0 ) included', () => {
    const rows = [
      row({ fulfilmentStatus: 'Open' }),
      row({ fulfilmentStatus: 'PartlyReceived' }),
      row({ fulfilmentStatus: 'Closed' }),
    ];
    const tabs = countStatusTabs(rows, PURCHASE_REGISTERS['PurchaseOrder'].statuses, purchaseListStatus);

    expect(tabs.map((t) => `${t.label} ( ${t.count} )`)).toEqual([
      'All ( 3 )',
      'Draft ( 0 )',
      'Open ( 1 )',
      'PartlyReceived ( 1 )',
      'Closed ( 1 )',
      'Cancelled ( 0 )',
    ]);
  });

  it('PREG-04: each document type has its series code', () => {
    expect(PURCHASE_REGISTERS['PurchaseOrder'].code).toBe('POR');
    expect(PURCHASE_REGISTERS['GoodsReceipt'].code).toBe('GRN');
    expect(PURCHASE_REGISTERS['Bill'].code).toBe('BIL');
    expect(PURCHASE_REGISTERS['DebitNote'].code).toBe('DBN');
  });
});
