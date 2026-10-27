import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ProjectUnbilledLookupsResponse,
  ProjectUnbilledWorkReportRequest,
  ProjectUnbilledWorkReportResponse,
} from './project-unbilled-work-report.models';

@Injectable({ providedIn: 'root' })
export class ProjectUnbilledWorkReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/statements/project-unbilled-work';

  getReport(request: ProjectUnbilledWorkReportRequest): Observable<ProjectUnbilledWorkReportResponse> {
    let params = new HttpParams().set('asOfDate', request.asOfDate);

    if (request.customerId != null) {
      params = params.set('customerId', request.customerId.toString());
    }

    if (request.projectId != null) {
      params = params.set('projectId', request.projectId.toString());
    }

    if (request.orgId) {
      params = params.set('orgId', request.orgId);
    }

    return this.http.get<ProjectUnbilledWorkReportResponse>(this.baseUrl, { params });
  }

  getLookups(): Observable<ProjectUnbilledLookupsResponse> {
    return this.http.get<ProjectUnbilledLookupsResponse>(`${this.baseUrl}/lookups`);
  }
}
