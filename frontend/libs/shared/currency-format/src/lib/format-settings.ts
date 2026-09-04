/**
 * The branch's display formats, and the pure functions that apply them.
 *
 * **Why the grouping is read off a mask rather than named.** `mst.Currency`
 * already stores a mask per currency — `##,##,##0.00` for the rupee,
 * `###,###,##0.00` for the dollar — and that mask is the whole difference
 * between Indian and Western grouping. Deriving the group sizes from it means a
 * currency added later needs no code change and no new enum member; naming the
 * styles instead would give two sources for one answer, which is the thing the
 * backend service deliberately avoided.
 *
 * **Dates never go through `new Date()`.** A `DateOnly` arrives as `2026-09-04`,
 * and `new Date('2026-09-04')` parses as midnight UTC — so west of Greenwich it
 * renders as the third. Invoice dates that shift by a day depending on the
 * viewer's timezone are the kind of defect that surfaces in a filed return, so
 * the string is split rather than parsed.
 *
 * Plain functions, not methods, so they can be tested without standing up an
 * injector — the same reason `dominantTone` sits beside the message box rather
 * than inside it.
 */

/** As `GET /api/formats` returns it. */
export interface FormatSettings {
  datePattern: string;
  currencyCode: string;
  currencySymbol: string;
  symbolPosition: 'Prefix' | 'Suffix';
  currencyMask: string;
  currencyDecimals: number;
  unitPriceDecimals: number;
  quantityDecimals: number;
}

/**
 * What a screen renders before `/api/formats` has answered.
 *
 * Deliberately the same values the backend DTO defaults to. A screen that
 * flashes one format and then another is worse than one that waits, so these
 * exist to be correct for the common case rather than to be obviously wrong.
 */
export const DEFAULT_FORMAT_SETTINGS: FormatSettings = {
  datePattern: 'dd/MM/yyyy',
  currencyCode: 'INR',
  currencySymbol: '₹',
  symbolPosition: 'Prefix',
  currencyMask: '##,##,##0.00',
  currencyDecimals: 2,
  unitPriceDecimals: 2,
  quantityDecimals: 2,
};

const MONTHS_SHORT: readonly string[] = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
];

/**
 * The group sizes a mask asks for, right to left, with the last one repeating.
 *
 * `##,##,##0.00` gives `[3, 2]` — three digits, then twos forever, which is
 * lakh-crore grouping. `###,###,##0.00` gives `[3, 3]`, which is thousands.
 *
 * **A mask with no separator gives `[]`, meaning do not group at all.** That is
 * not the same as falling back to threes: a currency whose mask is `0.00` is
 * asking for `1234567.00`, and grouping it anyway would be this function
 * overruling the data it exists to read.
 */
export function groupSizesFromMask(mask: string): number[] {
  const integerPart = mask.split('.')[0] ?? '';
  const segments = integerPart.split(',').filter((s) => s.length > 0);

  if (segments.length <= 1) {
    return [];
  }

  // Right to left: the rightmost segment sizes the first group, and the one
  // before it sizes every group after that.
  const sizes = segments.map((s) => s.length).reverse();
  return [sizes[0], sizes[1]];
}

/** Inserts separators into a run of digits, per the mask's group sizes. */
function groupDigits(digits: string, mask: string): string {
  const sizes = groupSizesFromMask(mask);

  if (sizes.length === 0) {
    return digits;
  }

  const first = sizes[0];
  const rest = sizes[1] ?? sizes[0];

  if (digits.length <= first) {
    return digits;
  }

  const head = digits.slice(0, digits.length - first);
  const tail = digits.slice(digits.length - first);

  const parts: string[] = [];
  let remaining = head;
  while (remaining.length > rest) {
    parts.unshift(remaining.slice(remaining.length - rest));
    remaining = remaining.slice(0, remaining.length - rest);
  }
  if (remaining.length > 0) {
    parts.unshift(remaining);
  }

  return [...parts, tail].join(',');
}

/**
 * A number, grouped per the mask and fixed to `decimals`.
 *
 * The sign is applied after grouping so a negative never grows a separator in
 * the wrong place.
 */
export function formatNumber(
  value: number | null | undefined,
  decimals: number,
  mask: string = DEFAULT_FORMAT_SETTINGS.currencyMask,
): string {
  if (value == null || !Number.isFinite(value)) {
    return '';
  }

  const negative = value < 0;
  const fixed = Math.abs(value).toFixed(Math.max(0, decimals));
  const [whole, fraction] = fixed.split('.');
  const grouped = groupDigits(whole, mask);
  const body = fraction ? `${grouped}.${fraction}` : grouped;

  return negative ? `-${body}` : body;
}

/** An amount with its currency symbol on the side the branch's currency asks for. */
export function formatMoney(
  value: number | null | undefined,
  settings: FormatSettings,
): string {
  if (value == null || !Number.isFinite(value)) {
    return '';
  }

  const body = formatNumber(value, settings.currencyDecimals, settings.currencyMask);

  // The minus stays left of the symbol — "-₹100", not "₹-100" — because that is
  // how every other figure in the ledger reads.
  if (settings.symbolPosition === 'Suffix') {
    return `${body} ${settings.currencySymbol}`;
  }

  return body.startsWith('-')
    ? `-${settings.currencySymbol}${body.slice(1)}`
    : `${settings.currencySymbol}${body}`;
}

/**
 * A `DateOnly` string rendered in the branch's pattern.
 *
 * Understands `yyyy`, `yy`, `MMM`, `MM`, `dd` — the tokens the seeded patterns
 * use. Anything it does not recognise is left alone, so an unsupported pattern
 * degrades to visible literal text rather than to a wrong date.
 */
export function formatDate(
  value: string | null | undefined,
  pattern: string = DEFAULT_FORMAT_SETTINGS.datePattern,
): string {
  if (!value) {
    return '';
  }

  // Tolerates a full timestamp by taking the date half, so a column that turns
  // out to be a DateTime does not render as an empty cell.
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) {
    return value;
  }

  const [, yyyy, mm, dd] = match;
  const monthIndex = Number(mm) - 1;

  // Longest token first: replacing MM before MMM would leave a stray M.
  return pattern
    .replace(/yyyy/g, yyyy)
    .replace(/yy/g, yyyy.slice(2))
    .replace(/MMM/g, MONTHS_SHORT[monthIndex] ?? mm)
    .replace(/MM/g, mm)
    .replace(/dd/g, dd);
}
