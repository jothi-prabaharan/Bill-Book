import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProjectBudgetVsActualReportService } from './project-budget-vs-actual-report.service';
import {
  ProjectBudgetLookupsResponse,
  ProjectBudgetVsActualReportRequest,
  ProjectBudgetVsActualReportResponse,
} from './project-budget-vs-actual-report.models';

describe('ProjectBudgetVsActualReportService', () => {
  let service: ProjectBudgetVsActualReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ProjectBudgetVsActualReportService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(ProjectBudgetVsActualReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('fetches project budget vs actual report with query params', () => {
    const request: ProjectBudgetVsActualReportRequest = {
      asOfDate: '2026-10-28',
      customerId: 10,
      status: 'Active',
      projectId: 1,
    };

    const mockResponse: ProjectBudgetVsActualReportResponse = {
      asOfDate: '2026-10-28',
      projects: [
        {
          projectId: 1,
          projectCode: 'PRJ-01',
          projectName: 'Alpha Portal',
          customerId: 10,
          customer: 'Acme',
          status: 'Active',
          budgetAmount: 100000,
          actualCost: 75000,
          costVariance: 25000,
          costUsedPercent: 75,
          isCostOverBudget: false,
          budgetHours: 120,
          actualHours: 125,
          hoursVariance: -5,
          hoursUsedPercent: 104.17,
          isHoursOverBudget: true,
          tasks: [
            {
              projectTaskId: 101,
              taskName: 'Architecture',
              budgetHours: 40,
              actualHours: 30,
              hoursVariance: 10,
              hoursUsedPercent: 75,
              isOverBudget: false,
            },
          ],
        },
      ],
      summary: {
        totalProjects: 1,
        totalBudgetAmount: 100000,
        totalActualCost: 75000,
        totalCostVariance: 25000,
        totalCostUsedPercent: 75,
        totalBudgetHours: 120,
        totalActualHours: 125,
        totalHoursVariance: -5,
        totalHoursUsedPercent: 104.17,
        overBudgetProjectsCount: 1,
      },
    };

    service.getReport(request).subscribe((res) => {
      expect(res).toEqual(mockResponse);
      expect(res.projects.length).toBe(1);
      expect(res.projects[0].isHoursOverBudget).toBe(true);
    });

    const req = httpMock.expectOne((r) =>
      r.url === '/api/statements/project-budget-vs-actual' &&
      r.params.get('asOfDate') === '2026-10-28' &&
      r.params.get('customerId') === '10' &&
      r.params.get('status') === 'Active' &&
      r.params.get('projectId') === '1'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('fetches filter lookups', () => {
    const mockLookups: ProjectBudgetLookupsResponse = {
      customers: [{ id: 10, code: 'C-01', name: 'Acme Corp' }],
      statuses: ['Active', 'Completed'],
      projects: [{ id: 1, code: 'P-01', name: 'Alpha' }],
    };

    service.getLookups().subscribe((res) => {
      expect(res).toEqual(mockLookups);
      expect(res.customers.length).toBe(1);
    });

    const req = httpMock.expectOne('/api/statements/project-budget-vs-actual/lookups');
    expect(req.request.method).toBe('GET');
    req.flush(mockLookups);
  });
});
