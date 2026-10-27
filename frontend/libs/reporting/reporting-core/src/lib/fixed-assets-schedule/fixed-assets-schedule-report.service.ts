import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@bill-book/api-client';
import {
  FixedAssetCategoryOptionDto,
  FixedAssetsScheduleLookupsDto,
  FixedAssetsScheduleReportRequest,
  FixedAssetsScheduleReportResponse,
} from './fixed-assets-schedule-report.models';

@Injectable({ providedIn: 'root' })
export class FixedAssetsScheduleReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private get endpoint(): string {
    return `${this.baseUrl}/api/statements/fixed-assets-schedule`;
  }

  async getReport(request: FixedAssetsScheduleReportRequest): Promise<FixedAssetsScheduleReportResponse> {
    let params = new HttpParams()
      .set('from', request.from)
      .set('to', request.to);

    if (request.categoryId != null) {
      params = params.set('categoryId', request.categoryId.toString());
    }

    if (request.status) {
      params = params.set('status', request.status);
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
      this.http.get<FixedAssetsScheduleReportResponse>(this.endpoint, { params }),
    );
  }

  async getCategories(): Promise<FixedAssetCategoryOptionDto[]> {
    return await firstValueFrom(
      this.http.get<FixedAssetCategoryOptionDto[]>(`${this.endpoint}/categories`),
    );
  }

  async getLookups(): Promise<FixedAssetsScheduleLookupsDto> {
    return await firstValueFrom(
      this.http.get<FixedAssetsScheduleLookupsDto>(`${this.endpoint}/lookups`),
    );
  }
}
