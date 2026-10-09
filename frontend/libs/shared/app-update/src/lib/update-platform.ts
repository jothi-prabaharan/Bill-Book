import { InjectionToken } from '@angular/core';
import { resolveUpdateManifestUrl } from '@bill-book/api-client';
import { App } from '@capacitor/app';
import { Capacitor, CapacitorHttp, PluginListenerHandle } from '@capacitor/core';
import { Device } from '@capacitor/device';
import { DesktopBridge, DesktopUpdateResult, desktopBridge } from './desktop-bridge';
import { UpdateChannel } from './update-manifest';

export type AppPlatform = 'web' | 'electron' | 'android' | 'ios';

/** The `version.json` section a platform reads; a browser reads none. */
export function channelFor(platform: AppPlatform): UpdateChannel | null {
  return platform === 'web' ? null : platform;
}

/**
 * Everything the update service needs from the device it runs on (TK-184).
 * One implementation per platform, so the service itself is plain RxJS and is
 * tested with a fake.
 */
export interface UpdatePlatform {
  readonly platform: AppPlatform;
  installedVersion(): Promise<string | null>;
  /**
   * Reads `version.json`, natively rather than through the page, so the update
   * server needs no CORS headers and never sees the user's token. Null when no
   * update address is configured.
   */
  fetchManifest(): Promise<unknown>;
  /** Back from the background: the app resumed, the window was shown again, the computer woke. */
  onResume(listener: () => void): () => void;
  /** The user asked to check now (the desktop's Help menu). */
  onCheckRequested(listener: () => void): () => void;
  /** Android's hardware back button; never fires elsewhere. */
  onBackButton(listener: (canGoBack: boolean) => void): () => void;
  onDownloadProgress(listener: (percent: number) => void): () => void;
  /** Desktop only: fetch the new build from the update server. */
  download(): Promise<DesktopUpdateResult>;
  /** Desktop only: restart into the downloaded build. */
  install(): Promise<void>;
  /** Phone and tablet only: hand over to the app store. */
  openStore(url: string): void;
  exitApp(): void;
}

const NOTHING = (): void => undefined;

export const WEB_PLATFORM: UpdatePlatform = {
  platform: 'web',
  installedVersion: () => Promise.resolve(null),
  fetchManifest: () => Promise.resolve(null),
  onResume: () => NOTHING,
  onCheckRequested: () => NOTHING,
  onBackButton: () => NOTHING,
  onDownloadProgress: () => NOTHING,
  download: () => Promise.resolve({ available: false, version: null }),
  install: () => Promise.resolve(),
  openStore: NOTHING,
  exitApp: NOTHING,
};

export class ElectronUpdatePlatform implements UpdatePlatform {
  readonly platform = 'electron' as const;
  private info?: ReturnType<DesktopBridge['info']>;

  constructor(
    private readonly bridge: DesktopBridge,
    private readonly doc: Document,
  ) {}

  async installedVersion(): Promise<string | null> {
    return (await this.desktopInfo()).version;
  }

  fetchManifest(): Promise<unknown> {
    return this.bridge.fetchManifest();
  }

  onResume(listener: () => void): () => void {
    const offWake = this.bridge.onResume(listener);
    const shown = (): void => {
      if (this.doc.visibilityState === 'visible') {
        listener();
      }
    };
    this.doc.addEventListener('visibilitychange', shown);

    return () => {
      offWake();
      this.doc.removeEventListener('visibilitychange', shown);
    };
  }

  onCheckRequested(listener: () => void): () => void {
    return this.bridge.onCheckRequested(listener);
  }

  onBackButton(): () => void {
    return NOTHING;
  }

  onDownloadProgress(listener: (percent: number) => void): () => void {
    return this.bridge.onDownloadProgress(listener);
  }

  download(): Promise<DesktopUpdateResult> {
    return this.bridge.downloadUpdate();
  }

  install(): Promise<void> {
    return this.bridge.installUpdate();
  }

  openStore(): void {
    // A desktop build updates itself; there is no store.
  }

  exitApp(): void {
    // Closing the window is the user's own action on a desktop.
  }

  private desktopInfo(): ReturnType<DesktopBridge['info']> {
    this.info ??= this.bridge.info();
    return this.info;
  }
}

export class CapacitorUpdatePlatform implements UpdatePlatform {
  constructor(
    readonly platform: 'android' | 'ios',
    private readonly win: Window,
  ) {}

  async installedVersion(): Promise<string | null> {
    return (await App.getInfo()).version || null;
  }

  async fetchManifest(): Promise<unknown> {
    const url = resolveUpdateManifestUrl();

    if (!url) {
      return null;
    }

    // Native HTTP: the web view's origin is https://localhost or
    // capacitor://localhost, which a plain file server would refuse by CORS.
    const response = await CapacitorHttp.get({ url, params: { t: String(Date.now()) }, responseType: 'json' });

    if (response.status < 200 || response.status >= 300) {
      throw new Error(`The update server answered ${response.status}.`);
    }

    return typeof response.data === 'string' ? JSON.parse(response.data) : response.data;
  }

  onResume(listener: () => void): () => void {
    return this.listen(App.addListener('resume', listener));
  }

  onCheckRequested(): () => void {
    return NOTHING;
  }

  onBackButton(listener: (canGoBack: boolean) => void): () => void {
    return this.listen(App.addListener('backButton', (event) => listener(event.canGoBack)));
  }

  onDownloadProgress(): () => void {
    return NOTHING;
  }

  download(): Promise<DesktopUpdateResult> {
    return Promise.resolve({ available: false, version: null });
  }

  install(): Promise<void> {
    return Promise.resolve();
  }

  openStore(url: string): void {
    // A market:// or itms-apps:// address leaves the web view: Capacitor hands
    // any scheme that is not its own to the operating system.
    this.win.location.assign(url);
  }

  exitApp(): void {
    void App.exitApp();
  }

  private listen(handle: Promise<PluginListenerHandle>): () => void {
    return () => {
      void handle.then((h) => h.remove());
    };
  }
}

/**
 * Which platform this copy is running on. The desktop bridge is checked first:
 * the preload script is the only thing that can put it there. A native
 * Capacitor shell is confirmed with the Device plugin, which reports the
 * operating system rather than guessing from the user agent.
 */
export async function resolveUpdatePlatform(win: Window | null = globalThis.window ?? null): Promise<UpdatePlatform> {
  const bridge = desktopBridge();

  if (bridge && win) {
    return new ElectronUpdatePlatform(bridge, win.document);
  }

  if (win && Capacitor.isNativePlatform()) {
    const { platform } = await Device.getInfo();

    if (platform === 'android' || platform === 'ios') {
      return new CapacitorUpdatePlatform(platform, win);
    }
  }

  return WEB_PLATFORM;
}

export const UPDATE_PLATFORM = new InjectionToken<() => Promise<UpdatePlatform>>('UPDATE_PLATFORM', {
  providedIn: 'root',
  factory: () => resolveUpdatePlatform,
});
