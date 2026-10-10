import { FormatSettings, formatDate, formatMoney, formatNumber } from '@bill-book/currency-format';

/**
 * How a transaction register writes its dates and amounts (TK-187).
 *
 * A row's date reads as `11 Aug 2026` — a register is scanned by eye, and a
 * month name is quicker to place than a number. The branch's own pattern is
 * still what every date *field* shows, the period bar's From and To included.
 */
export const REGISTER_DATE_PATTERN = 'dd MMM yyyy';

export function registerDate(value: string | null | undefined): string {
  return formatDate(value, REGISTER_DATE_PATTERN);
}

/**
 * An amount in the branch's money format, without the paise when there are
 * none: `₹4,00,000`, but `₹3,38,983.05`. Never rounded — a figure with paise
 * keeps every one of them.
 *
 * When the document's currency is not the branch's, the branch's symbol would
 * be wrong, so the figure is written bare and the caller shows the code beside it.
 */
export function registerAmount(
  value: number | null | undefined,
  settings: FormatSettings,
  currencyCode?: string | null,
): string {
  if (value == null || !Number.isFinite(value)) return '';

  const whole = Math.abs(Math.round(value * 100) % 100) === 0;
  const decimals = whole ? 0 : settings.currencyDecimals;

  if (currencyCode && currencyCode !== settings.currencyCode) {
    return formatNumber(value, decimals, settings.currencyMask);
  }
  return formatMoney(value, { ...settings, currencyDecimals: decimals });
}

/** One status a register can be filtered to. */
export interface RegisterStatusOption {
  readonly value: string;
  readonly label: string;
}

/** A tab above a register: a status, its label and how many rows have it. */
export interface StatusTab extends RegisterStatusOption {
  readonly count?: number | null;
}

/**
 * The tabs for a register: All, then every status the document type can be in
 * — including the ones nobody has used yet, which read `( 0 )` — each with its
 * count over `rows`.
 */
export function countStatusTabs<T>(
  rows: readonly T[],
  statuses: readonly RegisterStatusOption[],
  statusOf: (row: T) => string,
  allLabel = 'All',
): StatusTab[] {
  const counts = new Map<string, number>();
  for (const row of rows) {
    const status = statusOf(row);
    counts.set(status, (counts.get(status) ?? 0) + 1);
  }

  // A status the page did not list still gets a tab when a row has it, so no
  // row is ever reachable only through All.
  const listed = new Set(statuses.map((s) => s.value));
  const unlisted = [...counts.keys()]
    .filter((value) => !listed.has(value))
    .map((value) => ({ value, label: registerStatusLabel(value) }));

  return [
    { value: '', label: allLabel, count: rows.length },
    ...[...statuses, ...unlisted].map((s) => ({ ...s, count: counts.get(s.value) ?? 0 })),
  ];
}

/**
 * The tabs for a register paged on the server, from the counts it returned:
 * All, then every status, zero included. The counts are the server's because
 * a page of rows cannot count the pages it was not sent.
 */
export function statusTabsFromCounts(
  counts: Readonly<Record<string, number>> | undefined,
  statuses: readonly RegisterStatusOption[],
  allLabel = 'All',
): StatusTab[] {
  const all = Object.values(counts ?? {}).reduce((sum, n) => sum + n, 0);
  return [
    { value: '', label: allLabel, count: all },
    ...statuses.map((s) => ({ ...s, count: counts?.[s.value] ?? 0 })),
  ];
}

/** How a status reads: its own name, except the two-word ones. */
export function registerStatusLabel(status: string): string {
  switch (status) {
    case 'ReadyToPost':
      return 'Ready to post';
    default:
      return status;
  }
}

/**
 * The tag a status wears, the same on every register: still to do in the
 * accent, done in neutral, not yet real or no longer real in outline.
 */
export function registerStatusTone(status: string): string {
  switch (status) {
    case 'Open':
    case 'Sent':
    case 'ReadyToPost':
    case 'PartlyReceived':
    case 'PartlyDelivered':
    case 'PartlyInvoiced':
    case 'Accepted':
      return 'tag-accent';
    case 'Posted':
    case 'Closed':
    case 'Received':
    case 'Delivered':
    case 'Invoiced':
    case 'Confirmed':
      return 'tag-neutral';
    case 'Overdue':
    case 'Rejected':
      return 'tag-danger';
    default:
      return 'tag-outline';
  }
}
