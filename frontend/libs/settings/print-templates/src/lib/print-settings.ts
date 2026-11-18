import type { PrintSettings } from '@bill-book/master-core';

/**
 * A template's page settings as the editor holds them (TK-267).
 *
 * The editor fills in every setting a template did not store before it works on it, and it compares the result
 * with what it loaded to know whether anything has changed. That comparison used the stored settings as they
 * came, so it differed from the first moment for any template whose settings lacked one of the editor's own
 * (`rollUnit`, which the server does not keep): the editor called every freshly opened template changed,
 * enabled Discard and Save on it, and asked "Discard changes?" before moving anywhere, with nothing edited.
 * Both sides are normalised the same way now, so only a real change counts.
 *
 * Pure, so it can be checked without a browser.
 */

/** The settings with every one filled in, in one fixed order, whichever way they arrived. */
export function normalizeSettings(s: PrintSettings): PrintSettings {
  return {
    printerType: s.printerType ?? 'laser',
    paperSize: s.paperSize ?? 'A4',
    rollWidthMm: s.rollWidthMm ?? 80,
    rollUnit: s.rollUnit ?? 'mm',
    marginTopMm: s.marginTopMm ?? 16,
    marginBottomMm: s.marginBottomMm ?? 16,
    marginLeftMm: s.marginLeftMm ?? 18,
    marginRightMm: s.marginRightMm ?? 18,
    footerPos: s.footerPos ?? 'inline',
    fixedFooterPos: s.fixedFooterPos ?? 'inline',
    fixedFooterPref: s.fixedFooterPref ?? 'inline',
    segMargins: {
      fixedHeader: {
        aboveMm: s.segMargins?.fixedHeader?.aboveMm ?? 0,
        belowMm: s.segMargins?.fixedHeader?.belowMm ?? 2,
      },
      header: {
        aboveMm: s.segMargins?.header?.aboveMm ?? 2,
        belowMm: s.segMargins?.header?.belowMm ?? 2,
      },
      details: {
        aboveMm: s.segMargins?.details?.aboveMm ?? 2,
        belowMm: s.segMargins?.details?.belowMm ?? 2,
      },
      footer: {
        aboveMm: s.segMargins?.footer?.aboveMm ?? 2,
        belowMm: s.segMargins?.footer?.belowMm ?? 0,
      },
      fixedFooter: {
        aboveMm: s.segMargins?.fixedFooter?.aboveMm ?? 2,
        belowMm: s.segMargins?.fixedFooter?.belowMm ?? 0,
      },
    },
  };
}

/** Whether the settings being edited differ from the ones the template was opened with. */
export function settingsChanged(draft: PrintSettings, saved: PrintSettings): boolean {
  return JSON.stringify(normalizeSettings(draft)) !== JSON.stringify(normalizeSettings(saved));
}
