/**
 * The date ranges the register filter bar offers, worked out as plain functions
 * so the arithmetic can be tested without a component around it.
 *
 * Dates are handled as local `YYYY-MM-DD` strings because that is what a
 * `<input type="date">` reads and writes, and because a register is filtered in
 * the branch's own day, not in UTC.
 */

export type PeriodPreset = 'this-month' | 'last-month' | 'this-financial-year' | 'custom';

export interface PeriodRange {
  readonly preset: PeriodPreset;
  /** Inclusive start, `YYYY-MM-DD`. Empty when the range is open. */
  readonly from: string;
  /** Inclusive end, `YYYY-MM-DD`. Empty when the range is open. */
  readonly to: string;
}

/** The Indian financial year runs 1 April to 31 March. */
export const FINANCIAL_YEAR_START_MONTH = 3; // zero-based: April

export function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * The range a preset means on a given day. `custom` carries no range of its
 * own — it is the person's to fill in — so it comes back open.
 */
export function rangeForPreset(preset: PeriodPreset, today: Date = new Date()): PeriodRange {
  switch (preset) {
    case 'this-month': {
      const from = new Date(today.getFullYear(), today.getMonth(), 1);
      const to = new Date(today.getFullYear(), today.getMonth() + 1, 0);
      return { preset, from: toIsoDate(from), to: toIsoDate(to) };
    }
    case 'last-month': {
      const from = new Date(today.getFullYear(), today.getMonth() - 1, 1);
      const to = new Date(today.getFullYear(), today.getMonth(), 0);
      return { preset, from: toIsoDate(from), to: toIsoDate(to) };
    }
    case 'this-financial-year': {
      // Before April the current financial year began in the previous calendar
      // year — January is in the year that started last April.
      const startYear =
        today.getMonth() >= FINANCIAL_YEAR_START_MONTH
          ? today.getFullYear()
          : today.getFullYear() - 1;
      const from = new Date(startYear, FINANCIAL_YEAR_START_MONTH, 1);
      const to = new Date(startYear + 1, FINANCIAL_YEAR_START_MONTH, 0);
      return { preset, from: toIsoDate(from), to: toIsoDate(to) };
    }
    case 'custom':
    default:
      return { preset: 'custom', from: '', to: '' };
  }
}

/** Whether a row's date falls inside the range. An open end does not exclude. */
export function isWithin(range: PeriodRange, isoDate: string): boolean {
  if (!isoDate) return false;
  const day = isoDate.slice(0, 10);
  if (range.from && day < range.from) return false;
  if (range.to && day > range.to) return false;
  return true;
}
