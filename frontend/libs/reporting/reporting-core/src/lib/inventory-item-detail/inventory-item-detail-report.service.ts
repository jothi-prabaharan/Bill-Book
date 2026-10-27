import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { PayableInvoiceCategoryChoice, PayableInvoiceItemChoice } from '../payable-invoice-detail/payable-invoice-detail.models';
import { SalesAnalysisWarehouseChoice } from '../sales-analysis/sales-analysis.models';
import {
  InventoryItemDetailRequest,
  InventoryItemDetailResult,
} from './inventory-item-detail-report.models';

const BASE = '/api/statements/inventory-item-detail';

/**
 * The Inventory Item Detail report's own endpoint (TK-244), and the lists its item, category,
 * and warehouse filters offer. Owns no state and touches no browser global.
 */
@Injectable({ providedIn: 'root' })
export class InventoryItemDetailService {
  private readonly http = inject(HttpClient);

  /** Runs the report for a period and optional filters. */
  async run(request: InventoryItemDetailRequest): Promise<InventoryItemDetailResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.itemId) {
      params = params.set('itemId', request.itemId);
    }
    if (request.itemCategoryId) {
      params = params.set('itemCategoryId', request.itemCategoryId);
    }
    if (request.warehouseId) {
      params = params.set('warehouseId', request.warehouseId);
    }
    if (request.source) {
      params = params.set('source', request.source);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<InventoryItemDetailResult>(BASE, { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's stocked items, for the item filter. */
  async items(): Promise<PayableInvoiceItemChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<PayableInvoiceItemChoice[]>(`${BASE}/items`).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's item categories, for the category filter. */
  async itemCategories(): Promise<PayableInvoiceCategoryChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<PayableInvoiceCategoryChoice[]>(`${BASE}/item-categories`).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's warehouses, for the warehouse filter. */
  async warehouses(): Promise<SalesAnalysisWarehouseChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<SalesAnalysisWarehouseChoice[]>(`${BASE}/warehouses`).subscribe({ next: resolve, error: reject }),
    );
  }
}
