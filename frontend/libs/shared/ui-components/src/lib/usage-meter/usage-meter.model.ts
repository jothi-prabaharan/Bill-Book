/** How close a count is to its limit. */
export type MeterTone = 'ok' | 'warning' | 'danger';

const COUNT = new Intl.NumberFormat('en-IN');

/**
 * A warning once four fifths of the limit is used, and a danger once all of it
 * is. A count with no limit is never either: there is nothing to run out of.
 */
export function meterTone(used: number, limit: number | null): MeterTone {
  if (limit === null) {
    return 'ok';
  }

  if (used >= limit) {
    return 'danger';
  }

  return used * 5 >= limit * 4 ? 'warning' : 'ok';
}

/** "3 of 5", or "12 used · Unlimited" when nothing limits it. */
export function meterFigures(used: number, limit: number | null): string {
  return limit === null
    ? `${COUNT.format(used)} used · Unlimited`
    : `${COUNT.format(used)} of ${COUNT.format(limit)}`;
}

/** The bar's full width. A `<progress>` with a maximum of zero is invalid, so a limit of none at all is drawn as one that is full. */
export function meterMax(limit: number | null): number {
  return limit === null ? 1 : Math.max(limit, 1);
}
