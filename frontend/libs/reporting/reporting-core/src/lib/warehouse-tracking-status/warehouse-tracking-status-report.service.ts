import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@bill-book/api-client';
import {
  BatchTrackingWarehouseChoice,
  WarehouseTrackingCategoryChoice,
  WarehouseTrackingStatusReportRequest,
  WarehouseTrackingStatusReportResultDto,
} from './warehouse-tracking-status-report.models';

@Injectable({ providedIn: 'root' })
export class WarehouseTrackingStatusReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private get endpoint(): string {
    return `${this.baseUrl}/api/statements/warehouse-tracking-status-report`;
  }

  async getReport(request: WarehouseTrackingStatusReportRequest): Promise<WarehouseTrackingStatusReportResultDto> {
    let params = new HttpParams().set('asOfDate', request.asOfDate);

    if (request.warehouseId != null) {
      params = params.set('warehouseId', request.warehouseId.toString());
    }

    if (request.itemCategoryId != null) {
      params = params.set('itemCategoryId', request.itemCategoryId.toString());
    }

    if (request.showZeroBalance != null) {
      params = params.set('showZeroBalance', request.showZeroBalance.toString());
    }

    if (request.search) {
      params = params.set('search', request.search);
    }

    if (request.pageNumber != null) {
      params = params.set('pageNumber', request.pageNumber.toString());
    }

    if (request.pageSize != null) {
      params = params.set('pageSize', request.pageSize.toString());
    }

    if (request.orgId) {
      params = params.set('orgId', request.orgId);
    }

    return await firstValueFrom(
      this.http.get<WarehouseTrackingStatusReportResultDto>(this.endpoint, { params }),
    );
  }

  async getWarehouses(): Promise<BatchTrackingWarehouseChoice[]> {
    return await firstValueFrom(
      this.http.get<BatchTrackingWarehouseChoice[]>(`${this.endpoint}/warehouses`),
    );
  }

  async getCategories(): Promise<WarehouseTrackingCategoryChoice[]> {
    return await firstValueFrom(
      this.http.get<WarehouseTrackingCategoryChoice[]>(`${this.endpoint}/categories`),
    );
  }
}
