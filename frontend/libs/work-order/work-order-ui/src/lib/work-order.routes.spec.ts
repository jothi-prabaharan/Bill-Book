import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { workOrderRoutes } from './work-order.routes';

describe('workOrderRoutes (TK-66)', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(workOrderRoutes)).toEqual([]);
  });

  it('are School pages', () => {
    for (const route of workOrderRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });
});
