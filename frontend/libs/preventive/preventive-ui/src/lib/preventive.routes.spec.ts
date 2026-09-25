import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { preventiveRoutes } from './preventive.routes';

describe('preventiveRoutes (TK-67)', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(preventiveRoutes)).toEqual([]);
  });

  it('are School pages', () => {
    for (const route of preventiveRoutes) {
      expect(route.data?.['access']?.apps).toEqual(['School']);
    }
  });
});
