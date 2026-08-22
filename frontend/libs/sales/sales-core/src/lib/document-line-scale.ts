import { DocumentLine } from '@bill-book/ui-components';

/**
 * The scale boundary between the API and the line grid.
 *
 * **They do not speak the same units, and nothing was converting.** The API
 * serves decimal rupees and plain quantities, because the server has a decimal
 * type and that is where the figures are authoritative. `bb-document-line-grid`
 * works in **integer paise** with quantity at six decimal places, because
 * JavaScript has no decimal and money that ties to a ledger cannot be added up
 * in floating point.
 *
 * Handing one to the other unconverted does not fail loudly — it computes. A
 * line of 10 units at ₹100 passed straight in becomes `10 × 100 / 1_000_000`,
 * so the grid shows a gross of zero and a total to match, and the document looks
 * merely empty rather than wrong. That is why this is a named boundary with
 * tests rather than a conversion sprinkled at each field: the one place somebody
 * forgets is a line off by a factor of a million that still renders.
 *
 * **Nothing here computes.** Conversion only — the arithmetic belongs to
 * `line-math.ts` on the way in and `GstCalculator` on the way out, and a third
 * implementation hiding in a mapper is precisely what the shared tax fixture
 * cannot catch.
 */

/** Paise per rupee. */
const PAISE = 100;

/** Quantity carries six decimals, matching `decimal(18,6)` on the column. */
const QTY_SCALE = 1_000_000;

// There is deliberately no rate scale here. Tax rows are not carried across:
// the grid rebuilds them from the line's treatment and rate as it renders, so
// converting the server's would put two sets of the same rows on one line.

/** A line as the API serves it: rupees and plain quantities. */
export interface ApiDocumentLine {
  itemId?: number | null;
  itemName?: string | null;
  itemCode?: string | null;
  hsnSacCode?: string | null;
  description?: string | null;
  warehouseId?: number | null;
  quantity?: number | null;
  uomId?: number | null;
  conversionFactor?: number | null;
  unitPrice?: number | null;
  isPriceInclusive?: boolean | null;
  discountPercent?: number | null;
  discountAmount?: number | null;
  taxTreatment?: string | null;
  taxMasterId?: number | null;
  taxGroupId?: number | null;
  lineType?: string | null;
  accountId?: number | null;
  fixedAssetCategoryId?: number | null;
  itemBatchId?: number | null;
  lineNotes?: string | null;
}

export function toPaise(rupees: number): number {
  return Math.round(rupees * PAISE);
}

export function toRupees(paise: number): number {
  return paise / PAISE;
}

/**
 * An API line, scaled for the grid.
 *
 * Computed amounts — gross, taxable, tax, line total — are left at zero rather
 * than carried across: the grid recalculates them from quantity, price and
 * discount the moment it renders, so copying the server's figures in would put
 * two answers on screen for the same line until the first keystroke.
 */
export function toGridLine(line: ApiDocumentLine, lineNumber: number): DocumentLine {
  const quantity = line.quantity ?? 0;
  const conversionFactor = line.conversionFactor ?? 1;

  return {
    detailId: 0,
    lineNumber,
    itemId: line.itemId ?? null,
    itemLabel: line.itemName ?? line.itemCode ?? null,
    hsnSacCode: line.hsnSacCode ?? null,
    description: line.description ?? null,
    warehouseId: line.warehouseId ?? null,
    quantity: Math.round(quantity * QTY_SCALE),
    uomId: line.uomId ?? null,
    uomLabel: null,
    conversionFactor: Math.round(conversionFactor * QTY_SCALE),
    baseQuantity: Math.round(quantity * conversionFactor * QTY_SCALE),
    unitPrice: toPaise(line.unitPrice ?? 0),
    isPriceInclusive: line.isPriceInclusive ?? false,
    discountPercent: line.discountPercent ?? null,
    discountAmount: toPaise(line.discountAmount ?? 0),
    grossAmount: 0,
    taxableAmount: 0,
    taxTreatment: (line.taxTreatment ?? 'Taxable') as DocumentLine['taxTreatment'],
    taxMasterId: line.taxMasterId ?? null,
    taxGroupId: line.taxGroupId ?? null,
    taxAmount: 0,
    taxes: [],
    lineType: (line.lineType ?? 'Stock') as DocumentLine['lineType'],
    accountId: line.accountId ?? null,
    fixedAssetCategoryId: line.fixedAssetCategoryId ?? null,
    lineTotal: 0,
    itemBatchId: line.itemBatchId ?? null,
    lineNotes: line.lineNotes ?? null,
  };
}

/**
 * A grid line, back in the units the API takes.
 *
 * The computed amounts are **dropped rather than sent**: the server recomputes
 * every figure against the rates in force on the document's date, so including
 * them would put two answers on the wire and invite somebody to trust the wrong
 * one.
 */
export function toApiLine(line: DocumentLine): ApiDocumentLine {
  return {
    itemId: line.itemId ?? undefined,
    hsnSacCode: line.hsnSacCode ?? undefined,
    description: line.description ?? undefined,
    warehouseId: line.warehouseId ?? undefined,
    quantity: line.quantity / QTY_SCALE,
    uomId: line.uomId ?? undefined,
    conversionFactor: line.conversionFactor / QTY_SCALE,
    unitPrice: toRupees(line.unitPrice),
    isPriceInclusive: line.isPriceInclusive,
    discountPercent: line.discountPercent ?? 0,
    discountAmount: toRupees(line.discountAmount),
    taxTreatment: line.taxTreatment,
    taxMasterId: line.taxMasterId ?? undefined,
    taxGroupId: line.taxGroupId ?? undefined,
    lineType: line.lineType,
    accountId: line.accountId ?? undefined,
    fixedAssetCategoryId: line.fixedAssetCategoryId ?? undefined,
    itemBatchId: line.itemBatchId ?? undefined,
    lineNotes: line.lineNotes ?? undefined,
  };
}

/** An empty line at one unit, ready to key. */
export function blankGridLine(lineNumber: number): DocumentLine {
  return toGridLine({ quantity: 1, conversionFactor: 1, unitPrice: 0 }, lineNumber);
}
