/**
 * Which item a scanned code names (TK-40's till rule, shared by TK-161): the
 * item whose code is exactly the scan, ignoring case, or the only match the
 * item search returned. Anything else — no match, or several — is for a person
 * to choose, so a code that matches several items never adds the wrong one.
 *
 * The item search matches a whole barcode and puts that item first (TK-14), so
 * a barcode that names one item comes back as the only match.
 */
export function matchScannedItem<T extends { itemCode: string }>(code: string, matches: readonly T[]): T | null {
  const wanted = code.trim().toUpperCase();
  if (!wanted) {
    return null;
  }
  const exact = matches.find((m) => m.itemCode.trim().toUpperCase() === wanted);
  return exact ?? (matches.length === 1 ? matches[0] : null);
}
