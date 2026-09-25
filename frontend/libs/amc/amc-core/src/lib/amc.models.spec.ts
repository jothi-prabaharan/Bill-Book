import { describe, expect, it } from 'vitest';
import { CONTRACT_STATUSES, daysLeft, takesChanges, termsEditable } from './amc.models';

describe('amc models (TK-68)', () => {
  it('fix an active contract\'s terms, and change nothing once it has ended', () => {
    expect(CONTRACT_STATUSES.filter((s) => termsEditable(s.value)).map((s) => s.value)).toEqual(['Draft']);
    expect(CONTRACT_STATUSES.filter((s) => takesChanges(s.value)).map((s) => s.value)).toEqual(['Draft', 'Active']);
  });

  it('count the days left to the end date', () => {
    expect(daysLeft('2027-03-31', '2027-03-01')).toBe(30);
    expect(daysLeft('2027-03-31', '2027-03-31')).toBe(0);
    expect(daysLeft('2027-03-31', '2027-04-02')).toBe(-2);
  });
});
