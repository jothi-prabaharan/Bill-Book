import type { PaperSize, PrintSettings } from '@bill-book/master-core';

/**
 * The paper types the print-template editor offers, as one list (TK-267).
 *
 * The editor used to ask for this in three places: a Printer type toggle, then a Paper size or a Roll width
 * depending on the toggle. A template stores exactly those three values (`printerType`, `paperSize` and
 * `rollWidthMm`), so the single list below is only a different way to choose them: nothing about the stored
 * shape changes, and a template saved before this file existed opens on the entry that matches it.
 *
 * Pure, so the mapping and the margin rule are the same wherever they are used and can be checked without a
 * browser.
 */

/** A sheet's width and height in millimetres. */
export const SHEET_SIZES_MM: Record<PaperSize, [number, number]> = {
  A4: [210, 297],
  A5: [148, 210],
  Letter: [216, 279],
  Legal: [216, 356],
};

export interface PageMargins {
  top: number;
  bottom: number;
  left: number;
  right: number;
}

/** A cut sheet has a height; a thermal roll is as wide as its paper and as long as its content. */
export type PaperKind = 'sheet' | 'roll';

export interface PaperType {
  /** Stable key: the sheet's name (`A4`), or `roll-<mm>` for a roll. */
  key: string;
  /** What the bar's button says. */
  label: string;
  /** The line under it: a sheet's size, or what a roll is. */
  detail: string;
  kind: PaperKind;
  /** Sheets only. */
  paperSize?: PaperSize;
  /** Rolls only, in millimetres. */
  rollWidthMm?: number;
  /** The margins a template of this type starts with, which is what the backend seeds. */
  margins: PageMargins;
}

const SHEET_MARGINS: PageMargins = { top: 16, bottom: 16, left: 18, right: 18 };
const A5_MARGINS: PageMargins = { top: 10, bottom: 10, left: 10, right: 10 };

/** The margins the backend seeds on a roll: 3 mm up to an 80 mm roll, 4 mm on the wider ones. */
function rollMargins(widthMm: number): PageMargins {
  const each = widthMm <= 80 ? 3 : 4;
  return { top: each, bottom: each, left: each, right: each };
}

function round1(value: number): number {
  return Math.round(value * 10) / 10;
}

/** A roll's width, or the 80 mm every template starts with when there is none to read. */
function rollWidth(value: number | undefined): number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? round1(value) : 80;
}

function rollKey(widthMm: number): string {
  return `roll-${round1(widthMm)}`;
}

function sheet(size: PaperSize, margins: PageMargins): PaperType {
  const [width, height] = SHEET_SIZES_MM[size];
  return { key: size, label: size, detail: `${width} × ${height} mm`, kind: 'sheet', paperSize: size, margins };
}

function namedRoll(widthMm: number, label: string): PaperType {
  return {
    key: rollKey(widthMm),
    label,
    detail: `${widthMm} mm roll`,
    kind: 'roll',
    rollWidthMm: widthMm,
    margins: rollMargins(widthMm),
  };
}

/** A roll that is not one of the named ones: the 112 mm roll, or a width typed into an older template. */
function plainRoll(widthMm: number): PaperType {
  return {
    key: rollKey(widthMm),
    label: `Roll ${widthMm} mm`,
    detail: 'continuous',
    kind: 'roll',
    rollWidthMm: widthMm,
    margins: rollMargins(widthMm),
  };
}

/**
 * The bar, in order. The first four are the sheets; Legal and the 58 and 112 mm rolls are kept because
 * templates already use them. A 3 inch roll is the 80 mm one and a 4 inch roll the 104 mm one, which is what
 * the backend seeds under those names.
 */
export const PAPER_TYPES: readonly PaperType[] = [
  sheet('A4', SHEET_MARGINS),
  sheet('A5', A5_MARGINS),
  sheet('Letter', SHEET_MARGINS),
  sheet('Legal', SHEET_MARGINS),
  namedRoll(58, 'Thermal 2 inch'),
  namedRoll(80, 'Thermal 3 inch'),
  namedRoll(104, 'Thermal 4 inch'),
  plainRoll(112),
];

