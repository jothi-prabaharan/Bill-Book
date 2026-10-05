import { describe, expect, it } from 'vitest';
import { loyaltyKindLabel, pointsFor, pointsLabel } from './restaurant-loyalty';

describe('loyalty points (TK-166)', () => {
  it('a bill earns whole points at the rate per ₹100', () => {
    expect(pointsFor(250, 1)).toBe(2);
    expect(pointsFor(99.99, 1)).toBe(0);
    expect(pointsFor(1000, 2.5)).toBe(25);
  });

  it('earns nothing with no rate or no total', () => {
    expect(pointsFor(1000, 0)).toBe(0);
    expect(pointsFor(0, 5)).toBe(0);
    expect(pointsFor(-100, 5)).toBe(0);
  });

  it('words points and their history', () => {
    expect(pointsLabel(1)).toBe('1 point');
    expect(pointsLabel(12)).toBe('12 points');
    expect(pointsLabel(-12)).toBe('−12 points');
    expect(loyaltyKindLabel('Reversed')).toContain('voided');
  });
});
