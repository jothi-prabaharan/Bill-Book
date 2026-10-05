import { describe, expect, it } from 'vitest';
import { defaultRestaurantSettings } from './restaurant-settings';
import { RestaurantReportItem, hiddenReportNotes, reportLink, sortReports } from './restaurant-reports';

const report = (reportKey: string, sortOrder: number, title = reportKey): RestaurantReportItem => ({ reportKey, title, description: null, sortOrder });

describe('restaurant reports', () => {
  it('opens a report on the report screen', () => {
    expect(reportLink('restaurant-day-end')).toEqual(['/reports', 'restaurant-day-end']);
  });

  it('keeps the seeded order', () => {
    const sorted = sortReports([report('b', 1602), report('a', 1601), report('c', 1602, 'Alpha')]);
    expect(sorted.map((r) => r.reportKey)).toEqual(['a', 'c', 'b']);
  });

  it('says which reports a switch that is off hides', () => {
    const all = { ...defaultRestaurantSettings(), tablesEnabled: true, kitchenEnabled: true, recipesEnabled: true };
    expect(hiddenReportNotes(all)).toEqual([]);

    expect(hiddenReportNotes({ ...all, recipesEnabled: false })).toEqual([
      'Food cost and Wastage are hidden while Recipes is off in Settings › Restaurant.',
    ]);
    expect(hiddenReportNotes({ ...all, tablesEnabled: false })).toEqual([
      'Table turnover is hidden while Tables is off in Settings › Restaurant.',
    ]);
    expect(hiddenReportNotes(defaultRestaurantSettings())).toHaveLength(3);
  });
});
