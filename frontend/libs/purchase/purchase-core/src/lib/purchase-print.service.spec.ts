import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  PurchasePrintKind,
  PurchasePrintService,
  purchaseDocumentRoute,
  purchasePrintLabel,
  purchasePrintRoute,
} from './purchase-print.service';

describe('PurchasePrintService', () => {
  let service: PurchasePrintService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PurchasePrintService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it.each<[PurchasePrintKind, string]>([
    ['purchase-orders', '/api/purchase/purchase-orders/7/print'],
    ['goods-receipts', '/api/purchase/goods-receipts/7/print'],
    ['bills', '/api/purchase/bills/7/print'],
    ['debit-notes', '/api/purchase/debit-notes/7/print'],
  ])('PPRINT-01: asks for the %s print at %s', async (kind, url) => {
    const pending = service.print(kind, 7);
    http.expectOne(url).flush({ html: '<p>x</p>', pageCount: 1, unknownTags: [], printTemplateId: null });

    expect(await pending).toMatchObject({ html: '<p>x</p>', pageCount: 1 });
  });

  it('PPRINT-02: the print and document routes name the same document', () => {
    expect(purchasePrintRoute('bills', 12)).toEqual(['/purchase', 'bills', '12', 'print']);
    expect(purchaseDocumentRoute('bills', 12)).toEqual(['/purchase', 'bills', '12']);
  });

  it('PPRINT-03: every kind has a label for the page to say what it printed', () => {
    expect(purchasePrintLabel('purchase-orders')).toBe('purchase order');
    expect(purchasePrintLabel('goods-receipts')).toBe('goods receipt');
    expect(purchasePrintLabel('bills')).toBe('bill');
    expect(purchasePrintLabel('debit-notes')).toBe('debit note');
  });
});
