import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { appRoutes } from './app.routes';

/** Every page under the School shell declares its access (TK-60). */
describe('apps/school routes', () => {
  it('declare data.access on every page under the shell', () => {
    expect(auditShellRoutes(appRoutes)).toEqual([]);
  });

  it('mount the shell once, for School', () => {
    const shells = appRoutes.filter((r) => r.data?.['shellRoot'] === true);

    expect(shells).toHaveLength(1);
    expect(shells[0].data?.['app']).toBe('School');
  });

  it('mount the shared settings pages and the employee master rather than copies of them', () => {
    const paths = childPaths(appRoutes);

    expect(paths).toContain('settings/users');
    expect(paths).toContain('settings/applications');
    expect(paths.some((p) => p.startsWith('hrm'))).toBe(true);
    expect(paths).toContain('contacts');
    expect(paths).toContain('sis/students');
    expect(paths).toContain('admission/applications');
    expect(paths).toContain('attendance/register');
    expect(paths).toContain('fee/receipts');
    expect(paths).toContain('facility/assets');
    expect(paths).toContain('work-orders');
  });
});

function childPaths(routes: typeof appRoutes): string[] {
  return (routes.find((r) => r.data?.['shellRoot'] === true)?.children ?? []).map((r) => r.path ?? '');
}
