import { describe, expect, it } from 'vitest';
import { BarcodeBurst, burstDetector } from './scan-burst';

function feed(push: (key: string, at: number) => string | null, keys: string[], start: number, gap: number): (string | null)[] {
  return keys.map((key, i) => push(key, start + i * gap));
}

describe('BarcodeBurst', () => {
  it('reads a fast burst ending in Enter as a scan', () => {
    const burst = new BarcodeBurst();
    const out = feed((k, t) => burst.push(k, t), [...'8901234567890', 'Enter'], 1000, 5);
    expect(out.at(-1)).toBe('8901234567890');
  });

  it('ignores a person typing at human speed', () => {
    const burst = new BarcodeBurst();
    const out = feed((k, t) => burst.push(k, t), [...'SOAP01', 'Enter'], 1000, 120);
    expect(out.at(-1)).toBeNull();
  });

  it('refuses a burst shorter than the minimum', () => {
    const burst = new BarcodeBurst();
    expect(feed((k, t) => burst.push(k, t), [...'12', 'Enter'], 0, 5).at(-1)).toBeNull();
  });

  it('starts over after reset', () => {
    const burst = new BarcodeBurst();
    feed((k, t) => burst.push(k, t), [...'1234'], 0, 5);
    burst.reset();
    expect(burst.push('Enter', 25)).toBeNull();
  });
});

describe('burstDetector', () => {
  it('is the same rule as a plain function', () => {
    const detect = burstDetector();
    expect(feed(detect, [...'ABC-123', 'Enter'], 0, 10).at(-1)).toBe('ABC-123');
  });

  it('keeps its own state per detector', () => {
    const a = burstDetector();
    const b = burstDetector();
    feed(a, [...'1111'], 0, 5);
    expect(b('Enter', 25)).toBeNull();
    expect(a('Enter', 25)).toBe('1111');
  });
});
