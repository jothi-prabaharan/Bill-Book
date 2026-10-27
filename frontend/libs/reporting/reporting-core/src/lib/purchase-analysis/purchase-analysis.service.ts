import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedPayablesVendorChoice } from '../aged-payables-summary/aged-payables-summary.models';
import { PayableInvoiceCategoryChoice } from '../payable-invoice-detail/payable-invoice-detail.models';
import { PurchaseAnalysisRequest, PurchaseAnalysisResult } from './purchase-analysis.models';

/**
 * The Purchase Analysis's own endpoint (TK-239), and the two lists its filters offer: the branch's
 * vendors and item categories. Owns no state and touches no browser global, so it runs in a mobile
 * shell too.
 */
@Injectable({ providedIn: 'root' })
export class PurchaseAnalysisService {
  private readonly http = inject(HttpClient);

  /** A page of the period's purchases added up by item, category or vendor, with the totals of all of them. */
  async run(request: PurchaseAnalysisRequest): Promise<PurchaseAnalysisResult> {
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
    if (request.includeDebitNotes === false) {
      params = params.set('includeDebitNotes', false);
    }
    if (request.includeFixedAssets) {
      params = params.set('includeFixedAssets', true);
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
      this.http.get<PurchaseAnalysisResult>('/api/statements/purchase-analysis', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's vendors, for the vendor filter. */
  async vendors(): Promise<AgedPayablesVendorChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<AgedPayablesVendorChoice[]>('/api/statements/purchase-analysis/vendors').subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's item categories, for the category filter. */
  async itemCategories(): Promise<PayableInvoiceCategoryChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<PayableInvoiceCategoryChoice[]>('/api/statements/purchase-analysis/item-categories').subscribe({ next: resolve, error: reject }),
    );
  }
}
