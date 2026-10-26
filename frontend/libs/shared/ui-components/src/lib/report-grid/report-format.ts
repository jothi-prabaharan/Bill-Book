import { FormatSettings, formatDate, formatMoney, formatNumber } from '@bill-book/currency-format';

/**
 * How a report writes its dates and amounts (TK-262): **exactly as the branch's settings
 * say**, wherever a report shows one: the branch's date pattern, and the branch's money format
 * with its symbol on the side the branch's currency asks for, its grouping mask and its decimals,
 * always shown.
 *
 * This is deliberately not what a document register does (TK-187): a register row reads
 * `11 Aug 2026` and drops the paise of a whole amount, because a register is scanned by eye.
 * A report is read figure by figure and compared with the books, so it shows the format the
 * branch's own date fields and ledger use.
 *
 * Plain functions over {@link FormatSettings}, not methods, so they can be tested without an
 * injector.
 */

/** A date in the branch's pattern; a value that is not a date is left as it came. */
export function reportDate(value: string | null | undefined, settings: FormatSettings): string {
  return formatDate(value, settings.datePattern);
}

/** A date and time: the date in the branch's pattern, then the time as `HH:mm`. */
export function reportDateTime(value: string | null | undefined, settings: FormatSettings): string {
  if (!value) {
    return '';
  }

  const time = /[T ](\d{2}:\d{2})/.exec(value)?.[1];
  const date = formatDate(value, settings.datePattern);

  return time ? `${date} ${time}` : date;
}

/**
 * An amount in the branch's money format, its decimals always shown.
 *
 * When the amount is in a currency other than the branch's, the branch's symbol would be wrong,
 * so it is written as a bare number in the branch's grouping and decimals and the caller shows
 * the currency code beside it.
 */
export function reportAmount(
  value: number | null | undefined,
  settings: FormatSettings,
  currencyCode?: string | null,
): string {
  if (value == null || !Number.isFinite(value)) {
    return '';
  }

  if (currencyCode && currencyCode !== settings.currencyCode) {
    return reportNumber(value, settings);
  }

  return formatMoney(value, settings);
}

/**
 * An amount with no symbol, in the branch's grouping and decimals: a money column of the generic
 * report grid, whose header names the currency, and some of whose columns are in a document's own.
 */
export function reportNumber(value: number | null | undefined, settings: FormatSettings): string {
  return formatNumber(value, settings.currencyDecimals, settings.currencyMask);
}
