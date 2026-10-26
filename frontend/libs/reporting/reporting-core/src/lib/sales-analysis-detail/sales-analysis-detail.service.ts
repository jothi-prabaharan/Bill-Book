import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import { ReceivableInvoiceCategoryChoice, ReceivableInvoiceItemChoice } from '../receivable-invoice-detail/receivable-invoice-detail.models';
import { SalesAnalysisWarehouseChoice } from '../sales-analysis/sales-analysis.models';
import { SalesAnalysisDetailRequest, SalesAnalysisDetailResult } from './sales-analysis-detail.models';

/**
 * The Sales Analysis Detail Report's own endpoint (TK-229), and the four lists its filters offer: the
 * branch's customers, items, item categories and warehouses. Owns no state and touches no browser
 * global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class SalesAnalysisDetailService {
  private readonly http = inject(HttpClient);

  /** A page of the period's sales lines in their groups, each group with its subtotal, and the totals of all of them. */
  async run(request: SalesAnalysisDetailRequest): Promise<SalesAnalysisDetailResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.groupBy) {
      params = params.set('groupBy', request.groupBy);
    }
    if (request.itemId) {
      params = params.set('itemId', request.itemId);
    }
    if (request.itemCategoryId) {
      params = params.set('itemCategoryId', request.itemCategoryId);
    }
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.warehouseId) {
      params = params.set('warehouseId', request.warehouseId);
    }
    if (request.includeCreditNotes === false) {
      params = params.set('includeCreditNotes', false);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<SalesAnalysisDetailResult>('/api/statements/sales-analysis-detail-report', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<AgedReceivablesCustomerChoice[]>('/api/statements/sales-analysis-detail-report/customers').subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's active items, for the item filter. */
  async items(): Promise<ReceivableInvoiceItemChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<ReceivableInvoiceItemChoice[]>('/api/statements/sales-analysis-detail-report/items').subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's item categories, for the category filter. */
  async itemCategories(): Promise<ReceivableInvoiceCategoryChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<ReceivableInvoiceCategoryChoice[]>('/api/statements/sales-analysis-detail-report/item-categories').subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's warehouses, for the warehouse filter. */
  async warehouses(): Promise<SalesAnalysisWarehouseChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<SalesAnalysisWarehouseChoice[]>('/api/statements/sales-analysis-detail-report/warehouses').subscribe({ next: resolve, error: reject }),
    );
  }
}
