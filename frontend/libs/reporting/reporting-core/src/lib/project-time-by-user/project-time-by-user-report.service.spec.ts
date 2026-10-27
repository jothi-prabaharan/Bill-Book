import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProjectTimeByUserReportService } from './project-time-by-user-report.service';
import {
  ProjectTimeByUserLookupsResponse,
  ProjectTimeByUserReportRequest,
  ProjectTimeByUserReportResponse,
} from './project-time-by-user-report.models';

describe('ProjectTimeByUserReportService', () => {
  let service: ProjectTimeByUserReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ProjectTimeByUserReportService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(ProjectTimeByUserReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('fetches project time by user report with query params', () => {
    const request: ProjectTimeByUserReportRequest = {
      fromDate: '2026-10-01',
      toDate: '2026-10-28',
      userId: '11111111-1111-1111-1111-111111111111',
      projectId: 1,
      groupBy: 'user',
    };

    const mockResponse: ProjectTimeByUserReportResponse = {
      fromDate: '2026-10-01',
      toDate: '2026-10-28',
      groupBy: 'user',
      groups: [
        {
          groupKey: '11111111-1111-1111-1111-111111111111',
          groupName: 'Asha Sharma',
          groupSubtext: '1 project',
          totalHours: 10,
          totalBillableHours: 8,
          totalBilledHours: 3,
          totalNonBillableHours: 2,
          totalUtilisationPercent: 80,
          items: [
            {
              projectCode: 'PRJ-01',
              projectName: 'Alpha Portal',
              taskName: 'Dev',
              userId: '11111111-1111-1111-1111-111111111111',
              userName: 'Asha Sharma',
              hours: 10,
              billableHours: 8,
              billedHours: 3,
              nonBillableHours: 2,
              utilisationPercent: 80,
            },
          ],
        },
      ],
      summary: {
        totalUsers: 1,
        totalProjects: 1,
        totalHours: 10,
        totalBillableHours: 8,
        totalBilledHours: 3,
        totalNonBillableHours: 2,
        overallUtilisationPercent: 80,
      },
    };

    service.getReport(request).subscribe((res) => {
      expect(res).toEqual(mockResponse);
      expect(res.groups.length).toBe(1);
      expect(res.summary.overallUtilisationPercent).toBe(80);
    });

    const req = httpMock.expectOne((r) =>
      r.url === '/api/statements/project-time-by-user' &&
      r.params.get('fromDate') === '2026-10-01' &&
      r.params.get('toDate') === '2026-10-28' &&
      r.params.get('userId') === '11111111-1111-1111-1111-111111111111' &&
      r.params.get('projectId') === '1' &&
      r.params.get('groupBy') === 'user'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('fetches lookups', () => {
    const mockLookups: ProjectTimeByUserLookupsResponse = {
      users: [{ userId: '11111111-1111-1111-1111-111111111111', name: 'Asha Sharma' }],
      projects: [{ id: 1, code: 'PRJ-01', name: 'Alpha Portal' }],
    };

    service.getLookups().subscribe((res) => {
      expect(res).toEqual(mockLookups);
      expect(res.users.length).toBe(1);
    });

    const req = httpMock.expectOne('/api/statements/project-time-by-user/lookups');
    expect(req.request.method).toBe('GET');
    req.flush(mockLookups);
  });
});
