import { describe, expect, it } from 'vitest';
import { PlatformRelease, decideUpdate, parseManifest } from './update-manifest';

const release = (over: Partial<PlatformRelease> = {}): PlatformRelease => ({
  latestVersion: '1.2.0',
  minSupportedVersion: '1.1.0',
  forceUpdate: false,
  releaseNotes: 'Fixes.',
  storeUrl: null,
  ...over,
});

describe('parseManifest', () => {
  it('reads the three sections of version.json', () => {
    const manifest = parseManifest({
      electron: { latestVersion: '1.2.0', minSupportedVersion: '1.1.0', forceUpdate: true, releaseNotes: 'Critical fixes.' },
      android: { latestVersion: '1.2.0', minSupportedVersion: '1.1.0', forceUpdate: false, storeUrl: 'market://details?id=x' },
      ios: { latestVersion: '1.2.0', storeUrl: 'itms-apps://itunes.apple.com/app/id1' },
    });

    expect(manifest.electron).toEqual({
      latestVersion: '1.2.0',
      minSupportedVersion: '1.1.0',
      forceUpdate: true,
      releaseNotes: 'Critical fixes.',
      storeUrl: null,
    });
    expect(manifest.android?.storeUrl).toBe('market://details?id=x');
    expect(manifest.ios?.minSupportedVersion).toBeNull();
    expect(manifest.ios?.forceUpdate).toBe(false);
  });

  it('drops a section without a usable latestVersion and keeps the others', () => {
    const manifest = parseManifest({ electron: { latestVersion: 'soon' }, android: { latestVersion: '2.0.0' } });

    expect(manifest.electron).toBeUndefined();
    expect(manifest.android?.latestVersion).toBe('2.0.0');
  });

  it('treats only a literal true as forcing', () => {
    expect(parseManifest({ ios: { latestVersion: '1.0.0', forceUpdate: 'true' } }).ios?.forceUpdate).toBe(false);
  });

  it('answers an empty manifest for anything that is not an object', () => {
    expect(parseManifest(null)).toEqual({});
    expect(parseManifest('version.json')).toEqual({});
    expect(parseManifest([1, 2])).toEqual({});
  });
});

describe('decideUpdate', () => {
  it('forces an update below minSupportedVersion even when forceUpdate is false', () => {
    expect(decideUpdate('1.0.9', release())).toMatchObject({ kind: 'forced', reason: 'below-minimum' });
  });

  it('forces an update below latestVersion when forceUpdate is true', () => {
    expect(decideUpdate('1.1.5', release({ forceUpdate: true }))).toMatchObject({ kind: 'forced', reason: 'force-flag' });
  });

  it('offers an optional update below latestVersion when forceUpdate is false', () => {
    expect(decideUpdate('1.1.5', release())).toMatchObject({
      kind: 'optional',
      reason: 'newer',
      installedVersion: '1.1.5',
      latestVersion: '1.2.0',
      releaseNotes: 'Fixes.',
    });
  });

  it('never blocks a copy that is already current, whatever forceUpdate says', () => {
    expect(decideUpdate('1.2.0', release({ forceUpdate: true }))).toMatchObject({ kind: 'none', reason: 'up-to-date' });
    expect(decideUpdate('1.3.0', release({ forceUpdate: true }))).toMatchObject({ kind: 'none' });
  });

  it('decides nothing when the installed version cannot be read', () => {
    expect(decideUpdate(null, release({ forceUpdate: true }))).toMatchObject({ kind: 'none', reason: 'unknown-version' });
    expect(decideUpdate('dev', release({ forceUpdate: true }))).toMatchObject({ kind: 'none', reason: 'unknown-version' });
  });

  it('decides nothing when the platform has no section', () => {
    expect(decideUpdate('1.0.0', undefined)).toMatchObject({ kind: 'none', reason: 'no-release' });
  });

  it('ignores a minSupportedVersion that is not a version', () => {
    expect(decideUpdate('1.0.0', release({ minSupportedVersion: 'n/a' }))).toMatchObject({ kind: 'optional' });
  });
});
