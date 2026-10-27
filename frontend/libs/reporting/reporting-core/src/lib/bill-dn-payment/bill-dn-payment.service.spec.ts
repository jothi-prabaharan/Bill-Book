import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  BillDnPaymentResult,
  PaidDocument,
  VendorPayment,
  appendPaymentPage,
  hasMorePayments,
  isForeignPayment,
  paymentRows,
  paymentSentence,
} from './bill-dn-payment.models';
import { BillDnPaymentService } from './bill-dn-payment.service';

/** The Bill/DN Payment Report's calls and the helpers its page uses (TK-238). */

function setup(answer: unknown = { payments: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [BillDnPaymentService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(BillDnPaymentService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const document = (over: Partial<PaidDocument> = {}): PaidDocument => ({
  typeCode: 'BIL',
  documentId: 9401,
  documentNo: 'BIL-9401',
  vendorBillNo: 'V/9401',
  documentDate: '2026-10-02',
  allocationDate: '2026-10-10',
  amount: 11800,
  ...over,
});

const payment = (over: Partial<VendorPayment> = {}): VendorPayment => ({
  spendMoneyId: 9411,
  paymentNo: 'SPM-9411',
  paymentDate: '2026-10-10',
  contactId: 9111,
  contactCode: 'STEELCO',
  contactName: 'Steelco Metals',
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

const result = (payments: VendorPayment[], over: Partial<BillDnPaymentResult> = {}): BillDnPaymentResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  payments,
  totalPayments: payments.length,
  pageNumber: 1,
  pageSize: 25,
  totals: {
    payments: payments.length,
    paid: payments.reduce((t, r) => t + r.amountBase, 0),
    unallocated: payments.reduce((t, r) => t + r.unallocated, 0),
  },
  topVendors: [],
  otherVendors: 0,
  otherVendorsPaid: 0,
  ...over,
});

describe('BillDnPaymentService', () => {
  it('PD-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/bill-dn-payment-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'bankAccountId', 'paymentMethod', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('PD-02: sends the vendor, bank account, payment method and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: 9111, bankAccountId: 9302, paymentMethod: 'Upi', pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('9111');
    expect(params.get('bankAccountId')).toBe('9302');
    expect(params.get('paymentMethod')).toBe('Upi');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('PD-03: leaves a cleared filter out of the request', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: null, bankAccountId: null, paymentMethod: null });

    const params = paramsOf(get.mock.calls[0]);
    for (const name of ['contactId', 'bankAccountId', 'paymentMethod']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('PD-04: gives back what the endpoint answered', async () => {
    const answer = result([payment()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('PD-05: reads the vendors and bank accounts the filters offer from the report’s own lookups', async () => {
    const vendors = [{ contactId: 101, contactCode: 'STEELCO', contactName: 'Steelco Metals' }];
    const first = setup(vendors);
    expect(await first.service.vendors()).toEqual(vendors);
    expect(first.get.mock.calls[0][0]).toBe('/api/statements/bill-dn-payment-report/vendors');

    TestBed.resetTestingModule();
    const accounts = [{ bankAccountId: 9301, accountName: 'Cash in hand', currencyCode: 'INR' }];
    const second = setup(accounts);
    expect(await second.service.bankAccounts()).toEqual(accounts);
    expect(second.get.mock.calls[0][0]).toBe('/api/statements/bill-dn-payment-report/bank-accounts');
  });
});

describe('bill dn payment helpers', () => {
  it('PD-07: a payment is foreign when its currency is not the branch’s, whatever the case', () => {
    expect(isForeignPayment({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignPayment({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignPayment({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('PD-08: a payment is a heading, then the documents it settled, and then the total paid', () => {
    const rows = paymentRows(result([payment({ documents: [document(), document({ documentId: 9402, documentNo: 'BIL-9402', amount: 0 })] })]));

    expect(rows.map((r) => r.kind)).toEqual(['payment', 'document', 'document', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(4);
    expect(rows[3].name).toBe('Total paid');
  });

  it('PD-09: a payment heading links to the payment and carries its vendor, bank account and method with the reference', () => {
    const [heading] = paymentRows(result([payment({ paymentMethod: 'Cheque', referenceNo: 'CHQ-001' })]));

    expect(heading.link).toBe('/banking/spend-money/9411');
    expect([heading.date, heading.contactName, heading.contactCode, heading.bankAccountName, heading.method]).toEqual([
      '2026-10-10',
      'Steelco Metals',
      'STEELCO',
      'Cash in hand',
      'Cheque (CHQ-001)',
    ]);
  });

  it('PD-10: a document row links to its bill and carries its date, what was settled and the day it took effect', () => {
    const [, doc] = paymentRows(result([payment()]));

    expect(doc).toMatchObject({ kind: 'document', name: 'BIL-9401', link: '/purchase/bills/9401', date: '2026-10-02', settledOn: '2026-10-10', amount: 11800 });
  });

  it('PD-11: a document with no page of its own has no link', () => {
    const [, doc] = paymentRows(result([payment({ documents: [document({ typeCode: 'OPB', documentId: 1, documentNo: 'OPB-1', vendorBillNo: null, documentDate: null })] })]));

    expect([doc.name, doc.link, doc.date]).toEqual(['OPB-1', null, null]);
  });

  it('PD-12: what a payment did not settle is a row of its own, and a payment that settled it all has none', () => {
    const advance = paymentRows(result([payment({ amount: 3000, amountBase: 3000, settled: 2000, unallocated: 1000, documents: [document({ amount: 2000 })] })]));
    expect(advance.map((r) => r.kind)).toEqual(['payment', 'document', 'unallocated', 'total']);
    expect(advance[2]).toMatchObject({ name: 'Not settled against a document', amount: 1000 });

    expect(paymentRows(result([payment()])).some((r) => r.kind === 'unallocated')).toBe(false);
  });

  it('PD-13: a payment in another currency names it on its rows and carries its amount in base', () => {
    const rows = paymentRows(result([payment({ currencyCode: 'USD', amount: 100, amountBase: 8000, settled: 100, documents: [document({ amount: 100 })] })]));

    expect(rows[0]).toMatchObject({ currency: 'USD', amount: 100, amountBase: 8000 });
    expect(rows[1]).toMatchObject({ currency: 'USD', amount: 100, amountBase: null });
    // The total is in base and names no currency.
    expect(rows[2]).toMatchObject({ kind: 'total', currency: null, amount: 8000 });
  });

  it('PD-14: a payment in the branch’s currency names none and has no base amount', () => {
    const [heading] = paymentRows(result([payment()]));

    expect([heading.currency, heading.amountBase]).toEqual([null, null]);
  });

  it('PD-15: a payment with TDS withheld carries it, and one without carries none', () => {
    expect(paymentRows(result([payment({ tdsAmount: 500 })]))[0].tds).toBe(500);
    expect(paymentRows(result([payment()]))[0].tds).toBeNull();
  });

  it('PD-16: the total row is the server’s figure, and says it is of all when not every payment is loaded', () => {
    const rows = paymentRows(result([payment()], { totalPayments: 120, totals: { payments: 120, paid: 99999, unallocated: 0 } }));

    expect(rows[rows.length - 1]).toMatchObject({ name: 'Total paid, all 120 payments', amount: 99999 });
  });

  it('PD-17: with no payments there are no rows, not even the total', () => {
    expect(paymentRows(result([]))).toEqual([]);
  });

  it('PD-18: the next page is added to the ones loaded and the totals are the latest answer’s', () => {
    const first = result([payment({ spendMoneyId: 1 })], { totalPayments: 3, pageNumber: 1 });
    const next = result([payment({ spendMoneyId: 2 })], { totalPayments: 3, pageNumber: 2, totals: { payments: 3, paid: 9, unallocated: 0 } });

    const merged = appendPaymentPage(first, next);

    expect(merged.payments.map((r) => r.spendMoneyId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.paid).toBe(9);
  });

  it('PD-19: there is more while fewer payments are loaded than match', () => {
    expect(hasMorePayments({ payments: [payment()], totalPayments: 2 })).toBe(true);
    expect(hasMorePayments({ payments: [payment(), payment({ spendMoneyId: 2 })], totalPayments: 2 })).toBe(false);
  });

  it('PD-20: a bill document row carries the vendor’s own bill number, and an opening balance has none', () => {
    const rows = paymentRows(
      result([payment({ documents: [document(), document({ typeCode: 'OPB', documentId: 1, documentNo: 'OPB-1', vendorBillNo: null, documentDate: null })] })]),
    );

    expect([rows[1].vendorBillNo, rows[2].vendorBillNo]).toEqual(['V/9401', null]);
  });

  it('PD-21: the sentence says how many payments, what they paid and how much settled nothing', () => {
    const answer = result([payment(), payment({ spendMoneyId: 2 })], { totals: { payments: 2, paid: 300, unallocated: 40 } });

    expect(paymentSentence(answer, money)).toBe('2 payments paid ₹300.00, of which ₹40.00 was not settled against a document.');
    expect(paymentSentence(result([payment()]), money)).toBe('1 payment paid ₹11800.00, all settled against documents.');
  });
});
