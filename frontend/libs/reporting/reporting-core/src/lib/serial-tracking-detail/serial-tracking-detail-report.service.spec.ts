import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@bill-book/api-client';
import { SerialTrackingDetailReportService } from './serial-tracking-detail-report.service';

describe('SerialTrackingDetailReportService', () => {
  let service: SerialTrackingDetailReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
        SerialTrackingDetailReportService,
      ],
    });

    service = TestBed.inject(SerialTrackingDetailReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('builds query parameters correctly for getReport', async () => {
    const promise = service.getReport({
      fromDate: '2026-10-01',
      toDate: '2026-10-28',
      itemSerialId: 10,
      itemId: 101,
      warehouseId: 1,
      contactId: 5,
      search: 'SR-100',
      pageNumber: 1,
      pageSize: 25,
      orgId: '00000000-0000-0000-0000-000000000001',
    });

    const req = httpMock.expectOne((r) =>
      r.url === 'https://api.test/api/statements/serial-tracking-detail-report' &&
      r.params.get('fromDate') === '2026-10-01' &&
      r.params.get('toDate') === '2026-10-28' &&
      r.params.get('itemSerialId') === '10' &&
      r.params.get('itemId') === '101' &&
      r.params.get('warehouseId') === '1' &&
      r.params.get('contactId') === '5' &&
      r.params.get('search') === 'SR-100' &&
      r.params.get('pageNumber') === '1' &&
      r.params.get('pageSize') === '25' &&
      r.params.get('orgId') === '00000000-0000-0000-0000-000000000001',
    );

    expect(req.request.method).toBe('GET');
    req.flush({
      fromDate: '2026-10-01',
      toDate: '2026-10-28',
      currency: 'INR',
      serials: [],
      totals: {
        totalSerials: 0,
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
    expect(res.serials).toEqual([]);
  });
});
