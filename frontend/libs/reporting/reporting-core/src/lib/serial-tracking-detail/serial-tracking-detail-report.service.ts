import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@bill-book/api-client';
import {
  SerialTrackingChoice,
  SerialTrackingContactChoice,
  SerialTrackingDetailReportRequest,
  SerialTrackingDetailReportResultDto,
  SerialTrackingItemChoice,
  SerialTrackingWarehouseChoice,
} from './serial-tracking-detail-report.models';

@Injectable({ providedIn: 'root' })
export class SerialTrackingDetailReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private get endpoint(): string {
    return `${this.baseUrl}/api/statements/serial-tracking-detail-report`;
  }

  async getReport(request: SerialTrackingDetailReportRequest): Promise<SerialTrackingDetailReportResultDto> {
    let params = new HttpParams()
      .set('fromDate', request.fromDate)
      .set('toDate', request.toDate);

    if (request.itemSerialId != null) {
      params = params.set('itemSerialId', request.itemSerialId.toString());
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
      this.http.get<SerialTrackingDetailReportResultDto>(this.endpoint, { params }),
    );
  }

  async getSerials(): Promise<SerialTrackingChoice[]> {
    return await firstValueFrom(
      this.http.get<SerialTrackingChoice[]>(`${this.endpoint}/serials`),
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

  async getContacts(): Promise<SerialTrackingContactChoice[]> {
    return await firstValueFrom(
      this.http.get<SerialTrackingContactChoice[]>(`${this.endpoint}/contacts`),
    );
  }
}
