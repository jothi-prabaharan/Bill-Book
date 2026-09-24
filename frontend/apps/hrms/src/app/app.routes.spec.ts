import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { appRoutes } from './app.routes';

/** Every page under the HRMS shell declares its access (TK-47). */
describe('apps/hrms routes', () => {
  it('declare data.access on every page under the shell', () => {
    expect(auditShellRoutes(appRoutes)).toEqual([]);
  });

  it('mount the shell once, for Hrms', () => {
    const shells = appRoutes.filter((r) => r.data?.['shellRoot'] === true);

    expect(shells).toHaveLength(1);
    expect(shells[0].data?.['app']).toBe('Hrms');
  });

  it('mount the shared settings pages rather than copies of them', () => {
    const paths = shells(appRoutes);

    expect(paths).toContain('settings/users');
    expect(paths).toContain('settings/applications');
  });
});

function shells(routes: typeof appRoutes): string[] {
  return (routes.find((r) => r.data?.['shellRoot'] === true)?.children ?? []).map((r) => r.path ?? '');
}
