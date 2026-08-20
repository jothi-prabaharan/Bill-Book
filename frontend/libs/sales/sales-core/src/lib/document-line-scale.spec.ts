import { describe, expect, it } from 'vitest';
import { grossOf, recalculate } from '@bill-book/ui-components';
import {
  ApiDocumentLine,
  blankGridLine,
  toApiLine,
  toGridLine,
  toPaise,
  toRupees,
} from './document-line-scale';

/**
 * The scale boundary, tested on its own because its failure mode is silent.
 *
 * An unconverted line does not throw — it computes, and computes a total of
 * zero, so the document looks empty rather than wrong. Nothing about the screen
 * says which it is.
 */
describe('document line scale', () => {
  const apiLine: ApiDocumentLine = {
    itemId: 7,
    itemCode: 'ITM7',
    itemName: 'Widget',
    hsnSacCode: '7113',
    description: 'Widget',
    quantity: 10,
    conversionFactor: 1,
    unitPrice: 100,
    taxTreatment: 'Taxable',
    taxGroupId: 1,
    lineType: 'Stock',
  };

  it('converts rupees to whole paise', () => {
    expect(toPaise(100)).toBe(10_000);
    expect(toPaise(0.01)).toBe(1);
    // 19.99 × 100 is 1998.9999999999998 in floating point.
    expect(toPaise(19.99)).toBe(1999);
    expect(toRupees(10_000)).toBe(100);
  });

  it('scales a line into the units the grid actually uses', () => {
    const line = toGridLine(apiLine, 1);

    expect(line.quantity).toBe(10_000_000);
    expect(line.conversionFactor).toBe(1_000_000);
    expect(line.baseQuantity).toBe(10_000_000);
    expect(line.unitPrice).toBe(10_000);
  });

  it('makes the grid arrive at the right gross, which unconverted input does not', () => {
    const converted = toGridLine(apiLine, 1);

    // 10 × ₹100 is ₹1,000, which is 100,000 paise.
    expect(grossOf(converted)).toBe(100_000);

    // The same line handed over unconverted, which is what every sales form was
    // doing: 10 × 100 / 1_000_000 rounds to nothing at all. The screen shows a
    // gross of zero and a total to match, and looks merely empty.
    const unconverted = { ...converted, quantity: 10, unitPrice: 100 };
    expect(grossOf(unconverted)).toBe(0);
  });

  it('recalculates to a real total once converted', () => {
    const line = recalculate(toGridLine(apiLine, 1), {
      isInterState: false,
      allowFreeTextLines: true,
      discountBeforeTax: true,
      discountLevel: 'Line',
      readonly: false,
      currencyDecimals: 2,
    });

    // No tax rows on the line, so the total is the taxable value: ₹1,000.
    expect(line.taxableAmount).toBe(100_000);
    expect(line.lineTotal).toBe(100_000);
  });

  it('round-trips without changing what the line says', () => {
    const back = toApiLine(toGridLine(apiLine, 1));

    expect(back.quantity).toBe(10);
    expect(back.conversionFactor).toBe(1);
    expect(back.unitPrice).toBe(100);
    expect(back.taxGroupId).toBe(1);
    expect(back.taxTreatment).toBe('Taxable');
  });

  it('sends no computed amount back to the server', () => {
    const back = toApiLine(toGridLine(apiLine, 1)) as Record<string, unknown>;

    // Every figure is recomputed server-side against the rates in force on the
    // document's date. Sending them would put two answers on the wire.
    expect(back['grossAmount']).toBeUndefined();
    expect(back['taxableAmount']).toBeUndefined();
    expect(back['taxAmount']).toBeUndefined();
    expect(back['lineTotal']).toBeUndefined();
  });

  it('leaves computed amounts at zero on the way in', () => {
    const line = toGridLine(apiLine, 1);

    // The grid recalculates on render. Copying the server's figures across
    // would show two answers for one line until the first keystroke.
    expect(line.grossAmount).toBe(0);
    expect(line.taxAmount).toBe(0);
    expect(line.taxes).toHaveLength(0);
  });

  it('starts a blank line at one unit priced at nothing', () => {
    const line = blankGridLine(3);

    expect(line.lineNumber).toBe(3);
    expect(line.quantity).toBe(1_000_000);
    expect(line.unitPrice).toBe(0);
    expect(line.taxTreatment).toBe('Taxable');
    expect(line.lineType).toBe('Stock');
  });
});
