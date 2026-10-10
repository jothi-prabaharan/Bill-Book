import { describe, expect, it } from 'vitest';
import { DEFAULT_FORMAT_SETTINGS } from '@bill-book/currency-format';
import {
  countStatusTabs,
  registerAmount,
  registerDate,
  registerStatusLabel,
  registerStatusTone,
  statusTabsFromCounts,
} from './register-format';

/** How a register writes its figures and counts its tabs (TK-187). */
describe('register-format', () => {
  it('REG-FMT-01: a whole amount drops its paise; any other keeps every one', () => {
    expect(registerAmount(400000, DEFAULT_FORMAT_SETTINGS)).toBe('₹4,00,000');
    expect(registerAmount(338983.05, DEFAULT_FORMAT_SETTINGS)).toBe('₹3,38,983.05');
    expect(registerAmount(-1500, DEFAULT_FORMAT_SETTINGS)).toBe('-₹1,500');
    expect(registerAmount(0, DEFAULT_FORMAT_SETTINGS)).toBe('₹0');
  });

  it('REG-FMT-02: a document in another currency is written without the branch symbol', () => {
    expect(registerAmount(1200, DEFAULT_FORMAT_SETTINGS, 'USD')).toBe('1,200');
    expect(registerAmount(1200, DEFAULT_FORMAT_SETTINGS, 'INR')).toBe('₹1,200');
  });

  it('REG-FMT-03: nothing to show is empty, not zero', () => {
    expect(registerAmount(null, DEFAULT_FORMAT_SETTINGS)).toBe('');
    expect(registerAmount(undefined, DEFAULT_FORMAT_SETTINGS)).toBe('');
    expect(registerAmount(Number.NaN, DEFAULT_FORMAT_SETTINGS)).toBe('');
  });

  it('REG-FMT-04: a row date reads with the month by name', () => {
    expect(registerDate('2026-08-11')).toBe('11 Aug 2026');
    expect(registerDate('2026-08-11T10:00:00Z')).toBe('11 Aug 2026');
    expect(registerDate(null)).toBe('');
  });

  it('REG-FMT-05: the tabs count every status, zero included, with All first', () => {
    const rows = [{ s: 'Open' }, { s: 'Open' }, { s: 'Closed' }];
    const tabs = countStatusTabs(
      rows,
      [
        { value: 'Open', label: 'Open' },
        { value: 'Closed', label: 'Closed' },
        { value: 'Cancelled', label: 'Cancelled' },
      ],
      (r) => r.s,
    );

    expect(tabs.map((t) => [t.value, t.count])).toEqual([
      ['', 3],
      ['Open', 2],
      ['Closed', 1],
      ['Cancelled', 0],
    ]);
  });

  it('REG-FMT-06: a status a row has that the page did not list still gets a tab', () => {
    const tabs = countStatusTabs([{ s: 'ReadyToPost' }], [{ value: 'Draft', label: 'Draft' }], (r) => r.s);
    expect(tabs.map((t) => [t.value, t.label, t.count])).toEqual([
      ['', 'All', 1],
      ['Draft', 'Draft', 0],
      ['ReadyToPost', 'Ready to post', 1],
    ]);
  });

  it('REG-FMT-07: a server-paged register builds its tabs from the counts it was sent', () => {
    const tabs = statusTabsFromCounts({ Draft: 4, Posted: 18 }, [
      { value: 'Draft', label: 'Draft' },
      { value: 'Posted', label: 'Posted' },
      { value: 'Void', label: 'Void' },
    ]);
    expect(tabs.map((t) => [t.value, t.count])).toEqual([
      ['', 22],
      ['Draft', 4],
      ['Posted', 18],
      ['Void', 0],
    ]);
    expect(statusTabsFromCounts(undefined, [])[0].count).toBe(0);
  });

  it('REG-FMT-08: a status wears the same tag on every register', () => {
    expect(registerStatusTone('Open')).toBe('tag-accent');
    expect(registerStatusTone('PartlyReceived')).toBe('tag-accent');
    expect(registerStatusTone('Posted')).toBe('tag-neutral');
    expect(registerStatusTone('Closed')).toBe('tag-neutral');
    expect(registerStatusTone('Draft')).toBe('tag-outline');
    expect(registerStatusTone('Void')).toBe('tag-outline');
    expect(registerStatusLabel('ReadyToPost')).toBe('Ready to post');
    expect(registerStatusLabel('PartlyReceived')).toBe('PartlyReceived');
  });
});
