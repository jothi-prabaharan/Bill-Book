import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DisposalScheduleReportService } from './disposal-schedule-report.service';

describe('DisposalScheduleReportService', () => {
  let service: DisposalScheduleReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        DisposalScheduleReportService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(DisposalScheduleReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('requests report with required from and to params', () => {
    service.getReport({ from: '2026-04-01', to: '2026-10-28', categoryId: 10, search: 'Drill' }).subscribe();

    const req = httpMock.expectOne((r) =>
      r.url === '/api/statements/disposal-schedule-report' &&
      r.params.get('from') === '2026-04-01' &&
      r.params.get('to') === '2026-10-28' &&
      r.params.get('categoryId') === '10' &&
      r.params.get('search') === 'Drill'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ categories: [], summary: {} });
  });

  it('fetches lookups from lookups sub-route', () => {
    service.getLookups().subscribe();

    const req = httpMock.expectOne('/api/statements/disposal-schedule-report/lookups');
    expect(req.request.method).toBe('GET');
    req.flush({ categories: [] });
  });
});
