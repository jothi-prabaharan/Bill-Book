import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '@bill-book/api-client';
import {
  InventoryAgingCategoryChoice,
  InventoryAgingReportDto,
  InventoryAgingReportRequest,
  InventoryAgingWarehouseChoice,
} from './inventory-aging-report.models';

@Injectable({ providedIn: 'root' })
export class InventoryAgingReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  private get endpoint(): string {
    return `${this.baseUrl}/api/statements/inventory-aging-report`;
  }

  async getReport(request: InventoryAgingReportRequest): Promise<InventoryAgingReportDto> {
    let params = new HttpParams().set('asOfDate', request.asOfDate);

    if (request.itemCategoryId != null) {
      params = params.set('itemCategoryId', request.itemCategoryId.toString());
    }

    if (request.warehouseId != null) {
      params = params.set('warehouseId', request.warehouseId.toString());
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
      this.http.get<InventoryAgingReportDto>(this.endpoint, { params }),
    );
  }

  async getCategories(): Promise<InventoryAgingCategoryChoice[]> {
    return await firstValueFrom(
      this.http.get<InventoryAgingCategoryChoice[]>(`${this.endpoint}/item-categories`),
    );
  }

  async getWarehouses(): Promise<InventoryAgingWarehouseChoice[]> {
    return await firstValueFrom(
      this.http.get<InventoryAgingWarehouseChoice[]>(`${this.endpoint}/warehouses`),
    );
  }
}
