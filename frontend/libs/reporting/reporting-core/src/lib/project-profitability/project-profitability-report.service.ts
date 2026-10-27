import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ProjectProfitabilityLookups,
  ProjectProfitabilityReportRequest,
  ProjectProfitabilityReportResponse,
} from './project-profitability-report.models';

@Injectable({ providedIn: 'root' })
export class ProjectProfitabilityReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/statements/project-profitability';

  getReport(request: ProjectProfitabilityReportRequest): Observable<ProjectProfitabilityReportResponse> {
    let params = new HttpParams()
      .set('from', request.from)
      .set('to', request.to);

    if (request.customerId != null) {
      params = params.set('customerId', request.customerId.toString());
    }

    if (request.status) {
      params = params.set('status', request.status);
    }

    if (request.billingMethod) {
      params = params.set('billingMethod', request.billingMethod);
    }

    if (request.projectId != null) {
      params = params.set('projectId', request.projectId.toString());
    }

    if (request.orgId) {
      params = params.set('orgId', request.orgId);
    }

    return this.http.get<ProjectProfitabilityReportResponse>(this.baseUrl, { params });
  }

  getLookups(): Observable<ProjectProfitabilityLookups> {
    return this.http.get<ProjectProfitabilityLookups>(`${this.baseUrl}/lookups`);
  }
}
