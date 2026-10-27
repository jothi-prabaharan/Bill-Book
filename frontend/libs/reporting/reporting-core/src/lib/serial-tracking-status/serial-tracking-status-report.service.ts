import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@bill-book/api-client';
import {
  SerialTrackingItemChoice,
  SerialTrackingStatusReportRequest,
  SerialTrackingStatusReportResultDto,
  SerialTrackingWarehouseChoice,
} from './serial-tracking-status-report.models';

@Injectable({ providedIn: 'root' })
export class SerialTrackingStatusReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private get endpoint(): string {
    return `${this.baseUrl}/api/statements/serial-tracking-status-report`;
  }

  async getReport(request: SerialTrackingStatusReportRequest): Promise<SerialTrackingStatusReportResultDto> {
    let params = new HttpParams().set('asOfDate', request.asOfDate);

    if (request.itemId != null) {
      params = params.set('itemId', request.itemId.toString());
    }

    if (request.warehouseId != null) {
      params = params.set('warehouseId', request.warehouseId.toString());
    }

    if (request.status && request.status !== 'All') {
      params = params.set('status', request.status);
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
      this.http.get<SerialTrackingStatusReportResultDto>(this.endpoint, { params }),
    );
  }

  async getItems(): Promise<SerialTrackingItemChoice[]> {
    return await firstValueFrom(
      this.http.get<SerialTrackingItemChoice[]>(`${this.endpoint}/items`),
    );
  }

  async getWarehouses(): Promise<SerialTrackingWarehouseChoice[]> {
    return await firstValueFrom(
      this.http.get<SerialTrackingWarehouseChoice[]>(`${this.endpoint}/warehouses`),
    );
  }
}
