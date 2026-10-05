/**
 * Telling a barcode scanner from a person typing (TK-40, moved here by TK-160
 * so every screen can use it, not only the till).
 *
 * A USB or Bluetooth scanner in keyboard mode "types" the code as keystrokes a
 * few milliseconds apart and ends with Enter; nobody types that fast.
 * Characters closer together than `maxGapMs` build a burst; a slower one starts
 * a new one. Enter after a burst of at least `minLength` characters is a scan.
 *
 * Pure: no `window`, no `document`, so a `-core` lib can use it.
 */

/** Gap between a scanner's keystrokes, in milliseconds, above which it is a person. */
export const SCAN_MAX_GAP_MS = 40;

/** The shortest code a burst counts as. */
export const SCAN_MIN_LENGTH = 4;

export class BarcodeBurst {
  private buffer = '';
  private last = Number.NEGATIVE_INFINITY;

  constructor(
    private readonly maxGapMs = SCAN_MAX_GAP_MS,
    private readonly minLength = SCAN_MIN_LENGTH,
  ) {}

  /**
   * Feeds one key press at a time in milliseconds. Returns the scanned code
   * when this key completes a scan, otherwise null.
   */
  push(key: string, at: number): string | null {
    const gap = at - this.last;
    this.last = at;

    if (key === 'Enter') {
      const code = gap <= this.maxGapMs && this.buffer.length >= this.minLength ? this.buffer : null;
      this.buffer = '';
      return code;
    }

    if (key.length !== 1) {
      this.buffer = '';
      return null;
    }

    this.buffer = gap <= this.maxGapMs ? this.buffer + key : key;
    return null;
  }

  /** Forgets a half-read burst, as when focus moves away. */
  reset(): void {
    this.buffer = '';
    this.last = Number.NEGATIVE_INFINITY;
  }
}

/**
 * The same detector as a plain function: feed it `(key, at)` and it answers
 * the code when that key completes a scan, otherwise null.
 */
export function burstDetector(
  maxGapMs = SCAN_MAX_GAP_MS,
  minLength = SCAN_MIN_LENGTH,
): (key: string, at: number) => string | null {
  const burst = new BarcodeBurst(maxGapMs, minLength);
  return (key, at) => burst.push(key, at);
}
