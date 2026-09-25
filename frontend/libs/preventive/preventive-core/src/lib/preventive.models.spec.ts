import { describe, expect, it } from 'vitest';
import { everyLabel, FREQUENCIES } from './preventive.models';

describe('preventive models (TK-67)', () => {
  it('say a recurrence as people do', () => {
    expect(everyLabel('Monthly', 1)).toBe('Every month');
    expect(everyLabel('Weekly', 2)).toBe('Every 2 weeks');
    expect(everyLabel('HalfYearly', 1)).toBe('Every half-year');
  });

  it('offer the frequencies the server accepts', () => {
    expect(FREQUENCIES.map((f) => f.value)).toEqual(['Daily', 'Weekly', 'Monthly', 'Quarterly', 'HalfYearly', 'Yearly']);
  });
});
