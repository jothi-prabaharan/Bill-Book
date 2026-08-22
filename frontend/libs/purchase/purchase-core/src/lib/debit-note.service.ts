import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/**
 * A debit note — `DBN`. The way back out of a purchase.
 *
 * The bill is required, and so is the bill line on every row: GST wants the note
 * to name what it corrects, and stock going back needs the bill line to find the
 * cost layer it arrived on.
 */
export interface SaveDebitNoteRequest {
  documentDate: string;
  contactId: number;
  billId: number;
  /** Only `PurchaseReturn` moves stock; the rest are money-only corrections. */
  reasonCode: string;
  contactGstin?: string | null;
  placeOfSupplyStateCode?: string | null;
  currencyCode?: string | null;
  exchangeRate?: number | null;
  notes?: string | null;
  lines: SaveDebitNoteLineRequest[];
}

export interface SaveDebitNoteLineRequest {
  billDetailId: number;
  itemId?: number | null;
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

export interface VoidDebitNoteRequest {
  reason: string;
}

export interface DebitNoteListItem {
  debitNoteId: number;
  documentNo: string;
  documentDate: string;
  billId: number;
  billNo?: string | null;
  vendorBillNo?: string | null;
  reasonCode: string;
  contactId: number;
  contactName?: string | null;
  contactCode?: string | null;
  currencyCode: string;
  taxableAmount: number;
  totalAmount: number;
  status: string;
  isInterState: boolean;
  /** Whether this note sent goods back, or only money. */
  movesStock: boolean;
}

export interface DebitNoteView extends DebitNoteListItem {
  contactGstin?: string | null;
  placeOfSupplyStateId: number;
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
  postedAt?: string | null;
  voidedAt?: string | null;
  voidReason?: string | null;
  lines: DebitNoteLineView[];
}

export interface DebitNoteLineView {
  debitNoteDetailId: number;
  lineNumber: number;
  billDetailId: number;
  itemId?: number | null;
  itemLabel?: string | null;
  description?: string | null;
  hsnSacCode?: string | null;
  warehouseId?: number | null;
  quantity: number;
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
  taxes: DebitNoteLineTaxView[];
}

export interface DebitNoteLineTaxView {
  debitNoteDetailTaxId: number;
  taxComponent: string;
  subAccountId: number;
  rate: number;
  taxableAmount: number;
  amount: number;
  amountBase: number;
}

/** The reasons, and whether each sends goods back. */
export const DEBIT_NOTE_REASONS: ReadonlyArray<{
  code: string;
  label: string;
  movesStock: boolean;
  hint: string;
}> = [
  {
    code: 'PurchaseReturn',
    label: 'Purchase return',
    movesStock: true,
    hint: 'Goods go back to the vendor. The only reason that moves stock.',
  },
  {
    code: 'PriceCorrection',
    label: 'Price correction',
    movesStock: false,
    hint: 'The vendor billed the wrong price. The goods stayed.',
  },
  {
    code: 'PostPurchaseDiscount',
    label: 'Post-purchase discount',
    movesStock: false,
    hint: 'A discount agreed after the bill was raised.',
  },
  {
    code: 'Deficiency',
    label: 'Short delivery or damage',
    movesStock: false,
    hint: 'Short delivery or damage in transit. Nothing goes back.',
  },
  {
    code: 'Cancellation',
    label: 'Cancellation',
    movesStock: false,
    hint: 'The bill should not have existed and is reversed in full.',
  },
];

@Injectable({ providedIn: 'root' })
export class DebitNoteService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/purchase/debit-notes';

  list(): Promise<DebitNoteListItem[]> {
    return firstValueFrom(this.http.get<DebitNoteListItem[]>(this.apiUrl));
  }

  get(debitNoteId: number): Promise<DebitNoteView> {
    return firstValueFrom(
      this.http.get<DebitNoteView>(`${this.apiUrl}/${debitNoteId}`),
    );
  }

  create(request: SaveDebitNoteRequest): Promise<{ debitNoteId: number }> {
    return firstValueFrom(
      this.http.post<{ debitNoteId: number }>(this.apiUrl, request),
    );
  }

  update(debitNoteId: number, request: SaveDebitNoteRequest): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`${this.apiUrl}/${debitNoteId}`, request),
    );
  }

  post(debitNoteId: number): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${debitNoteId}/post`, {}),
    );
  }

  void(debitNoteId: number, request: VoidDebitNoteRequest): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${debitNoteId}/void`, request),
    );
  }
}
