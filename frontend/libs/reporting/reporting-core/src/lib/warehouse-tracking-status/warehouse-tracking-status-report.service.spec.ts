import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@bill-book/api-client';
import { WarehouseTrackingStatusReportService } from './warehouse-tracking-status-report.service';

describe('WarehouseTrackingStatusReportService', () => {
  let service: WarehouseTrackingStatusReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
        WarehouseTrackingStatusReportService,
      ],
    });

    service = TestBed.inject(WarehouseTrackingStatusReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('builds query parameters correctly for getReport', async () => {
    const promise = service.getReport({
      asOfDate: '2026-10-28',
      warehouseId: 1,
      itemCategoryId: 10,
      showZeroBalance: true,
      search: 'Vault',
      pageNumber: 1,
      pageSize: 25,
      orgId: '00000000-0000-0000-0000-000000000001',
    });

    const req = httpMock.expectOne((r) =>
      r.url === 'https://api.test/api/statements/warehouse-tracking-status-report' &&
      r.params.get('asOfDate') === '2026-10-28' &&
      r.params.get('warehouseId') === '1' &&
      r.params.get('itemCategoryId') === '10' &&
      r.params.get('showZeroBalance') === 'true' &&
      r.params.get('search') === 'Vault' &&
      r.params.get('pageNumber') === '1' &&
      r.params.get('pageSize') === '25' &&
      r.params.get('orgId') === '00000000-0000-0000-0000-000000000001',
    );

    expect(req.request.method).toBe('GET');
    req.flush({
      asOfDate: '2026-10-28',
      currency: 'INR',
      warehouses: [],
      totals: {
        totalWarehouses: 0,
        totalItems: 0,
        totalQuantityIn: 0,
        totalQuantityOut: 0,
        totalAvailableQuantity: 0,
        totalEstimatedValue: 0,
      },
      totalWarehousesCount: 0,
      pageNumber: 1,
      pageSize: 25,
    });

    const res = await promise;
    expect(res.warehouses).toEqual([]);
  });
});
