import { DestroyRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppUpdateService, RESUME_CHECK_GAP_MS } from './app-update.service';
import { DesktopUpdateResult } from './desktop-bridge';
import { AppPlatform, UPDATE_PLATFORM, UpdatePlatform } from './update-platform';

/** Lets every pending promise and the first HTTP call go out. */
const settle = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));

class FakePlatform implements UpdatePlatform {
  version: string | null = '1.0.0';
  /** False when no update address is configured. */
  configured = true;
  /** Each fetch waits here until the test answers it. */
  pending: { resolve: (body: unknown) => void; reject: (error: Error) => void }[] = [];
  fetches = 0;
  resume: (() => void) | null = null;
  checkRequested: (() => void) | null = null;
  back: ((canGoBack: boolean) => void) | null = null;
  progress: ((percent: number) => void) | null = null;
  download$: Promise<DesktopUpdateResult> = Promise.resolve({ available: true, version: '1.2.0' });
  installs = 0;
  stores: string[] = [];
  exits = 0;
  unsubscribed = 0;

  constructor(readonly platform: AppPlatform) {}

  installedVersion(): Promise<string | null> {
    return Promise.resolve(this.version);
  }

  fetchManifest(): Promise<unknown> {
    if (!this.configured) {
      return Promise.resolve(null);
    }

    this.fetches++;
    return new Promise((resolve, reject) => this.pending.push({ resolve, reject }));
  }

  onResume(listener: () => void): () => void {
    this.resume = listener;
    return () => this.unsubscribed++;
  }

  onCheckRequested(listener: () => void): () => void {
    this.checkRequested = listener;
    return () => this.unsubscribed++;
  }

  onBackButton(listener: (canGoBack: boolean) => void): () => void {
    this.back = listener;
    return () => this.unsubscribed++;
  }

  onDownloadProgress(listener: (percent: number) => void): () => void {
    this.progress = listener;
    return () => this.unsubscribed++;
  }

  download(): Promise<DesktopUpdateResult> {
    return this.download$;
  }

  install(): Promise<void> {
    this.installs++;
    return Promise.resolve();
  }

  openStore(url: string): void {
    this.stores.push(url);
  }

  exitApp(): void {
    this.exits++;
  }
}

const section = (latestVersion: string, extra: Record<string, unknown> = {}): Record<string, unknown> => ({
  latestVersion,
  minSupportedVersion: '0.5.0',
  forceUpdate: false,
  releaseNotes: 'Notes.',
  ...extra,
});

