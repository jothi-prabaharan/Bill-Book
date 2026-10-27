import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ProjectBudgetLookupsResponse,
  ProjectBudgetVsActualReportRequest,
  ProjectBudgetVsActualReportResponse,
} from './project-budget-vs-actual-report.models';

@Injectable({ providedIn: 'root' })
export class ProjectBudgetVsActualReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/statements/project-budget-vs-actual';

  getReport(request: ProjectBudgetVsActualReportRequest): Observable<ProjectBudgetVsActualReportResponse> {
    let params = new HttpParams().set('asOfDate', request.asOfDate);

    if (request.status) {
      params = params.set('status', request.status);
    }

    if (request.customerId != null) {
      params = params.set('customerId', request.customerId.toString());
    }

    if (request.projectId != null) {
      params = params.set('projectId', request.projectId.toString());
    }

    if (request.orgId) {
      params = params.set('orgId', request.orgId);
    }

    return this.http.get<ProjectBudgetVsActualReportResponse>(this.baseUrl, { params });
  }

  getLookups(): Observable<ProjectBudgetLookupsResponse> {
    return this.http.get<ProjectBudgetLookupsResponse>(`${this.baseUrl}/lookups`);
  }
}
