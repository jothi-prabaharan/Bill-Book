import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnDestroy,
  OnInit,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationStart, Router } from '@angular/router';
import {
  BarcodeBurst,
  CAMERA_POPUP,
  SCAN_FORMATS,
  SCAN_FRAME_MS,
  SCAN_IDLE_MS,
  ScanGate,
  cameraErrorMessage,
  isScanIdle,
  normaliseScan,
} from '@bill-book/scan-core';
import { FormFieldComponent } from '../forms/form-field/form-field.component';

/** The part of the `BarcodeDetector` API this component uses, native or polyfilled. */
interface CodeDetector {
  detect(source: HTMLVideoElement): Promise<{ rawValue: string }[]>;
}

interface DetectorConstructor {
  new (options?: { formats?: string[] }): CodeDetector;
  getSupportedFormats?: () => Promise<readonly string[]>;
}

let scanFieldId = 0;

/**
 * Where a code comes in (TK-160): a field a keyboard-wedge scanner types into,
 * and on a phone a camera that reads codes in a small popup in the top-right
 * corner, so the page and its line grid stay in view.
 *
 * It only emits `scanned`. What a code means — an item, a document, a batch —
 * is the page's business, as with `bb-search-input`.
 *
 * - **Typed or scanned into the field**: Enter emits what is there and clears it.
 * - **`global`**: also catches a scanner's burst of keystrokes anywhere on the
 *   page while no other field has focus, using the same burst rule as the till.
 * - **Camera**: `getUserMedia` on the rear camera and the browser's
 *   `BarcodeDetector`; where a browser lacks it, the `barcode-detector`
 *   polyfill (MIT, D-46) is loaded on first use, its wasm served from our own
 *   `assets/zxing/`. Continuous: a short pause, a beep and a flash per scan.
 *   It closes itself after `SCAN_IDLE_MS` with nothing read, and always stops
 *   the camera on close, on navigation, when the tab is hidden and on destroy.
 *   The camera needs HTTPS; any failure is one line, and the field still works.
 */
