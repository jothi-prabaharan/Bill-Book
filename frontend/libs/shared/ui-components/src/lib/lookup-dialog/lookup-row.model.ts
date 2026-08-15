/**
 * One choosable row in a {@link LookupDialogComponent}.
 *
 * Deliberately thin: an id, the two strings every master in this product has,
 * and an optional third line. Contacts, items, accounts and warehouses all
 * collapse to this, which is what lets one dialog serve every picker instead of
 * one dialog per master.
 */
export interface LookupRow {
  id: number;
  /** The code — `ITM-0001`, `C42`. Shown first because it is what people type. */
  code: string;
  name: string;
  /** Anything worth seeing before choosing: a GSTIN, a unit, a category. */
  meta?: string | null;
}
