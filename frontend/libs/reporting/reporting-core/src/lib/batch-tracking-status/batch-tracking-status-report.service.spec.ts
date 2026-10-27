import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@bill-book/api-client';
import { BatchTrackingStatusReportService } from './batch-tracking-status-report.service';

describe('BatchTrackingStatusReportService', () => {
  let service: BatchTrackingStatusReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
        BatchTrackingStatusReportService,
      ],
    });

    service = TestBed.inject(BatchTrackingStatusReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('builds query parameters correctly for getReport', async () => {
    const promise = service.getReport({
      asOfDate: '2026-10-27',
      itemId: 10,
      warehouseId: 2,
      expiringWithinDays: 30,
      expiredOnly: true,
      withStockOnly: true,
      search: 'BATCH-001',
      pageNumber: 1,
      pageSize: 25,
    });

    const req = httpMock.expectOne((r) =>
      r.url === 'https://api.test/api/statements/batch-tracking-status-report' &&
      r.params.get('asOfDate') === '2026-10-27' &&
      r.params.get('itemId') === '10' &&
      r.params.get('warehouseId') === '2' &&
      r.params.get('expiringWithinDays') === '30' &&
      r.params.get('expiredOnly') === 'true' &&
      r.params.get('withStockOnly') === 'true' &&
      r.params.get('search') === 'BATCH-001' &&
      r.params.get('pageNumber') === '1' &&
      r.params.get('pageSize') === '25',
    );
    expect(req.request.method).toBe('GET');
    req.flush({
      asOfDate: '2026-10-27',
      items: [],
      totalItems: 0,
      pageNumber: 1,
      pageSize: 25,
      totals: {
        totalReceivedQuantity: 0,
        totalAvailableQuantity: 0,
        expiredBatchCount: 0,
        nearExpiryBatchCount: 0,
      },
    });

    const res = await promise;
    expect(res.totalItems).toBe(0);
  });
});
