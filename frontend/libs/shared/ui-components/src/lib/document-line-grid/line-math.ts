import { DocumentLine, DocumentLineContext, DocumentLineTax } from './document-line.model';

/**
 * Line arithmetic, in integer paise.
 *
 * Pure and free of Angular, so it can be tested without a component and shares
 * a fixture with the C# side. **The order of operations is fixed here and must
 * match `Shared.Kernel` exactly** — two implementations of the same sum diverge
 * by a paisa without either looking wrong, and a GST return is where that
 * surfaces months later.
 *
 *   gross    = qty × unitPrice
 *   discount = percent → amount, or the amount as keyed
 *   taxable  = gross − discount        (when discountBeforeTax)
 *   tax      = taxable × rate, split by component
 *   line     = taxable + tax
 */

/** Quantity carries six decimals; prices and amounts are whole paise. */
const QTY_SCALE = 1_000_000;

/** Rates carry four decimals: 18.0000 is stored as 180000. */
const RATE_SCALE = 10_000;

/**
 * Round half away from zero, matching `MidpointRounding.AwayFromZero` in
 * `LedgerPostingService`. JavaScript's `Math.round` rounds half *up*, which
 * differs on negative halves — and a round-off line is signed.
 */
export function roundHalfAwayFromZero(value: number): number {
  return value < 0 ? -Math.round(-value) : Math.round(value);
}

/** Gross in paise: quantity (6 dp) × unit price (paise). */
export function grossOf(line: DocumentLine): number {
  return roundHalfAwayFromZero((line.quantity * line.unitPrice) / QTY_SCALE);
}

/** The discount actually applied, whether keyed as a percent or an amount. */
export function discountOf(line: DocumentLine, gross: number): number {
  if (line.discountPercent === null) {
    return Math.min(line.discountAmount, gross);
  }
  const fromPercent = roundHalfAwayFromZero(
    (gross * line.discountPercent) / 100,
  );
  return Math.min(fromPercent, gross);
}

/**
 * The value tax is charged on.
 *
 * An inclusive price already contains its tax, so the taxable value is backed
 * out: `taxable = inclusive ÷ (1 + rate)`. MRP-inclusive pricing is the Indian
 * retail default, so this is the common path rather than an edge case.
 */
export function taxableOf(
  line: DocumentLine,
  context: DocumentLineContext,
): number {
  const gross = grossOf(line);
  const discount = discountOf(line, gross);
  const net = context.discountBeforeTax ? gross - discount : gross;

  if (!line.isPriceInclusive || !chargesTax(line)) {
    return net;
  }

  // taxable = net ÷ (1 + rate/100). The rate is a percent at RATE_SCALE, so
  // one hundred percent is 100 × RATE_SCALE — dividing by RATE_SCALE alone
  // makes the denominator a hundred times too small and the taxable value a
  // hundredth of what it should be.
  const hundredPercent = 100 * RATE_SCALE;
  const totalRate = totalRateOf(line);
  return roundHalfAwayFromZero(
    (net * hundredPercent) / (hundredPercent + totalRate),
  );
}

/** Whether this treatment produces tax rows at all. ZeroRated does, at rate 0. */
export function chargesTax(line: DocumentLine): boolean {
  return line.taxTreatment === 'Taxable' || line.taxTreatment === 'ZeroRated';
}

function totalRateOf(line: DocumentLine): number {
  return line.taxes.reduce((sum, tax) => sum + tax.rate, 0);
}

/**
 * Recompute a line in place and return it.
 *
 * **Rounds per component, then sums.** Not sum-then-round: GSTR-1 reconciles
 * per line and per rate, so the component is the rounded unit.
 */
export function recalculate(
  line: DocumentLine,
  context: DocumentLineContext,
): DocumentLine {
  const gross = grossOf(line);
  const discount = discountOf(line, gross);
  const taxable = taxableOf(line, context);

  const taxes: DocumentLineTax[] = chargesTax(line)
    ? line.taxes.map((tax) => ({
        ...tax,
        taxableAmount: taxable,
        amount: roundHalfAwayFromZero((taxable * tax.rate) / (100 * RATE_SCALE)),
      }))
    : [];

  const taxAmount = taxes.reduce((sum, tax) => sum + tax.amount, 0);

  return {
    ...line,
    grossAmount: gross,
    discountAmount: discount,
    taxableAmount: taxable,
    taxes,
    taxAmount,
    lineTotal: taxable + taxAmount,
    baseQuantity: roundHalfAwayFromZero(
      (line.quantity * line.conversionFactor) / QTY_SCALE,
    ),
  };
}

/**
 * The components a line should carry, given where the supply is going.
 *
 * Intra-state splits the rate in half across CGST and SGST; inter-state puts
 * all of it on IGST. Cess, when present, rides on top of either.
 */
export function componentsFor(
  isInterState: boolean,
): ReadonlyArray<'Cgst' | 'Sgst' | 'Igst'> {
  return isInterState ? ['Igst'] : ['Cgst', 'Sgst'];
}

/** Document totals, summed from already-rounded lines. */
export function totalsOf(lines: readonly DocumentLine[]) {
  const sum = (pick: (line: DocumentLine) => number) =>
    lines.reduce((total, line) => total + pick(line), 0);

  const byComponent = (component: string) =>
    lines.reduce(
      (total, line) =>
        total +
        line.taxes
          .filter((tax) => tax.component === component)
          .reduce((componentTotal, tax) => componentTotal + tax.amount, 0),
      0,
    );

  const taxable = sum((line) => line.taxableAmount);
  const cgst = byComponent('Cgst');
  const sgst = byComponent('Sgst');
  const igst = byComponent('Igst');
  const cess = byComponent('Cess');

  return {
    subTotal: sum((line) => line.grossAmount),
    discountAmount: sum((line) => line.discountAmount),
    taxableAmount: taxable,
    cgstAmount: cgst,
    sgstAmount: sgst,
    igstAmount: igst,
    cessAmount: cess,
    totalAmount: taxable + cgst + sgst + igst + cess,
  };
}
