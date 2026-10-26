import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import { ReceivableInvoiceCategoryChoice } from '../receivable-invoice-detail/receivable-invoice-detail.models';
import { SalesAnalysisRequest, SalesAnalysisResult, SalesAnalysisWarehouseChoice } from './sales-analysis.models';

/**
 * The Sales Analysis's own endpoint (TK-228), and the three lists its filters offer: the branch's
 * customers, item categories and warehouses. Owns no state and touches no browser global, so it runs
 * in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class SalesAnalysisService {
  private readonly http = inject(HttpClient);

  /** A page of the period's sales added up by item, category or customer, with the totals of all of them. */
  async run(request: SalesAnalysisRequest): Promise<SalesAnalysisResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.groupBy) {
      params = params.set('groupBy', request.groupBy);
    }
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.itemCategoryId) {
      params = params.set('itemCategoryId', request.itemCategoryId);
    }
    if (request.warehouseId) {
      params = params.set('warehouseId', request.warehouseId);
    }
    if (request.includeCreditNotes === false) {
      params = params.set('includeCreditNotes', false);
    }
    if (request.compareFromDate) {
      params = params.set('compareFromDate', request.compareFromDate);
    }
    if (request.compareToDate) {
      params = params.set('compareToDate', request.compareToDate);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<SalesAnalysisResult>('/api/statements/sales-analysis', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<AgedReceivablesCustomerChoice[]>('/api/statements/sales-analysis/customers').subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's item categories, for the category filter. */
  async itemCategories(): Promise<ReceivableInvoiceCategoryChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<ReceivableInvoiceCategoryChoice[]>('/api/statements/sales-analysis/item-categories').subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's warehouses, for the warehouse filter. */
  async warehouses(): Promise<SalesAnalysisWarehouseChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<SalesAnalysisWarehouseChoice[]>('/api/statements/sales-analysis/warehouses').subscribe({ next: resolve, error: reject }),
    );
  }
}
