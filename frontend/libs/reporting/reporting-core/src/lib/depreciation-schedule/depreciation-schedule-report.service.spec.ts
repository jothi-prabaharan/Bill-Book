import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@bill-book/api-client';
import { DepreciationScheduleReportService } from './depreciation-schedule-report.service';

describe('DepreciationScheduleReportService', () => {
  let service: DepreciationScheduleReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
        DepreciationScheduleReportService,
      ],
    });

    service = TestBed.inject(DepreciationScheduleReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('builds query parameters correctly for getReport', async () => {
    const promise = service.getReport({
      from: '2025-04-01',
      to: '2026-03-31',
      categoryId: 10,
      method: 'Straight Line',
      book: 'Books',
      search: 'Server',
      orgId: '00000000-0000-0000-0000-000000000001',
    });

    const req = httpMock.expectOne((r) =>
      r.url === 'https://api.test/api/statements/depreciation-schedule' &&
      r.params.get('from') === '2025-04-01' &&
      r.params.get('to') === '2026-03-31' &&
      r.params.get('categoryId') === '10' &&
      r.params.get('method') === 'Straight Line' &&
      r.params.get('book') === 'Books' &&
      r.params.get('search') === 'Server' &&
      r.params.get('orgId') === '00000000-0000-0000-0000-000000000001',
    );

    expect(req.request.method).toBe('GET');
    req.flush({
      fromDate: '2025-04-01',
      toDate: '2026-03-31',
      book: 'Books',
      summary: {
        totalAssets: 0,
        activeAssets: 0,
        disposedAssets: 0,
        totalCost: 0,
        totalOpeningAccumDep: 0,
        totalDepreciation: 0,
        totalDisposalAccumDep: 0,
        totalClosingAccumDep: 0,
        totalOpeningNbv: 0,
        totalClosingNbv: 0,
      },
      categories: [],
    });

    const res = await promise;
    expect(res.categories).toEqual([]);
  });
});
