import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { PosSaleService, TenderAccount } from './pos-sale.service';

/** The till posts through TK-39 and lists its tender accounts (TK-40). */
describe('PosSaleService', () => {
  let service: PosSaleService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PosSaleService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the sale to the POS endpoint', async () => {
    const pending = service.sell({
      tillId: 1,
      documentDate: '2026-09-24',
      contactId: 42,
      lines: [],
      tenders: [{ mode: 'Cash', amount: 200, bankAccountId: 11 }],
    });

    const request = http.expectOne('/api/sales/pos/sales');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.tenders[0]).toEqual({ mode: 'Cash', amount: 200, bankAccountId: 11 });
    request.flush({ invoiceId: 5, documentNo: 'POS-0001', totalAmount: 118, changeAmount: 82 });

    expect((await pending).changeAmount).toBe(82);
  });

  it('reads the tender accounts', async () => {
    const pending = service.tenderAccounts();
    http.expectOne('/api/bank-accounts/tender-options').flush([]);
    expect(await pending).toEqual([]);
  });

  it('defaults cash to a cash account and card to a non-cash one, the default first', () => {
    const accounts: TenderAccount[] = [
      { bankAccountId: 1, accountName: 'Drawer', accountType: 'Cash', isDefault: false },
      { bankAccountId: 2, accountName: 'HDFC current', accountType: 'Current', isDefault: false },
      { bankAccountId: 3, accountName: 'Card terminal', accountType: 'Current', isDefault: true },
    ];

    expect(PosSaleService.defaultAccountFor('Cash', accounts)?.bankAccountId).toBe(1);
    expect(PosSaleService.defaultAccountFor('Card', accounts)?.bankAccountId).toBe(3);
    expect(PosSaleService.defaultAccountFor('Upi', [])).toBeNull();
  });
});