type PaperSettings = Pick<PrintSettings, 'printerType' | 'paperSize' | 'rollWidthMm'>;

/** The key of the entry a template's settings are on. A sheet name this file does not know reads as A4. */
export function paperTypeKey(settings: PaperSettings): string {
  if (settings.printerType === 'thermal') {
    return rollKey(rollWidth(settings.rollWidthMm));
  }
  const size = settings.paperSize;
  return size !== undefined && size in SHEET_SIZES_MM ? size : 'A4';
}

/** The entry a template's settings are on, which is a roll of its own when its width is not on the bar. */
export function selectedPaperType(settings: PaperSettings): PaperType {
  const key = paperTypeKey(settings);
  return PAPER_TYPES.find((type) => type.key === key) ?? plainRoll(rollWidth(settings.rollWidthMm));
}

/**
 * Every button the bar shows for a template: the list above, and one more at the end when the template's roll
 * is a width the list does not have. It used to show a blank Roll width box for a 104 mm roll, which the
 * backend seeds as the 4 inch one; now it is on Thermal 4 inch, and any other width is on a button of its own.
 */
export function paperTypesFor(settings: PaperSettings): PaperType[] {
  const selected = selectedPaperType(settings);
  return PAPER_TYPES.some((type) => type.key === selected.key) ? [...PAPER_TYPES] : [...PAPER_TYPES, selected];
}

/** The margins a template has, with the editor's own fallbacks for any that were never stored. */
export function currentMargins(settings: PrintSettings): PageMargins {
  return {
    top: settings.marginTopMm ?? SHEET_MARGINS.top,
    bottom: settings.marginBottomMm ?? SHEET_MARGINS.bottom,
    left: settings.marginLeftMm ?? SHEET_MARGINS.left,
    right: settings.marginRightMm ?? SHEET_MARGINS.right,
  };
}

export function marginsEqual(a: PageMargins, b: PageMargins): boolean {
  return a.top === b.top && a.bottom === b.bottom && a.left === b.left && a.right === b.right;
}

/**
 * The settings after choosing `next`: its sheet or roll, and its margins, but only when the margins are still
 * the ones the old type starts with. Margins somebody typed stay, so changing the paper never undoes a
 * decision; margins nobody touched follow the paper, so an 80 mm roll is not left with the 18 mm margins of an
 * A4 sheet, which left 44 mm to print on. Returns a new object and leaves the one it was given alone.
 */
export function applyPaperType(settings: PrintSettings, next: PaperType): PrintSettings {
  const from = selectedPaperType(settings);
  const untouched = marginsEqual(currentMargins(settings), from.margins);

  const result: PrintSettings = { ...settings };
  if (next.kind === 'roll') {
    result.printerType = 'thermal';
    result.rollWidthMm = next.rollWidthMm;
  } else {
    result.printerType = 'laser';
    result.paperSize = next.paperSize;
  }

  if (untouched) {
    result.marginTopMm = next.margins.top;
    result.marginBottomMm = next.margins.bottom;
    result.marginLeftMm = next.margins.left;
    result.marginRightMm = next.margins.right;
  }

  return result;
}

/**
 * The name the Save As box files a template under for these settings: the five the backend seeds. Null for
 * Legal, the 58 and 112 mm rolls and any other roll, which have none, so the box keeps whatever the template
 * already had.
 */
export function paperSizeNameFor(settings: PaperSettings): string | null {
  switch (paperTypeKey(settings)) {
    case 'A4':
    case 'A5':
    case 'Letter':
      return paperTypeKey(settings);
    case 'roll-80':
      return 'Thermal3Inch';
    case 'roll-104':
      return 'Thermal4Inch';
    default:
      return null;
  }
}

/** The line under the bar: what the chosen paper is, in words. */
export function paperReadout(type: PaperType): string {
  if (type.kind === 'roll') {
    return `${type.rollWidthMm} mm wide roll, printed on a continuous length`;
  }
  const [width, height] = SHEET_SIZES_MM[type.paperSize ?? 'A4'];
  return `${type.label} sheet, ${width} × ${height} mm`;
}
