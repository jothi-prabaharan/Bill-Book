import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  ForeignCurrencyDetailAccount,
  ForeignCurrencyDetailBlock,
  ForeignCurrencyDetailsResult,
  ForeignCurrencyDocument,
  detailAccountLabel,
  detailCurrencyLabel,
  documentDetail,
  foreignDetailRows,
  rateRange,
} from './foreign-currency-gain-or-loss-details.models';
import { ForeignCurrencyGainLossDetailsService } from './foreign-currency-gain-or-loss-details.service';

/** The Foreign Currency Gain or Loss Details report's calls and the helpers its page uses (TK-221). */

function setup(answer: unknown = { currencies: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [ForeignCurrencyGainLossDetailsService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(ForeignCurrencyGainLossDetailsService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const open = (over: Partial<ForeignCurrencyDocument> = {}): ForeignCurrencyDocument => ({
  kind: 'Open',
  date: '2026-08-10',
  typeCode: 'INV',
  documentNo: 'INV-0001',
  contactId: 9101,
  contactName: 'Acme Exports',
  reference: null,
  amount: 500,
  bookedRate: 80,
  currentRate: 84,
  carryingBase: 40000,
  currentBase: 42000,
  realized: null,
  unrealized: 2000,
  ...over,
});

const settlement = (over: Partial<ForeignCurrencyDocument> = {}): ForeignCurrencyDocument => ({
  kind: 'Settlement',
  date: '2026-10-12',
  typeCode: 'RCM',
  documentNo: 'RCM-0002',
  contactId: 9101,
  contactName: 'Acme Exports',
  reference: 'Settles INV-0001',
  amount: 400,
  bookedRate: 80,
  currentRate: 83,
  carryingBase: 32000,
  currentBase: 33200,
  realized: 1200,
  unrealized: null,
  ...over,
});

const account = (documents: ForeignCurrencyDocument[], over: Partial<ForeignCurrencyDetailAccount> = {}): ForeignCurrencyDetailAccount => ({
  accountId: 31,
  accountName: 'Accounts Receivable',
  kind: 'Receivable',
  dueSource: 500,
  carryingBase: 40000,
  revaluedBase: 42000,
  unrealized: 2000,
  realized: 1200,
  documents,
  ...over,
});

const block = (accounts: ForeignCurrencyDetailAccount[], over: Partial<ForeignCurrencyDetailBlock> = {}): ForeignCurrencyDetailBlock => ({
  currencyCode: 'USD',
  rate: 84,
  needsRate: false,
  accounts,
  dueSource: accounts.reduce((t, a) => t + a.dueSource, 0),
  carryingBase: accounts.reduce((t, a) => t + a.carryingBase, 0),
  revaluedBase: accounts.reduce((t, a) => t + (a.revaluedBase ?? 0), 0),
  unrealized: accounts.reduce((t, a) => t + (a.unrealized ?? 0), 0),
  realized: accounts.reduce((t, a) => t + a.realized, 0),
  net: accounts.reduce((t, a) => t + a.realized + (a.unrealized ?? 0), 0),
  ...over,
});

const result = (currencies: ForeignCurrencyDetailBlock[], over: Partial<ForeignCurrencyDetailsResult> = {}): ForeignCurrencyDetailsResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  currencies,
  unassigned: [],
  unassignedRealized: 0,
  totalRealized: currencies.reduce((t, c) => t + c.realized, 0),
  totalUnrealized: currencies.reduce((t, c) => t + (c.unrealized ?? 0), 0),
  totalNet: currencies.reduce((t, c) => t + c.net, 0),
  currenciesWithoutRate: [],
  ...over,
});

describe('ForeignCurrencyGainLossDetailsService', () => {
  it('FD-01: runs with both dates and nothing else when no filter or rate was given', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/foreign-currency-gain-or-loss-details');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    expect(params.keys().filter((k) => k !== 'fromDate' && k !== 'toDate')).toEqual([]);
  });

  it('FD-02: sends the currency, contact and account chosen and one rate for each currency as Rates[CODE]', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      currencyCode: 'USD',
      contactId: 9101,
      accountId: 31,
      rates: { USD: 84, EUR: 91.5 },
    });

    const params = paramsOf(get.mock.calls[0]);
    expect([params.get('currencyCode'), params.get('contactId'), params.get('accountId')]).toEqual(['USD', '9101', '31']);
    expect(params.get('rates[USD]')).toBe('84');
    expect(params.get('rates[EUR]')).toBe('91.5');
  });

  it('FD-03: asks what the filters offer at the to date', async () => {
    const answer = { baseCurrencyCode: 'INR', currencies: [], accounts: [], contacts: [] };
    const { service, get } = setup(answer);

    expect(await service.choices('2026-10-31')).toEqual(answer);
    expect(get.mock.calls[0][0]).toBe('/api/statements/foreign-currency-gain-or-loss-details/choices');
    expect(paramsOf(get.mock.calls[0]).get('toDate')).toBe('2026-10-31');
  });
});

