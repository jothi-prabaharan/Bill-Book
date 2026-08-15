import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/**
 * A vendor bill — `BIL`. What is owed, and the document input tax credit is
 * claimed on.
 *
 * `vendorBillNo` is theirs, not ours: `documentNo` is our internal reference,
 * and the credit is claimed against the vendor's number. One vendor cannot bill
 * the same number twice in a financial year.
 */
export interface SaveBillRequest {
  documentDate: string;
  contactId: number;
  purchaseOrderId?: number | null;
  /** Its presence changes what the bill posts: against a receipt it moves no stock. */
  goodsReceiptId?: number | null;
  vendorBillNo: string;
  vendorBillDate: string;
  paymentTermId?: number | null;
  dueDate?: string | null;
  contactGstin?: string | null;
  placeOfSupplyStateCode?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  landedCostAmount: number;
  notes?: string | null;
  termsAndConditions?: string | null;
  lines: SaveBillLineRequest[];
}

export interface SaveBillLineRequest {
  itemId?: number | null;
  goodsReceiptDetailId?: number | null;
  purchaseOrderDetailId?: number | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
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
  lineNotes?: string | null;
}

export interface VoidBillRequest {
  reason: string;
}

export interface BillListItem {
  billId: number;
  documentNo: string;
  documentDate: string;
  vendorBillNo: string;
  vendorBillDate: string;
  dueDate: string;
  purchaseOrderId?: number | null;
  goodsReceiptId?: number | null;
  goodsReceiptNo?: string | null;
  contactId: number;
  contactName?: string | null;
  contactCode?: string | null;
  currencyCode: string;
  taxableAmount: number;
  totalAmount: number;
  status: string;
  isInterState: boolean;
  /** Days past due for a posted bill. Negative is not yet due. */
  daysOverdue: number;
}

export interface BillView extends BillListItem {
  contactGstin?: string | null;
  placeOfSupplyStateId: number;
  paymentTermId?: number | null;
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
  landedCostAmount: number;
  notes?: string | null;
  termsAndConditions?: string | null;
  postedAt?: string | null;
  voidedAt?: string | null;
  voidReason?: string | null;
  lines: BillLineView[];
}

export interface BillLineView {
  billDetailId: number;
  lineNumber: number;
  goodsReceiptDetailId?: number | null;
  purchaseOrderDetailId?: number | null;
  itemId?: number | null;
  itemLabel?: string | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
  uomId?: number | null;
  conversionFactor: number;
  baseQuantity: number;
  returnedQuantity: number;
  apportionedLandedCost: number;
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
  taxes: BillLineTaxView[];
}

export interface BillLineTaxView {
  billDetailTaxId: number;
  taxComponent: string;
  subAccountId: number;
  rate: number;
  taxableAmount: number;
  amount: number;
  amountBase: number;
}

@Injectable({ providedIn: 'root' })
export class BillService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/purchase/bills';

  list(): Promise<BillListItem[]> {
    return firstValueFrom(this.http.get<BillListItem[]>(this.apiUrl));
  }

  get(billId: number): Promise<BillView> {
    return firstValueFrom(this.http.get<BillView>(`${this.apiUrl}/${billId}`));
  }

  create(request: SaveBillRequest): Promise<{ billId: number }> {
    return firstValueFrom(
      this.http.post<{ billId: number }>(this.apiUrl, request),
    );
  }

  update(billId: number, request: SaveBillRequest): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`${this.apiUrl}/${billId}`, request),
    );
  }

  post(billId: number): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${billId}/post`, {}),
    );
  }

  void(billId: number, request: VoidBillRequest): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${billId}/void`, request),
    );
  }
}
