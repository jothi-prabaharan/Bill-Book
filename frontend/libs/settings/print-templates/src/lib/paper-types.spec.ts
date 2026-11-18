import { describe, expect, it } from 'vitest';
import type { PrintSettings } from '@bill-book/master-core';
import {
  PAPER_TYPES,
  SHEET_SIZES_MM,
  applyPaperType,
  currentMargins,
  paperReadout,
  paperSizeNameFor,
  paperTypeKey,
  paperTypesFor,
  selectedPaperType,
} from './paper-types';

const type = (key: string) => PAPER_TYPES.find((t) => t.key === key)!;

const a4: PrintSettings = {
  printerType: 'laser',
  paperSize: 'A4',
  marginTopMm: 16,
  marginBottomMm: 16,
  marginLeftMm: 18,
  marginRightMm: 18,
};

const roll80: PrintSettings = {
  printerType: 'thermal',
  paperSize: 'A4',
  rollWidthMm: 80,
  marginTopMm: 3,
  marginBottomMm: 3,
  marginLeftMm: 3,
  marginRightMm: 3,
};

describe('The print-template editor paper types (TK-267)', () => {
  it('PPT-01: lists the sheets and then the rolls, in the order the bar shows them', () => {
    expect(PAPER_TYPES.map((t) => t.key)).toEqual([
      'A4',
      'A5',
      'Letter',
      'Legal',
      'roll-58',
      'roll-80',
      'roll-104',
      'roll-112',
    ]);
    expect(PAPER_TYPES.map((t) => t.label).slice(0, 4)).toEqual(['A4', 'A5', 'Letter', 'Legal']);
    expect(type('roll-80').label).toBe('Thermal 3 inch');
    expect(type('roll-104').label).toBe('Thermal 4 inch');
    expect(PAPER_TYPES.filter((t) => t.kind === 'roll').every((t) => t.rollWidthMm !== undefined)).toBe(true);
  });

  it('PPT-02: reads the entry a template is on from its three stored settings', () => {
    expect(paperTypeKey(a4)).toBe('A4');
    expect(paperTypeKey({ printerType: 'laser', paperSize: 'Legal' })).toBe('Legal');
    expect(paperTypeKey(roll80)).toBe('roll-80');
    expect(paperTypeKey({ printerType: 'thermal', rollWidthMm: 104 })).toBe('roll-104');

    // A template with nothing stored is an A4 sheet; a roll with no width is the 80 mm one.
    expect(paperTypeKey({})).toBe('A4');
    expect(paperTypeKey({ printerType: 'thermal' })).toBe('roll-80');
    expect(paperTypeKey({ printerType: 'thermal', rollWidthMm: Number.NaN })).toBe('roll-80');
    expect(paperTypeKey({ printerType: 'thermal', rollWidthMm: 0 })).toBe('roll-80');

    // A sheet name this editor does not know reads as A4, as the page already did.
    expect(paperTypeKey({ printerType: 'laser', paperSize: 'A3' as never })).toBe('A4');
  });

  it('PPT-03: puts a 104 mm roll on Thermal 4 inch, where it showed a blank Roll width box', () => {
    const stored: PrintSettings = { printerType: 'thermal', rollWidthMm: 104 };

    expect(selectedPaperType(stored).label).toBe('Thermal 4 inch');
    expect(paperTypesFor(stored)).toHaveLength(PAPER_TYPES.length);
  });

  it('PPT-04: gives a roll the list does not have a button of its own, at the end, and selects it', () => {
    const stored: PrintSettings = { printerType: 'thermal', rollWidthMm: 100 };
    const buttons = paperTypesFor(stored);

    expect(buttons).toHaveLength(PAPER_TYPES.length + 1);
    const own = buttons[buttons.length - 1];
    expect(own.key).toBe('roll-100');
    expect(own.label).toBe('Roll 100 mm');
    expect(selectedPaperType(stored).key).toBe(own.key);
    // A width with a decimal keeps it, so a 76.2 mm roll is not reported as 76.
    expect(selectedPaperType({ printerType: 'thermal', rollWidthMm: 76.2 }).label).toBe('Roll 76.2 mm');
  });

  it('PPT-05: choosing a roll makes the template a thermal one of that width and leaves the sheet alone', () => {
    const next = applyPaperType(a4, type('roll-80'));

    expect(next.printerType).toBe('thermal');
    expect(next.rollWidthMm).toBe(80);
    expect(next.paperSize).toBe('A4');
  });

  it('PPT-06: choosing a sheet makes the template a laser one of that size and leaves the roll width alone', () => {
    const next = applyPaperType(roll80, type('Letter'));

    expect(next.printerType).toBe('laser');
    expect(next.paperSize).toBe('Letter');
    expect(next.rollWidthMm).toBe(80);
  });

  it('PPT-07: moves margins nobody changed to the new type, so a roll is not left with an A4 sheet margins', () => {
    const thermal = applyPaperType(a4, type('roll-80'));
    expect(currentMargins(thermal)).toEqual({ top: 3, bottom: 3, left: 3, right: 3 });

    const wide = applyPaperType(thermal, type('roll-104'));
    expect(currentMargins(wide)).toEqual({ top: 4, bottom: 4, left: 4, right: 4 });

    const a5 = applyPaperType(wide, type('A5'));
    expect(currentMargins(a5)).toEqual({ top: 10, bottom: 10, left: 10, right: 10 });

    const back = applyPaperType(a5, type('A4'));
    expect(currentMargins(back)).toEqual({ top: 16, bottom: 16, left: 18, right: 18 });
  });

  it('PPT-08: keeps margins somebody typed, however the paper changes', () => {
    const typed: PrintSettings = { ...a4, marginTopMm: 12, marginLeftMm: 20 };
    const next = applyPaperType(typed, type('roll-80'));

    expect(next.marginTopMm).toBe(12);
    expect(next.marginBottomMm).toBe(16);
    expect(next.marginLeftMm).toBe(20);
    expect(next.marginRightMm).toBe(18);
  });

  it('PPT-09: treats margins that were never stored as the A4 ones, so the first change writes the new ones', () => {
    const next = applyPaperType({ printerType: 'laser', paperSize: 'A4' }, type('roll-58'));

    expect(currentMargins(next)).toEqual({ top: 3, bottom: 3, left: 3, right: 3 });
  });

  it('PPT-10: leaves the settings it was given as they were, and every other setting with them', () => {
    const source: PrintSettings = { ...a4, footerPos: 'bottom', rollUnit: 'in' };
    const copy = JSON.stringify(source);

    const next = applyPaperType(source, type('roll-104'));

    expect(JSON.stringify(source)).toBe(copy);
    expect(next).not.toBe(source);
    expect(next.footerPos).toBe('bottom');
    expect(next.rollUnit).toBe('in');
  });

  it('PPT-11: choosing the entry a template is already on changes nothing', () => {
    expect(applyPaperType(a4, type('A4'))).toEqual(a4);
    expect(applyPaperType(roll80, type('roll-80'))).toEqual(roll80);
  });

  it('PPT-12: names the five sizes the backend seeds for Save As, and nothing else', () => {
    expect(paperSizeNameFor(a4)).toBe('A4');
    expect(paperSizeNameFor({ printerType: 'laser', paperSize: 'A5' })).toBe('A5');
    expect(paperSizeNameFor({ printerType: 'laser', paperSize: 'Letter' })).toBe('Letter');
    expect(paperSizeNameFor(roll80)).toBe('Thermal3Inch');
    expect(paperSizeNameFor({ printerType: 'thermal', rollWidthMm: 104 })).toBe('Thermal4Inch');

    expect(paperSizeNameFor({ printerType: 'laser', paperSize: 'Legal' })).toBeNull();
    expect(paperSizeNameFor({ printerType: 'thermal', rollWidthMm: 58 })).toBeNull();
    expect(paperSizeNameFor({ printerType: 'thermal', rollWidthMm: 112 })).toBeNull();
  });

  it('PPT-13: says in words what the chosen paper is', () => {
    expect(paperReadout(type('A4'))).toBe('A4 sheet, 210 × 297 mm');
    expect(paperReadout(type('Legal'))).toBe('Legal sheet, 216 × 356 mm');
    expect(paperReadout(type('roll-80'))).toBe('80 mm wide roll, printed on a continuous length');
  });

  it('PPT-14: the sheet sizes are the ones the editor has always drawn', () => {
    expect(SHEET_SIZES_MM).toEqual({ A4: [210, 297], A5: [148, 210], Letter: [216, 279], Legal: [216, 356] });
  });
});
