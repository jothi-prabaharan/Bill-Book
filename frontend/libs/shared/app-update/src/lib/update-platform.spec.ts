import { afterEach, describe, expect, it, vi } from 'vitest';
import { DesktopBridge } from './desktop-bridge';
import { ElectronUpdatePlatform, WEB_PLATFORM, channelFor, resolveUpdatePlatform } from './update-platform';

const bridge = (over: Partial<DesktopBridge> = {}): DesktopBridge => ({
  info: vi.fn().mockResolvedValue({ version: '1.4.0', os: 'win32', updateManifestUrl: 'http://10.0.0.5:8080/updates/version.json' }),
  fetchManifest: vi.fn().mockResolvedValue({ electron: { latestVersion: '1.5.0' } }),
  printHtml: vi.fn().mockResolvedValue(undefined),
  downloadUpdate: vi.fn().mockResolvedValue({ available: true, version: '1.5.0' }),
  installUpdate: vi.fn().mockResolvedValue(undefined),
  onDownloadProgress: vi.fn().mockReturnValue(() => undefined),
  onResume: vi.fn().mockReturnValue(() => undefined),
  onCheckRequested: vi.fn().mockReturnValue(() => undefined),
  ...over,
});

afterEach(() => {
  delete window.billBookDesktop;
});

describe('channelFor', () => {
  it('maps each installable platform to its version.json section, and the browser to none', () => {
    expect(channelFor('electron')).toBe('electron');
    expect(channelFor('android')).toBe('android');
    expect(channelFor('ios')).toBe('ios');
    expect(channelFor('web')).toBeNull();
  });
});

describe('resolveUpdatePlatform', () => {
  it('is the desktop when the preload script put its bridge on the window', async () => {
    window.billBookDesktop = bridge();

    expect(await resolveUpdatePlatform(window)).toBeInstanceOf(ElectronUpdatePlatform);
  });

  it('is the browser otherwise', async () => {
    expect(await resolveUpdatePlatform(window)).toBe(WEB_PLATFORM);
    expect(await resolveUpdatePlatform(null)).toBe(WEB_PLATFORM);
  });
});

describe('ElectronUpdatePlatform', () => {
  it('reads the installed version from the desktop app, asking once', async () => {
    const desktop = bridge();
    const platform = new ElectronUpdatePlatform(desktop, document);

    expect(await platform.installedVersion()).toBe('1.4.0');
    expect(await platform.installedVersion()).toBe('1.4.0');
    expect(desktop.info).toHaveBeenCalledTimes(1);
  });

  it('has the main process fetch version.json, which needs no CORS from the update server', async () => {
    const desktop = bridge();
    const platform = new ElectronUpdatePlatform(desktop, document);

    expect(await platform.fetchManifest()).toEqual({ electron: { latestVersion: '1.5.0' } });
    expect(desktop.fetchManifest).toHaveBeenCalledTimes(1);
  });

  it('counts the window being shown again as a resume, as well as the computer waking', () => {
    let wake: () => void = () => undefined;
    const offWake = vi.fn();
    const platform = new ElectronUpdatePlatform(
      bridge({
        onResume: (listener) => {
          wake = listener;
          return offWake;
        },
      }),
      document,
    );
    const resumed = vi.fn();
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });

    const off = platform.onResume(resumed);
    wake();
    document.dispatchEvent(new Event('visibilitychange'));
    expect(resumed).toHaveBeenCalledTimes(2);

    off();
    document.dispatchEvent(new Event('visibilitychange'));
    expect(resumed).toHaveBeenCalledTimes(2);
    expect(offWake).toHaveBeenCalledTimes(1);
  });

  it('downloads and installs through the bridge', async () => {
    const desktop = bridge();
    const platform = new ElectronUpdatePlatform(desktop, document);

    expect(await platform.download()).toEqual({ available: true, version: '1.5.0' });
    await platform.install();

    expect(desktop.installUpdate).toHaveBeenCalledTimes(1);
  });
});
