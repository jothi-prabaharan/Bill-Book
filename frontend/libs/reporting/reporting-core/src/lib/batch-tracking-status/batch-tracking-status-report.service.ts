import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@bill-book/api-client';
import {
  BatchTrackingItemChoice,
  BatchTrackingStatusReportDto,
  BatchTrackingStatusReportRequest,
  BatchTrackingWarehouseChoice,
} from './batch-tracking-status-report.models';

@Injectable({ providedIn: 'root' })
export class BatchTrackingStatusReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private get endpoint(): string {
    return `${this.baseUrl}/api/statements/batch-tracking-status-report`;
  }

  async getReport(request: BatchTrackingStatusReportRequest): Promise<BatchTrackingStatusReportDto> {
    let params = new HttpParams().set('asOfDate', request.asOfDate);

    if (request.itemId != null) {
      params = params.set('itemId', request.itemId.toString());
    }

    if (request.warehouseId != null) {
      params = params.set('warehouseId', request.warehouseId.toString());
    }

    if (request.expiringWithinDays != null) {
      params = params.set('expiringWithinDays', request.expiringWithinDays.toString());
    }

    if (request.expiredOnly) {
      params = params.set('expiredOnly', 'true');
    }

    if (request.withStockOnly) {
      params = params.set('withStockOnly', 'true');
    }

    if (request.search && request.search.trim()) {
      params = params.set('search', request.search.trim());
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
      this.http.get<BatchTrackingStatusReportDto>(this.endpoint, { params }),
    );
  }

  async getItems(): Promise<BatchTrackingItemChoice[]> {
    return await firstValueFrom(
      this.http.get<BatchTrackingItemChoice[]>(`${this.endpoint}/items`),
    );
  }

  async getWarehouses(): Promise<BatchTrackingWarehouseChoice[]> {
    return await firstValueFrom(
      this.http.get<BatchTrackingWarehouseChoice[]>(`${this.endpoint}/warehouses`),
    );
  }
}
