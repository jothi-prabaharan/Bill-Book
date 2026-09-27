/**
 * The arithmetic behind Accounting › Budgets (TK-108): spreading a year total
 * across twelve months, evenly or by last year's shape, and the totals the
 * grid shows. Amounts are worked in paise so twelve parts always add back to
 * the total exactly.
 */

export const MONTHS = 12;

function toPaise(rupees: number): number {
  return Math.round(rupees * 100);
}

function toRupees(paise: number): number {
  return paise / 100;
}

/**
 * A year total split into twelve equal months. The paise that do not divide
 * go one each to the first months, so the months always add up to the total.
 */
export function spreadEvenly(total: number): number[] {
  const paise = Math.max(0, toPaise(total));
  const base = Math.floor(paise / MONTHS);
  const extra = paise - base * MONTHS;
  return Array.from({ length: MONTHS }, (_, i) => toRupees(base + (i < extra ? 1 : 0)));
}

/**
 * A year total split in proportion to a shape, last year's months. A shape
 * with nothing in it (no actuals last year) spreads evenly instead. The
 * rounding difference goes to the largest month, where it matters least.
 */
export function spreadByShape(total: number, shape: readonly number[]): number[] {
  const weights = Array.from({ length: MONTHS }, (_, i) => Math.max(0, shape[i] ?? 0));
  const sum = weights.reduce((a, b) => a + b, 0);
  if (sum <= 0) {
    return spreadEvenly(total);
  }

  const paise = Math.max(0, toPaise(total));
  const parts = weights.map((w) => Math.floor((paise * w) / sum));
  const short = paise - parts.reduce((a, b) => a + b, 0);
  const largest = weights.indexOf(Math.max(...weights));
  parts[largest] += short;
  return parts.map(toRupees);
}

/** A row's year total, exact to the paisa. */
export function rowTotal(amounts: readonly number[]): number {
  return toRupees(amounts.reduce((sum, a) => sum + toPaise(a || 0), 0));
}

/** Each month's total down a set of rows, and the grand total last. */
export function columnTotals(rows: readonly (readonly number[])[]): { months: number[]; total: number } {
  const months = Array.from({ length: MONTHS }, (_, i) => toRupees(rows.reduce((sum, r) => sum + toPaise(r[i] || 0), 0)));
  return { months, total: rowTotal(months) };
}

/** "Apr 2026" to "Mar 2027" for a year starting on an ISO date. */
export function monthLabels(yearStart: string): string[] {
  const [year, month] = yearStart.split('-').map(Number);
  return Array.from({ length: MONTHS }, (_, i) => {
    const date = new Date(year, month - 1 + i, 1);
    return date.toLocaleDateString('en-IN', { month: 'short', year: 'numeric' });
  });
}

/** "FY 2026-27" for a year starting on an ISO date. */
export function yearLabel(yearStart: string): string {
  const [year, month] = yearStart.split('-').map(Number);
  if (month === 1) {
    return `FY ${year}`;
  }
  return `FY ${year}-${String((year + 1) % 100).padStart(2, '0')}`;
}
