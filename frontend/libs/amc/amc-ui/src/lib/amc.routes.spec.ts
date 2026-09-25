import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { amcRoutes } from './amc.routes';

describe('amcRoutes (TK-68)', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(amcRoutes)).toEqual([]);
  });

  it('are School pages', () => {
    for (const route of amcRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });
});
