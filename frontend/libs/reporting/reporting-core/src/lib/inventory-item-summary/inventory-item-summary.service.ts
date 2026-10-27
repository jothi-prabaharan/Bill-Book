import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { PayableInvoiceCategoryChoice, PayableInvoiceItemChoice } from '../payable-invoice-detail/payable-invoice-detail.models';
import { InventoryItemSummaryRequest, InventoryItemSummaryResult } from './inventory-item-summary.models';

const BASE = '/api/statements/inventory-item-summary';

/**
 * The Inventory Item Summary's own endpoint (TK-243), and the lists its item and category filters offer.
 * Owns no state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class InventoryItemSummaryService {
  private readonly http = inject(HttpClient);

  /** A page of the period's items with their stock movements and closing stock, and the values of every item the filters match. */
  async run(request: InventoryItemSummaryRequest): Promise<InventoryItemSummaryResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.itemCategoryId) {
      params = params.set('itemCategoryId', request.itemCategoryId);
    }
    if (request.itemId) {
      params = params.set('itemId', request.itemId);
    }
    if (request.showItemsWithNoMovement) {
      params = params.set('showItemsWithNoMovement', true);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<InventoryItemSummaryResult>(BASE, { params }).subscribe({ next: resolve, error: reject }),
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
}
