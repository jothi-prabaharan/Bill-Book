import { describe, expect, it } from 'vitest';
import { gstinState, placeOfSupplyFor, supplyIsInterState } from './place-of-supply';

describe('place of supply', () => {
  it('reads the state off a GSTIN, and nothing off anything else', () => {
    expect(gstinState('29AABCU9603R1ZM')).toBe('29');
    expect(gstinState(' 33AAACR5055K1Z5 ')).toBe('33');
    expect(gstinState('')).toBeNull();
    expect(gstinState(null)).toBeNull();
    expect(gstinState('AB1234')).toBeNull();
  });

  it("takes a registered vendor's state, and the branch's for an unregistered one", () => {
    expect(placeOfSupplyFor('29AABCU9603R1ZM', '33')).toBe('29');
    expect(placeOfSupplyFor(null, '33')).toBe('33');
  });

  it('is inter-state when the supply state is not the branch state', () => {
    expect(supplyIsInterState('29', '29AABCU9603R1ZM', '33')).toBe(true);
    expect(supplyIsInterState('33', null, '33')).toBe(false);
    // A blank place of supply falls back to the GSTIN, as the server does.
    expect(supplyIsInterState('', '29AABCU9603R1ZM', '33')).toBe(true);
    // Nothing to go on: unknown, not intra-state.
    expect(supplyIsInterState('', '', '33')).toBeNull();
  });
});
