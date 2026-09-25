import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { employeeRoutes } from './employee.routes';

/** Every Employee page declares its access, and HRMS-only pages say so (TK-48). */
describe('employeeRoutes', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(employeeRoutes)).toEqual([]);
  });

  it('keep announcements and policies to HRMS', () => {
    for (const path of ['hrm/announcements', 'hrm/policies']) {
      expect(employeeRoutes.find((r) => r.path === path)?.data?.['access']?.apps).toEqual(['Hrms']);
    }
  });

  it('list new before :id, so the id route does not swallow it', () => {
    const paths = employeeRoutes.map((r) => r.path);
    expect(paths.indexOf('hrm/employees/new')).toBeLessThan(paths.indexOf('hrm/employees/:id'));
  });
});
