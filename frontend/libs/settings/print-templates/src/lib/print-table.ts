/**
 * The table the print-template editor's **Insert table** builds (TK-264).
 *
 * Pure, so the markup the editor writes into a band is the same whichever
 * band it goes into and can be checked without a browser. Every property in
 * the style strings is one the other table tools already write (`width`,
 * `border`, `padding`, `font-size`), so what Insert table makes is shaped by
 * them like any other table, and the printing service's sanitiser keeps all of
 * it.
 */

/** How big the table to insert can be. The ribbon's Rows and Cols steppers stay inside it. */
export const TABLE_LIMITS = { minRows: 1, maxRows: 20, minCols: 1, maxCols: 8 } as const;

/** The size the steppers start at: the same 2 × 2 Insert table always made. */
export const DEFAULT_TABLE_SIZE = { rows: 2, cols: 2 } as const;

function clamp(value: number, min: number, max: number): number {
  const whole = Number.isFinite(value) ? Math.round(value) : min;
  return Math.min(max, Math.max(min, whole));
}

export function clampRows(rows: number): number {
  return clamp(rows, TABLE_LIMITS.minRows, TABLE_LIMITS.maxRows);
}

export function clampCols(cols: number): number {
  return clamp(cols, TABLE_LIMITS.minCols, TABLE_LIMITS.maxCols);
}

/**
 * Equal column widths in percent, to one decimal place, adding up to exactly
 * 100: the last column takes whatever the rounding leaves, so three columns are
 * 33.3, 33.3 and 33.4 rather than a table a hair too narrow or too wide.
 */
export function equalWidths(cols: number): number[] {
  const count = clampCols(cols);
  const each = Math.round((100 / count) * 10) / 10;
  const widths = Array<number>(count).fill(each);
  widths[count - 1] = Math.round((100 - each * (count - 1)) * 10) / 10;
  return widths;
}

export interface TableSpec {
  rows: number;
  cols: number;
  /** The page's font size in pixels; 11 when it is not known. */
  fontSizePx?: number;
}

const DEFAULT_FONT_SIZE_PX = 11;

/**
 * A full-width table of empty bordered cells: `rows` rows of `cols` equal
 * columns, each cell holding a non-breaking space so it has a line to click
 * into. Merge, split, widths, borders and shading are the Table tools' to
 * change afterwards.
 */
export function buildTableHtml(spec: TableSpec): string {
  const rows = clampRows(spec.rows);
  const size =
    spec.fontSizePx !== undefined && Number.isFinite(spec.fontSizePx) && spec.fontSizePx > 0
      ? spec.fontSizePx
      : DEFAULT_FONT_SIZE_PX;

  const cell = (width: number): string =>
    `<td style="width:${width}%;border:1px solid #ccc;padding:4px 6px;font-size:${size}px">&nbsp;</td>`;
  const row = `<tr>${equalWidths(spec.cols).map(cell).join('')}</tr>`;

  return `<table style="width:100%;border-collapse:collapse;margin:6px 0"><tbody>${row.repeat(rows)}</tbody></table>`;
}

/**
 * Whether a band holds nothing a person put there: no text, no picture, no
 * table. The editor leaves a line break or a non-breaking space behind when a
 * band is emptied, and those do not count.
 */
export function isBlankHtml(html: string): boolean {
  if (/<(img|table|svg|hr)\b/i.test(html)) {
    return false;
  }

  const text = html
    .replace(/<br\s*\/?>/gi, '')
    .replace(/<[^>]*>/g, '')
    .replace(/&nbsp;/gi, '')
    .replace(/\s+/g, '');

  return text === '';
}
