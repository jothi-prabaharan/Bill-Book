import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@bill-book/api-client';
import { InventoryAgingReportService } from './inventory-aging-report.service';

describe('InventoryAgingReportService', () => {
  let service: InventoryAgingReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
        InventoryAgingReportService,
      ],
    });

    service = TestBed.inject(InventoryAgingReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('builds query parameters correctly for getReport', async () => {
    const promise = service.getReport({
      asOfDate: '2026-10-27',
      itemCategoryId: 5,
      warehouseId: 2,
      onlyWithStock: true,
      search: 'item1',
      pageNumber: 1,
      pageSize: 25,
    });

    const req = httpMock.expectOne((r) =>
      r.url === 'https://api.test/api/statements/inventory-aging-report' &&
      r.params.get('asOfDate') === '2026-10-27' &&
      r.params.get('itemCategoryId') === '5' &&
      r.params.get('warehouseId') === '2' &&
      r.params.get('onlyWithStock') === 'true' &&
      r.params.get('search') === 'item1' &&
      r.params.get('pageNumber') === '1' &&
      r.params.get('pageSize') === '25',
    );
    expect(req.request.method).toBe('GET');
    req.flush({
      asOfDate: '2026-10-27',
      baseCurrencyCode: 'INR',
      items: [],
      totalItems: 0,
      pageNumber: 1,
      pageSize: 25,
      totals: {
        totalQuantity: 0,
        totalValue: 0,
        bucket0To30: { quantity: 0, value: 0 },
        bucket31To60: { quantity: 0, value: 0 },
        bucket61To90: { quantity: 0, value: 0 },
        bucket91To180: { quantity: 0, value: 0 },
        bucketOver180: { quantity: 0, value: 0 },
      },
    });

    const res = await promise;
    expect(res.baseCurrencyCode).toBe('INR');
  });
});
