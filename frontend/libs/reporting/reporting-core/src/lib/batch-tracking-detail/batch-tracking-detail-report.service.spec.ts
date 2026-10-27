import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@bill-book/api-client';
import { BatchTrackingDetailReportService } from './batch-tracking-detail-report.service';

describe('BatchTrackingDetailReportService', () => {
  let service: BatchTrackingDetailReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
        BatchTrackingDetailReportService,
      ],
    });

    service = TestBed.inject(BatchTrackingDetailReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('builds query parameters correctly for getReport', async () => {
    const promise = service.getReport({
      fromDate: '2026-04-01',
      toDate: '2026-10-28',
      batchId: 501,
      itemId: 101,
      warehouseId: 1,
      contactId: 42,
      pageNumber: 1,
      pageSize: 25,
      orgId: '00000000-0000-0000-0000-000000000001',
    });

    const req = httpMock.expectOne((r) =>
      r.url === 'https://api.test/api/statements/batch-tracking-detail-report' &&
      r.params.get('fromDate') === '2026-04-01' &&
      r.params.get('toDate') === '2026-10-28' &&
      r.params.get('batchId') === '501' &&
      r.params.get('itemId') === '101' &&
      r.params.get('warehouseId') === '1' &&
      r.params.get('contactId') === '42' &&
      r.params.get('pageNumber') === '1' &&
      r.params.get('pageSize') === '25' &&
      r.params.get('orgId') === '00000000-0000-0000-0000-000000000001',
    );

    expect(req.request.method).toBe('GET');
    req.flush({
      fromDate: '2026-04-01',
      toDate: '2026-10-28',
      currency: 'INR',
      batches: [],
      totals: {
        totalBatches: 0,
        totalOpeningQuantity: 0,
        totalInQuantity: 0,
        totalOutQuantity: 0,
        totalClosingQuantity: 0,
        totalMovements: 0,
      },
      totalItems: 0,
      pageNumber: 1,
      pageSize: 25,
    });

    const res = await promise;
    expect(res.batches).toEqual([]);
  });
});
