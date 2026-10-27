import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ProjectTimeByUserLookupsResponse,
  ProjectTimeByUserReportRequest,
  ProjectTimeByUserReportResponse,
} from './project-time-by-user-report.models';

@Injectable({ providedIn: 'root' })
export class ProjectTimeByUserReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/statements/project-time-by-user';

  getReport(request: ProjectTimeByUserReportRequest): Observable<ProjectTimeByUserReportResponse> {
    let params = new HttpParams()
      .set('fromDate', request.fromDate)
      .set('toDate', request.toDate);

    if (request.userId) {
      params = params.set('userId', request.userId);
    }

    if (request.projectId != null) {
      params = params.set('projectId', request.projectId.toString());
    }

    if (request.groupBy) {
      params = params.set('groupBy', request.groupBy);
    }

    if (request.orgId) {
      params = params.set('orgId', request.orgId);
    }

    return this.http.get<ProjectTimeByUserReportResponse>(this.baseUrl, { params });
  }

  getLookups(): Observable<ProjectTimeByUserLookupsResponse> {
    return this.http.get<ProjectTimeByUserLookupsResponse>(`${this.baseUrl}/lookups`);
  }
}
