import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  ForeignCurrencyAccount,
  ForeignCurrencyBlock,
  ForeignCurrencyGainLossResult,
  currencyChoiceLabel,
  foreignCurrencyRows,
  foreignCurrencySentence,
  foreignKindLabel,
  headingDetail,
  missingRatesSentence,
  netOf,
  rateKeyCurrency,
  rateText,
  ratesOf,
} from './foreign-currency-gain-or-loss.models';
import { ForeignCurrencyGainLossService } from './foreign-currency-gain-or-loss.service';

/** The Foreign Currency Gain or Loss report's calls and the helpers its page uses (TK-220). */

function setup(answer: unknown = { currencies: [] }, failure?: unknown) {
  const get = vi.fn().mockReturnValue(failure ? throwError(() => failure) : of(answer));

  TestBed.configureTestingModule({
    providers: [ForeignCurrencyGainLossService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(ForeignCurrencyGainLossService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const account = (over: Partial<ForeignCurrencyAccount> = {}): ForeignCurrencyAccount => ({
  accountId: 31,
  accountName: 'Accounts Receivable',
  kind: 'Receivable',
  dueSource: 500,
  carryingBase: 40000,
  revaluedBase: 42000,
  unrealized: 2000,
  realized: 1200,
  ...over,
});

const block = (accounts: ForeignCurrencyAccount[], over: Partial<ForeignCurrencyBlock> = {}): ForeignCurrencyBlock => ({
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

const result = (currencies: ForeignCurrencyBlock[], over: Partial<ForeignCurrencyGainLossResult> = {}): ForeignCurrencyGainLossResult => ({
  fromDate: '2026-10-01',
  asAtDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  currencies,
  unassignedRealized: 0,
  totalRealized: currencies.reduce((t, c) => t + c.realized, 0),
  totalUnrealized: currencies.reduce((t, c) => t + (c.unrealized ?? 0), 0),
  totalNet: currencies.reduce((t, c) => t + c.net, 0),
  currenciesWithoutRate: [],
  ...over,
});

describe('ForeignCurrencyGainLossService', () => {
  it('FX-01: runs with both dates, and with no currency or rate when none was given', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', asAtDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/foreign-currency-gain-or-loss');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('asAtDate')).toBe('2026-10-31');
    expect(params.has('currencyCode')).toBe(false);
    expect(params.keys().filter((k) => k.startsWith('rates'))).toEqual([]);
  });

  it('FX-02: sends the currency chosen and one rate for each currency, as Rates[CODE]', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', asAtDate: '2026-10-31', currencyCode: 'USD', rates: { USD: 84, EUR: 91.5 } });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('currencyCode')).toBe('USD');
    expect(params.get('rates[USD]')).toBe('84');
    expect(params.get('rates[EUR]')).toBe('91.5');
  });

  it('FX-03: asks which currencies hold a balance at the as-at date', async () => {
    const answer = { baseCurrencyCode: 'INR', currencies: [{ currencyCode: 'USD', accounts: 3 }] };
    const { service, get } = setup(answer);

    expect(await service.choices('2026-10-31')).toEqual(answer);
    expect(get.mock.calls[0][0]).toBe('/api/statements/foreign-currency-gain-or-loss/currencies');
    expect(paramsOf(get.mock.calls[0]).get('asAtDate')).toBe('2026-10-31');
  });

  it('FX-04: reads the rate master’s rate for a pair on a date', async () => {
    const { service, get } = setup({ rate: 83.25 });

    expect(await service.masterRate('USD', 'INR', '2026-10-31')).toBe(83.25);

    expect(get.mock.calls[0][0]).toBe('/api/rates/exchange');
    const params = paramsOf(get.mock.calls[0]);
    expect([params.get('from'), params.get('to'), params.get('on')]).toEqual(['USD', 'INR', '2026-10-31']);
  });

  it('FX-05: a pair with no rate on file is null, and any other failure is passed on', async () => {
    const none = setup(null, new HttpErrorResponse({ status: 404 }));
    expect(await none.service.masterRate('USD', 'INR', '2026-10-31')).toBeNull();

    TestBed.resetTestingModule();
    const broken = setup(null, new HttpErrorResponse({ status: 500 }));
    await expect(broken.service.masterRate('USD', 'INR', '2026-10-31')).rejects.toBeInstanceOf(HttpErrorResponse);
  });
});

describe('Foreign Currency Gain or Loss helpers', () => {
  it('FX-06: a kind of account reads as a person would say it', () => {
    expect(foreignKindLabel('Receivable')).toBe('Receivable');
    expect(foreignKindLabel('Payable')).toBe('Payable');
    expect(foreignKindLabel('Bank')).toBe('Bank');
  });

  it('FX-07: a rate reads with no more places than it was given, and a heading says when there is none', () => {
    expect(rateText(84)).toBe('84');
    expect(rateText(83.125)).toBe('83.125');
    expect(rateText(0.01204819)).toBe('0.01204819');

    expect(headingDetail({ rate: 84, needsRate: false }, 'INR')).toBe('rate 84 INR');
    expect(headingDetail({ rate: null, needsRate: true }, 'INR')).toBe('no rate given');
    expect(headingDetail({ rate: null, needsRate: false }, 'INR')).toBe('');
  });

  it('FX-08: net is realized plus unrealized, and an unrealized figure not worked out counts as nothing', () => {
    expect(netOf(1200, 2000)).toBe(3200);
    expect(netOf(-100, 150)).toBe(50);
    expect(netOf(-100, null)).toBe(-100);
  });

  it('FX-09: the grid runs a heading, its accounts and its total for each currency, then the grand total', () => {
    const usd = block([
      account(),
      account({ accountId: 36, accountName: 'Accounts Payable', kind: 'Payable', dueSource: -300, carryingBase: -24600, revaluedBase: -25200, unrealized: -600, realized: -400 }),
    ]);
    const eur = block([account({ dueSource: 150, carryingBase: 13500, revaluedBase: 13650, unrealized: 150, realized: -100 })], {
      currencyCode: 'EUR',
      rate: 91,
    });

    const rows = foreignCurrencyRows(result([eur, usd]));

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'account', 'heading', 'account', 'account', 'subtotal', 'total']);
    expect(rows.map((r) => r.name)).toEqual(['EUR', 'Accounts Receivable', 'USD', 'Accounts Receivable', 'Accounts Payable', 'Total USD', 'Total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(rows.length);
  });

  it('FX-10: an account row carries its own net, a heading carries no figures and a heading says its rate', () => {
    const rows = foreignCurrencyRows(result([block([account({ realized: 1200, unrealized: 2000 })])]));

    const heading = rows[0];
    expect(heading.detail).toBe('rate 84 INR');
    expect([heading.dueSource, heading.carryingBase, heading.realized, heading.net]).toEqual([null, null, null, null]);

    const row = rows[1];
    expect([row.detail, row.dueSource, row.carryingBase, row.revaluedBase, row.unrealized, row.realized, row.net]).toEqual([
      'Receivable', 500, 40000, 42000, 2000, 1200, 3200,
    ]);
  });

  it('FX-11: one currency with one account is a heading and its account, with no total to add up', () => {
    const rows = foreignCurrencyRows(result([block([account()])]));

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'account']);
  });

  it('FX-12: a currency with no rate has blank revalued and unrealized cells and its net is its realized part', () => {
    const noRate = block([account({ revaluedBase: null, unrealized: null, realized: -100 })], { rate: null, needsRate: true, revaluedBase: null, unrealized: null, realized: -100, net: -100 });

    const rows = foreignCurrencyRows(result([noRate]));

    expect(rows[0].detail).toBe('no rate given');
    expect(rows[1].needsRate).toBe(true);
    expect([rows[1].revaluedBase, rows[1].unrealized, rows[1].net]).toEqual([null, null, -100]);
  });

  it('FX-13: what no currency claims is its own row, and with it a single currency still has a grand total', () => {
    const rows = foreignCurrencyRows(result([block([account()])], { unassignedRealized: 50, totalRealized: 1250, totalNet: 3250 }));

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'account', 'unassigned', 'total']);

    const unassigned = rows[2];
    expect([unassigned.name, unassigned.realized, unassigned.net, unassigned.dueSource]).toEqual(['Not tied to a currency', 50, 50, null]);

    const total = rows[3];
    expect([total.realized, total.unrealized, total.net]).toEqual([1250, 2000, 3250]);
  });

  it('FX-14: no currencies and nothing unassigned is no rows', () => {
    expect(foreignCurrencyRows(result([]))).toEqual([]);
  });

  it('FX-15: the sentence says realized, unrealized and net, with a loss shown by its minus sign', () => {
    expect(foreignCurrencySentence(result([], { totalRealized: 750, totalUnrealized: 1850, totalNet: 2600 }), money)).toBe(
      'Realized ₹750.00 + unrealized ₹1850.00 = net ₹2600.00.',
    );
    expect(foreignCurrencySentence(result([], { totalRealized: -100, totalUnrealized: 50, totalNet: -50 }), money)).toBe(
      'Realized −₹100.00 + unrealized ₹50.00 = net −₹50.00.',
    );
  });

  it('FX-16: currencies with no rate are named, singular and plural, and none is silent', () => {
    expect(missingRatesSentence({ currenciesWithoutRate: [] })).toBe('');
    expect(missingRatesSentence({ currenciesWithoutRate: ['EUR'] })).toContain('No rate for EUR, so its unrealized');
    expect(missingRatesSentence({ currenciesWithoutRate: ['EUR', 'GBP'] })).toContain('No rate for EUR, GBP, so their unrealized');
  });

  it('FX-17: only a rate above nought is sent, by upper-case code', () => {
    expect(ratesOf({ usd: 84, EUR: 0, GBP: null, JPY: undefined, ' chf ': 91.5, AUD: Number.NaN })).toEqual({ USD: 84, CHF: 91.5 });
  });

  it('FX-18: a refused rate is matched to its currency from the key the server gave it', () => {
    expect(rateKeyCurrency('Rates[USD]')).toBe('USD');
    expect(rateKeyCurrency('rates[eur]')).toBe('EUR');
    expect(rateKeyCurrency('AsAtDate')).toBeNull();
  });

  it('FX-19: a currency on the filter says how many accounts hold a balance in it', () => {
    expect(currencyChoiceLabel({ currencyCode: 'USD', accounts: 3 })).toBe('USD · 3 accounts');
    expect(currencyChoiceLabel({ currencyCode: 'EUR', accounts: 1 })).toBe('EUR · 1 account');
  });
});
