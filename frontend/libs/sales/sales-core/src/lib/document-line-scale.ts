import { DocumentLine, DocumentLineTax } from '@bill-book/ui-components';

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

/** A tax rate carries four decimals in the grid, as `DocumentLineTax.rate` says. */
const RATE_SCALE = 10_000;

/** A line as the API serves it: rupees and plain quantities. */
export interface ApiDocumentLine {
  itemId?: number | null;
  /** What a saved document's line is served with: the item's "CODE - Name". */
  itemLabel?: string | null;
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
  /** The project the line belongs to (TK-105). */
  projectId?: number | null;
  itemBatchId?: number | null;
  lineNotes?: string | null;
  /** The amounts the server worked out when it saved the line, in rupees. */
  grossAmount?: number | null;
  taxableAmount?: number | null;
  taxAmount?: number | null;
  lineTotal?: number | null;
  /** The tax rows the server saved with the line; the rate is a percent. */
  taxes?: readonly ApiDocumentLineTax[] | null;
}

/** One saved tax row on a line, as the server serves it. */
export interface ApiDocumentLineTax {
  taxComponent?: string | null;
  subAccountId?: number | null;
  rate?: number | null;
  taxableAmount?: number | null;
  amount?: number | null;
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
 * <b>A saved line arrives with the amounts and tax rows the server saved it with</b>,
 * because the grid recalculates a line only when somebody edits it. Left at zero, a
 * reopened ₹100 invoice shows a total of 0.00 until a key is pressed, and editing
 * a quantity would drop its tax, as there would be no rows to recompute from. A new
 * line carries none, so it starts at zero and the first edit fills it in.
 */
export function toGridLine(line: ApiDocumentLine, lineNumber: number): DocumentLine {
  const quantity = line.quantity ?? 0;
  const conversionFactor = line.conversionFactor ?? 1;

  return {
    detailId: 0,
    lineNumber,
    itemId: line.itemId ?? null,
    itemLabel: line.itemLabel ?? line.itemName ?? line.itemCode ?? null,
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
    grossAmount: toPaise(line.grossAmount ?? 0),
    taxableAmount: toPaise(line.taxableAmount ?? 0),
    taxTreatment: (line.taxTreatment ?? 'Taxable') as DocumentLine['taxTreatment'],
    taxMasterId: line.taxMasterId ?? null,
    taxGroupId: line.taxGroupId ?? line.taxMasterId ?? null,
    taxAmount: toPaise(line.taxAmount ?? 0),
    taxes: (line.taxes ?? []).map((tax) => ({
      component: (tax.taxComponent ?? 'Cgst') as DocumentLineTax['component'],
      subAccountId: tax.subAccountId ?? 0,
      rate: Math.round((tax.rate ?? 0) * RATE_SCALE),
      taxableAmount: toPaise(tax.taxableAmount ?? 0),
      amount: toPaise(tax.amount ?? 0),
    })),
    lineType: (line.lineType ?? 'Stock') as DocumentLine['lineType'],
    accountId: line.accountId ?? null,
    fixedAssetCategoryId: line.fixedAssetCategoryId ?? null,
    projectId: line.projectId ?? null,
    lineTotal: toPaise(line.lineTotal ?? 0),
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
    taxMasterId: (line.taxGroupId && line.taxGroupId > 0) ? line.taxGroupId : ((line.taxMasterId && line.taxMasterId > 0) ? line.taxMasterId : undefined),
    taxGroupId: (line.taxGroupId && line.taxGroupId > 0) ? line.taxGroupId : ((line.taxMasterId && line.taxMasterId > 0) ? line.taxMasterId : undefined),
    lineType: line.lineType,
    accountId: line.accountId ?? undefined,
    fixedAssetCategoryId: line.fixedAssetCategoryId ?? undefined,
    projectId: line.projectId ?? undefined,
    itemBatchId: line.itemBatchId ?? undefined,
    lineNotes: line.lineNotes ?? undefined,
  };
}

/** An empty line at one unit, ready to key. */
export function blankGridLine(lineNumber: number): DocumentLine {
  return toGridLine({ quantity: 1, conversionFactor: 1, unitPrice: 0 }, lineNumber);
}
