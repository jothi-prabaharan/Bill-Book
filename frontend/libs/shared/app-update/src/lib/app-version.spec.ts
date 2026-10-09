import { describe, expect, it } from 'vitest';
import { compareVersions, parseVersion } from './app-version';

const order = (a: string, b: string): number => {
  const x = parseVersion(a);
  const y = parseVersion(b);
  if (!x || !y) {
    throw new Error(`unparseable: ${a} / ${b}`);
  }
  return Math.sign(compareVersions(x, y));
};

describe('parseVersion', () => {
  it('reads full, short and prefixed versions', () => {
    expect(parseVersion('1.2.3')).toEqual({ major: 1, minor: 2, patch: 3, pre: [] });
    expect(parseVersion('v1.2')).toEqual({ major: 1, minor: 2, patch: 0, pre: [] });
    expect(parseVersion(' 2 ')).toEqual({ major: 2, minor: 0, patch: 0, pre: [] });
  });

  it('keeps pre-release identifiers and ignores build metadata', () => {
    expect(parseVersion('1.2.0-beta.2+abc')).toEqual({ major: 1, minor: 2, patch: 0, pre: ['beta', 2] });
  });

  it('refuses anything that is not a version', () => {
    expect(parseVersion('')).toBeNull();
    expect(parseVersion('latest')).toBeNull();
    expect(parseVersion('1.2.3.4')).toBeNull();
    expect(parseVersion(null)).toBeNull();
    expect(parseVersion(undefined)).toBeNull();
  });
});

describe('compareVersions', () => {
  it('orders by major, then minor, then patch, numerically', () => {
    expect(order('1.0.0', '2.0.0')).toBe(-1);
    expect(order('1.10.0', '1.9.0')).toBe(1);
    expect(order('1.2.10', '1.2.9')).toBe(1);
    expect(order('1.2', '1.2.0')).toBe(0);
  });

  it('puts a pre-release below its release', () => {
    expect(order('1.2.0-beta', '1.2.0')).toBe(-1);
    expect(order('1.2.0', '1.2.0-rc.1')).toBe(1);
  });

  it('orders pre-releases by their identifiers, numbers below words', () => {
    expect(order('1.2.0-beta.2', '1.2.0-beta.10')).toBe(-1);
    expect(order('1.2.0-alpha', '1.2.0-beta')).toBe(-1);
    expect(order('1.2.0-1', '1.2.0-alpha')).toBe(-1);
    expect(order('1.2.0-beta', '1.2.0-beta.1')).toBe(-1);
  });
});
