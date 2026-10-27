import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@bill-book/api-client';
import { SerialTrackingStatusReportService } from './serial-tracking-status-report.service';

describe('SerialTrackingStatusReportService', () => {
  let service: SerialTrackingStatusReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
        SerialTrackingStatusReportService,
      ],
    });

    service = TestBed.inject(SerialTrackingStatusReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('builds query parameters correctly for getReport', async () => {
    const promise = service.getReport({
      asOfDate: '2026-10-28',
      itemId: 101,
      warehouseId: 1,
      status: 'InStock',
      search: 'SR-2026',
      pageNumber: 1,
      pageSize: 50,
      orgId: '00000000-0000-0000-0000-000000000001',
    });

    const req = httpMock.expectOne((r) =>
      r.url === 'https://api.test/api/statements/serial-tracking-status-report' &&
      r.params.get('asOfDate') === '2026-10-28' &&
      r.params.get('itemId') === '101' &&
      r.params.get('warehouseId') === '1' &&
      r.params.get('status') === 'InStock' &&
      r.params.get('search') === 'SR-2026' &&
      r.params.get('pageNumber') === '1' &&
      r.params.get('pageSize') === '50' &&
      r.params.get('orgId') === '00000000-0000-0000-0000-000000000001',
    );

    expect(req.request.method).toBe('GET');
    req.flush({
      asOfDate: '2026-10-28',
      items: [],
      totals: {
        totalSerials: 0,
        inStockCount: 0,
        soldCount: 0,
        returnedCount: 0,
        scrappedCount: 0,
      },
      totalItems: 0,
      pageNumber: 1,
      pageSize: 50,
    });

    const res = await promise;
    expect(res.items).toEqual([]);
  });
});
