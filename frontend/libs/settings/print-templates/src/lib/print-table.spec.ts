import { describe, expect, it } from 'vitest';
import {
  DEFAULT_TABLE_SIZE,
  TABLE_LIMITS,
  buildTableHtml,
  clampCols,
  clampRows,
  equalWidths,
  isBlankHtml,
} from './print-table';

/** The table the markup describes, parsed the way the editor's `insertHTML` would. */
function parse(html: string): HTMLTableElement {
  const holder = document.createElement('div');
  holder.innerHTML = html;
  return holder.querySelector('table') as HTMLTableElement;
}

describe('The print-template editor tables (TK-264)', () => {
  it('PTBL-01: keeps the size inside the limits, and reads anything that is not a number as the smallest', () => {
    expect(clampRows(0)).toBe(TABLE_LIMITS.minRows);
    expect(clampRows(3)).toBe(3);
    expect(clampRows(99)).toBe(TABLE_LIMITS.maxRows);
    expect(clampRows(2.6)).toBe(3);
    expect(clampRows(Number.NaN)).toBe(TABLE_LIMITS.minRows);

    expect(clampCols(-4)).toBe(TABLE_LIMITS.minCols);
    expect(clampCols(5)).toBe(5);
    expect(clampCols(40)).toBe(TABLE_LIMITS.maxCols);
    expect(clampCols(Number.POSITIVE_INFINITY)).toBe(TABLE_LIMITS.minCols);
  });

  it('PTBL-02: starts at the 2 x 2 that Insert table always made', () => {
    expect(DEFAULT_TABLE_SIZE).toEqual({ rows: 2, cols: 2 });
  });

  it('PTBL-03: equal column widths add up to exactly 100, with the last column taking the rounding', () => {
    expect(equalWidths(1)).toEqual([100]);
    expect(equalWidths(2)).toEqual([50, 50]);
    expect(equalWidths(3)).toEqual([33.3, 33.3, 33.4]);
    expect(equalWidths(4)).toEqual([25, 25, 25, 25]);

    for (let cols = TABLE_LIMITS.minCols; cols <= TABLE_LIMITS.maxCols; cols += 1) {
      const widths = equalWidths(cols);
      expect(widths).toHaveLength(cols);
      expect(widths.reduce((sum, w) => sum + w, 0)).toBeCloseTo(100, 6);
    }
  });

  it('PTBL-04: builds a full-width table of the size asked for, every cell empty and bordered', () => {
    const table = parse(buildTableHtml({ rows: 3, cols: 4 }));

    expect(table.style.width).toBe('100%');
    expect(table.rows).toHaveLength(3);
    for (const row of Array.from(table.rows)) {
      expect(row.cells).toHaveLength(4);
      for (const cell of Array.from(row.cells)) {
        expect(cell.style.width).toBe('25%');
        expect(cell.style.border).toContain('1px solid');
        expect(cell.textContent?.trim()).toBe('');
      }
    }
  });

  it('PTBL-05: a size outside the limits is brought inside them, not built as asked', () => {
    const table = parse(buildTableHtml({ rows: 500, cols: 500 }));

    expect(table.rows).toHaveLength(TABLE_LIMITS.maxRows);
    expect(table.rows[0].cells).toHaveLength(TABLE_LIMITS.maxCols);
  });

  it('PTBL-06: sets the cells in the page font size, and in 11 when there is none to follow', () => {
    expect(parse(buildTableHtml({ rows: 1, cols: 1, fontSizePx: 14 })).rows[0].cells[0].style.fontSize).toBe('14px');
    expect(parse(buildTableHtml({ rows: 1, cols: 1 })).rows[0].cells[0].style.fontSize).toBe('11px');
    expect(parse(buildTableHtml({ rows: 1, cols: 1, fontSizePx: 0 })).rows[0].cells[0].style.fontSize).toBe('11px');
  });

  it('PTBL-07: writes only the properties the other table tools write, so the printing service keeps all of them', () => {
    const html = buildTableHtml({ rows: 2, cols: 3, fontSizePx: 11 });
    const properties = new Set(
      Array.from(html.matchAll(/style="([^"]*)"/g)).flatMap((match) =>
        match[1]
          .split(';')
          .filter(Boolean)
          .map((declaration) => declaration.split(':')[0].trim()),
      ),
    );

    expect([...properties].sort()).toEqual(['border', 'border-collapse', 'font-size', 'margin', 'padding', 'width']);
  });

  it('PTBL-08: a band is blank until something is put in it', () => {
    expect(isBlankHtml('')).toBe(true);
    expect(isBlankHtml('<br>')).toBe(true);
    expect(isBlankHtml('&nbsp;')).toBe(true);
    expect(isBlankHtml('<div><br></div>')).toBe(true);
    expect(isBlankHtml('  <p> &nbsp; </p> ')).toBe(true);

    expect(isBlankHtml('Terms apply')).toBe(false);
    expect(isBlankHtml('<div>{{Document.No}}</div>')).toBe(false);
    expect(isBlankHtml(buildTableHtml({ rows: 1, cols: 1 }))).toBe(false);
    expect(isBlankHtml('<div><img src="https://example.com/logo.png"></div>')).toBe(false);
    expect(isBlankHtml('<hr>')).toBe(false);
  });
});
