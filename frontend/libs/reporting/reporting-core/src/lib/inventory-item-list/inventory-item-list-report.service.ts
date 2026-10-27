import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@bill-book/api-client';
import {
  InventoryItemListCategoryChoice,
  InventoryItemListLookupChoice,
  InventoryItemListReportDto,
  InventoryItemListReportRequest,
} from './inventory-item-list-report.models';

@Injectable({ providedIn: 'root' })
export class InventoryItemListReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private get endpoint(): string {
    return `${this.baseUrl}/api/statements/inventory-item-list`;
  }

  async getReport(request: InventoryItemListReportRequest): Promise<InventoryItemListReportDto> {
    let params = new HttpParams().set('asOfDate', request.asOfDate);

    if (request.itemCategoryId != null) {
      params = params.set('itemCategoryId', request.itemCategoryId.toString());
    }

    if (request.status && request.status !== 'All') {
      params = params.set('status', request.status);
    }

    if (request.itemType && request.itemType !== 'All') {
      params = params.set('itemType', request.itemType);
    }

    if (request.onlyWithStock) {
      params = params.set('onlyWithStock', 'true');
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
      this.http.get<InventoryItemListReportDto>(this.endpoint, { params }),
    );
  }

  async getCategories(): Promise<InventoryItemListCategoryChoice[]> {
    return await firstValueFrom(
      this.http.get<InventoryItemListCategoryChoice[]>(`${this.endpoint}/item-categories`),
    );
  }

  async getItemTypes(): Promise<InventoryItemListLookupChoice[]> {
    return await firstValueFrom(
      this.http.get<InventoryItemListLookupChoice[]>(`${this.endpoint}/item-types`),
    );
  }

  async getStatuses(): Promise<InventoryItemListLookupChoice[]> {
    return await firstValueFrom(
      this.http.get<InventoryItemListLookupChoice[]>(`${this.endpoint}/statuses`),
    );
  }
}
