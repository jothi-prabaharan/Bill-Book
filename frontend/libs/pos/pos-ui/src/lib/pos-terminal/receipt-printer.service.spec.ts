import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ReceiptPrinterService } from './receipt-printer.service';

const SALE = {
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
};

/** The receipt's images, decoded: each code is an SVG data URI. */
function images(page: string): string {
  return [...page.matchAll(/src="data:image\/svg\+xml;charset=utf-8,([^"]+)"/g)]
    .map((m) => decodeURIComponent(m[1]))
    .join('\n') + page.replace(/src="[^"]*"/g, '');
}

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
    // A stand-in for the zxing writer: the real one loads wasm.
    service.codeWriter = () =>
      Promise.resolve({
        writeBarcode: (input: string, options: { format: string }) =>
          Promise.resolve({ svg: `<svg data-format="${options.format}" data-text="${input}"/>`, error: '' }),
      });
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
    http.expectOne('/api/sales/invoices/42/upi-link').flush({ payUri: null });
    await done;

    expect(printed).toHaveLength(1);
    expect(printed[0]).toContain('@page { size: 58mm auto; margin: 0; }');
    expect(printed[0]).toContain('DUPLICATE');
    expect(printed[0]).toContain('POS/2627/00001');
  });

  it('prints the invoice number as a barcode, and no UPI QR on a paid sale (TK-164)', async () => {
    const done = service.printInvoice(42);
    http.expectOne('/api/sales/invoices/42').flush(SALE);
    http.expectOne('/api/organizations/current').flush({ name: 'Kumar Stores' });
    http.expectOne('/api/sales/invoices/42/upi-link').flush({ payUri: null });
    await done;

    const page = images(printed[0]);
    expect(page).toContain('data-format="Code128" data-text="POS/2627/00001"');
    expect(page).not.toContain('QRCode');
    expect(page).not.toContain('Scan to pay');
  });

  it('adds a UPI QR for what is still owed', async () => {
    const done = service.printInvoice(42);
    http.expectOne('/api/sales/invoices/42').flush(SALE);
    http.expectOne('/api/organizations/current').flush({ name: 'Kumar Stores' });
    http.expectOne('/api/sales/invoices/42/upi-link').flush({ payUri: 'upi://pay?pa=shop@okhdfcbank&am=18.00' });
    await done;

    const page = images(printed[0]);
    expect(page).toContain('data-format="QRCode" data-text="upi://pay?pa=shop@okhdfcbank&am=18.00"');
    expect(page).toContain('Scan to pay with any UPI app');
  });

  it('still prints when the UPI link cannot be read', async () => {
    const done = service.printInvoice(42);
    http.expectOne('/api/sales/invoices/42').flush(SALE);
    http.expectOne('/api/organizations/current').flush({ name: 'Kumar Stores' });
    http.expectOne('/api/sales/invoices/42/upi-link').flush(null, { status: 500, statusText: 'Server Error' });
    await done;

    expect(printed).toHaveLength(1);
    expect(printed[0]).not.toContain('Scan to pay');
  });

  it('prints a test page at the chosen width', () => {
    service.printTest({ paper: 80 });
    expect(printed[0]).toContain('80 mm, 48 columns');
  });
});
