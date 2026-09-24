import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { hrmRoutes } from './hrm.routes';

/** Every Hrm page declares its access, and HRMS-only pages say so (TK-48). */
describe('hrmRoutes', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(hrmRoutes)).toEqual([]);
  });

  it('keep announcements and policies to HRMS', () => {
    for (const path of ['hrm/announcements', 'hrm/policies']) {
      expect(hrmRoutes.find((r) => r.path === path)?.data?.['access']?.apps).toEqual(['Hrms']);
    }
  });

  it('list new before :id, so the id route does not swallow it', () => {
    const paths = hrmRoutes.map((r) => r.path);
    expect(paths.indexOf('hrm/employees/new')).toBeLessThan(paths.indexOf('hrm/employees/:id'));
  });
});
