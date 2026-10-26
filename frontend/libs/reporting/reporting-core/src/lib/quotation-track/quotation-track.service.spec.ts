import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  QUOTE_OUTCOMES,
  QuoteOutcome,
  QuotationTrackQuote,
  QuotationTrackResult,
  appendQuotationTrackPage,
  conversionLink,
  conversionRateLabel,
  daysToConvertLabel,
  hasMoreQuotationTrack,
  isConverted,
  isForeignQuote,
  quoteLink,
  quoteOutcomeLabel,
  quoteOutcomeTag,
  quotationTrackRows,
  quotationTrackTabs,
} from './quotation-track.models';
import { QuotationTrackService } from './quotation-track.service';

/** The Quotation Track Report's calls and the helpers its page uses (TK-231). */

function setup(answer: unknown = { quotes: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [QuotationTrackService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(QuotationTrackService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const quote = (over: Partial<QuotationTrackQuote> = {}): QuotationTrackQuote => ({
  quoteId: 1,
  documentNo: 'QTE-0001',
  documentDate: '2026-10-02',
  validUntil: '2026-11-02',
  contactId: 101,
  contactCode: 'ZENITH',
  contactName: 'Zenith Traders',
  currencyCode: 'INR',
  totalInCurrency: 5000,
  total: 5000,
  outcome: 'Open',
  convertedTo: null,
  daysToConvert: null,
  ...over,
});

const result = (quotes: QuotationTrackQuote[], over: Partial<QuotationTrackResult> = {}): QuotationTrackResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  quotes,
  totalQuotes: quotes.length,
  pageNumber: 1,
  pageSize: 25,
  outcomeCounts: { Open: quotes.length, Expired: 0, Accepted: 0, ConvertedToOrder: 0, ConvertedToInvoice: 0, Declined: 0, Void: 0 },
  summary: { raised: quotes.length, raisedValue: 0, converted: 0, convertedValue: 0, conversionRateByCount: 0, conversionRateByValue: 0 },
  totalValue: quotes.reduce((t, q) => t + q.total, 0),
  ...over,
});

describe('QuotationTrackService', () => {
  it('QT-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/quotation-track-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'outcome', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('QT-02: sends the customer, outcome and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: 102, outcome: 'ConvertedToInvoice', pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('102');
    expect(params.get('outcome')).toBe('ConvertedToInvoice');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('QT-03: gives back what the endpoint answered', async () => {
    const answer = result([quote()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('QT-04: reads the customers the filter offers from the report’s own lookup', async () => {
    const customers = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const { service, get } = setup(customers);

    expect(await service.customers()).toEqual(customers);
    expect(get.mock.calls[0][0]).toBe('/api/statements/quotation-track-report/customers');
  });
});

describe('quotation track helpers', () => {
  it('QT-05: the outcomes run along a quote’s life and read as words', () => {
    expect(QUOTE_OUTCOMES.map((o) => o.value)).toEqual(['Open', 'Expired', 'Accepted', 'ConvertedToOrder', 'ConvertedToInvoice', 'Declined', 'Void']);
    expect(quoteOutcomeLabel('ConvertedToOrder')).toBe('Converted to order');
    expect(quoteOutcomeLabel('ConvertedToInvoice')).toBe('Converted to invoice');
  });

  it('QT-06: converted reads as won, declined as lost, expired as a warning and void as set aside', () => {
    expect(quoteOutcomeTag('ConvertedToOrder')).toBe('tag-success');
    expect(quoteOutcomeTag('ConvertedToInvoice')).toBe('tag-success');
    expect(quoteOutcomeTag('Declined')).toBe('tag-danger');
    expect(quoteOutcomeTag('Expired')).toBe('tag-warning');
    expect(quoteOutcomeTag('Accepted')).toBe('tag-info');
    expect(quoteOutcomeTag('Open')).toBe('tag-outline');
    expect(quoteOutcomeTag('Void')).toBe('tag-neutral');
  });

  it('QT-07: the tabs are All and then every outcome, with a nought kept', () => {
    const tabs = quotationTrackTabs({ Open: 3, Expired: 3, Accepted: 1, ConvertedToOrder: 1, ConvertedToInvoice: 2, Declined: 1, Void: 1 });

    expect(tabs.map((t) => t.label)).toEqual(['All', 'Open', 'Expired', 'Accepted', 'Converted to order', 'Converted to invoice', 'Declined', 'Void']);
    expect(tabs.map((t) => t.value)).toEqual(['', 'Open', 'Expired', 'Accepted', 'ConvertedToOrder', 'ConvertedToInvoice', 'Declined', 'Void']);
    expect(tabs.map((t) => t.count)).toEqual([11, 3, 3, 1, 1, 2, 1, 1]);
  });

  it('QT-08: All leaves the void ones out, as the list does without an outcome', () => {
    const counts: Record<QuoteOutcome, number> = { Open: 1, Expired: 0, Accepted: 0, ConvertedToOrder: 0, ConvertedToInvoice: 3, Declined: 0, Void: 4 };

    expect(quotationTrackTabs(counts)[0].count).toBe(4);
  });

  it('QT-09: with no counts yet every tab reads nought', () => {
    expect(quotationTrackTabs(undefined).map((t) => t.count)).toEqual([0, 0, 0, 0, 0, 0, 0, 0]);
  });

  it('QT-10: only the two converted outcomes are converted', () => {
    expect(isConverted('ConvertedToOrder')).toBe(true);
    expect(isConverted('ConvertedToInvoice')).toBe(true);
    for (const outcome of ['Open', 'Expired', 'Accepted', 'Declined', 'Void'] as const) {
      expect(isConverted(outcome)).toBe(false);
    }
  });

  it('QT-11: a foreign quote is told by a currency other than the branch’s, whatever the case', () => {
    expect(isForeignQuote({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignQuote({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignQuote({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('QT-12: a quote opens at its page, and an invoice or order it became at its own', () => {
    expect(quoteLink(7)).toBe('/sales/quotes/7');
    expect(conversionLink({ typeCode: 'INV', documentId: 9 })).toBe('/sales/invoices/9');
    expect(conversionLink({ typeCode: 'POS', documentId: 9 })).toBe('/sales/invoices/9');
    expect(conversionLink({ typeCode: 'SOR', documentId: 4 })).toBe('/sales/sales-orders/4');
    expect(conversionLink({ typeCode: 'DCH', documentId: 4 })).toBeNull();
  });

  it('QT-13: the days a conversion took read as same day, a day or n days, and nothing is nothing', () => {
    expect(daysToConvertLabel(0)).toBe('Same day');
    expect(daysToConvertLabel(1)).toBe('1 day');
    expect(daysToConvertLabel(9)).toBe('9 days');
    expect(daysToConvertLabel(null)).toBe('');
  });

  it('QT-14: a rate reads to one decimal place with its percent sign, and nothing raised reads as a dash', () => {
    expect(conversionRateLabel(27.3)).toBe('27.3%');
    expect(conversionRateLabel(50)).toBe('50.0%');
    expect(conversionRateLabel(0)).toBe('0.0%');
    expect(conversionRateLabel(null)).toBe('—');
  });

  it('QT-15: the grid has a row for each quote and then the value of every quote the filters match', () => {
    const rows = quotationTrackRows(result([quote({ quoteId: 1 }), quote({ quoteId: 2, documentNo: 'QTE-0002' })]));

    expect(rows.map((r) => r.kind)).toEqual(['quote', 'quote', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
    expect(rows[2].name).toBe('Total');
    expect(rows[2].total).toBe(10000);
  });

  it('QT-16: when not every quote is loaded the total row says it is of all of them and carries the server’s figure', () => {
    const rows = quotationTrackRows(result([quote()], { totalQuotes: 120, totalValue: 123456 }));

    expect(rows[rows.length - 1].name).toBe('Total of all 120 quotes');
    expect(rows[rows.length - 1].total).toBe(123456);
  });

  it('QT-17: a quote row links to its page and carries its dates and outcome', () => {
    const [row] = quotationTrackRows(result([quote({ quoteId: 7, outcome: 'Expired' })]));

    expect(row.link).toBe('/sales/quotes/7');
    expect([row.documentDate, row.validUntil, row.outcome]).toEqual(['2026-10-02', '2026-11-02', 'Expired']);
    expect([row.convertedNo, row.convertedLink, row.convertedKind, row.daysToConvert]).toEqual([null, null, null, null]);
  });

  it('QT-18: a converted quote names the invoice or order it became, links to it and says how long it took', () => {
    const rows = quotationTrackRows(
      result([
        quote({
          quoteId: 1,
          outcome: 'ConvertedToInvoice',
          convertedTo: { typeCode: 'INV', documentId: 9, documentNo: 'INV-0009', documentDate: '2026-10-11' },
          daysToConvert: 9,
        }),
        quote({
          quoteId: 2,
          outcome: 'ConvertedToOrder',
          convertedTo: { typeCode: 'SOR', documentId: 4, documentNo: 'SOR-0004', documentDate: '2026-10-04' },
          daysToConvert: 2,
        }),
      ]),
    );

    expect([rows[0].convertedNo, rows[0].convertedLink, rows[0].convertedKind, rows[0].daysToConvert]).toEqual(['INV-0009', '/sales/invoices/9', 'invoice', 9]);
    expect([rows[1].convertedNo, rows[1].convertedLink, rows[1].convertedKind, rows[1].daysToConvert]).toEqual(['SOR-0004', '/sales/sales-orders/4', 'order', 2]);
  });

  it('QT-19: a quote in another currency carries its own total, and one in the branch’s does not', () => {
    const rows = quotationTrackRows(
      result([quote({ currencyCode: 'USD', totalInCurrency: 250, total: 20000 }), quote({ quoteId: 2, currencyCode: 'INR' })]),
    );

    expect(rows[0].ownTotal).toBe(250);
    expect(rows[1].ownTotal).toBeNull();
  });

  it('QT-20: with no quotes there are no rows, not even the total', () => {
    expect(quotationTrackRows(result([]))).toEqual([]);
  });

  it('QT-21: the next page is added to the ones loaded and the counts, summary and total are the latest answer’s', () => {
    const first = result([quote({ quoteId: 1 })], { totalQuotes: 3, pageNumber: 1 });
    const next = result([quote({ quoteId: 2 })], { totalQuotes: 3, pageNumber: 2, totalValue: 9 });

    const merged = appendQuotationTrackPage(first, next);

    expect(merged.quotes.map((q) => q.quoteId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totalValue).toBe(9);
  });

  it('QT-22: there is more while fewer quotes are loaded than match', () => {
    expect(hasMoreQuotationTrack({ quotes: [quote()], totalQuotes: 2 })).toBe(true);
    expect(hasMoreQuotationTrack({ quotes: [quote(), quote({ quoteId: 2 })], totalQuotes: 2 })).toBe(false);
  });
});
