import { describe, expect, it } from 'vitest';
import { resolveAppClient, resolveAppPlatform, APP_HEADER, PLATFORM_HEADER } from './client-platform';

describe('client-platform', () => {
  it('defines the standard header names', () => {
    expect(APP_HEADER).toBe('X-App');
    expect(PLATFORM_HEADER).toBe('X-Platform');
  });

  it('resolves web client by default', () => {
    expect(resolveAppClient()).toBe('web');
  });

  it('resolves platform for electron', () => {
    expect(resolveAppPlatform('electron')).toBeDefined();
  });

  it('resolves platform for ionic', () => {
    expect(resolveAppPlatform('ionic')).toBeDefined();
  });

  it('resolves platform for web', () => {
    expect(resolveAppPlatform('web')).toBeDefined();
  });
});
