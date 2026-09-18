import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { PeriodFilterBarComponent } from './period-filter-bar.component';
import { isWithin, rangeForPreset, toIsoDate } from './period-range';

describe('Period ranges', () => {
  it('PERIOD-01: This month spans the first to the last day of the current month', () => {
    const range = rangeForPreset('this-month', new Date(2026, 8, 14)); // 14 Sep 2026
    expect(range.from).toBe('2026-09-01');
    expect(range.to).toBe('2026-09-30');
  });

  it('PERIOD-02: Last month crosses the year boundary correctly', () => {
    const range = rangeForPreset('last-month', new Date(2026, 0, 9)); // 9 Jan 2026
    expect(range.from).toBe('2025-12-01');
    expect(range.to).toBe('2025-12-31');
  });

  it('PERIOD-03: The financial year runs April to March', () => {
    const inSeptember = rangeForPreset('this-financial-year', new Date(2026, 8, 14));
    expect(inSeptember.from).toBe('2026-04-01');
    expect(inSeptember.to).toBe('2027-03-31');

    // January belongs to the year that began the previous April.
    const inJanuary = rangeForPreset('this-financial-year', new Date(2027, 0, 9));
    expect(inJanuary.from).toBe('2026-04-01');
    expect(inJanuary.to).toBe('2027-03-31');
  });

  it('PERIOD-04: Custom carries no range of its own', () => {
    const range = rangeForPreset('custom', new Date(2026, 8, 14));
    expect(range).toEqual({ preset: 'custom', from: '', to: '' });
  });

  it('PERIOD-05: A February range ends on the right day in a leap year', () => {
    expect(rangeForPreset('this-month', new Date(2028, 1, 3)).to).toBe('2028-02-29');
    expect(rangeForPreset('this-month', new Date(2026, 1, 3)).to).toBe('2026-02-28');
  });

  it('PERIOD-06: Membership treats both ends as inclusive and an open end as no bound', () => {
    const range = rangeForPreset('this-month', new Date(2026, 8, 14));
    expect(isWithin(range, '2026-09-01')).toBe(true);
    expect(isWithin(range, '2026-09-30')).toBe(true);
    expect(isWithin(range, '2026-08-31')).toBe(false);
    expect(isWithin(range, '2026-10-01')).toBe(false);
    // Timestamps are compared by their day.
    expect(isWithin(range, '2026-09-14T18:30:00Z')).toBe(true);
    expect(isWithin(range, '')).toBe(false);

    const open = { preset: 'custom' as const, from: '', to: '' };
    expect(isWithin(open, '1999-01-01')).toBe(true);
  });

  it('PERIOD-07: Local dates are formatted without drifting a day through UTC', () => {
    expect(toIsoDate(new Date(2026, 0, 1))).toBe('2026-01-01');
    expect(toIsoDate(new Date(2026, 11, 31))).toBe('2026-12-31');
  });
});

describe('PeriodFilterBarComponent', () => {
  const create = (): PeriodFilterBarComponent =>
    TestBed.runInInjectionContext(() => new PeriodFilterBarComponent());

  it('PERIOD-08: Dates are only editable under Custom', () => {
    const comp = create();
    comp.pick('this-month');
    expect(comp.isCustom()).toBe(false);

    comp.pick('custom');
    expect(comp.isCustom()).toBe(true);
    expect(comp.from()).toBe('');
  });

  it('PERIOD-09: Choosing a preset and typing a custom date both announce the range', () => {
    const comp = create();
    const seen: string[] = [];
    comp.rangeChange.subscribe((range) => seen.push(range.preset));

    comp.pick('last-month');
    comp.pick('custom');
    comp.setFrom('2026-01-01');

    expect(seen).toEqual(['last-month', 'custom', 'custom']);
    expect(comp.from()).toBe('2026-01-01');
  });

  it('PERIOD-10: The total is labelled with the branch currency and set tabular', () => {
    const comp = create();
    expect(comp.totalLabel()).toBe('Total in INR');
    expect(comp.formattedTotal()).toBe('0');
  });
});
