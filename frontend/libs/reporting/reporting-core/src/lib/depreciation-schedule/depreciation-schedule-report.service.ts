import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@bill-book/api-client';
import {
  DepreciationScheduleLookupsDto,
  DepreciationScheduleReportRequest,
  DepreciationScheduleReportResponse,
} from './depreciation-schedule-report.models';

@Injectable({ providedIn: 'root' })
export class DepreciationScheduleReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private get endpoint(): string {
    return `${this.baseUrl}/api/statements/depreciation-schedule`;
  }

  async getReport(request: DepreciationScheduleReportRequest): Promise<DepreciationScheduleReportResponse> {
    let params = new HttpParams()
      .set('from', request.from)
      .set('to', request.to);

    if (request.categoryId != null) {
      params = params.set('categoryId', request.categoryId.toString());
    }

    if (request.method) {
      params = params.set('method', request.method);
    }

    if (request.book) {
      params = params.set('book', request.book);
    }

    if (request.search) {
      params = params.set('search', request.search);
    }

    if (request.orgId) {
      params = params.set('orgId', request.orgId);
    }

    return await firstValueFrom(
      this.http.get<DepreciationScheduleReportResponse>(this.endpoint, { params }),
    );
  }

  async getCategories(): Promise<{ id: number; name: string }[]> {
    return await firstValueFrom(
      this.http.get<{ id: number; name: string }[]>(`${this.endpoint}/categories`),
    );
  }

  async getLookups(): Promise<DepreciationScheduleLookupsDto> {
    return await firstValueFrom(
      this.http.get<DepreciationScheduleLookupsDto>(`${this.endpoint}/lookups`),
    );
  }
}
