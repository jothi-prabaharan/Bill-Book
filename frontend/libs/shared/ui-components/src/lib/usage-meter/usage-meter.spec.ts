import { describe, expect, it } from 'vitest';
import { meterFigures, meterMax, meterTone } from './usage-meter.model';

describe('Usage meter', () => {
  it('METER-01: is a warning from four fifths of the limit and a danger once it is reached', () => {
    expect(meterTone(0, 5)).toBe('ok');
    expect(meterTone(3, 5)).toBe('ok');
    expect(meterTone(4, 5)).toBe('warning');
    expect(meterTone(5, 5)).toBe('danger');

    // A count past its limit, as when a plan is lowered under people already in it.
    expect(meterTone(8, 5)).toBe('danger');

    expect(meterTone(799, 1000)).toBe('ok');
    expect(meterTone(800, 1000)).toBe('warning');
    expect(meterTone(1000, 1000)).toBe('danger');
  });

  it('METER-02: a count with no limit has nothing to run out of', () => {
    expect(meterTone(0, null)).toBe('ok');
    expect(meterTone(400_000, null)).toBe('ok');
  });

  it('METER-03: says how much is used, in the Indian grouping', () => {
    expect(meterFigures(3, 5)).toBe('3 of 5');
    expect(meterFigures(1234, 100000)).toBe('1,234 of 1,00,000');
    expect(meterFigures(12, null)).toBe('12 used · Unlimited');
  });

  it('METER-04: never gives a progress bar a maximum of nothing', () => {
    expect(meterMax(5)).toBe(5);
    expect(meterMax(0)).toBe(1);
    expect(meterMax(null)).toBe(1);

    // A limit of none at all is a full bar, not an empty one.
    expect(meterTone(0, 0)).toBe('danger');
  });
});
