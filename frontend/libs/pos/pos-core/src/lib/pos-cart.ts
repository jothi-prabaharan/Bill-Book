import { blankGridLine } from '@bill-book/sales-core';
import {
  DocumentLine,
  DocumentLineContext,
  DocumentLineTax,
  TaxGroupOption,
  TaxTreatment,
  componentsFor,
  recalculate,
  totalsOf,
} from '@bill-book/ui-components';

/**
 * The till's cart, as pure functions over `DocumentLine`.
 *
 * **A cart line is an invoice line**, not a shape of its own. A POS sale is an
 * `sal.Invoices` row, so the cart is held in the same paise-and-scaled-quantity
 * form the invoice grid uses and goes to the API through the same `toApiLine`.
 * Every sum runs through `line-math.ts`, which is held to the C# calculator by a
 * shared fixture — a second implementation here would be a second GST answer.
 *
 * Kept free of Angular so the arithmetic can be tested without a component; the
 * terminal holds the result in a signal and calls these to change it.
 */

/** Quantity carries six decimals, matching `line-math.ts`. */
export const QTY_SCALE = 1_000_000;

/** Rates carry four decimals, matching `line-math.ts`. */
const RATE_SCALE = 10_000;

/** What the cart needs to know about an item to sell one. */
export interface CartItem {
  itemId: number;
  itemCode: string;
  itemName: string;
  /** Rupees, as the item master holds it. */
  unitPrice: number;
  isPriceInclusive: boolean;
  taxTreatment: TaxTreatment;
  taxGroupId: number | null;
}

/**
 * A till's document context.
 *
 * Free-text lines are off — every till line names an item, because the stock
 * decrement has nothing to decrement otherwise — and discounts are per line.
 */
export function cartContext(
  isInterState: boolean,
  discountBeforeTax: boolean,
): DocumentLineContext {
  return {
    isInterState,
    allowFreeTextLines: false,
    discountBeforeTax,
    discountLevel: 'Line',
    readonly: false,
    currencyDecimals: 2,
  };
}

/**
 * Whether a sale to this customer crosses a state line.
 *
 * A GSTIN's first two digits are its state. A customer with no GSTIN — the
 * walk-in case — is supplied at the counter, so the place of supply is the
 * branch's own state and the sale is intra-state. A branch with no GSTIN has
 * no state to compare against, and is treated the same way.
 */
export function isInterStateSale(
  branchGstin: string | null | undefined,
  customerGstin: string | null | undefined,
): boolean {
  const branch = (branchGstin ?? '').trim().slice(0, 2);
  const customer = (customerGstin ?? '').trim().slice(0, 2);

  return branch.length === 2 && customer.length === 2 && branch !== customer;
}

/**
 * The tax rows a line carries under this group, at zero until recalculated.
 *
 * The same construction as the document grid's: CGST + SGST inside the state,
 * IGST across it, and cess on top of either when the group has one.
 */
export function taxRowsFor(
  group: TaxGroupOption | null | undefined,
  context: DocumentLineContext,
): DocumentLineTax[] {
  if (!group) {
    return [];
  }

  const taxes: DocumentLineTax[] = componentsFor(
    context.isInterState,
    context.isUnionTerritory ?? false,
  ).map((component) => ({
    component,
    // Resolved server-side on save; the till cannot know the GST sub-account.
    subAccountId: 0,
    rate: Math.round(rateOf(group, component) * RATE_SCALE),
    taxableAmount: 0,
    amount: 0,
  }));

  if (group.cessRate > 0) {
    taxes.push({
      component: 'Cess',
      subAccountId: 0,
      rate: Math.round(group.cessRate * RATE_SCALE),
      taxableAmount: 0,
      amount: 0,
    });
  }

  return taxes;
}

