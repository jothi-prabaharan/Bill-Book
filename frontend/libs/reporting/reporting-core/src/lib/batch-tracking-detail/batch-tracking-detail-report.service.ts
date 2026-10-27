import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@bill-book/api-client';
import {
  BatchTrackingBatchChoice,
  BatchTrackingContactChoice,
  BatchTrackingDetailReportRequest,
  BatchTrackingDetailReportResultDto,
  BatchTrackingItemChoice,
  BatchTrackingWarehouseChoice,
} from './batch-tracking-detail-report.models';

@Injectable({ providedIn: 'root' })
export class BatchTrackingDetailReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private get endpoint(): string {
    return `${this.baseUrl}/api/statements/batch-tracking-detail-report`;
  }

  async getReport(request: BatchTrackingDetailReportRequest): Promise<BatchTrackingDetailReportResultDto> {
    let params = new HttpParams()
      .set('fromDate', request.fromDate)
      .set('toDate', request.toDate);

    if (request.batchId != null) {
      params = params.set('batchId', request.batchId.toString());
    }

    if (request.itemId != null) {
      params = params.set('itemId', request.itemId.toString());
    }

    if (request.warehouseId != null) {
      params = params.set('warehouseId', request.warehouseId.toString());
    }

    if (request.contactId != null) {
      params = params.set('contactId', request.contactId.toString());
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
      this.http.get<BatchTrackingDetailReportResultDto>(this.endpoint, { params }),
    );
  }

  async getBatches(): Promise<BatchTrackingBatchChoice[]> {
    return await firstValueFrom(
      this.http.get<BatchTrackingBatchChoice[]>(`${this.endpoint}/batches`),
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

  async getContacts(): Promise<BatchTrackingContactChoice[]> {
    return await firstValueFrom(
      this.http.get<BatchTrackingContactChoice[]>(`${this.endpoint}/contacts`),
    );
  }
}
