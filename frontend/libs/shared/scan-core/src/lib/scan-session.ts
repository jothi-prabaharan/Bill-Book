/**
 * The rules of a camera scanning session (TK-160), kept apart from the camera
 * itself so they are tested without a browser. Only the `-ui` component
 * touches `window`, `navigator` and the video element.
 */

/** The camera popup's size: a corner of the screen, never all of it. */
export const CAMERA_POPUP = { width: 160, height: 120 } as const;

/** After a scan is taken, nothing more is read for this long. */
export const SCAN_PAUSE_MS = 800;

/** The same code is not taken twice within this long, so one held label scans once. */
export const SCAN_REPEAT_MS = 2500;

/** The camera closes itself after this long with nothing scanned. */
export const SCAN_IDLE_MS = 30_000;

/** How often a video frame is looked at for a code. */
export const SCAN_FRAME_MS = 150;

/**
 * What the camera looks for: retail codes, Code128 and Code39 that shop labels
 * use, ITF on cartons, and QR and DataMatrix for documents and pharma packs.
 * Named as the `BarcodeDetector` API names them.
 */
export const SCAN_FORMATS: readonly string[] = [
  'ean_13',
  'ean_8',
  'upc_a',
  'upc_e',
  'code_128',
  'code_39',
  'itf',
  'qr_code',
  'data_matrix',
];

/**
 * Continuous scanning lets each code through once: a short pause after every
 * scan, and a longer one before the same code counts again.
 */
export class ScanGate {
  private lastCode: string | null = null;
  private lastAt = Number.NEGATIVE_INFINITY;

  constructor(
    private readonly pauseMs = SCAN_PAUSE_MS,
    private readonly repeatMs = SCAN_REPEAT_MS,
  ) {}

  /** True when `code`, read at `at` ms, is a new scan to act on. */
  accept(code: string, at: number): boolean {
    const value = normaliseScan(code);
    if (!value) {
      return false;
    }
    const since = at - this.lastAt;
    if (since < this.pauseMs) {
      return false;
    }
    if (value === this.lastCode && since < this.repeatMs) {
      return false;
    }
    this.lastCode = value;
    this.lastAt = at;
    return true;
  }

  reset(): void {
    this.lastCode = null;
    this.lastAt = Number.NEGATIVE_INFINITY;
  }
}

/** A code as read, without the spaces or line ends a reader may add around it. */
export function normaliseScan(raw: string | null | undefined): string {
  return (raw ?? '').replace(/^[\s\r\n]+|[\s\r\n]+$/g, '');
}

/** True when the camera has gone `idleMs` with nothing scanned since it opened or last read. */
export function isScanIdle(lastActivityAt: number, now: number, idleMs = SCAN_IDLE_MS): boolean {
  return now - lastActivityAt >= idleMs;
}

/**
 * The one line shown when the camera cannot start, by the error's name. Every
 * answer says the code can still be typed, because it always can.
 */
export function cameraErrorMessage(errorName: string | null | undefined, secureContext = true): string {
  if (!secureContext) {
    return 'The camera needs a secure (https) address. Type or scan the code instead.';
  }
  switch (errorName) {
    case 'NotAllowedError':
    case 'SecurityError':
      return 'Camera permission was denied. Type or scan the code instead.';
    case 'NotFoundError':
    case 'OverconstrainedError':
      return 'No camera was found. Type or scan the code instead.';
    case 'NotReadableError':
    case 'AbortError':
      return 'The camera is in use by another app. Type or scan the code instead.';
    case 'Unsupported':
      return 'This browser cannot use the camera. Type or scan the code instead.';
    default:
      return 'The camera could not start. Type or scan the code instead.';
  }
}
