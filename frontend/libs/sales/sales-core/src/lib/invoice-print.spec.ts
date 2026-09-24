import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { InvoiceService } from './invoice.service';

/**
 * Printing an invoice asks Sales, not Printing: Sales holds the invoice and
 * builds its data, then has Printing lay it out (TK-81).
 */
describe('InvoiceService.print', () => {
  let service: InvoiceService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(InvoiceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('reads the laid-out invoice from Sales’ print route', async () => {
    const pending = service.print(7);

    const request = httpMock.expectOne('/api/sales/invoices/7/print');
    expect(request.request.method).toBe('GET');
    request.flush({ html: '<p>INV-0007</p>', pageCount: 1, unknownTags: [], printTemplateId: 5 });

    const printed = await pending;
    expect(printed.html).toBe('<p>INV-0007</p>');
    expect(printed.printTemplateId).toBe(5);
  });
});

/** The archived PDF is Sales' own file, fetched as a blob (TK-22). */
describe('InvoiceService.downloadPdf', () => {
  let service: InvoiceService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(InvoiceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('reads the archived file from the pdf route as a blob', async () => {
    const pending = service.downloadPdf(7);

    const request = httpMock.expectOne('/api/sales/invoices/7/pdf');
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['%PDF'], { type: 'application/pdf' }));

    const blob = await pending;
    expect(blob.type).toBe('application/pdf');
  });

  it('names the file after the invoice number with no slashes', () => {
    expect(InvoiceService.pdfFileName('INV/26-27/0001')).toBe('INV-26-27-0001.pdf');
  });
});