@Component({
  selector: 'bb-scan',
  standalone: true,
  imports: [FormFieldComponent],
  templateUrl: './scan.component.html',
  styleUrl: './scan.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScanComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router, { optional: true });
  private readonly destroyRef = inject(DestroyRef);

  readonly label = input<string>('');
  readonly placeholder = input<string>('Scan or type a code');
  readonly ariaLabel = input<string>('Scan or type a code');
  readonly disabled = input<boolean>(false);
  /** Offers the camera button. On by default; the camera itself still needs HTTPS. */
  readonly camera = input<boolean>(true);
  /** Also catches a scanner's keystrokes while no other field has focus. */
  readonly global = input<boolean>(false);

  /**
   * Clears the field after each code, ready for the next — right for adding
   * lines. Off, the last code stays in the field, as a lookup box wants.
   */
  readonly clearOnScan = input<boolean>(true);

  /** A code, typed and entered or scanned, trimmed and never empty. */
  readonly scanned = output<string>();

  protected readonly controlId = `bb-scan-${++scanFieldId}`;
  protected readonly popup = CAMERA_POPUP;
  protected readonly text = signal('');
  protected readonly cameraOpen = signal(false);
  protected readonly starting = signal(false);
  protected readonly flash = signal(false);
  protected readonly message = signal<string | null>(null);

  private readonly field = viewChild<ElementRef<HTMLInputElement>>('field');
  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');

  private readonly burst = new BarcodeBurst();
  private readonly gate = new ScanGate();
  private stream: MediaStream | null = null;
  private detector: CodeDetector | null = null;
  private frameTimer: ReturnType<typeof setTimeout> | null = null;
  private lastActivity = 0;
  private audio: AudioContext | null = null;

  private readonly onDocumentKeyDown = (event: KeyboardEvent): void => this.handleGlobalKey(event);
  private readonly onVisibility = (): void => {
    if (document.visibilityState === 'hidden') {
      this.closeCamera();
    }
  };

  constructor() {
    // The video element exists once the popup has rendered; attach the stream then.
    effect(() => {
      const video = this.video()?.nativeElement;
      if (video && this.stream && video.srcObject !== this.stream) {
        void this.attachStream();
      }
    });
  }

  ngOnInit(): void {
    document.addEventListener('keydown', this.onDocumentKeyDown, true);
    document.addEventListener('visibilitychange', this.onVisibility);
    this.router?.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      if (event instanceof NavigationStart) {
        this.closeCamera();
      }
    });
  }

  ngOnDestroy(): void {
    document.removeEventListener('keydown', this.onDocumentKeyDown, true);
    document.removeEventListener('visibilitychange', this.onVisibility);
    this.closeCamera();
    void this.audio?.close();
    this.audio = null;
  }

  /** Puts the cursor in the field, ready for a scanner. */
  focus(): void {
    this.field()?.nativeElement.focus();
  }

  protected onInput(event: Event): void {
    this.text.set((event.target as HTMLInputElement).value);
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (event.key !== 'Enter') {
      return;
    }
    event.preventDefault();
    this.take(normaliseScan(this.text()));
  }

  /**
   * A burst typed while focus is on nothing editable: a scanner aimed at the
   * page. Keys typed into another field are that field's, never ours.
   */
  protected handleGlobalKey(event: KeyboardEvent): void {
    if (!this.global() || this.disabled()) {
      return;
    }
    if (isEditable(event.target)) {
      this.burst.reset();
      return;
    }
    const code = this.burst.push(event.key, event.timeStamp);
    if (code) {
      event.preventDefault();
      this.take(code);
    }
  }

  /** Emits a code, and leaves it in the field or clears the field for the next. */
  private take(code: string): void {
    this.text.set(this.clearOnScan() ? '' : code);
    if (code) {
      this.scanned.emit(code);
    }
  }

  protected async toggleCamera(): Promise<void> {
    if (this.cameraOpen()) {
      this.closeCamera();
    } else {
      await this.openCamera();
    }
  }

  async openCamera(): Promise<void> {
    if (this.cameraOpen() || this.starting() || this.disabled()) {
      return;
    }
    this.message.set(null);
    if (!window.isSecureContext) {
      this.message.set(cameraErrorMessage(null, false));
      this.focus();
      return;
    }
    if (!navigator.mediaDevices?.getUserMedia) {
      this.message.set(cameraErrorMessage('Unsupported'));
      this.focus();
      return;
    }

    this.starting.set(true);
    try {
      const [stream, detector] = await Promise.all([
        navigator.mediaDevices.getUserMedia({
          video: { facingMode: { ideal: 'environment' }, width: { ideal: 640 }, height: { ideal: 480 } },
          audio: false,
        }),
        this.detector ? Promise.resolve(this.detector) : createDetector(),
      ]);
      this.stream = stream;
      this.detector = detector;
      this.gate.reset();
      this.lastActivity = performance.now();
      this.cameraOpen.set(true);
    } catch (error) {
      this.stopStream();
      this.message.set(cameraErrorMessage(error instanceof Error ? error.name : null));
      this.focus();
    } finally {
      this.starting.set(false);
    }
  }

  closeCamera(): void {
    if (this.frameTimer) {
      clearTimeout(this.frameTimer);
      this.frameTimer = null;
    }
    this.stopStream();
    this.cameraOpen.set(false);
    this.flash.set(false);
  }

  private async attachStream(): Promise<void> {
    const video = this.video()?.nativeElement;
    if (!video || !this.stream) {
      return;
    }
    video.srcObject = this.stream;
    try {
      await video.play();
    } catch {
      // Autoplay is muted and inline; a refused play still shows frames on the next tick.
    }
    this.scheduleFrame();
  }

  private scheduleFrame(): void {
    this.frameTimer = setTimeout(() => void this.readFrame(), SCAN_FRAME_MS);
  }

  private async readFrame(): Promise<void> {
    this.frameTimer = null;
    const video = this.video()?.nativeElement;
    if (!this.cameraOpen() || !video || !this.detector) {
      return;
    }
    const now = performance.now();
    if (isScanIdle(this.lastActivity, now, SCAN_IDLE_MS)) {
      this.closeCamera();
      return;
    }
    if (video.readyState >= 2) {
      try {
        const found = await this.detector.detect(video);
        for (const result of found) {
          if (this.gate.accept(result.rawValue, performance.now())) {
            this.lastActivity = performance.now();
            this.signalScan();
            this.take(normaliseScan(result.rawValue));
            break;
          }
        }
      } catch {
        // A frame the detector could not read is skipped, not reported.
      }
    }
    if (this.cameraOpen()) {
      this.scheduleFrame();
    }
  }

  /** A short beep and a flash, so a scan is noticed without looking. */
  private signalScan(): void {
    this.flash.set(true);
    setTimeout(() => this.flash.set(false), 200);
    try {
      const Ctx = window.AudioContext;
      if (!Ctx) {
        return;
      }
      this.audio ??= new Ctx();
      const oscillator = this.audio.createOscillator();
      const gain = this.audio.createGain();
      oscillator.frequency.value = 1800;
      gain.gain.value = 0.08;
      oscillator.connect(gain).connect(this.audio.destination);
      oscillator.start();
      oscillator.stop(this.audio.currentTime + 0.08);
    } catch {
      // No sound is fine; the flash still shows.
    }
  }

  private stopStream(): void {
    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;
    const video = this.video()?.nativeElement;
    if (video) {
      video.srcObject = null;
    }
  }
}

/** True for a target a person types into, where keys are not a page-level scan. */
export function isEditable(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }
  if (target.isContentEditable) {
    return true;
  }
  const tag = target.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
}

/**
 * The browser's own `BarcodeDetector` where it reads our formats, otherwise the
 * MIT polyfill, loaded only now and pointed at our copy of its wasm.
 */
async function createDetector(): Promise<CodeDetector> {
  const native = (window as unknown as { BarcodeDetector?: DetectorConstructor }).BarcodeDetector;
  if (native) {
    try {
      const supported = (await native.getSupportedFormats?.()) ?? [];
      const formats = SCAN_FORMATS.filter((f) => supported.includes(f));
      if (formats.length > 0) {
        return new native({ formats });
      }
    } catch {
      // Fall through to the polyfill.
    }
  }

  const polyfill = await import('barcode-detector/ponyfill');
  const wasm = new URL('assets/zxing/zxing_reader.wasm', document.baseURI).href;
  polyfill.prepareZXingModule({
    overrides: {
      locateFile: (path: string, prefix: string) => (path.endsWith('.wasm') ? wasm : prefix + path),
    },
  });
  const Polyfill = polyfill.BarcodeDetector as unknown as DetectorConstructor;
  return new Polyfill({ formats: [...SCAN_FORMATS] });
}
