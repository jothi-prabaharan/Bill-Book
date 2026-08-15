import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/** A vendor, as the contacts list serves it. */
export interface VendorOption {
  contactId: number;
  contactCode: string;
  displayName: string;
  isVendor: boolean;
  gstin: string | null;
  currencyCode: string;
  isActive: boolean;
}

/** An item, as the items list serves it. */
export interface ItemOption {
  itemId: number;
  itemCode: string;
  itemName: string;
  inventoryUomCode: string;
  isActive: boolean;
}

/** A purchase order, as the orders list serves it — enough to pick one. */
export interface OrderOption {
  purchaseOrderId: number;
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName?: string | null;
  status: string;
  fulfilmentStatus: string;
  totalAmount: number;
  currencyCode: string;
}

/** A goods receipt, as the receipts list serves it — enough to pick one. */
export interface ReceiptOption {
  goodsReceiptId: number;
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName?: string | null;
  vendorDeliveryNoteNo?: string | null;
  status: string;
  totalAmount: number;
  currencyCode: string;
}

/** A bill, as the bills list serves it — enough to pick one. */
export interface BillOption {
  billId: number;
  documentNo: string;
  vendorBillNo: string;
  documentDate: string;
  contactId: number;
  contactName?: string | null;
  status: string;
  totalAmount: number;
  currencyCode: string;
}

/** A GST rate, as the tax master serves it. */
export interface TaxRateOption {
  taxMasterId: number;
  taxGroupId: number;
  taxName: string;
  totalRate: number;
  cgstRate: number;
  sgstRate: number;
  igstRate: number;
  cessRate: number;
  isPurchase: boolean;
  isCurrent: boolean;
  isActive: boolean;
}

/** What the branch has decided about how its documents are built. */
export interface BranchDocumentSettings {
  stateCode: string;
  allowFreeTextLines: boolean;
  discountLevel: 'Line' | 'Header' | 'Both';
  discountBeforeTax: boolean;
}

/**
 * The masters a purchase document has to look things up in.
 *
 * **One service rather than three, because they are one concern**: everything a
 * purchase screen needs that Purchase itself does not own. Each call goes to the
 * service that owns the data — contacts and org settings to Master, items to
 * Inventory, rates to Accounting — through the gateway, so no boundary is
 * crossed by reading somebody else's tables.
 */
@Injectable({ providedIn: 'root' })
export class PurchaseLookupService {
  private readonly http = inject(HttpClient);

  /**
   * Vendors matching a search term.
   *
   * **Filtered to `isVendor` here rather than by the API**, because the contacts
   * endpoint serves one master with roles on it — the same contact can be a
   * customer and a vendor at once, and a purchase screen wants the vendor half.
   */
  async vendors(search: string): Promise<VendorOption[]> {
    const query = new URLSearchParams();
    if (search.trim()) {
      query.set('search', search.trim());
    }
    query.set('includeInactive', 'false');

    const rows = await firstValueFrom(
      this.http.get<VendorOption[]>(`/api/contacts?${query}`),
    );

    return rows.filter((row) => row.isVendor);
  }

  /**
   * Orders a receipt can be raised against: issued, not voided, and not already
   * fully received. A draft order has not been committed to the vendor, so goods
   * arriving against one mean somebody skipped a step rather than that the
   * order should be receivable.
   */
  async openOrders(search: string, contactId: number | null): Promise<OrderOption[]> {
    const rows = await firstValueFrom(
      this.http.get<OrderOption[]>('/api/purchase/purchase-orders'),
    );

    const term = search.trim().toLowerCase();

    return rows.filter(
      (row) =>
        row.status === 'Posted' &&
        row.fulfilmentStatus !== 'Closed' &&
        row.fulfilmentStatus !== 'Cancelled' &&
        (contactId === null || row.contactId === contactId) &&
        (term === '' ||
          row.documentNo.toLowerCase().includes(term) ||
          (row.contactName ?? '').toLowerCase().includes(term)),
    );
  }

  /**
   * Receipts a bill can be raised against: posted, so something is actually
   * sitting in the clearing account for the bill to clear. A draft receipt has
   * credited nothing.
   */
  async billableReceipts(search: string, contactId: number | null): Promise<ReceiptOption[]> {
    const rows = await firstValueFrom(
      this.http.get<ReceiptOption[]>('/api/purchase/goods-receipts'),
    );

    const term = search.trim().toLowerCase();

    return rows.filter(
      (row) =>
        row.status === 'Posted' &&
        (contactId === null || row.contactId === contactId) &&
        (term === '' ||
          row.documentNo.toLowerCase().includes(term) ||
          (row.contactName ?? '').toLowerCase().includes(term) ||
          (row.vendorDeliveryNoteNo ?? '').toLowerCase().includes(term)),
    );
  }

  /**
   * Bills a debit note can be raised against: posted, so something is actually
   * owed. A draft bill owes nothing yet and is corrected by editing it.
   */
  async creditableBills(search: string, contactId: number | null): Promise<BillOption[]> {
    const rows = await firstValueFrom(
      this.http.get<BillOption[]>('/api/purchase/bills'),
    );

    const term = search.trim().toLowerCase();

    return rows.filter(
      (row) =>
        row.status === 'Posted' &&
        (contactId === null || row.contactId === contactId) &&
        (term === '' ||
          row.documentNo.toLowerCase().includes(term) ||
          row.vendorBillNo.toLowerCase().includes(term) ||
          (row.contactName ?? '').toLowerCase().includes(term)),
    );
  }

  async items(search: string): Promise<ItemOption[]> {
    const query = new URLSearchParams();
    if (search.trim()) {
      query.set('search', search.trim());
    }
    query.set('includeInactive', 'false');

    return firstValueFrom(this.http.get<ItemOption[]>(`/api/items?${query}`));
  }

  /**
   * The rates a purchase line may use: in force today, active, and flagged for
   * purchase. A rate marked sales-only is not offered, which is what the flag
   * is for.
   */
  async purchaseTaxRates(): Promise<TaxRateOption[]> {
    const rows = await firstValueFrom(
      this.http.get<TaxRateOption[]>('/api/tax-masters?includeHistory=false'),
    );

    return rows.filter((row) => row.isPurchase && row.isActive && row.isCurrent);
  }

  /**
   * The branch's own document settings.
   *
   * These decide what the line grid lets you key — whether a line may stand on a
   * description alone, and whether a discount reduces the taxable value. They
   * are read rather than assumed because getting `discountBeforeTax` wrong
   * changes the tax on every line.
   */
  settings(): Promise<BranchDocumentSettings> {
    return firstValueFrom(
      this.http.get<BranchDocumentSettings>('/api/organizations/current'),
    );
  }
}
