import { describe, expect, it } from 'vitest';
import { BarcodeBurst, commandFor } from './pos-keys';

/** The till's keyboard and scanner detection (TK-40). */
describe('commandFor', () => {
  it('maps the function keys to the till commands', () => {
    expect(commandFor({ key: 'F2' })).toBe('addItem');
    expect(commandFor({ key: 'F4' })).toBe('quantity');
    expect(commandFor({ key: 'F6' })).toBe('voidLine');
    expect(commandFor({ key: 'F8' })).toBe('hold');
    expect(commandFor({ key: 'F9' })).toBe('tender');
    expect(commandFor({ key: 'F10' })).toBe('reprint');
    expect(commandFor({ key: 'Escape' })).toBe('cancel');
  });

  it('leaves modified keys and ordinary letters alone', () => {
    expect(commandFor({ key: 'F2', ctrlKey: true })).toBeNull();
    expect(commandFor({ key: 'a' })).toBeNull();
  });
});

describe('BarcodeBurst', () => {
  function feed(burst: BarcodeBurst, keys: string[], start: number, gap: number): (string | null)[] {
    return keys.map((key, i) => burst.push(key, start + i * gap));
  }

  it('reads a fast burst ending in Enter as a scan', () => {
    const burst = new BarcodeBurst();
    const results = feed(burst, [...'8901030865278', 'Enter'], 1000, 8);

    expect(results.at(-1)).toBe('8901030865278');
  });

  it('does not read a person typing as a scan', () => {
    const burst = new BarcodeBurst();
    const results = feed(burst, [...'1234', 'Enter'], 1000, 180);

    expect(results.at(-1)).toBeNull();
  });

  it('refuses a burst too short to be a barcode', () => {
    const burst = new BarcodeBurst();
    expect(feed(burst, ['1', '2', 'Enter'], 0, 5).at(-1)).toBeNull();
  });

  it('starts again after a pause, so stray keys before a scan are dropped', () => {
    const burst = new BarcodeBurst();
    burst.push('x', 0);
    const results = feed(burst, [...'ABCD1234', 'Enter'], 500, 6);

    expect(results.at(-1)).toBe('ABCD1234');
  });
});
