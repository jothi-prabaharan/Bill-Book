/**
 * The till's keyboard (TK-40): which function key does what, and how a barcode
 * scanner's burst of keystrokes is told apart from a cashier typing.
 *
 * Pure, so the rules are tested without a browser.
 */

export type PosCommand =
  | 'addItem'
  | 'customer'
  | 'quantity'
  | 'voidLine'
  | 'recall'
  | 'hold'
  | 'tender'
  | 'reprint'
  | 'selectUp'
  | 'selectDown'
  | 'cancel';

/** The keys, printed on the hint bar in this order. */
export const POS_KEYS: readonly { key: string; command: PosCommand; label: string }[] = [
  { key: 'F2', command: 'addItem', label: 'Add item' },
  { key: 'F3', command: 'customer', label: 'Customer' },
  { key: 'F4', command: 'quantity', label: 'Quantity' },
  { key: 'F6', command: 'voidLine', label: 'Void line' },
  { key: 'F7', command: 'recall', label: 'Recall' },
  { key: 'F8', command: 'hold', label: 'Hold' },
  { key: 'F9', command: 'tender', label: 'Tender' },
  { key: 'F10', command: 'reprint', label: 'Reprint' },
];

const EXTRA: Record<string, PosCommand> = {
  ArrowUp: 'selectUp',
  ArrowDown: 'selectDown',
  Escape: 'cancel',
};

/**
 * The command a key press means, or null. A key with Ctrl, Alt or Meta held is
 * never a till command, so the browser's and the operating system's own
 * shortcuts keep working.
 */
export function commandFor(event: {
  key: string;
  ctrlKey?: boolean;
  altKey?: boolean;
  metaKey?: boolean;
}): PosCommand | null {
  if (event.ctrlKey || event.altKey || event.metaKey) {
    return null;
  }
  return POS_KEYS.find((k) => k.key === event.key)?.command ?? EXTRA[event.key] ?? null;
}

/**
 * Tells a scanner from a person. A USB barcode scanner "types" the code as
 * keystrokes a few milliseconds apart and ends with Enter; nobody types that
 * fast. Characters closer together than `maxGapMs` build a burst; a slower one
 * starts a new one. Enter after a burst of at least `minLength` characters is a
 * scan.
 */
export class BarcodeBurst {
  private buffer = '';
  private last = Number.NEGATIVE_INFINITY;

  constructor(
    private readonly maxGapMs = 40,
    private readonly minLength = 4,
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
}
