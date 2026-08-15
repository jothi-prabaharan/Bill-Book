import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/**
 * A goods receipt — `GRN`. The goods arrived.
 *
 * As everywhere else, no totals are sent: the server computes every figure from
 * the lines. What is unique to this document is the split between what the
 * vendor delivered and what was actually taken in, and the batch and expiry
 * captured off the carton.
 */
export interface SaveGoodsReceiptRequest {
  documentDate: string;
  contactId: number;
  purchaseOrderId?: number | null;
  vendorDeliveryNoteNo?: string | null;
  vendorDeliveryNoteDate?: string | null;
  receivedBy?: string | null;
  contactGstin?: string | null;
  placeOfSupplyStateCode?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  notes?: string | null;
  termsAndConditions?: string | null;
  lines: SaveGoodsReceiptLineRequest[];
}

export interface SaveGoodsReceiptLineRequest {
  itemId?: number | null;
  purchaseOrderDetailId?: number | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  /** What the vendor delivered. Accepted plus rejected must equal this. */
  quantity: number;
  /** What is taken into stock. This, not the quantity, is what moves. */
  acceptedQuantity: number;
  rejectedQuantity: number;
  /** Required when anything was rejected. */
  rejectionReason?: string | null;
  uomId?: number | null;
  conversionFactor: number;
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
  batchNumber?: string | null;
  batchExpiryDate?: string | null;
  batchManufactureDate?: string | null;
  lineNotes?: string | null;
}

export interface VoidGoodsReceiptRequest {
  reason: string;
}

export interface GoodsReceiptListItem {
  goodsReceiptId: number;
  documentNo: string;
  documentDate: string;
  purchaseOrderId?: number | null;
  purchaseOrderNo?: string | null;
  vendorDeliveryNoteNo?: string | null;
  contactId: number;
  contactName?: string | null;
  contactCode?: string | null;
  currencyCode: string;
  taxableAmount: number;
  totalAmount: number;
  status: string;
  isInterState: boolean;
  /** How much was refused across the whole receipt. Zero on a clean delivery. */
  rejectedQuantity: number;
}

export interface GoodsReceiptView extends GoodsReceiptListItem {
  vendorDeliveryNoteDate?: string | null;
  receivedBy?: string | null;
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
  lines: GoodsReceiptLineView[];
}

export interface GoodsReceiptLineView {
  goodsReceiptDetailId: number;
  lineNumber: number;
  purchaseOrderDetailId?: number | null;
  itemId?: number | null;
  itemLabel?: string | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  rejectionReason?: string | null;
  uomId?: number | null;
  conversionFactor: number;
  baseQuantity: number;
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
  taxes: GoodsReceiptLineTaxView[];
}

export interface GoodsReceiptLineTaxView {
  goodsReceiptDetailTaxId: number;
  taxComponent: string;
  subAccountId: number;
  rate: number;
  taxableAmount: number;
  amount: number;
  amountBase: number;
}

/** Reads and writes goods receipts. */
@Injectable({ providedIn: 'root' })
export class GoodsReceiptService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/purchase/goods-receipts';

  list(): Promise<GoodsReceiptListItem[]> {
    return firstValueFrom(this.http.get<GoodsReceiptListItem[]>(this.apiUrl));
  }

  get(goodsReceiptId: number): Promise<GoodsReceiptView> {
    return firstValueFrom(
      this.http.get<GoodsReceiptView>(`${this.apiUrl}/${goodsReceiptId}`),
    );
  }

  create(
    request: SaveGoodsReceiptRequest,
  ): Promise<{ goodsReceiptId: number }> {
    return firstValueFrom(
      this.http.post<{ goodsReceiptId: number }>(this.apiUrl, request),
    );
  }

  update(
    goodsReceiptId: number,
    request: SaveGoodsReceiptRequest,
  ): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`${this.apiUrl}/${goodsReceiptId}`, request),
    );
  }

  /** Stock in, ledger written, the order advanced. Safe to call again after a failure. */
  post(goodsReceiptId: number): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${goodsReceiptId}/post`, {}),
    );
  }

  void(
    goodsReceiptId: number,
    request: VoidGoodsReceiptRequest,
  ): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${goodsReceiptId}/void`, request),
    );
  }
}
