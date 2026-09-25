import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { feeRoutes } from './fee.routes';

describe('feeRoutes (TK-64)', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(feeRoutes)).toEqual([]);
  });

  it('are School pages', () => {
    for (const route of feeRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });
});
