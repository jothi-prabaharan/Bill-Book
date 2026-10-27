import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProjectUnbilledWorkReportService } from './project-unbilled-work-report.service';
import {
  ProjectUnbilledLookupsResponse,
  ProjectUnbilledWorkReportRequest,
  ProjectUnbilledWorkReportResponse,
} from './project-unbilled-work-report.models';

describe('ProjectUnbilledWorkReportService', () => {
  let service: ProjectUnbilledWorkReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ProjectUnbilledWorkReportService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(ProjectUnbilledWorkReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('fetches project unbilled work report with query params', () => {
    const request: ProjectUnbilledWorkReportRequest = {
      asOfDate: '2026-10-28',
      customerId: 10,
      projectId: 1,
    };

    const mockResponse: ProjectUnbilledWorkReportResponse = {
      asOfDate: '2026-10-28',
      projects: [
        {
          projectId: 1,
          projectCode: 'PRJ-01',
          projectName: 'Alpha Portal',
          customerId: 10,
          customer: 'Acme',
          billingMethod: 'TimeAndMaterials',
          rateBasis: 'TaskRate',
          hourlyRate: 100,
          unbilledHours: 12,
          timeValue: 1200,
          expenses: 300,
          milestones: 0,
          total: 1500,
          lineCount: 2,
          lines: [
            {
              lineType: 'Time',
              id: 101,
              date: '2026-10-20',
              description: 'Asha - Dev',
              reference: 'Dev',
              quantity: 12,
              unitRate: 100,
              amount: 1200,
            },
            {
              lineType: 'Expense',
              id: 201,
              date: '2026-10-22',
              description: 'Travel',
              reference: 'SM-01',
              quantity: 1,
              unitRate: 300,
              amount: 300,
            },
          ],
        },
      ],
      summary: {
        totalProjects: 1,
        totalUnbilledHours: 12,
        totalTimeValue: 1200,
        totalExpenses: 300,
        totalMilestones: 0,
        grandTotal: 1500,
      },
    };

    service.getReport(request).subscribe((res) => {
      expect(res).toEqual(mockResponse);
      expect(res.projects.length).toBe(1);
      expect(res.projects[0].total).toBe(1500);
      expect(res.summary.grandTotal).toBe(1500);
    });

    const req = httpMock.expectOne((r) =>
      r.url === '/api/statements/project-unbilled-work' &&
      r.params.get('asOfDate') === '2026-10-28' &&
      r.params.get('customerId') === '10' &&
      r.params.get('projectId') === '1'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('fetches lookups', () => {
    const mockLookups: ProjectUnbilledLookupsResponse = {
      customers: [{ id: 10, code: 'C-01', name: 'Acme Corp' }],
      projects: [{ id: 1, code: 'PRJ-01', name: 'Alpha Portal' }],
    };

    service.getLookups().subscribe((res) => {
      expect(res).toEqual(mockLookups);
      expect(res.customers.length).toBe(1);
    });

    const req = httpMock.expectOne('/api/statements/project-unbilled-work/lookups');
    expect(req.request.method).toBe('GET');
    req.flush(mockLookups);
  });
});
