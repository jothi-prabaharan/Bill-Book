/**
 * The place-of-supply rule a purchase form applies as it is filled in, the same
 * one `PlaceOfSupply.Resolve` applies on save (TK-206, TK-208).
 *
 * A registered vendor's GSTIN fixes the state, and the server refuses a
 * document whose place of supply contradicts it; an unregistered vendor
 * supplies from the branch's own state unless the person says otherwise.
 * Inter-state is the supply state against the branch's.
 */

/** The two-digit state a GSTIN is registered in, or null when it does not start with one. */
export function gstinState(gstin: string | null | undefined): string | null {
  const code = (gstin ?? '').trim().slice(0, 2);
  return /^\d{2}$/.test(code) ? code : null;
}

/** The place of supply a vendor implies: their GSTIN's state, else the branch's. */
export function placeOfSupplyFor(gstin: string | null | undefined, branchState: string): string {
  return gstinState(gstin) ?? branchState;
}

/**
 * Whether the document is inter-state, from the place of supply (or, when that
 * is blank, the vendor's GSTIN) against the branch's state. Null when neither
 * names a state, so the caller keeps what the server last said.
 */
export function supplyIsInterState(
  placeOfSupply: string | null | undefined,
  gstin: string | null | undefined,
  branchState: string,
): boolean | null {
  const supply = (placeOfSupply ?? '').trim() || gstinState(gstin) || '';
  return supply.length === 2 ? supply !== branchState : null;
}
