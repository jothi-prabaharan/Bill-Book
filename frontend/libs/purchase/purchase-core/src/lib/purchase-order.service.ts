import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/**
 * The purchase order — `POR`.
 *
 * **Rupees never appear in a request.** Every money figure is computed on the
 * server from the lines, at the tax rates in force on the document's date, so
 * this request carries quantities and prices and no totals at all. A caller free
 * to send its own totals is one that can save a document whose foot disagrees
 * with its body.
 */
export interface SavePurchaseOrderRequest {
  documentDate: string;
  contactId: number;
  expectedDate?: string | null;
  contactGstin?: string | null;
  placeOfSupplyStateCode?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  notes?: string | null;
  termsAndConditions?: string | null;
  lines: SavePurchaseOrderLineRequest[];
}

export interface SavePurchaseOrderLineRequest {
  itemId?: number | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  uomId?: number | null;
  conversionFactor: number;
  /** Rupees, as keyed. The grid holds paise; the form converts on the way out. */
  unitPrice: number;
  isPriceInclusive: boolean;
  discountPercent?: number | null;
  discountAmount: number;
  taxTreatment: string;
  taxGroupId?: number | null;
  lineType: string;
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  itemBatchId?: number | null;
  lineNotes?: string | null;
}

export interface VoidPurchaseOrderRequest {
  reason: string;
}

export interface PurchaseOrderListItem {
  purchaseOrderId: number;
  documentNo: string;
  documentDate: string;
  expectedDate?: string | null;
  /** Open, PartlyReceived, Closed or Cancelled. */
  fulfilmentStatus: string;
  contactId: number;
  contactName?: string | null;
  contactCode?: string | null;
  currencyCode: string;
  taxableAmount: number;
  totalAmount: number;
  status: string;
  isInterState: boolean;
  /** How many goods receipts have been raised against this order. */
  receiptCount: number;
}

export interface PurchaseOrderView extends PurchaseOrderListItem {
  contactGstin?: string | null;
  placeOfSupplyStateId: number;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  exchangeRate: number;
  subTotal: number;
  discountAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  totalAmountBase: number;
  notes?: string | null;
  termsAndConditions?: string | null;
  postedAt?: string | null;
  voidedAt?: string | null;
  voidReason?: string | null;
  lines: PurchaseOrderLineView[];
}

export interface PurchaseOrderLineView {
  purchaseOrderDetailId: number;
  lineNumber: number;
  itemId?: number | null;
  itemLabel?: string | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  uomId?: number | null;
  conversionFactor: number;
  baseQuantity: number;
  receivedQuantity: number;
  billedQuantity: number;
  unitPrice: number;
  isPriceInclusive: boolean;
  discountPercent?: number | null;
  discountAmount: number;
  grossAmount: number;
  taxableAmount: number;
  taxTreatment: string;
  taxMasterId?: number | null;
  taxGroupId?: number | null;
  taxAmount: number;
  lineType: string;
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  lineTotal: number;
  itemBatchId?: number | null;
  lineNotes?: string | null;
  taxes: PurchaseOrderLineTaxView[];
}

export interface PurchaseOrderLineTaxView {
  purchaseOrderDetailTaxId: number;
  taxComponent: string;
  subAccountId: number;
  rate: number;
  taxableAmount: number;
  amount: number;
  amountBase: number;
}

/**
 * Reads and writes purchase orders.
 *
 * Promises rather than piped observables, per the Angular conventions in
 * `CLAUDE.md` — these are one-shot REST calls and a stream buys nothing. The
 * existing sales services return `Observable`; they predate the convention.
 */
@Injectable({ providedIn: 'root' })
export class PurchaseOrderService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/purchase/purchase-orders';

  list(): Promise<PurchaseOrderListItem[]> {
    return firstValueFrom(this.http.get<PurchaseOrderListItem[]>(this.apiUrl));
  }

  get(purchaseOrderId: number): Promise<PurchaseOrderView> {
    return firstValueFrom(
      this.http.get<PurchaseOrderView>(`${this.apiUrl}/${purchaseOrderId}`),
    );
  }

  create(
    request: SavePurchaseOrderRequest,
  ): Promise<{ purchaseOrderId: number }> {
    return firstValueFrom(
      this.http.post<{ purchaseOrderId: number }>(this.apiUrl, request),
    );
  }

  update(
    purchaseOrderId: number,
    request: SavePurchaseOrderRequest,
  ): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`${this.apiUrl}/${purchaseOrderId}`, request),
    );
  }

  /** Draft to ready-to-post: the review step, for branches that want one. */
  approve(purchaseOrderId: number): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${purchaseOrderId}/approve`, {}),
    );
  }

  /** Issued to the vendor. Posts nothing to the ledger and moves no stock. */
  confirm(purchaseOrderId: number): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${purchaseOrderId}/confirm`, {}),
    );
  }

  void(
    purchaseOrderId: number,
    request: VoidPurchaseOrderRequest,
  ): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${purchaseOrderId}/void`, request),
    );
  }
}
