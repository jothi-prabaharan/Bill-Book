import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import {
  DocumentLine,
  DocumentLineContext,
  DocumentLineTax,
  TaxComponent,
  TaxTreatment,
} from './document-line.model';
import { componentsFor, recalculate, totalsOf } from './line-math';

/**
 * The TypeScript half of the shared tax fixture.
 *
 * **The same file drives `Shared.Kernel.Tests/GstCalculatorFixtureTests.cs`.**
 * Two implementations of one sum diverge by a paisa without either looking
 * wrong, and a GST return is where that surfaces months later — so the expected
 * answers live in one place and both sides are held to them. `line-math.spec.ts`
 * beside this file still covers the arithmetic on its own terms; this one exists
 * only to prove the two languages agree.
 *
 * The fixture is written in rupees, which is how C# holds money. This side
 * multiplies by a hundred on the way in and compares in paise. **That conversion
 * is the only thing the two sides are allowed to do differently** — if a case
 * needs anything else special-cased here, the implementations have diverged and
 * the fixture is the wrong place to paper over it.
 */

interface FixtureRate {
  taxMasterId: number;
  taxGroupId: number;
  totalRate: number;
  cgstRate: number;
  sgstRate: number;
  igstRate: number;
  cessRate: number;
}

interface FixtureContext {
  isInterState: boolean;
  discountBeforeTax: boolean;
}

interface FixtureLine {
  quantity: number;
  unitPrice: number;
  discountPercent?: number | null;
  discountAmount?: number;
  isPriceInclusive: boolean;
  taxTreatment: string;
  rate: string | null;
}

interface ComponentExpectation {
  component: string;
  rate: number;
  amount: number;
}

interface LineCase {
  name: string;
  context: FixtureContext;
  line: FixtureLine;
  expected: {
    grossAmount: number;
    discountAmount: number;
    taxableAmount: number;
    taxAmount: number;
    lineTotal: number;
    components: ComponentExpectation[];
  };
}

interface DocumentCase {
  name: string;
  context: FixtureContext;
  lines: FixtureLine[];
  expected: Record<string, number>;
}

interface Fixture {
  rates: Record<string, FixtureRate>;
  lineCases: LineCase[];
  documentCases: DocumentCase[];
}

/** Quantity carries six decimals; prices and amounts are whole paise; rates four decimals. */
const QTY_SCALE = 1_000_000;
const RATE_SCALE = 10_000;

/** Rupees to paise. The fixture is authored in rupees because C# holds decimals. */
const paise = (rupees: number): number => Math.round(rupees * 100);

const fixture: Fixture = JSON.parse(
  readFileSync(
    new URL('../../../../../../../shared-fixtures/tax-fixture.json', import.meta.url),
    'utf-8',
  ),
) as Fixture;

/**
 * Builds the tax rows a line carries, the way a host page does.
 *
 * The grid takes its components already resolved — that is the seam — so the
 * split is applied here through `componentsFor`, which is the same function the
 * grid uses and is therefore itself under test.
 */
function taxRowsFor(rate: FixtureRate | null, isInterState: boolean): DocumentLineTax[] {
  if (rate === null) {
    return [];
  }

  const rateOf: Record<TaxComponent, number> = {
    Cgst: rate.cgstRate,
    Sgst: rate.sgstRate,
    Igst: rate.igstRate,
    Cess: rate.cessRate,
  };

  const row = (component: TaxComponent): DocumentLineTax => ({
    component,
    // Any non-zero id: the calculator never reads it, and the fixture is about
    // arithmetic rather than which sub-account a leg lands on.
    subAccountId: 1,
    rate: Math.round(rateOf[component] * RATE_SCALE),
    taxableAmount: 0,
    amount: 0,
  });

  const rows = componentsFor(isInterState).map(row);

  // Cess rides on top of whichever arrangement applies, and only when there is
  // one — an empty cess row on every line would put a meaningless zero into the
  // GSTR-1 cess column.
  if (rate.cessRate > 0) {
    rows.push(row('Cess'));
  }

  return rows;
}

function toLine(source: FixtureLine): DocumentLine {
  const rate = source.rate === null ? null : fixture.rates[source.rate];

  return {
    detailId: 0,
    lineNumber: 1,
    itemId: 1,
    itemLabel: null,
    hsnSacCode: null,
    description: null,
    warehouseId: null,
    quantity: Math.round(source.quantity * QTY_SCALE),
    uomId: null,
    uomLabel: null,
    conversionFactor: QTY_SCALE,
    baseQuantity: 0,
    unitPrice: paise(source.unitPrice),
    isPriceInclusive: source.isPriceInclusive,
    discountPercent: source.discountPercent ?? null,
    discountAmount: paise(source.discountAmount ?? 0),
    grossAmount: 0,
    taxableAmount: 0,
    taxTreatment: source.taxTreatment as TaxTreatment,
    taxMasterId: rate?.taxMasterId ?? null,
    taxGroupId: rate?.taxGroupId ?? null,
    taxAmount: 0,
    taxes: taxRowsFor(rate ?? null, false),
    lineType: 'Stock',
    accountId: null,
    fixedAssetCategoryId: null,
    lineTotal: 0,
    itemBatchId: null,
    lineNotes: null,
  };
}

function contextOf(source: FixtureContext): DocumentLineContext {
  return {
    isInterState: source.isInterState,
    discountBeforeTax: source.discountBeforeTax,
    allowFreeTextLines: true,
    discountLevel: 'Line',
    readonly: false,
    currencyDecimals: 2,
  };
}

/** The line with its tax rows rebuilt for the case's own intra/inter split. */
function lineFor(source: FixtureLine, context: FixtureContext): DocumentLine {
  const rate = source.rate === null ? null : fixture.rates[source.rate];
  return { ...toLine(source), taxes: taxRowsFor(rate ?? null, context.isInterState) };
}

describe('tax fixture — TypeScript agrees with C#', () => {
  for (const testCase of fixture.lineCases) {
    it(`line: ${testCase.name}`, () => {
      const got = recalculate(lineFor(testCase.line, testCase.context), contextOf(testCase.context));
      const want = testCase.expected;

      expect(got.grossAmount).toBe(paise(want.grossAmount));
      expect(got.discountAmount).toBe(paise(want.discountAmount));
      expect(got.taxableAmount).toBe(paise(want.taxableAmount));
      expect(got.taxAmount).toBe(paise(want.taxAmount));
      expect(got.lineTotal).toBe(paise(want.lineTotal));

      expect(got.taxes.map((tax) => tax.component)).toEqual(
        want.components.map((component) => component.component),
      );

      got.taxes.forEach((tax, index) => {
        expect(tax.rate).toBe(Math.round(want.components[index].rate * RATE_SCALE));
        expect(tax.amount).toBe(paise(want.components[index].amount));
      });
    });
  }

  for (const testCase of fixture.documentCases) {
    it(`document: ${testCase.name}`, () => {
      const lines = testCase.lines.map((line) =>
        recalculate(lineFor(line, testCase.context), contextOf(testCase.context)),
      );

      const got = totalsOf(lines);

      for (const [key, rupees] of Object.entries(testCase.expected)) {
        expect(got[key as keyof typeof got]).toBe(paise(rupees));
      }
    });

    it(`document total is the sum of its lines: ${testCase.name}`, () => {
      const lines = testCase.lines.map((line) =>
        recalculate(lineFor(line, testCase.context), contextOf(testCase.context)),
      );

      const got = totalsOf(lines);
      const summed = lines.reduce((total, line) => total + line.lineTotal, 0);

      expect(got.totalAmount).toBe(summed);
    });
  }
});