describe('Foreign Currency Gain or Loss Details helpers', () => {
  it('FD-04: what sits under a document is its contact and what it settled, with no empty part', () => {
    expect(documentDetail({ contactName: 'Acme Exports', reference: 'Settles INV-0001' })).toBe('Acme Exports · Settles INV-0001');
    expect(documentDetail({ contactName: 'Acme Exports', reference: null })).toBe('Acme Exports');
    expect(documentDetail({ contactName: null, reference: null })).toBe('');
  });

  it('FD-05: the grid runs a heading, each account with its documents and its total, then the currency total and the grand total', () => {
    const usdAr = account([open(), settlement(), open({ kind: 'Open', documentNo: 'RCM-0004', typeCode: 'RCM', amount: -100, unrealized: -100 })]);
    const usdAp = account(
      [open({ typeCode: 'BIL', documentNo: 'BIL-0001', amount: -300, unrealized: -600 })],
      { accountId: 36, accountName: 'Accounts Payable', kind: 'Payable', dueSource: -300, carryingBase: -24600, revaluedBase: -25200, unrealized: -600, realized: -400 },
    );
    const eurAr = account([open({ documentNo: 'INV-0002', amount: 150, unrealized: 150 })], { dueSource: 150, carryingBase: 13500, revaluedBase: 13650, unrealized: 150, realized: -100 });

    const rows = foreignDetailRows(result([block([eurAr], { currencyCode: 'EUR', rate: 91 }), block([usdAr, usdAp])]));

    expect(rows.map((r) => r.kind)).toEqual([
      'heading', 'account', 'open',
      'heading', 'account', 'open', 'settlement', 'open', 'subtotal', 'account', 'open',
      'currencyTotal',
      'total',
    ]);
    expect(rows.map((r) => r.name).filter((n, i) => [3, 8, 11, 12].includes(i))).toEqual(['USD', 'Total Accounts Receivable', 'Total USD', 'Total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(rows.length);
  });

  it('FD-06: an open document row carries its rates, amounts, unrealized figure and its own net', () => {
    const rows = foreignDetailRows(result([block([account([open()])])]));

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'account', 'open']);

    const row = rows[2];
    expect([row.date, row.name, row.typeCode, row.detail]).toEqual(['2026-08-10', 'INV-0001', 'INV', 'Acme Exports']);
    expect([row.bookedRate, row.currentRate, row.amount, row.carryingBase, row.currentBase]).toEqual([80, 84, 500, 40000, 42000]);
    expect([row.realized, row.unrealized, row.net]).toEqual([null, 2000, 2000]);
  });

  it('FD-07: a settlement row names what it settled and carries its realized amount as its net', () => {
    const rows = foreignDetailRows(result([block([account([settlement()])])]));

    const row = rows[2];
    expect(row.kind).toBe('settlement');
    expect(row.detail).toBe('Acme Exports · Settles INV-0001');
    expect([row.bookedRate, row.currentRate, row.amount, row.carryingBase, row.currentBase]).toEqual([80, 83, 400, 32000, 33200]);
    expect([row.realized, row.unrealized, row.net]).toEqual([1200, null, 1200]);
  });

  it('FD-08: a currency heading says its rate or that it has none, and an open document without a rate has blank unrealized and net cells', () => {
    const noRate = block([account([open({ currentRate: null, currentBase: null, unrealized: null })], { revaluedBase: null, unrealized: null })], {
      rate: null,
      needsRate: true,
      revaluedBase: null,
      unrealized: null,
    });

    const rows = foreignDetailRows(result([noRate]));

    expect(rows[0].detail).toBe('no rate given');
    expect(rows[0].needsRate).toBe(true);
    expect([rows[2].unrealized, rows[2].net, rows[2].needsRate]).toEqual([null, null, true]);
  });

  it('FD-09: one account with one document, and one currency with one account, have no totals to add up', () => {
    expect(foreignDetailRows(result([block([account([open()])])])).map((r) => r.kind)).toEqual(['heading', 'account', 'open']);
  });

  it('FD-10: what no currency claims is its own section, and with it a single currency still has a grand total', () => {
    const journal: ForeignCurrencyDocument = {
      kind: 'Settlement', date: '2026-10-28', typeCode: 'JRN', documentNo: 'JRN-0001', contactId: null, contactName: null, reference: null,
      amount: null, bookedRate: null, currentRate: null, carryingBase: null, currentBase: null, realized: 50, unrealized: null,
    };

    const rows = foreignDetailRows(result([block([account([open()])])], { unassigned: [journal], unassignedRealized: 50, totalRealized: 1250, totalNet: 3250 }));

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'account', 'open', 'heading', 'unassigned', 'total']);
    expect(rows[3].name).toBe('Not tied to a currency');
    expect([rows[4].name, rows[4].realized, rows[4].net]).toEqual(['JRN-0001', 50, 50]);
    expect([rows[5].realized, rows[5].unrealized, rows[5].net]).toEqual([1250, 2000, 3250]);
  });

  it('FD-11: no currencies and nothing unassigned is no rows', () => {
    expect(foreignDetailRows(result([]))).toEqual([]);
  });

  it('FD-12: the rates of a document read booked then current, with whichever of them there is', () => {
    const format = (rate: number): string => rate.toFixed(2);

    expect(rateRange({ bookedRate: 80, currentRate: 84 }, format)).toBe('80.00 → 84.00');
    expect(rateRange({ bookedRate: 80, currentRate: null }, format)).toBe('80.00');
    expect(rateRange({ bookedRate: null, currentRate: 84 }, format)).toBe('84.00');
    expect(rateRange({ bookedRate: null, currentRate: null }, format)).toBe('');
  });

  it('FD-13: the filters label a currency by its accounts and an account by its kind', () => {
    expect(detailCurrencyLabel({ currencyCode: 'USD', accounts: 3 })).toBe('USD · 3 accounts');
    expect(detailCurrencyLabel({ currencyCode: 'EUR', accounts: 1 })).toBe('EUR · 1 account');
    expect(detailAccountLabel({ accountName: 'Accounts Payable', kind: 'Payable' })).toBe('Accounts Payable · Payable');
  });
});
