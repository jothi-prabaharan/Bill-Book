import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@bill-book/auth';
import { ScanToOpenService, documentPath } from './scan-to-open.service';

describe('documentPath (TK-162)', () => {
  it('opens each kind of document on its own screen', () => {
    expect(documentPath({ transactionType: 'Invoice', transactionId: 42 })).toBe('/sales/invoices/42');
    expect(documentPath({ transactionType: 'GoodsReceipt', transactionId: 7 })).toBe('/purchase/goods-receipts/7');
    expect(documentPath({ transactionType: 'DebitNote', transactionId: 3 })).toBe('/purchase/debit-notes/3');
  });

  it('opens nothing for a kind no screen shows, or no match', () => {
    expect(documentPath({ transactionType: 'Unknown', transactionId: 1 })).toBeNull();
    expect(documentPath(null)).toBeNull();
  });
});

describe('ScanToOpenService (TK-162)', () => {
  let service: ScanToOpenService;
  let http: HttpTestingController;
  const navigateByUrl = vi.fn().mockResolvedValue(true);
  const canView = vi.fn((module: string) => module === 'sales' || module === 'purchase');

  beforeEach(() => {
    navigateByUrl.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigateByUrl } },
        { provide: AuthService, useValue: { canView } },
      ],
    });
    service = TestBed.inject(ScanToOpenService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('scanning a printed invoice code opens that invoice', async () => {
    const opened = service.open(' INV-0042 ');

    http.expectOne('/api/sales/transactions/by-number?no=INV-0042').flush({ transactionType: 'Invoice', transactionId: 42 });
    http.expectOne('/api/purchase/transactions/by-number?no=INV-0042').flush(null, { status: 404, statusText: 'Not Found' });

    expect(await opened).toBe(true);
    expect(navigateByUrl).toHaveBeenCalledWith('/sales/invoices/42');
  });

  it('opens nothing for a code no document in the branch has', async () => {
    const opened = service.open('8901030865278');

    http.expectOne((r) => r.url.startsWith('/api/sales/transactions/by-number')).flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne((r) => r.url.startsWith('/api/purchase/transactions/by-number')).flush(null, { status: 404, statusText: 'Not Found' });

    expect(await opened).toBe(false);
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('asks only the modules the user may view', async () => {
    canView.mockImplementation((module: string) => module === 'purchase');
    const found = service.find('GRN-0007');

    http.expectOne('/api/purchase/transactions/by-number?no=GRN-0007').flush({ transactionType: 'GoodsReceipt', transactionId: 7 });

    expect(await found).toBe('/purchase/goods-receipts/7');
    canView.mockImplementation((module: string) => module === 'sales' || module === 'purchase');
  });

  it('leaves a burst that a screen scan field already took', () => {
    const open = vi.spyOn(service, 'open').mockResolvedValue(true);
    const keys = [...'INV-0042', 'Enter'];
    keys.forEach((key, i) => {
      const event = { key, timeStamp: i * 5, target: document.body, defaultPrevented: key === 'Enter', preventDefault: vi.fn() };
      service.onKeyDown(event as unknown as KeyboardEvent);
    });

    expect(open).not.toHaveBeenCalled();
  });

  it('opens a burst aimed at the page', () => {
    const open = vi.spyOn(service, 'open').mockResolvedValue(true);
    [...'INV-0042', 'Enter'].forEach((key, i) => {
      const event = { key, timeStamp: 1000 + i * 5, target: document.body, defaultPrevented: false, preventDefault: vi.fn() };
      service.onKeyDown(event as unknown as KeyboardEvent);
    });

    expect(open).toHaveBeenCalledWith('INV-0042');
  });
});
