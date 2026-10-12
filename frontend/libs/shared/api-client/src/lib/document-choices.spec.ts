import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { DocumentChoicesService } from './document-choices.service';

/**
 * A create page's option lists come from the asking module's own routes (TK-192),
 * so a clerk without accounting or settings access still gets them.
 */
describe('DocumentChoicesService', () => {
  let service: DocumentChoicesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(DocumentChoicesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('CHOICE-01: reads tax rates through the asking module, for the document date', async () => {
    const pending = service.taxRates('purchase', '2026-10-12');
    const request = http.expectOne((r) => r.url === '/api/choices/purchase/tax-rates');
    expect(request.request.params.get('on')).toBe('2026-10-12');
    request.flush([{ taxMasterId: 4, taxGroupId: 4, taxName: 'GST 18%', totalRate: 18, cgstRate: 9, sgstRate: 9, igstRate: 18, cessRate: 0 }]);
    expect((await pending)[0].taxName).toBe('GST 18%');
  });

  it('CHOICE-02: leaves the date to the server when the page has none', async () => {
    const pending = service.taxRates('sales');
    const request = http.expectOne((r) => r.url === '/api/choices/sales/tax-rates');
    expect(request.request.params.has('on')).toBe(false);
    request.flush([]);
    expect(await pending).toEqual([]);
  });

  it('CHOICE-03: reads payment terms and accounts through the asking module', async () => {
    const terms = service.paymentTerms('contacts');
    http.expectOne('/api/choices/contacts/payment-terms').flush([]);
    expect(await terms).toEqual([]);

    const accounts = service.accounts('banking');
    http.expectOne('/api/choices/banking/accounts').flush([]);
    expect(await accounts).toEqual([]);
  });

  it('CHOICE-04: previews the next number of one series', async () => {
    const pending = service.nextNumber('sales', 'INV', '2026-10-12');
    const request = http.expectOne((r) => r.url === '/api/choices/sales/next-number/INV');
    expect(request.request.params.get('on')).toBe('2026-10-12');
    request.flush({ seriesCode: 'INV', number: 'INV/2627/00042' });
    expect((await pending).number).toBe('INV/2627/00042');
  });

  it("CHOICE-05: reads one document type's print templates through its module", async () => {
    const pending = service.printTemplates('purchase', 'BIL');
    http.expectOne('/api/print-templates/choices/purchase/BIL').flush([{ printTemplateId: 3, templateName: 'Standard', isDefault: true }]);
    expect((await pending)[0].isDefault).toBe(true);
  });

  it('CHOICE-06: reads the master exchange rate, and says none when none is on file', async () => {
    const found = service.exchangeRate('USD', 'INR', '2026-10-12');
    const request = http.expectOne((r) => r.url === '/api/rates/exchange');
    expect(request.request.params.get('from')).toBe('USD');
    expect(request.request.params.get('to')).toBe('INR');
    expect(request.request.params.get('on')).toBe('2026-10-12');
    request.flush({ exchangeRateId: 1, fromCurrencyCode: 'USD', toCurrencyCode: 'INR', rateDate: '2026-10-11', rate: 83.25, source: 'Manual' });
    expect((await found)?.rate).toBe(83.25);

    const missing = service.exchangeRate('AED', 'INR');
    http.expectOne((r) => r.url === '/api/rates/exchange').flush(null, { status: 404, statusText: 'Not Found' });
    expect(await missing).toBeNull();
  });

  it('CHOICE-07: passes on a failure that is not a missing rate', async () => {
    const pending = service.exchangeRate('USD', 'INR');
    http.expectOne((r) => r.url === '/api/rates/exchange').flush(null, { status: 500, statusText: 'Server Error' });
    await expect(pending).rejects.toBeTruthy();
  });
});
