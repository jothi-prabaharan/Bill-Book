import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { appRoutes } from './app.routes';

/**
 * Every page under the shell declares its access (TK-44). A page that declares
 * nothing is refused at run time; this makes it a failing build instead.
 */
describe('apps/web routes', () => {
  it('declare data.access on every page under the shell', () => {
    expect(auditShellRoutes(appRoutes)).toEqual([]);
  });

  it('mount the shell once, for RetailErp', () => {
    const shells = appRoutes.filter((r) => r.data?.['shellRoot'] === true);

    expect(shells).toHaveLength(1);
    expect(shells[0].data?.['app']).toBe('RetailErp');
  });
});
