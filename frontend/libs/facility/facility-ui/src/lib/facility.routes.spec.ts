import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { facilityRoutes } from './facility.routes';

describe('facilityRoutes (TK-65)', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(facilityRoutes)).toEqual([]);
  });

  it('are School pages', () => {
    for (const route of facilityRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });
});
