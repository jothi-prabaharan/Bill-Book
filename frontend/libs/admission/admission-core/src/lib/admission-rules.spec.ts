import { describe, expect, it } from 'vitest';
import { canAdmit, nextStages } from './admission-rules';

describe('admission rules (TK-62)', () => {
  it('move forward to Offered, or end, and never to Admitted', () => {
    expect(nextStages('Submitted')).toEqual(['DocumentsVerified', 'Assessed', 'Offered', 'Rejected', 'Withdrawn']);
    expect(nextStages('Offered')).toEqual(['Rejected', 'Withdrawn']);
    expect(nextStages('Submitted')).not.toContain('Admitted');
  });

  it('stop at a closed application', () => {
    expect(nextStages('Admitted')).toEqual([]);
    expect(nextStages('Rejected')).toEqual([]);
    expect(nextStages('Withdrawn')).toEqual([]);
  });

  it('admit only an offered application', () => {
    expect(canAdmit('Offered')).toBe(true);
    expect(canAdmit('Assessed')).toBe(false);
    expect(canAdmit('Admitted')).toBe(false);
  });
});