function rateOf(
  group: TaxGroupOption,
  component: 'Cgst' | 'Sgst' | 'Igst' | 'Utgst',
): number {
  switch (component) {
    case 'Cgst':
      return group.cgstRate;
    case 'Sgst':
    case 'Utgst':
      return group.sgstRate;
    default:
      return group.igstRate;
  }
}

/** A new line for one unit of the item, already calculated. */
export function lineFor(
  item: CartItem,
  lineNumber: number,
  group: TaxGroupOption | null | undefined,
  context: DocumentLineContext,
): DocumentLine {
  const blank = blankGridLine(lineNumber);

  return recalculate(
    {
      ...blank,
      itemId: item.itemId,
      itemLabel: `${item.itemCode} · ${item.itemName}`,
      description: item.itemName,
      unitPrice: Math.round(item.unitPrice * 100),
      isPriceInclusive: item.isPriceInclusive,
      taxTreatment: item.taxTreatment,
      taxGroupId: group?.taxGroupId ?? null,
      taxMasterId: group?.taxMasterId ?? null,
      taxes: taxRowsFor(group, context),
      lineType: 'Stock',
    },
    context,
  );
}

/**
 * Adds one unit of the item.
 *
 * Scanning the same item twice is one line at two, not two lines at one — the
 * way every till behaves, and what the receipt should read.
 */
export function addItem(
  lines: readonly DocumentLine[],
  item: CartItem,
  group: TaxGroupOption | null | undefined,
  context: DocumentLineContext,
): DocumentLine[] {
  const existing = lines.findIndex((line) => line.itemId === item.itemId);

  if (existing >= 0) {
    return setQuantity(
      lines,
      existing,
      lines[existing].quantity / QTY_SCALE + 1,
      context,
    );
  }

  return [...lines, lineFor(item, lines.length + 1, group, context)];
}

/**
 * Sets a line's quantity, in units.
 *
 * Zero or less removes the line: a till has no use for a line selling nothing,
 * and a negative one would be a return, which is a credit note rather than a
 * cart line.
 */
export function setQuantity(
  lines: readonly DocumentLine[],
  index: number,
  quantity: number,
  context: DocumentLineContext,
): DocumentLine[] {
  if (!Number.isFinite(quantity) || quantity <= 0) {
    return removeLine(lines, index);
  }

  return lines.map((line, at) =>
    at === index
      ? recalculate({ ...line, quantity: Math.round(quantity * QTY_SCALE) }, context)
      : line,
  );
}

/** Sets a line's unit price, in rupees. A negative price is refused. */
export function setUnitPrice(
  lines: readonly DocumentLine[],
  index: number,
  rupees: number,
  context: DocumentLineContext,
): DocumentLine[] {
  if (!Number.isFinite(rupees) || rupees < 0) {
    return [...lines];
  }

  return lines.map((line, at) =>
    at === index
      ? recalculate({ ...line, unitPrice: Math.round(rupees * 100) }, context)
      : line,
  );
}

/** Removes a line and renumbers the rest, so line numbers stay 1..n. */
export function removeLine(
  lines: readonly DocumentLine[],
  index: number,
): DocumentLine[] {
  return lines
    .filter((_, at) => at !== index)
    .map((line, at) => ({ ...line, lineNumber: at + 1 }));
}

/**
 * Rebuilds every line's tax rows for a new context.
 *
 * Changing the customer can move the sale across a state line, and CGST + SGST
 * becoming IGST is not a recalculation of the same rows — the rows themselves
 * change. Each line keeps its own group; only the split is redone.
 */
export function reprice(
  lines: readonly DocumentLine[],
  groups: readonly TaxGroupOption[],
  context: DocumentLineContext,
): DocumentLine[] {
  return lines.map((line) => {
    const group = groups.find((candidate) => candidate.taxGroupId === line.taxGroupId);
    return recalculate({ ...line, taxes: taxRowsFor(group, context) }, context);
  });
}

/** The cart's totals, in paise. */
export function cartTotals(lines: readonly DocumentLine[]) {
  return totalsOf(lines);
}
