import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProjectProfitabilityReportService } from './project-profitability-report.service';
import {
  ProjectProfitabilityLookups,
  ProjectProfitabilityReportRequest,
  ProjectProfitabilityReportResponse,
} from './project-profitability-report.models';

describe('ProjectProfitabilityReportService', () => {
  let service: ProjectProfitabilityReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ProjectProfitabilityReportService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(ProjectProfitabilityReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('fetches project profitability report with query params', () => {
    const request: ProjectProfitabilityReportRequest = {
      from: '2026-04-01',
      to: '2026-09-30',
      customerId: 10,
      status: 'Active',
      billingMethod: 'FixedFee',
      projectId: 1,
    };

    const mockResponse: ProjectProfitabilityReportResponse = {
      from: '2026-04-01',
      to: '2026-09-30',
      rows: [
        {
          projectId: 1,
          projectCode: 'PRJ-01',
          projectName: 'Alpha Portal',
          customerId: 10,
          customer: 'Acme',
          status: 'Active',
          billingMethod: 'FixedFee',
          revenue: 100000,
          cost: 40000,
          profit: 60000,
          marginPercent: 60,
          budgetAmount: 120000,
          budgetVariance: 80000,
        },
      ],
      summary: {
        totalProjects: 1,
        totalRevenue: 100000,
        totalCost: 40000,
        totalProfit: 60000,
        overallMarginPercent: 60,
        totalBudgetAmount: 120000,
      },
    };

    service.getReport(request).subscribe((res) => {
      expect(res).toEqual(mockResponse);
      expect(res.rows.length).toBe(1);
      expect(res.rows[0].profit).toBe(60000);
    });

    const req = httpMock.expectOne((r) =>
      r.url === '/api/statements/project-profitability' &&
      r.params.get('from') === '2026-04-01' &&
      r.params.get('to') === '2026-09-30' &&
      r.params.get('customerId') === '10' &&
      r.params.get('status') === 'Active' &&
      r.params.get('billingMethod') === 'FixedFee' &&
      r.params.get('projectId') === '1'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('fetches filter lookups', () => {
    const mockLookups: ProjectProfitabilityLookups = {
      customers: [{ id: 10, code: 'C-01', name: 'Acme Corp' }],
      statuses: ['Active', 'Completed'],
      billingMethods: ['FixedFee', 'TimeAndMaterials'],
      projects: [{ id: 1, code: 'P-01', name: 'Alpha' }],
    };

    service.getLookups().subscribe((res) => {
      expect(res).toEqual(mockLookups);
      expect(res.customers.length).toBe(1);
      expect(res.statuses).toContain('Active');
    });

    const req = httpMock.expectOne('/api/statements/project-profitability/lookups');
    expect(req.request.method).toBe('GET');
    req.flush(mockLookups);
  });
});
