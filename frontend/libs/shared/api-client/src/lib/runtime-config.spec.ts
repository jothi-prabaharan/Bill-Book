import { afterEach, describe, expect, it } from 'vitest';
import { resolveApiBaseUrl, resolveUpdateManifestUrl } from './runtime-config';

/**
 * The override has to be safe in every shape a deployment can leave it: absent,
 * empty, half-filled, or wrong. A bundle that will not start because ops did not
 * write a config file is worse than one that uses its build-time default.
 */
describe('resolveApiBaseUrl', () => {
  afterEach(() => {
    delete window.__BB_CONFIG__;
  });

  it('uses the build-time value when the deployment set nothing', () => {
    expect(resolveApiBaseUrl('https://built-in')).toBe('https://built-in');
  });

  it('prefers the deployment value over the build-time one', () => {
    window.__BB_CONFIG__ = { apiBaseUrl: 'https://gateway.example' };

    expect(resolveApiBaseUrl('https://built-in')).toBe('https://gateway.example');
  });

  it('honours an explicit empty origin, which means same-origin', () => {
    // Not the same as unset: a deployment served from behind the gateway says
    // so by setting this to empty, and falling back would send its calls to
    // whatever the build happened to name.
    window.__BB_CONFIG__ = { apiBaseUrl: '' };

    expect(resolveApiBaseUrl('https://built-in')).toBe('');
  });

  it('falls back when the config exists but names no origin', () => {
    window.__BB_CONFIG__ = {};

    expect(resolveApiBaseUrl('https://built-in')).toBe('https://built-in');
  });

  it('falls back when the config holds something that is not a string', () => {
    (window as { __BB_CONFIG__?: unknown }).__BB_CONFIG__ = { apiBaseUrl: 42 };

    expect(resolveApiBaseUrl('https://built-in')).toBe('https://built-in');
  });
});

describe('resolveUpdateManifestUrl', () => {
  afterEach(() => {
    delete window.__BB_CONFIG__;
  });

  it('is null in a browser deployment that set nothing', () => {
    expect(resolveUpdateManifestUrl()).toBeNull();
  });

  it('reads the address the mobile build wrote', () => {
    window.__BB_CONFIG__ = { updateManifestUrl: ' https://updates.example/version.json ' };

    expect(resolveUpdateManifestUrl()).toBe('https://updates.example/version.json');
  });

  it('treats blank and non-string values as unset', () => {
    window.__BB_CONFIG__ = { updateManifestUrl: '  ' };
    expect(resolveUpdateManifestUrl()).toBeNull();

    (window as { __BB_CONFIG__?: unknown }).__BB_CONFIG__ = { updateManifestUrl: 7 };
    expect(resolveUpdateManifestUrl()).toBeNull();
  });
});
