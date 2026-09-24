import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { PrinterBridge, PrinterTarget, ReceiptPrinterService } from './receipt-printer.service';

/** Printing a till receipt (TK-41): settings, the Electron bridge, and the invoice it reads. */
describe('ReceiptPrinterService', () => {
  let service: ReceiptPrinterService;
  let http: HttpTestingController;
  const sent: { bytes: Uint8Array; target: PrinterTarget }[] = [];
  const host = globalThis as { billBookPrinter?: PrinterBridge };

  beforeEach(() => {
    sent.length = 0;
    localStorage.clear();
    host.billBookPrinter = {
      print: (bytes, target) => {
        sent.push({ bytes, target });
        return Promise.resolve();
      },
    };
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ReceiptPrinterService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    delete host.billBookPrinter;
  });

  it('reads stored settings field by field', () => {
    expect(ReceiptPrinterService.parseSettings('{"paper":58,"target":{"kind":"network","host":" 10.0.0.9 "}}'))
      .toEqual({ paper: 58, target: { kind: 'network', host: '10.0.0.9', port: 9100 }, footer: undefined });
    expect(ReceiptPrinterService.parseSettings('{"paper":12,"target":{"kind":"device","path":""}}'))
      .toEqual({ paper: 80, target: null, footer: undefined });
  });

  it('refuses to print when no printer is set up', async () => {
    await expect(service.printInvoice(7)).rejects.toThrow(/No receipt printer/);
  });

  it('refuses to print outside the desktop app', async () => {
    delete host.billBookPrinter;
    service.saveSettings({ paper: 80, target: { kind: 'device', path: '/dev/usb/lp0' } });

    expect(service.canPrint()).toBe(false);
    await expect(service.printInvoice(7)).rejects.toThrow(/desktop app/);
  });

  it('reads the posted invoice and the branch, then sends the bytes to the chosen printer', async () => {
    service.saveSettings({ paper: 58, target: { kind: 'network', host: '10.0.0.9', port: 9100 } });

    const pending = service.printInvoice(7, true);
    http.expectOne('/api/sales/invoices/7').flush({
      documentNo: 'POS-0007',
      documentDate: '2026-09-24',
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
      changeAmount: 2,
      lines: [],
      tenders: [{ mode: 'Cash', amount: 120 }],
    });
    http.expectOne('/api/organizations/current').flush({ name: 'Anna Stores' });
    await pending;

    expect(sent).toHaveLength(1);
    expect(sent[0].target).toEqual({ kind: 'network', host: '10.0.0.9', port: 9100 });
    const text = String.fromCharCode(...sent[0].bytes);
    expect(text).toContain('*** DUPLICATE ***');
    expect(text).toContain('POS-0007');
  });
});
