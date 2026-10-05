import { describe, expect, it } from 'vitest';
import { CUSTOMER_KINDS, kindQuery } from './customers.page';

describe('customer kinds (TK-152)', () => {
  it('offers the three kinds', () => {
    expect(CUSTOMER_KINDS.map((k) => k.value)).toEqual(['Standard', 'Internal', 'Demo']);
  });

  it('filters the list by kind, or not at all', () => {
    expect(kindQuery('Demo')).toBe('?kind=Demo');
    expect(kindQuery('')).toBe('');
  });
});
