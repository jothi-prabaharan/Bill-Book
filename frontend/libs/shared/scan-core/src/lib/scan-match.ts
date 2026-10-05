/** What a scan is matched against: an item's code and, when the list carries them, its barcodes. */
export interface ScanMatchable {
  itemCode: string;
  /** The item's active barcodes (TK-164); absent from a server that does not send them. */
  barcodeValues?: readonly string[] | null;
}

/**
 * Which item a scanned code names (TK-40's till rule, shared by TK-161 and
 * confirmed by TK-164):
 *
 * 1. the item one of whose barcodes is exactly the scan;
 * 2. else the item whose code is exactly the scan, ignoring case;
 * 3. else, only when the list did not say which barcodes each item has, the
 *    only match the item search returned — the search matches a whole barcode
 *    and puts that item first (TK-14), but without the barcodes a single match
 *    on a name that happens to contain the digits cannot be told from it.
 *
 * Anything else — no match, or several — is for a person to choose, so a code
 * never adds the wrong item.
 */
export function matchScannedItem<T extends ScanMatchable>(code: string, matches: readonly T[]): T | null {
  const scanned = code.trim();
  if (!scanned) {
    return null;
  }

  const byBarcode = matches.find((m) => m.barcodeValues?.some((b) => b.trim() === scanned));
  if (byBarcode) {
    return byBarcode;
  }

  const wanted = scanned.toUpperCase();
  const byCode = matches.find((m) => m.itemCode.trim().toUpperCase() === wanted);
  if (byCode) {
    return byCode;
  }

  return matches.length === 1 && matches[0].barcodeValues == null ? matches[0] : null;
}
