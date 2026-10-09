import { describe, expect, it } from 'vitest';
import { httpUrl, parseConfigLayer, resolveDesktopConfig } from './desktop-config';

describe('resolveDesktopConfig', () => {
  it('uses what the installer was built with', () => {
    expect(
      resolveDesktopConfig({ serverUrl: 'https://books.example.com/', updateUrl: 'http://10.0.0.5:8080/updates/' }, null, {}),
    ).toEqual({
      serverUrl: 'https://books.example.com',
      updateUrl: 'http://10.0.0.5:8080/updates',
      updateManifestUrl: 'http://10.0.0.5:8080/updates/version.json',
      devUrl: null,
    });
  });

  it("lets the computer's own file override the installer, and the environment override both", () => {
    const config = resolveDesktopConfig(
      { serverUrl: 'https://built.example', updateUrl: 'https://built.example/updates' },
      { serverUrl: 'https://site.example' },
      { BILLBOOK_UPDATE_URL: 'https://env.example/updates' },
    );

    expect(config.serverUrl).toBe('https://site.example');
    expect(config.updateUrl).toBe('https://env.example/updates');
    expect(config.updateManifestUrl).toBe('https://env.example/updates/version.json');
  });

  it('takes an explicit manifest address over the one beside the installers', () => {
    expect(
      resolveDesktopConfig({ updateUrl: 'https://u.example', updateManifestUrl: 'https://api.example/app/version.json' }, null, {})
        .updateManifestUrl,
    ).toBe('https://api.example/app/version.json');
  });

  it('ignores values that are not http(s) addresses', () => {
    expect(resolveDesktopConfig({ serverUrl: 'file:///etc/passwd', updateUrl: 42 }, null, {})).toEqual({
      serverUrl: null,
      updateUrl: null,
      updateManifestUrl: null,
      devUrl: null,
    });
  });

  it('reads the dev server only from the environment', () => {
    expect(resolveDesktopConfig(null, null, { BILLBOOK_DEV_URL: 'http://localhost:4200' }).devUrl).toBe('http://localhost:4200');
  });
});

describe('httpUrl', () => {
  it('trims and drops a trailing slash', () => {
    expect(httpUrl('  https://a.example/updates/  ')).toBe('https://a.example/updates');
  });

  it('refuses blanks, other schemes and nonsense', () => {
    expect(httpUrl('')).toBeNull();
    expect(httpUrl('ftp://a.example')).toBeNull();
    expect(httpUrl('books')).toBeNull();
    expect(httpUrl(undefined)).toBeNull();
  });
});

describe('parseConfigLayer', () => {
  it('reads a JSON object and ignores anything else', () => {
    expect(parseConfigLayer('{"serverUrl":"https://a.example"}')).toEqual({ serverUrl: 'https://a.example' });
    expect(parseConfigLayer('not json')).toBeNull();
    expect(parseConfigLayer('null')).toBeNull();
    expect(parseConfigLayer(null)).toBeNull();
  });
});
