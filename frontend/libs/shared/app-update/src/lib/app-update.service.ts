import { DestroyRef, Injectable, NgZone, inject } from '@angular/core';
import { BehaviorSubject, Observable, distinctUntilChanged, map } from 'rxjs';
import { NO_UPDATE, UpdateDecision, decideUpdate, parseManifest } from './update-manifest';
import { AppPlatform, UPDATE_PLATFORM, UpdatePlatform, channelFor } from './update-platform';

export type UpdatePhase = 'idle' | 'available' | 'downloading' | 'ready' | 'error';

export interface UpdateState {
  readonly platform: AppPlatform;
  readonly decision: UpdateDecision;
  readonly phase: UpdatePhase;
  /** Whole percent while a desktop build downloads; null otherwise. */
  readonly progress: number | null;
  readonly error: string | null;
  /** Whether the modal is on screen. A forced update is never hidden. */
  readonly visible: boolean;
}

const INITIAL: UpdateState = {
  platform: 'web',
  decision: NO_UPDATE,
  phase: 'idle',
  progress: null,
  error: null,
  visible: false,
};

/** A resume within this long of the last check does not check again. */
export const RESUME_CHECK_GAP_MS = 60_000;

/**
 * Checks `version.json` at startup and on every return from the background,
 * and drives the update modal (TK-184).
 *
 * The manifest is read by the platform natively (Electron's main process,
 * Capacitor's HTTP plugin), never through Angular's `HttpClient`: an update
 * server is a plain file server with no CORS headers, and the auth interceptor
 * would attach the user's token to a request bound for it.
 *
 * A failed check changes nothing. Being offline must not unblock a copy that
 * was already found to be too old, and must not block one that was not.
 */
