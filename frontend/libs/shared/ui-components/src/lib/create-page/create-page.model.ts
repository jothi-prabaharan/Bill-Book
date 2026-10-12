/**
 * What the create page's read-only fields show (TK-192), kept apart from the
 * components so the wording can be pinned by a test.
 */

/** Where an exchange rate stands: the base currency, a lookup, or a saved document's own. */
export type RateState = 'base' | 'loading' | 'found' | 'missing' | 'failed' | 'locked';

/** The Number field: the document's own number, the series' next, or the promise of one. */
export function numberText(own: string | null, loading: boolean, preview: string | null): string {
  if (own) return own;
  if (loading) return '';
  return preview ?? 'Assigned on save';
}

/** The rate as the field shows it: up to six places, nothing trailing, a dash for none. */
export function rateText(rate: number | null, state: RateState): string {
  if (state === 'loading') return '';
  return rate === null ? '—' : String(Number(rate.toFixed(6)));
}

/**
 * The line under the rate, in the design's words where it has them
 * (Shell.dc.html 5340–5342): "Base currency of the organisation", or
 * "1 USD = ₹83.25 · currency master, 11/08/2026".
 */
export function rateNote(note: {
  state: RateState;
  currency: string | null;
  symbol: string;
  rate: string;
  rateDate: string;
}): string {
  const one = (where: string) => `1 ${note.currency} = ${note.symbol}${note.rate} · ${where}`;
  switch (note.state) {
    case 'base':
      return 'Base currency of the organisation';
    case 'loading':
      return 'Reading the currency master…';
    case 'found':
      return one(`currency master, ${note.rateDate}`);
    case 'locked':
      return note.rate === '—' ? 'No rate was saved with this document' : one('saved with the document');
    case 'missing':
      return `No ${note.currency} rate on or before this date in the currency master`;
    case 'failed':
      return 'The currency master could not be read';
  }
}
