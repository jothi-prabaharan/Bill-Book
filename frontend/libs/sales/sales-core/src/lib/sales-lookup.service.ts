import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

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

/** An item, as the items list serves it — enough to find and label one. */
export interface SalesItemOption {
  itemId: number;
  itemCode: string;
  itemName: string;
  inventoryUomCode: string;
  salesPrice?: number | null;
  mrp?: number | null;
  isActive: boolean;
}

/**
 * The masters a sales document picks from: its customer and its items (TK-15).
 *
 * The mirror of `PurchaseLookupService`. Each call goes to the service that owns
 * the data — contacts to Master, items to Inventory — through the gateway, so no
 * boundary is crossed by reading somebody else's tables. The till uses the same
 * two calls, so a customer or an item is found the same way on every screen.
 */
@Injectable({ providedIn: 'root' })
export class SalesLookupService {
  private readonly http = inject(HttpClient);

  /**
   * Active customers matching a name, code or GSTIN. Filtered by the API's
   * `role=customer`: one contact master serves every role, and a sales screen
   * wants the customer half.
   */
  customers(search: string): Promise<CustomerOption[]> {
    const query = new URLSearchParams();
    if (search.trim()) {
      query.set('search', search.trim());
    }
    query.set('role', 'customer');
    query.set('includeInactive', 'false');

    return firstValueFrom(this.http.get<CustomerOption[]>(`/api/contacts?${query}`));
  }

  /**
   * Active items matching a name or code anywhere, or a barcode exactly — a
   * scanned barcode's item comes first (TK-14).
   */
  items(search: string): Promise<SalesItemOption[]> {
    const query = new URLSearchParams();
    if (search.trim()) {
      query.set('search', search.trim());
    }
    query.set('includeInactive', 'false');

    return firstValueFrom(this.http.get<SalesItemOption[]>(`/api/items?${query}`));
  }
}
