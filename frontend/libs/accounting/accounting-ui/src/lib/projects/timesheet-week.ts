/**
 * The arithmetic behind Accounting › Timesheet (TK-106): which week a date is
 * in, and an entry list folded into a project × task by day grid. Pure, so the
 * page stays a thin shell over it.
 */

/** An entry as `GET api/projects/time` lists it. */
export interface TimeEntryRow {
  timeEntryId: number;
  projectId: number;
  projectCode: string;
  projectName: string;
  projectTaskId: number;
  taskName: string;
  userId: string;
  workDate: string;
  hours: number;
  notes: string | null;
  isBillable: boolean;
  billingStatus: 'Unbilled' | 'Billed' | 'WrittenOff';
}

/** One row of the week grid: a project's task, and its hours on each day. */
export interface WeekRow {
  key: string;
  label: string;
  hours: number[];
  total: number;
}

/** An ISO `yyyy-MM-dd` for a local date, never through UTC — midnight in India is yesterday in UTC. */
export function isoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** A `yyyy-MM-dd` read as a local date. */
export function parseIsoDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** The Monday of the week holding `date`. */
export function weekStart(date: Date): Date {
  const start = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const offset = (start.getDay() + 6) % 7;
  start.setDate(start.getDate() - offset);
  return start;
}

/** The seven ISO dates of the week starting `monday`. */
export function weekDays(monday: Date): string[] {
  return Array.from({ length: 7 }, (_, i) => {
    const day = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + i);
    return isoDate(day);
  });
}

/** Hours rounded to two decimals, the column's scale, so 0.1 + 0.2 reads 0.3. */
export function roundHours(hours: number): number {
  return Math.round(hours * 100) / 100;
}

/**
 * The entries folded into one row per project task, with its hours on each of
 * the week's days. Rows are ordered by project code, then task name.
 */
export function weekGrid(entries: readonly TimeEntryRow[], days: readonly string[]): WeekRow[] {
  const rows = new Map<string, WeekRow & { sort: string }>();
  for (const entry of entries) {
    const at = days.indexOf(entry.workDate);
    if (at < 0) {
      continue;
    }

    const key = `${entry.projectId}:${entry.projectTaskId}`;
    let row = rows.get(key);
    if (!row) {
      row = {
        key,
        label: `${entry.projectCode} · ${entry.taskName}`,
        hours: days.map(() => 0),
        total: 0,
        sort: `${entry.projectCode}\u0000${entry.taskName}`,
      };
      rows.set(key, row);
    }

    row.hours[at] = roundHours(row.hours[at] + entry.hours);
    row.total = roundHours(row.total + entry.hours);
  }

  return [...rows.values()]
    .sort((a, b) => a.sort.localeCompare(b.sort))
    .map(({ key, label, hours, total }) => ({ key, label, hours, total }));
}

/** Each day's total across every row. */
export function dayTotals(rows: readonly WeekRow[], dayCount = 7): number[] {
  return Array.from({ length: dayCount }, (_, i) => roundHours(rows.reduce((sum, r) => sum + r.hours[i], 0)));
}

/**
 * Elapsed timer milliseconds as hours for an entry: rounded up to the next
 * quarter hour, at least a quarter, and never past a day.
 */
export function timerHours(elapsedMs: number): number {
  const quarters = Math.max(1, Math.ceil(elapsedMs / (15 * 60 * 1000)));
  return Math.min(24, quarters / 4);
}
