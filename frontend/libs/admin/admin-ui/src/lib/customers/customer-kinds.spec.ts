import { describe, expect, it } from 'vitest';
import { CUSTOMER_KINDS, demoSummary, kindQuery } from './customers.page';

describe('customer kinds (TK-152)', () => {
  it('offers the three kinds', () => {
    expect(CUSTOMER_KINDS.map((k) => k.value)).toEqual(['Standard', 'Internal', 'Demo']);
  });

  it('filters the list by kind, or not at all', () => {
    expect(kindQuery('Demo')).toBe('?kind=Demo');
    expect(kindQuery('')).toBe('');
  });
});

describe('demo customers (TK-154)', () => {
  const row = (customerCode: string, created: boolean, unseeded: string[] = []) => ({
    businessType: 'Retail',
    customerCode,
    created,
    unseeded,
  });

  it('says how many were created and that all are set up', () => {
    expect(demoSummary([row('DEMO-RTL', true), row('DEMO-PHA', false)])).toBe('1 demo customer created. Every one is set up.');
  });

  it('names what is still to set up', () => {
    expect(demoSummary([row('DEMO-RTL', false, ['Sales'])])).toContain('DEMO-RTL (Sales)');
  });
});
