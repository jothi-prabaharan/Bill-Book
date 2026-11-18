import { describe, expect, it } from 'vitest';
import type { PrintSettings } from '@bill-book/master-core';
import { applyPaperType, PAPER_TYPES } from './paper-types';
import { normalizeSettings, settingsChanged } from './print-settings';

/** What the server sends for a template: every setting it keeps, and not `rollUnit`, which it does not. */
const fromServer: PrintSettings = {
  printerType: 'laser',
  paperSize: 'A4',
  rollWidthMm: 80,
  marginTopMm: 16,
  marginBottomMm: 16,
  marginLeftMm: 18,
  marginRightMm: 18,
  footerPos: 'inline',
  fixedFooterPos: 'inline',
  fixedFooterPref: 'inline',
  segMargins: {
    fixedHeader: { aboveMm: 0, belowMm: 2 },
    header: { aboveMm: 2, belowMm: 2 },
    details: { aboveMm: 2, belowMm: 2 },
    footer: { aboveMm: 2, belowMm: 0 },
    fixedFooter: { aboveMm: 2, belowMm: 0 },
  },
};

describe('The print-template editor settings (TK-267)', () => {
  it('PSET-01: a template opened as the server sent it is not changed, though the editor adds rollUnit', () => {
    const opened = normalizeSettings(fromServer);

    expect(opened.rollUnit).toBe('mm');
    expect(settingsChanged(opened, fromServer)).toBe(false);
  });

  it('PSET-02: a real change counts, and putting it back is no change', () => {
    const opened = normalizeSettings(fromServer);
    const edited = { ...opened, marginTopMm: 12 };

    expect(settingsChanged(edited, fromServer)).toBe(true);
    expect(settingsChanged({ ...edited, marginTopMm: 16 }, fromServer)).toBe(false);
  });

  it('PSET-03: the order the keys arrive in does not matter', () => {
    const shuffled: PrintSettings = {
      segMargins: fromServer.segMargins,
      marginRightMm: 18,
      paperSize: 'A4',
      printerType: 'laser',
      marginLeftMm: 18,
      marginBottomMm: 16,
      marginTopMm: 16,
      rollWidthMm: 80,
      fixedFooterPref: 'inline',
      fixedFooterPos: 'inline',
      footerPos: 'inline',
    };

    expect(settingsChanged(shuffled, fromServer)).toBe(false);
  });

  it('PSET-04: fills in whatever a template never stored, with the editor defaults', () => {
    const bare = normalizeSettings({});

    expect(bare.printerType).toBe('laser');
    expect(bare.paperSize).toBe('A4');
    expect(bare.rollWidthMm).toBe(80);
    expect(bare.marginTopMm).toBe(16);
    expect(bare.marginLeftMm).toBe(18);
    expect(bare.footerPos).toBe('inline');
    expect(bare.segMargins?.fixedHeader).toEqual({ aboveMm: 0, belowMm: 2 });
    expect(bare.segMargins?.footer).toEqual({ aboveMm: 2, belowMm: 0 });
  });

  it('PSET-05: normalising twice is the same as once', () => {
    const once = normalizeSettings(fromServer);

    expect(normalizeSettings(once)).toEqual(once);
  });

  it('PSET-06: changing the paper and changing it back leaves the template unchanged', () => {
    const opened = normalizeSettings(fromServer);
    const roll = PAPER_TYPES.find((t) => t.key === 'roll-80')!;
    const sheet = PAPER_TYPES.find((t) => t.key === 'A4')!;

    const changed = applyPaperType(opened, roll);
    expect(settingsChanged(changed, fromServer)).toBe(true);

    const back = applyPaperType(changed, sheet);
    expect(settingsChanged(back, fromServer)).toBe(false);
  });

  it('PSET-07: a setting the editor does not know is not part of the comparison', () => {
    expect(settingsChanged({ ...fromServer, somethingElse: 1 }, fromServer)).toBe(false);
  });
});