@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly resolvePlatform = inject(UPDATE_PLATFORM);
  private readonly zone = inject(NgZone);

  private readonly state = new BehaviorSubject<UpdateState>(INITIAL);
  private device: UpdatePlatform | null = null;
  private started: Promise<void> | null = null;
  private lastCheck = 0;
  private dismissed: string | null = null;

  readonly state$: Observable<UpdateState> = this.state.asObservable();
  readonly updateAvailable$ = this.select((s) => s.decision.kind !== 'none');
  readonly isForced$ = this.select((s) => s.decision.kind === 'forced');
  readonly downloadProgress$ = this.select((s) => s.progress);
  readonly visible$ = this.select((s) => s.visible);

  /** Starts checking: now, and on every resume until `destroyRef` ends. Calling it again does nothing. */
  start(destroyRef: DestroyRef): Promise<void> {
    this.started ??= this.begin(destroyRef);
    return this.started;
  }

  /** The synchronous answer the route guard needs. */
  isForcedNow(): boolean {
    return this.state.value.decision.kind === 'forced';
  }

  snapshot(): UpdateState {
    return this.state.value;
  }

  async check(): Promise<void> {
    const device = this.device;
    const channel = device ? channelFor(device.platform) : null;

    if (!device || !channel) {
      return;
    }

    this.lastCheck = Date.now();

    try {
      const [installed, raw] = await Promise.all([device.installedVersion(), device.fetchManifest()]);

      if (raw == null) {
        return;
      }

      this.apply(decideUpdate(installed, parseManifest(raw)[channel]));
    } catch {
      // Offline, or the server is down: keep whatever was decided last.
    }
  }

  /** "Later": hides an optional update until a newer version appears. A forced one stays. */
  later(): void {
    const current = this.state.value;

    if (current.decision.kind !== 'optional' || current.phase === 'downloading') {
      return;
    }

    this.dismissed = current.decision.latestVersion;
    this.patch({ visible: false });
  }

  /** "Update now": downloads on a desktop, opens the store on a phone or tablet. */
  async updateNow(): Promise<void> {
    const device = this.device;
    const current = this.state.value;

    if (!device || current.decision.kind === 'none' || current.phase === 'downloading') {
      return;
    }

    if (device.platform === 'android' || device.platform === 'ios') {
      if (current.decision.storeUrl) {
        device.openStore(current.decision.storeUrl);
      } else {
        this.patch({ phase: 'error', error: 'No store address is set for this update. Contact your administrator.' });
      }
      return;
    }

    if (current.phase === 'ready') {
      await this.install();
      return;
    }

    this.patch({ phase: 'downloading', progress: 0, error: null });

    try {
      const result = await device.download();

      this.patch(
        result.available
          ? { phase: 'ready', progress: 100 }
          : {
              phase: 'error',
              progress: null,
              error: 'The update server does not have the new version yet. Try again in a few minutes.',
            },
      );
    } catch (error) {
      this.patch({ phase: 'error', progress: null, error: messageOf(error) });
    }
  }

  async install(): Promise<void> {
    if (this.state.value.phase !== 'ready' || !this.device) {
      return;
    }

    try {
      await this.device.install();
    } catch (error) {
      this.patch({ phase: 'error', error: messageOf(error) });
    }
  }

  private async begin(destroyRef: DestroyRef): Promise<void> {
    let device: UpdatePlatform;

    try {
      device = await this.resolvePlatform();
    } catch {
      return;
    }

    this.device = device;
    this.patch({ platform: device.platform });

    if (!channelFor(device.platform)) {
      return;
    }

    // Device events arrive outside Angular; bring them back in so the modal redraws.
    const inZone =
      <A extends unknown[]>(fn: (...args: A) => void) =>
      (...args: A) =>
        this.zone.run(() => fn(...args));

    const offs = [
      device.onResume(
        inZone(() => {
          if (Date.now() - this.lastCheck >= RESUME_CHECK_GAP_MS) {
            void this.check();
          }
        }),
      ),
      device.onCheckRequested(
        inZone(() => {
          // Asked for by name: an update put off with Later is offered again.
          this.dismissed = null;
          void this.check();
        }),
      ),
      device.onDownloadProgress(
        inZone((percent: number) => {
          if (this.state.value.phase === 'downloading') {
            this.patch({ progress: Math.max(0, Math.min(100, Math.round(percent))) });
          }
        }),
      ),
      device.onBackButton(inZone((canGoBack: boolean) => this.back(canGoBack))),
    ];
    destroyRef.onDestroy(() => offs.forEach((off) => off()));

    await this.check();
  }

  /**
   * Android's back button. Registering a listener replaces Capacitor's own
   * handling, so the ordinary behaviour is kept here: back through the
   * history, and leave the app from its first page. A forced update swallows
   * the press.
   */
  private back(canGoBack: boolean): void {
    const current = this.state.value;

    if (current.decision.kind === 'forced') {
      return;
    }

    if (current.visible) {
      this.later();
      return;
    }

    if (canGoBack) {
      globalThis.history?.back();
    } else {
      this.device?.exitApp();
    }
  }

  private apply(decision: UpdateDecision): void {
    const previous = this.state.value;
    const sameVersion = previous.decision.latestVersion === decision.latestVersion;
    const keepPhase =
      sameVersion && (previous.phase === 'downloading' || previous.phase === 'ready');

    if (decision.kind === 'none') {
      this.patch({ decision, phase: 'idle', progress: null, error: null, visible: false });
      return;
    }

    const visible =
      decision.kind === 'forced' || keepPhase || this.dismissed !== decision.latestVersion;

    this.patch({
      decision,
      visible,
      phase: keepPhase ? previous.phase : 'available',
      progress: keepPhase ? previous.progress : null,
      error: keepPhase ? previous.error : null,
    });
  }

  private patch(change: Partial<UpdateState>): void {
    this.state.next({ ...this.state.value, ...change });
  }

  private select<T>(pick: (state: UpdateState) => T): Observable<T> {
    return this.state.pipe(map(pick), distinctUntilChanged());
  }
}

function messageOf(error: unknown): string {
  const text = error instanceof Error ? error.message : typeof error === 'string' ? error : '';
  return text.trim() !== '' ? text : 'The update could not be downloaded. Check the connection and try again.';
}
