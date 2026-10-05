import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ReceiptPrinterService } from './receipt-printer.service';

/** Printing a till receipt through the browser (TK-41, TK-155): settings, the invoice it reads, the page it prints. */
describe('ReceiptPrinterService', () => {
  let service: ReceiptPrinterService;
  let http: HttpTestingController;
  const printed: string[] = [];

  beforeEach(() => {
    printed.length = 0;
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ReceiptPrinterService);
    service.print = (html) => printed.push(html);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('keeps the paper and footer for this till', () => {
    service.saveSettings({ paper: 58, footer: 'Come again' });
    expect(service.settings()).toEqual({ paper: 58, footer: 'Come again' });
  });

  it('prints the posted invoice at the roll width, marked DUPLICATE on a reprint', async () => {
    service.saveSettings({ paper: 58 });
    const done = service.printInvoice(42, true);

    http.expectOne('/api/sales/invoices/42').flush({
      documentNo: 'POS/2627/00001',
      documentDate: '2026-10-05',
      status: 'Posted',
      subTotal: 100,
      discountAmount: 0,
      taxableAmount: 100,
      cgstAmount: 9,
      sgstAmount: 9,
      igstAmount: 0,
      cessAmount: 0,
      roundOffAmount: 0,
      totalAmount: 118,
      lines: [],
      tenders: [{ mode: 'Cash', amount: 118 }],
    });
    http.expectOne('/api/organizations/current').flush({ name: 'Kumar Stores' });
    await done;

    expect(printed).toHaveLength(1);
    expect(printed[0]).toContain('@page { size: 58mm auto; margin: 0; }');
    expect(printed[0]).toContain('DUPLICATE');
    expect(printed[0]).toContain('POS/2627/00001');
  });

  it('prints a test page at the chosen width', () => {
    service.printTest({ paper: 80 });
    expect(printed[0]).toContain('80 mm, 48 columns');
  });
});
