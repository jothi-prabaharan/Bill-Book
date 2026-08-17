import { describe, expect, it } from 'vitest';
import { ReportQuery, emptyQuery } from './models/report-contracts';
import { decodeQuery, encodeQuery } from './report-state';

/**
 * The URL round trip.
 *
 * What is being established is that a layout survives being put in an address bar
 * and taken out again — because if it does not, somebody loses their filters on a
 * refresh and blames the report.
 */
describe('report state', () => {
  const query: ReportQuery = {
    ...emptyQuery(),
    columns: ['date', 'account', 'debit'],
    filters: [{ column: 'account', operator: 'Contains', value: 'Cash' }],
    sorts: [{ column: 'date', direction: 'Desc' }],
    groupBy: ['accountCode'],
    page: { number: 3, size: 100, includeCount: true },
    freeze: { header: true, columns: 2 },
  };

  it('round-trips a layout', () => {
    const decoded = decodeQuery(encodeQuery(query));

    expect(decoded?.columns).toEqual(['date', 'account', 'debit']);
    expect(decoded?.filters[0]).toEqual({
      column: 'account',
      operator: 'Contains',
      value: 'Cash',
    });
    expect(decoded?.sorts[0]).toEqual({ column: 'date', direction: 'Desc' });
    expect(decoded?.groupBy).toEqual(['accountCode']);
    expect(decoded?.freeze).toEqual({ header: true, columns: 2 });
  });

  it('does not carry the page number into a shared link', () => {
    // A link should open the report, not page 3 of somebody else's session.
    expect(decodeQuery(encodeQuery(query))?.page.number).toBe(1);
    expect(decodeQuery(encodeQuery(query))?.page.size).toBe(100);
  });

  it('survives non-ASCII in a filter value', () => {
    // A naive btoa throws on this. The product supports Tamil and Chinese, so a
    // contact name in a filter is not an edge case.
    const tamil: ReportQuery = {
      ...query,
      filters: [{ column: 'account', operator: 'Contains', value: 'ரொக்கம் 现金' }],
    };

    expect(decodeQuery(encodeQuery(tamil))?.filters[0].value).toBe('ரொக்கம் 现金');
  });

  it('falls back rather than throwing on a stale or tampered link', () => {
    // A page that refuses to load because of an old bookmark is worse than one
    // that opens with its defaults.
    expect(decodeQuery(null)).toBeNull();
    expect(decodeQuery('')).toBeNull();
    expect(decodeQuery('not-base64!!')).toBeNull();
    expect(decodeQuery(btoa('{"nonsense":true}'))).toBeNull();
  });

  it('fills in anything a link omits', () => {
    // An older link, from before a field existed, must still open.
    const partial = btoa(
      JSON.stringify({ columns: ['date'], filters: [], sorts: [], groupBy: [] }),
    )
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');

    const decoded = decodeQuery(partial);

    expect(decoded?.columns).toEqual(['date']);
    expect(decoded?.page.size).toBe(50);
    expect(decoded?.freeze.header).toBe(true);
  });
});
