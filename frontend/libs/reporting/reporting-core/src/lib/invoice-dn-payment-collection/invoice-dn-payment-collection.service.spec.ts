import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  COLLECTION_PAYMENT_METHODS,
  InvoiceDnPaymentCollectionResult,
  ReceiptCollection,
  SettledDocument,
  appendCollectionPage,
  bankAccountChoiceLabel,
  collectionRows,
  collectionSentence,
  hasMoreCollection,
  isForeignReceipt,
  paymentMethodLabel,
} from './invoice-dn-payment-collection.models';
import { InvoiceDnPaymentCollectionService } from './invoice-dn-payment-collection.service';

/** The Invoice/DN Payment Collection Report's calls and the helpers its page uses (TK-227). */

function setup(answer: unknown = { receipts: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [InvoiceDnPaymentCollectionService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(InvoiceDnPaymentCollectionService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const document = (over: Partial<SettledDocument> = {}): SettledDocument => ({
  typeCode: 'INV',
  documentId: 9401,
  documentNo: 'INV-9401',
  documentDate: '2026-10-02',
  allocationDate: '2026-10-10',
  amount: 11800,
  ...over,
});

const receipt = (over: Partial<ReceiptCollection> = {}): ReceiptCollection => ({
  receiveMoneyId: 9411,
  receiptNo: 'RCM-9411',
  receiptDate: '2026-10-10',
  contactId: 9103,
  contactCode: 'ZENITH',
  contactName: 'Zenith Traders',
  bankAccountId: 9301,
  bankAccountName: 'Cash in hand',
  paymentMethod: 'Cash',
  referenceNo: null,
  currencyCode: 'INR',
  amount: 11800,
  amountBase: 11800,
  tdsAmount: 0,
  settled: 11800,
  unallocated: 0,
  documents: [document()],
  ...over,
});

const result = (receipts: ReceiptCollection[], over: Partial<InvoiceDnPaymentCollectionResult> = {}): InvoiceDnPaymentCollectionResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  receipts,
  totalReceipts: receipts.length,
  pageNumber: 1,
  pageSize: 25,
  totals: {
    receipts: receipts.length,
    collected: receipts.reduce((t, r) => t + r.amountBase, 0),
    unallocated: receipts.reduce((t, r) => t + r.unallocated, 0),
  },
  topCustomers: [],
  otherCustomers: 0,
  otherCustomersCollected: 0,
  ...over,
});

describe('InvoiceDnPaymentCollectionService', () => {
  it('PC-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/invoice-dn-payment-collection-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'bankAccountId', 'paymentMethod', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('PC-02: sends the customer, bank account, payment method and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: 9103, bankAccountId: 9302, paymentMethod: 'Upi', pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('9103');
    expect(params.get('bankAccountId')).toBe('9302');
    expect(params.get('paymentMethod')).toBe('Upi');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('PC-03: leaves a cleared filter out of the request', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: null, bankAccountId: null, paymentMethod: null });

    const params = paramsOf(get.mock.calls[0]);
    for (const name of ['contactId', 'bankAccountId', 'paymentMethod']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('PC-04: gives back what the endpoint answered', async () => {
    const answer = result([receipt()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('PC-05: reads the customers and bank accounts the filters offer from the report’s own lookups', async () => {
    const customers = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const first = setup(customers);
    expect(await first.service.customers()).toEqual(customers);
    expect(first.get.mock.calls[0][0]).toBe('/api/statements/invoice-dn-payment-collection-report/customers');

    TestBed.resetTestingModule();
    const accounts = [{ bankAccountId: 9301, accountName: 'Cash in hand', currencyCode: 'INR' }];
    const second = setup(accounts);
    expect(await second.service.bankAccounts()).toEqual(accounts);
    expect(second.get.mock.calls[0][0]).toBe('/api/statements/invoice-dn-payment-collection-report/bank-accounts');
  });
});

describe('invoice dn payment collection helpers', () => {
  it('PC-06: the payment methods read as words in the order the filter offers them', () => {
    expect(COLLECTION_PAYMENT_METHODS.map((m) => m.value)).toEqual(['Cash', 'Cheque', 'BankTransfer', 'Upi', 'Card', 'DemandDraft', 'Wallet', 'Other']);
    expect(paymentMethodLabel('BankTransfer')).toBe('Bank transfer');
    expect(paymentMethodLabel('Upi')).toBe('UPI');
    expect(paymentMethodLabel('DemandDraft')).toBe('Demand draft');
  });

  it('PC-07: a receipt is foreign when its currency is not the branch’s, whatever the case', () => {
    expect(isForeignReceipt({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignReceipt({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignReceipt({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('PC-08: a receipt is a heading, then the documents it settled, and then the total collected', () => {
    const rows = collectionRows(result([receipt({ documents: [document(), document({ documentId: 9402, documentNo: 'INV-9402', amount: 0 })] })]));

    expect(rows.map((r) => r.kind)).toEqual(['receipt', 'document', 'document', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(4);
    expect(rows[3].name).toBe('Total collected');
  });

  it('PC-09: a receipt heading links to the receipt and carries its customer, bank account and method with the reference', () => {
    const [heading] = collectionRows(result([receipt({ paymentMethod: 'Cheque', referenceNo: 'CHQ-001' })]));

    expect(heading.link).toBe('/banking/receive-money/9411');
    expect([heading.date, heading.contactName, heading.contactCode, heading.bankAccountName, heading.method]).toEqual([
      '2026-10-10',
      'Zenith Traders',
      'ZENITH',
      'Cash in hand',
      'Cheque (CHQ-001)',
    ]);
  });

  it('PC-10: a document row links to its invoice and carries its date, what was settled and the day it took effect', () => {
    const [, doc] = collectionRows(result([receipt()]));

    expect(doc).toMatchObject({ kind: 'document', name: 'INV-9401', link: '/sales/invoices/9401', date: '2026-10-02', settledOn: '2026-10-10', amount: 11800 });
  });

  it('PC-11: a document with no page of its own has no link', () => {
    const [, doc] = collectionRows(result([receipt({ documents: [document({ typeCode: 'OPB', documentId: 1, documentNo: 'OPB-1', documentDate: null })] })]));

    expect([doc.name, doc.link, doc.date]).toEqual(['OPB-1', null, null]);
  });

  it('PC-12: what a receipt did not settle is a row of its own, and a receipt that settled it all has none', () => {
    const advance = collectionRows(result([receipt({ amount: 3000, amountBase: 3000, settled: 2000, unallocated: 1000, documents: [document({ amount: 2000 })] })]));
    expect(advance.map((r) => r.kind)).toEqual(['receipt', 'document', 'unallocated', 'total']);
    expect(advance[2]).toMatchObject({ name: 'Not settled against a document', amount: 1000 });

    expect(collectionRows(result([receipt()])).some((r) => r.kind === 'unallocated')).toBe(false);
  });

  it('PC-13: a receipt in another currency names it on its rows and carries its amount in base', () => {
    const rows = collectionRows(result([receipt({ currencyCode: 'USD', amount: 100, amountBase: 8000, settled: 100, documents: [document({ amount: 100 })] })]));

    expect(rows[0]).toMatchObject({ currency: 'USD', amount: 100, amountBase: 8000 });
    expect(rows[1]).toMatchObject({ currency: 'USD', amount: 100, amountBase: null });
    // The total is in base and names no currency.
    expect(rows[2]).toMatchObject({ kind: 'total', currency: null, amount: 8000 });
  });

  it('PC-14: a receipt in the branch’s currency names none and has no base amount', () => {
    const [heading] = collectionRows(result([receipt()]));

    expect([heading.currency, heading.amountBase]).toEqual([null, null]);
  });

  it('PC-15: a receipt with TDS withheld carries it, and one without carries none', () => {
    expect(collectionRows(result([receipt({ tdsAmount: 500 })]))[0].tds).toBe(500);
    expect(collectionRows(result([receipt()]))[0].tds).toBeNull();
  });

  it('PC-16: the total row is the server’s figure, and says it is of all when not every receipt is loaded', () => {
    const rows = collectionRows(result([receipt()], { totalReceipts: 120, totals: { receipts: 120, collected: 99999, unallocated: 0 } }));

    expect(rows[rows.length - 1]).toMatchObject({ name: 'Total collected, all 120 receipts', amount: 99999 });
  });

  it('PC-17: with no receipts there are no rows, not even the total', () => {
    expect(collectionRows(result([]))).toEqual([]);
  });

  it('PC-18: the next page is added to the ones loaded and the totals are the latest answer’s', () => {
    const first = result([receipt({ receiveMoneyId: 1 })], { totalReceipts: 3, pageNumber: 1 });
    const next = result([receipt({ receiveMoneyId: 2 })], { totalReceipts: 3, pageNumber: 2, totals: { receipts: 3, collected: 9, unallocated: 0 } });

    const merged = appendCollectionPage(first, next);

    expect(merged.receipts.map((r) => r.receiveMoneyId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.collected).toBe(9);
  });

  it('PC-19: there is more while fewer receipts are loaded than match', () => {
    expect(hasMoreCollection({ receipts: [receipt()], totalReceipts: 2 })).toBe(true);
    expect(hasMoreCollection({ receipts: [receipt(), receipt({ receiveMoneyId: 2 })], totalReceipts: 2 })).toBe(false);
  });

  it('PC-20: a bank account reads by name, and by its currency too when that is not the branch’s', () => {
    expect(bankAccountChoiceLabel({ bankAccountId: 1, accountName: 'Cash in hand', currencyCode: 'INR' }, 'INR')).toBe('Cash in hand');
    expect(bankAccountChoiceLabel({ bankAccountId: 2, accountName: 'Dollar account', currencyCode: 'USD' }, 'INR')).toBe('Dollar account (USD)');
  });

  it('PC-21: the sentence says how many receipts, what they collected and how much settled nothing', () => {
    const answer = result([receipt(), receipt({ receiveMoneyId: 2 })], { totals: { receipts: 2, collected: 300, unallocated: 40 } });

    expect(collectionSentence(answer, money)).toBe('2 receipts collected ₹300.00, of which ₹40.00 was not settled against a document.');
    expect(collectionSentence(result([receipt()]), money)).toBe('1 receipt collected ₹11800.00, all settled against documents.');
  });
});
