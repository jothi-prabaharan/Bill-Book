import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { SalesLookupService } from './sales-lookup.service';

/**
 * The sales pickers' lookups: which URL each asks, and that the answer comes
 * back as served.
 */
describe('SalesLookupService', () => {
  let service: SalesLookupService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(SalesLookupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  /** The request goes out from a promise, a microtask after the call. */
  const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

  it('asks for active customers only, by trimmed search term', async () => {
    const pending = service.customers('  ravi ');
    await settle();

    httpMock
      .expectOne('/api/contacts?search=ravi&role=customer&includeInactive=false')
      .flush([
        {
          contactId: 7,
          contactCode: 'C7',
          displayName: 'Ravi Stores',
          isCustomer: true,
          gstin: '33ABCDE1234F1Z5',
          currencyCode: 'INR',
          isActive: true,
        },
      ]);

    const rows = await pending;
    expect(rows).toHaveLength(1);
    expect(rows[0].displayName).toBe('Ravi Stores');
    expect(rows[0].gstin).toBe('33ABCDE1234F1Z5');
  });

  it('asks for every active customer when the search is blank', async () => {
    const pending = service.customers('   ');
    await settle();

    httpMock.expectOne('/api/contacts?role=customer&includeInactive=false').flush([]);

    expect(await pending).toEqual([]);
  });

  it('asks for active items by search term, which also matches a whole barcode', async () => {
    const pending = service.items(' 8901030865278 ');
    await settle();

    httpMock
      .expectOne('/api/items?search=8901030865278&includeInactive=false')
      .flush([
        {
          itemId: 3,
          itemCode: 'SOAP-1',
          itemName: 'Sandal soap',
          inventoryUomCode: 'PCS',
          salesPrice: 42,
          mrp: 45,
          isActive: true,
        },
      ]);

    const rows = await pending;
    expect(rows.map((row) => row.itemCode)).toEqual(['SOAP-1']);
    expect(rows[0].salesPrice).toBe(42);
  });

  it('asks for every active item when the search is blank', async () => {
    const pending = service.items('');
    await settle();

    httpMock.expectOne('/api/items?includeInactive=false').flush([]);

    expect(await pending).toEqual([]);
  });
});
