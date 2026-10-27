import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DisposalScheduleLookupsDto,
  DisposalScheduleReportRequest,
  DisposalScheduleReportResponse,
} from './disposal-schedule-report.models';

@Injectable({ providedIn: 'root' })
export class DisposalScheduleReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/statements/disposal-schedule-report';

  getReport(request: DisposalScheduleReportRequest): Observable<DisposalScheduleReportResponse> {
    let params = new HttpParams()
      .set('from', request.from)
      .set('to', request.to);

    if (request.categoryId !== undefined && request.categoryId !== null && request.categoryId > 0) {
      params = params.set('categoryId', request.categoryId.toString());
    }

    if (request.search) {
      params = params.set('search', request.search.trim());
    }

    if (request.orgId) {
      params = params.set('orgId', request.orgId);
    }

    return this.http.get<DisposalScheduleReportResponse>(this.baseUrl, { params });
  }

  getLookups(): Observable<DisposalScheduleLookupsDto> {
    return this.http.get<DisposalScheduleLookupsDto>(`${this.baseUrl}/lookups`);
  }
}
