import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { PosLookupService, WALK_IN_CONTACT_CODE } from './pos-lookup.service';

/**
 * The till's lookups: which URL each one asks, and what it makes of the answer.
 * The walk-in rule is the one with teeth — a substring search must not hand the
 * till a contact that merely contains the code.
 */
describe('PosLookupService', () => {
  let service: PosLookupService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PosLookupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  /** The request goes out from a promise, a microtask after the call. */
  const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

  const customer = (contactId: number, contactCode: string) => ({
    contactId,
    contactCode,
    displayName: `Customer ${contactId}`,
    isCustomer: true,
    gstin: null,
    currencyCode: 'INR',
    isActive: true,
  });

  it('asks for active customers only, by search term', async () => {
    const pending = service.customers('  ravi ');
    await settle();

    const request = httpMock.expectOne(
      '/api/contacts?search=ravi&role=customer&includeInactive=false',
    );
    request.flush([customer(7, 'C7')]);

    expect(await pending).toHaveLength(1);
  });

  it('finds the walk-in customer by its exact code', async () => {
    const pending = service.walkInCustomer();
    await settle();

    httpMock
      .expectOne(
        `/api/contacts?search=${WALK_IN_CONTACT_CODE}&role=customer&includeInactive=false`,
      )
      .flush([customer(3, 'WALKIN-2'), customer(4, 'walkin')]);

    expect((await pending)?.contactId).toBe(4);
  });

  it('answers null when no contact carries the walk-in code exactly', async () => {
    const pending = service.walkInCustomer();
    await settle();

    httpMock
      .expectOne(() => true)
      .flush([customer(3, 'WALKIN-2')]);

    expect(await pending).toBeNull();
  });

  it('asks for active items only, by search term', async () => {
    const pending = service.items('soap');
    await settle();

    httpMock.expectOne('/api/items?search=soap&includeInactive=false').flush([]);

    expect(await pending).toEqual([]);
  });

  it('sells an item at its sales price, inclusive as the item master says', async () => {
    const pending = service.cartItem(12);
    await settle();

    httpMock.expectOne('/api/items/12').flush({
      itemId: 12,
      itemCode: 'ITM-0012',
      itemName: 'Soap',
      inventoryUomCode: 'PCS',
      salesPrice: 45,
      mrp: 50,
      isActive: true,
      taxGroupId: 18,
      taxPreference: 'Taxable',
      isPriceInclusiveOfTax: false,
    });

    expect(await pending).toEqual({
      itemId: 12,
      itemCode: 'ITM-0012',
      itemName: 'Soap',
      unitPrice: 45,
      isPriceInclusive: false,
      taxTreatment: 'Taxable',
      taxGroupId: 18,
    });
  });

  it('falls back to the MRP, tax-inclusive, when the item has no sales price', async () => {
    const pending = service.cartItem(12);
    await settle();

    httpMock.expectOne('/api/items/12').flush({
      itemId: 12,
      itemCode: 'ITM-0012',
      itemName: 'Soap',
      inventoryUomCode: 'PCS',
      salesPrice: null,
      mrp: 50,
      isActive: true,
      taxGroupId: null,
      taxPreference: 'Exempt',
      isPriceInclusiveOfTax: false,
    });

    const item = await pending;
    expect(item.unitPrice).toBe(50);
    expect(item.isPriceInclusive).toBe(true);
    expect(item.taxGroupId).toBeNull();
  });

  it('offers only current, active sales rates, as tax groups', async () => {
    const pending = service.salesTaxGroups();
    await settle();

    const rate = {
      taxMasterId: 1,
      taxGroupId: 18,
      taxName: 'GST 18%',
      cgstRate: 9,
      sgstRate: 9,
      igstRate: 18,
      cessRate: 0,
      isSales: true,
      isCurrent: true,
      isActive: true,
    };

    httpMock.expectOne('/api/tax-masters?includeHistory=false').flush([
      rate,
      { ...rate, taxMasterId: 2, taxGroupId: 5, isSales: false },
      { ...rate, taxMasterId: 3, taxGroupId: 12, isCurrent: false },
      { ...rate, taxMasterId: 4, taxGroupId: 28, isActive: false },
    ]);

    expect(await pending).toEqual([
      {
        taxGroupId: 18,
        taxMasterId: 1,
        label: 'GST 18%',
        cgstRate: 9,
        sgstRate: 9,
        igstRate: 18,
        cessRate: 0,
      },
    ]);
  });

  it('reads the branch GSTIN and discount rule from the current organization', async () => {
    const pending = service.branch();
    await settle();

    httpMock
      .expectOne('/api/organizations/current')
      .flush({ gstin: '33AAAAA0000A1Z5', discountBeforeTax: false });

    expect(await pending).toEqual({ gstin: '33AAAAA0000A1Z5', discountBeforeTax: false });
  });

  it('defaults to no GSTIN and discount before tax when the branch says nothing', async () => {
    const pending = service.branch();
    await settle();

    httpMock.expectOne('/api/organizations/current').flush({});

    expect(await pending).toEqual({ gstin: null, discountBeforeTax: true });
  });
});
