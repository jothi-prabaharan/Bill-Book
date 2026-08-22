import { ReportQuery, emptyQuery } from './models/report-contracts';

/**
 * A report's query, to and from the URL.
 *
 * **Why the URL and not just component state.** A layout in the address bar means
 * the back button works, a refresh keeps the report, and somebody can paste "the
 * one that shows the discrepancy" into a message. None of those work if the state
 * only exists in a component.
 *
 * The encoding is compact rather than readable: a report with fifteen columns and
 * four filters spelled out as query parameters produces a URL nobody can copy
 * without it wrapping. Base64 of the JSON keeps it to one token, and the query is
 * never parsed by anything but this file.
 *
 * **Round-tripping is the contract.** Anything this cannot encode is a layout
 * somebody loses on refresh, so `decode` returns a complete query or nothing —
 * never a half-applied one, which would silently show different rows than the
 * link promised.
 */
export const REPORT_STATE_PARAM = 'q';

export function encodeQuery(query: ReportQuery): string {
  // The page number is deliberately not carried. A shared link should open the
  // report, not page 7 of somebody else's session.
  const shareable: ReportQuery = {
    ...query,
    page: { ...query.page, number: 1 },
  };

  return toBase64Url(JSON.stringify(shareable));
}

/**
 * Returns null when the parameter is missing, malformed, or not a report query.
 * The caller falls back to the report's defaults rather than showing an error —
 * a stale link is a mild inconvenience, and a page that refuses to load because
 * of one is not.
 */
export function decodeQuery(encoded: string | null): ReportQuery | null {
  if (!encoded) {
    return null;
  }

  try {
    const parsed: unknown = JSON.parse(fromBase64Url(encoded));

    return isQuery(parsed) ? { ...emptyQuery(), ...parsed } : null;
  } catch {
    return null;
  }
}

/**
 * A structural check, not a validation.
 *
 * The server validates properly — it has to, since a request need not come from
 * this client at all. This only establishes that the decoded object is shaped
 * like a query, so a truncated or tampered link falls back to the defaults
 * instead of throwing halfway through rendering.
 */
function isQuery(value: unknown): value is ReportQuery {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<ReportQuery>;

  return (
    Array.isArray(candidate.columns) &&
    Array.isArray(candidate.filters) &&
    Array.isArray(candidate.sorts) &&
    Array.isArray(candidate.groupBy)
  );
}

/**
 * Base64url, over UTF-8.
 *
 * `btoa` is a browser global but is also present in Node and in the Ionic
 * runtimes, unlike `window` or `document` — so this stays within what a
 * `-core` lib may use. The percent-encoding dance is what makes it survive
 * non-ASCII: a Tamil account name in a filter breaks a naive `btoa`.
 */
function toBase64Url(text: string): string {
  const base64 = btoa(
    encodeURIComponent(text).replace(/%([0-9A-F]{2})/g, (_, hex: string) =>
      String.fromCharCode(parseInt(hex, 16)),
    ),
  );

  return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function fromBase64Url(encoded: string): string {
  const base64 = encoded.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, '='));

  return decodeURIComponent(
    Array.from(binary, (character) =>
      `%${character.charCodeAt(0).toString(16).padStart(2, '0')}`,
    ).join(''),
  );
}
