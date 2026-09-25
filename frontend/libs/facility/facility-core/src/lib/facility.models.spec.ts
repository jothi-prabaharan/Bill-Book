import { describe, expect, it } from 'vitest';
import { ASSET_STATUSES, floorLabel } from './facility.models';

describe('facility models (TK-65)', () => {
  it('name floors as people say them', () => {
    expect(floorLabel(0)).toBe('Ground');
    expect(floorLabel(2)).toBe('Floor 2');
    expect(floorLabel(-1)).toBe('Basement 1');
  });

  it('offer every asset status', () => {
    expect(ASSET_STATUSES.map((s) => s.value)).toEqual(['InUse', 'UnderRepair', 'Idle', 'Disposed']);
  });
});
