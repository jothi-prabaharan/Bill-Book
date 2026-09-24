/**
 * Calendar arithmetic on ISO `yyyy-MM-dd` strings, for `bb-branch-date-input`
 * (TK-23).
 *
 * Every step goes through `Date.UTC` and the `getUTC*` readers, never through
 * a local `Date` parsed from a string, so no timezone can move a day.
 */

export interface CalendarDay {
  iso: string;
  day: number;
  inMonth: boolean;
}

export function toIso(year: number, month: number, day: number): string {
  return `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

export function isoParts(iso: string): { year: number; month: number; day: number } {
  const [year, month, day] = iso.split('-').map(Number);
  return { year, month, day };
}

function fromUtc(ms: number): string {
  const date = new Date(ms);
  return toIso(date.getUTCFullYear(), date.getUTCMonth() + 1, date.getUTCDate());
}

export function addDays(iso: string, days: number): string {
  const { year, month, day } = isoParts(iso);
  return fromUtc(Date.UTC(year, month - 1, day + days));
}

/** The same day in another month, pulled back to the month's last day when it has fewer. */
export function addMonths(iso: string, months: number): string {
  const { year, month, day } = isoParts(iso);
  const first = new Date(Date.UTC(year, month - 1 + months, 1));
  const y = first.getUTCFullYear();
  const m = first.getUTCMonth() + 1;
  const last = new Date(Date.UTC(y, m, 0)).getUTCDate();
  return toIso(y, m, Math.min(day, last));
}

/** Monday 0 … Sunday 6. */
export function weekdayIndex(iso: string): number {
  const { year, month, day } = isoParts(iso);
  return (new Date(Date.UTC(year, month - 1, day)).getUTCDay() + 6) % 7;
}

/**
 * The weeks of the month holding `iso`, Monday first, padded with the days of
 * the months either side so every week has seven.
 */
export function monthGrid(iso: string): CalendarDay[][] {
  const { year, month } = isoParts(iso);
  const first = toIso(year, month, 1);
  let cursor = addDays(first, -weekdayIndex(first));
  const weeks: CalendarDay[][] = [];

  do {
    const week: CalendarDay[] = [];
    for (let i = 0; i < 7; i++) {
      const parts = isoParts(cursor);
      week.push({ iso: cursor, day: parts.day, inMonth: parts.month === month });
      cursor = addDays(cursor, 1);
    }
    weeks.push(week);
  } while (isoParts(cursor).month === month);

  return weeks;
}

/** Today on the viewer's own calendar, read through the local accessors. */
export function todayIso(now: Date = new Date()): string {
  return toIso(now.getFullYear(), now.getMonth() + 1, now.getDate());
}

/** ISO strings compare correctly as text, which is the point of the format. */
export function outOfRange(iso: string, min: string | null, max: string | null): boolean {
  return (!!min && iso < min) || (!!max && iso > max);
}
