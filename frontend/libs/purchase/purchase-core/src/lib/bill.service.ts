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
/** Whether a bill line's GST can be claimed (TK-115, D-31). Not eligible posts the tax into the line's cost. */
export type ItcEligibility = 'Eligible' | 'Ineligible' | 'Blocked';

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
  /** The vendor charged no GST and this business pays it (TK-115, D-30): the vendor is owed the net. */
  isReverseCharge?: boolean;
  notes?: string | null;
  termsAndConditions?: string | null;
  lines: SaveBillLineRequest[];
  printTemplateId?: number | null;
  /** Custom field values (TK-112); left out on an update, what is stored stays. */
  customFields?: Record<string, unknown>;
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
  /** The project the line belongs to (TK-105). */
  projectId?: number | null;
  /** Charged on to the project's client, with this markup (TK-106). */
  isBillable?: boolean;
  markupPercent?: number | null;
  itemBatchId?: number | null;
  lineNotes?: string | null;
  itcEligibility?: ItcEligibility;
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
  printTemplateId?: number | null;
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
  isReverseCharge?: boolean;
  /** TDS deducted when the bill posted (TK-117): the vendor is owed the payable less this. */
  tdsSectionCode?: string | null;
  tdsBase?: number;
  tdsAmount?: number;
  tdsNote?: string | null;
  notes?: string | null;
  termsAndConditions?: string | null;
  postedAt?: string | null;
  voidedAt?: string | null;
  voidReason?: string | null;
  lines: BillLineView[];
  /** The document's custom field values (TK-112). */
  customFields?: Record<string, unknown> | null;
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
  /** The project the line belongs to (TK-105). */
  projectId?: number | null;
  /** Charged on to the project's client, with this markup (TK-106). */
  isBillable?: boolean;
  markupPercent?: number | null;
  lineTotal: number;
  itemBatchId?: number | null;
  lineNotes?: string | null;
  itcEligibility?: ItcEligibility;
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

  /** Creates a draft. `warnings` name expense accounts the bill takes past the approved budget (TK-109). */
  create(request: SaveBillRequest): Promise<{ billId: number; warnings?: string[] }> {
    return firstValueFrom(
      this.http.post<{ billId: number; warnings?: string[] }>(this.apiUrl, request),
    );
  }

  /** Saves a draft. The body is empty unless there is something to say: a return from approval, or budget warnings. */
  update(billId: number, request: SaveBillRequest): Promise<{ message?: string | null; warnings?: string[] } | null> {
    return firstValueFrom(
      this.http.put<{ message?: string | null; warnings?: string[] } | null>(`${this.apiUrl}/${billId}`, request),
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
