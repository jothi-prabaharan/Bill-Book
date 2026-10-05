import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { SalesLookupService } from '@bill-book/sales-core';
import { TaxGroupOption, TaxTreatment } from '@bill-book/ui-components';
import { firstValueFrom } from 'rxjs';
import { CartItem } from './pos-cart';

/**
 * The walk-in customer's contact code.
 *
 * Looked up by code rather than by id, because ids are allocated per branch and
 * differ in every database — a hard-coded `contactId: 1` sold to whichever
 * contact happened to be created first.
 */
export const WALK_IN_CONTACT_CODE = 'WALKIN';

/** A customer, as the contacts list serves it. */
export interface CustomerOption {
  contactId: number;
  contactCode: string;
  displayName: string;
  isCustomer: boolean;
  gstin: string | null;
  currencyCode: string;
  isActive: boolean;
}

/** An item, as the items list serves it — enough to find one. */
export interface ItemOption {
  itemId: number;
  itemCode: string;
  itemName: string;
  inventoryUomCode: string;
  salesPrice: number | null;
  mrp: number | null;
  isActive: boolean;
}

/** The item detail fields the till reads to sell one. */
interface ItemDetailResponse extends ItemOption {
  taxGroupId: number | null;
  taxPreference: TaxTreatment;
  isPriceInclusiveOfTax: boolean;
}

/** A GST rate, as the tax master serves it. */
interface TaxRateResponse {
  taxMasterId: number;
  taxGroupId: number;
  taxName: string;
  cgstRate: number;
  sgstRate: number;
  igstRate: number;
  cessRate: number;
  isSales: boolean;
  isCurrent: boolean;
  isActive: boolean;
}

/** What the till needs from the branch. */
export interface TillBranch {
  gstin: string | null;
  discountBeforeTax: boolean;
}

/**
 * The masters the till looks things up in.
 *
 * **Finding a customer or an item is `SalesLookupService`'s** (TK-15), so the
 * till and every sales form search the same way; this keeps only what is the
 * till's own — the walk-in, an item as the cart sells it, the sales rates and
 * the branch. Each call goes to the service that owns the data — contacts and
 * the branch to Master, items to Inventory, rates to Accounting — so nothing
 * reads another service's tables.
 */
@Injectable({ providedIn: 'root' })
export class PosLookupService {
  private readonly http = inject(HttpClient);
  private readonly sales = inject(SalesLookupService);

  /** Active customers matching a name, code or GSTIN. */
  customers(search: string): Promise<CustomerOption[]> {
    return this.sales.customers(search);
  }

  /**
   * The branch's walk-in customer, or null when it has none.
   *
   * The search is a substring match, so the exact code is checked here — a
   * contact coded `WALKIN-2` must not be taken for the walk-in.
   */
  async walkInCustomer(): Promise<CustomerOption | null> {
    const rows = await this.customers(WALK_IN_CONTACT_CODE);

    return (
      rows.find(
        (row) => row.contactCode.toUpperCase() === WALK_IN_CONTACT_CODE,
      ) ?? null
    );
  }

  /**
   * Active items matching a name or code, or a whole barcode — a scanned
   * barcode's item comes first (TK-14). The list serves the prices the till
   * shows, so the shape is the till's own.
   */
  async items(search: string): Promise<ItemOption[]> {
    return (await this.sales.items(search)) as ItemOption[];
  }

  /**
   * One item, in the shape the cart sells it.
   *
   * The list does not carry the tax group or whether the price includes tax,
   * so choosing an item costs one more read. The price is the sales price; an
   * item with none falls back to its MRP, which is tax-inclusive by law.
   */
  async cartItem(itemId: number): Promise<CartItem> {
    const detail = await firstValueFrom(
      this.http.get<ItemDetailResponse>(`/api/items/${itemId}`),
    );

    const hasSalesPrice = detail.salesPrice !== null && detail.salesPrice !== undefined;

    return {
      itemId: detail.itemId,
      itemCode: detail.itemCode,
      itemName: detail.itemName,
      unitPrice: hasSalesPrice ? detail.salesPrice ?? 0 : detail.mrp ?? 0,
      isPriceInclusive: hasSalesPrice ? detail.isPriceInclusiveOfTax : true,
      taxTreatment: detail.taxPreference,
      taxGroupId: detail.taxGroupId ?? null,
    };
  }

  /**
   * The rates a sale may use: in force today, active, and flagged for sales.
   * One per tax group — the current row is the one the group means today.
   */
  async salesTaxGroups(): Promise<TaxGroupOption[]> {
    const rows = await firstValueFrom(
      this.http.get<TaxRateResponse[]>('/api/tax-masters?includeHistory=false'),
    );

    return rows
      .filter((row) => row.isSales && row.isActive && row.isCurrent)
      .map((row) => ({
        taxGroupId: row.taxGroupId,
        taxMasterId: row.taxMasterId,
        label: row.taxName,
        cgstRate: row.cgstRate,
        sgstRate: row.sgstRate,
        igstRate: row.igstRate,
        cessRate: row.cessRate,
      }));
  }

  /** The signed-in branch's GSTIN and discount rule. */
  async branch(): Promise<TillBranch> {
    const row = await firstValueFrom(
      this.http.get<{ gstin?: string | null; discountBeforeTax?: boolean }>(
        '/api/organizations/current',
      ),
    );

    return {
      gstin: row.gstin ?? null,
      discountBeforeTax: row.discountBeforeTax ?? true,
    };
  }
}
