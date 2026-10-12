import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { PurchaseLookupService } from './purchase-lookup.service';

describe('PurchaseLookupService', () => {
  let service: PurchaseLookupService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PurchaseLookupService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("PLOOK-01: reads tax rates through Purchase's own choices route, not the accounting tax master (TK-192)", async () => {
    const pending = service.purchaseTaxRates();
    http
      .expectOne((r) => r.url === '/api/choices/purchase/tax-rates')
      .flush([{ taxMasterId: 4, taxGroupId: 4, taxName: 'GST 18%', totalRate: 18, cgstRate: 9, sgstRate: 9, igstRate: 18, cessRate: 0 }]);

    const [rate] = await pending;
    expect(rate).toMatchObject({ taxGroupId: 4, taxName: 'GST 18%', isPurchase: true, isCurrent: true, isActive: true });
  });
});