describe('AppUpdateService', () => {
  let device: FakePlatform;
  let destroyers: (() => void)[];
  let now: number;

  const destroyRef = (): DestroyRef =>
    ({
      onDestroy: (fn: () => void) => {
        destroyers.push(fn);
        return () => undefined;
      },
    }) as unknown as DestroyRef;

  const setUp = (platform: AppPlatform): AppUpdateService => {
    device = new FakePlatform(platform);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: UPDATE_PLATFORM, useValue: () => Promise.resolve(device) }],
    });
    return TestBed.inject(AppUpdateService);
  };

  /** Answers the one outstanding manifest fetch with `body` for the platform's section. */
  const answer = async (key: string, body: Record<string, unknown>): Promise<void> => {
    await settle();
    expect(device.pending).toHaveLength(1);
    device.pending.shift()?.resolve({ [key]: body });
    await settle();
  };

  beforeEach(() => {
    destroyers = [];
    now = 1_000_000;
    vi.spyOn(Date, 'now').mockImplementation(() => now);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('does nothing in a browser', async () => {
    const service = setUp('web');

    await service.start(destroyRef());

    expect(device.fetches).toBe(0);
    expect(service.snapshot()).toMatchObject({ platform: 'web', visible: false, phase: 'idle' });
  });

  it('checks at startup and blocks a copy below minSupportedVersion', async () => {
    const service = setUp('electron');

    const started = service.start(destroyRef());
    await answer('electron', section('1.2.0', { minSupportedVersion: '1.1.0' }));
    await started;

    expect(service.isForcedNow()).toBe(true);
    expect(await firstValueFrom(service.isForced$)).toBe(true);
    expect(await firstValueFrom(service.updateAvailable$)).toBe(true);
    expect(service.snapshot()).toMatchObject({ visible: true, phase: 'available' });
  });

  it('reads the manifest through the platform, never through the page', async () => {
    // The page's HttpClient would need CORS from a plain file server and would
    // carry the user's token to it; the service has no HttpClient at all.
    const service = setUp('electron');

    const started = service.start(destroyRef());
    await answer('electron', section('1.2.0'));
    await started;

    expect(device.fetches).toBe(1);
  });

  it('reads only its own platform section', async () => {
    const service = setUp('android');

    const started = service.start(destroyRef());
    await answer('electron', section('9.0.0', { forceUpdate: true }));
    await started;

    expect(service.snapshot().decision.kind).toBe('none');
  });

  it('offers an optional update; Later hides it until a newer version appears', async () => {
    const service = setUp('electron');
    const started = service.start(destroyRef());
    await answer('electron', section('1.2.0'));
    await started;

    expect(service.snapshot()).toMatchObject({ visible: true, decision: { kind: 'optional' } });

    service.later();
    expect(service.snapshot().visible).toBe(false);
    expect(await firstValueFrom(service.updateAvailable$)).toBe(true);

    now += RESUME_CHECK_GAP_MS;
    device.resume?.();
    await answer('electron', section('1.2.0'));
    expect(service.snapshot().visible).toBe(false);

    now += RESUME_CHECK_GAP_MS;
    device.resume?.();
    await answer('electron', section('1.3.0'));
    expect(service.snapshot()).toMatchObject({ visible: true, decision: { latestVersion: '1.3.0' } });
  });

  it('becomes forced on resume once the manifest says so', async () => {
    const service = setUp('ios');
    const started = service.start(destroyRef());
    await answer('ios', section('1.2.0'));
    await started;
    service.later();

    now += RESUME_CHECK_GAP_MS;
    device.resume?.();
    await answer('ios', section('1.2.0', { forceUpdate: true }));

    expect(service.snapshot()).toMatchObject({ visible: true, decision: { kind: 'forced' } });
  });

  it('does not check again on a resume within a minute of the last check', async () => {
    const service = setUp('electron');
    const started = service.start(destroyRef());
    await answer('electron', section('1.2.0'));
    await started;

    now += RESUME_CHECK_GAP_MS - 1;
    device.resume?.();
    await settle();

    expect(device.fetches).toBe(1);
  });

  it('offers a dismissed update again when the user asks to check', async () => {
    const service = setUp('electron');
    const started = service.start(destroyRef());
    await answer('electron', section('1.2.0'));
    await started;
    service.later();

    device.checkRequested?.();
    await answer('electron', section('1.2.0'));

    expect(service.snapshot().visible).toBe(true);
  });

  it('never lets Later hide a forced update', async () => {
    const service = setUp('electron');
    const started = service.start(destroyRef());
    await answer('electron', section('1.2.0', { forceUpdate: true }));
    await started;

    service.later();

    expect(service.snapshot().visible).toBe(true);
  });

  it('keeps what it decided when a later check fails', async () => {
    const service = setUp('electron');
    const started = service.start(destroyRef());
    await answer('electron', section('1.2.0', { forceUpdate: true }));
    await started;

    now += RESUME_CHECK_GAP_MS;
    device.resume?.();
    await settle();
    device.pending.shift()?.reject(new Error('offline'));
    await settle();

    expect(service.isForcedNow()).toBe(true);
  });

  it('decides nothing when no update address is configured', async () => {
    const service = setUp('android');
    device.configured = false;

    await service.start(destroyRef());
    await settle();

    expect(device.fetches).toBe(0);
    expect(service.snapshot().decision.kind).toBe('none');
  });

  describe('on a desktop', () => {
    const ready = async (): Promise<AppUpdateService> => {
      const service = setUp('electron');
      const started = service.start(destroyRef());
      await answer('electron', section('1.2.0', { forceUpdate: true }));
      await started;
      return service;
    };

    it('downloads with progress, then installs on request', async () => {
      const service = await ready();
      let finish: (result: DesktopUpdateResult) => void = () => undefined;
      device.download$ = new Promise((resolve) => (finish = resolve));

      const updating = service.updateNow();
      expect(service.snapshot()).toMatchObject({ phase: 'downloading', progress: 0 });

      device.progress?.(41.6);
      expect(await firstValueFrom(service.downloadProgress$)).toBe(42);

      finish({ available: true, version: '1.2.0' });
      await updating;
      expect(service.snapshot()).toMatchObject({ phase: 'ready', progress: 100 });

      await service.updateNow();
      expect(device.installs).toBe(1);
    });

    it('says so when the update server has nothing newer yet', async () => {
      const service = await ready();
      device.download$ = Promise.resolve({ available: false, version: '1.0.0' });

      await service.updateNow();

      expect(service.snapshot().phase).toBe('error');
      expect(service.snapshot().error).toContain('does not have the new version yet');
      expect(service.isForcedNow()).toBe(true);
    });

    it("shows the main process's own sentence when the download fails", async () => {
      const service = await ready();
      device.download$ = Promise.reject(new Error('No update server is set for this desktop app.'));

      await service.updateNow();

      expect(service.snapshot()).toMatchObject({ phase: 'error', error: 'No update server is set for this desktop app.' });
    });

    it('keeps a download going across a check that finds the same version', async () => {
      const service = await ready();
      device.download$ = new Promise(() => undefined);
      void service.updateNow();

      now += RESUME_CHECK_GAP_MS;
      device.resume?.();
      await answer('electron', section('1.2.0', { forceUpdate: true }));

      expect(service.snapshot().phase).toBe('downloading');
    });
  });

  describe('on a phone or tablet', () => {
    it('opens the store for the update', async () => {
      const service = setUp('android');
      const started = service.start(destroyRef());
      await answer('android', section('1.2.0', { forceUpdate: true, storeUrl: 'market://details?id=com.billbook.app' }));
      await started;

      await service.updateNow();

      expect(device.stores).toEqual(['market://details?id=com.billbook.app']);
      expect(service.isForcedNow()).toBe(true);
    });

    it('explains when the manifest names no store', async () => {
      const service = setUp('ios');
      const started = service.start(destroyRef());
      await answer('ios', section('1.2.0'));
      await started;

      await service.updateNow();

      expect(device.stores).toEqual([]);
      expect(service.snapshot().phase).toBe('error');
    });
  });

  describe("Android's back button", () => {
    const startWith = async (body: Record<string, unknown>): Promise<AppUpdateService> => {
      const service = setUp('android');
      const started = service.start(destroyRef());
      await answer('android', body);
      await started;
      return service;
    };

    it('does nothing at all while an update is forced', async () => {
      const back = vi.spyOn(globalThis.history, 'back').mockImplementation(() => undefined);
      const service = await startWith(section('1.2.0', { forceUpdate: true }));

      device.back?.(true);
      device.back?.(false);

      expect(back).not.toHaveBeenCalled();
      expect(device.exits).toBe(0);
      expect(service.snapshot().visible).toBe(true);
    });

    it('means Later while an optional update is showing', async () => {
      const service = await startWith(section('1.2.0'));

      device.back?.(true);

      expect(service.snapshot().visible).toBe(false);
    });

    it('goes back through the history, and leaves the app from its first page', async () => {
      const back = vi.spyOn(globalThis.history, 'back').mockImplementation(() => undefined);
      await startWith(section('1.0.0'));

      device.back?.(true);
      expect(back).toHaveBeenCalledTimes(1);

      device.back?.(false);
      expect(device.exits).toBe(1);
    });
  });

  it('starts once, and stops listening when its owner is destroyed', async () => {
    const service = setUp('electron');
    const ref = destroyRef();

    const first = service.start(ref);
    expect(service.start(ref)).toBe(first);
    await answer('electron', section('1.0.0'));
    await first;

    destroyers.forEach((fn) => fn());
    expect(device.unsubscribed).toBe(4);
  });
});
