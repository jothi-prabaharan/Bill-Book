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

export interface ContactAddressOption {
  contactAddressId: number;
  kind: string;
  isDefaultBilling: boolean;
  isDefaultShipping: boolean;
  label?: string | null;
  addressLine1: string;
  addressLine2?: string | null;
  landmark?: string | null;
  city: string;
  stateId?: number | null;
  countryId?: number;
  postalCode?: string | null;
  gstin?: string | null;
  isActive: boolean;
}

export interface ContactAddressesSummary {
  contactId: number;
  displayName: string;
  gstin: string | null;
  addresses: ContactAddressOption[];
}

export function formatContactAddress(addr: {
  addressLine1?: string | null;
  addressLine2?: string | null;
  landmark?: string | null;
  city?: string | null;
  postalCode?: string | null;
}): string {
  const parts: string[] = [];
  if (addr.addressLine1?.trim()) parts.push(addr.addressLine1.trim());
  if (addr.addressLine2?.trim()) parts.push(addr.addressLine2.trim());
  if (addr.landmark?.trim()) parts.push(addr.landmark.trim());
  const cityZip = [addr.city?.trim(), addr.postalCode?.trim()].filter(Boolean).join(' - ');
  if (cityZip) parts.push(cityZip);
  return parts.join(', ');
}

export function formatAddressOptionLabel(addr: {
  kind: string;
  label?: string | null;
  addressLine1?: string | null;
  city?: string | null;
}): string {
  const kindLabel = addr.label ? `${addr.label} (${addr.kind})` : addr.kind;
  const summary = [addr.addressLine1, addr.city].filter(Boolean).join(', ');
  return summary ? `${kindLabel}: ${summary}` : kindLabel;
}

/** An item, as the items list serves it — enough to find and label one. */
export interface SalesItemOption {
  itemId: number;
  itemCode: string;
  itemName: string;
  inventoryUomCode: string;
  salesPrice?: number | null;
  mrp?: number | null;
  isActive: boolean;  /** The item's active barcodes, primary first (TK-164), to confirm a scan. */
  barcodeValues?: string[];
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
   * The addresses of a customer, read from the contact itself.
   */
  async contactAddresses(contactId: number): Promise<ContactAddressesSummary | null> {
    try {
      const res = await firstValueFrom(
        this.http.get<{
          contactId: number;
          displayName: string;
          gstin: string | null;
          addresses?: ContactAddressOption[];
        }>(`/api/contacts/${contactId}`)
      );
      return {
        contactId: res.contactId,
        displayName: res.displayName,
        gstin: res.gstin ?? null,
        addresses: (res.addresses ?? []).filter((a) => a.isActive !== false),
      };
    } catch {
      return null;
    }
  }

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
