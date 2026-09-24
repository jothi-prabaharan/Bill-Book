import { auditShellRoutes } from '@bill-book/app-shell';
import { describe, expect, it } from 'vitest';
import { sharedSettingsRoutes } from './shared-settings.routes';

/** The shared settings pages every app mounts (TK-47). */
describe('sharedSettingsRoutes', () => {
  it('declare data.access on every page', () => {
    expect(auditShellRoutes(sharedSettingsRoutes)).toEqual([]);
  });

  it('open every page on settings.view, which every app may hold', () => {
    for (const route of sharedSettingsRoutes) {
      expect(route.data?.['access']).toEqual({ permission: 'settings.view' });
    }
  });
});
