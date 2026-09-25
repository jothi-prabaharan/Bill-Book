/**
 * The attendance register's model (TK-63): who is on the register, the marks
 * a caller allows, and the three rules the component runs — which mark comes
 * next on a tap, which mark a key means, and how many of each there are.
 * Pure, so they are tested without rendering anything.
 */

/** How a mark looks. Tones map to the theme's success, danger, warning and accent tokens. */
export type RegisterTone = 'good' | 'bad' | 'warn' | 'neutral';

export interface RegisterStatus<TValue extends string = string> {
  value: TValue;
  label: string;
  /** The single key that sets this mark, e.g. `P`. Matched without regard to case. */
  key: string;
  /** A short mark drawn on the tile, e.g. `P`. */
  glyph: string;
  tone: RegisterTone;
}

export interface RegisterEntry<TValue extends string = string> {
  id: number;
  label: string;
  /** A line under the name: a roll or employee number. */
  caption?: string | null;
  status: TValue;
}

/** The mark after `current` in the caller's order, wrapping round. An unknown mark starts at the first. */
export function nextStatus<TValue extends string>(current: TValue, statuses: readonly RegisterStatus<TValue>[]): TValue {
  if (statuses.length === 0) {
    return current;
  }

  const index = statuses.findIndex((s) => s.value === current);
  return statuses[(index + 1) % statuses.length].value;
}

/** The mark a key sets, or null when the key means nothing here. */
export function statusForKey<TValue extends string>(key: string, statuses: readonly RegisterStatus<TValue>[]): TValue | null {
  if (key.length !== 1) {
    return null;
  }

  const wanted = key.toUpperCase();
  return statuses.find((s) => s.key.toUpperCase() === wanted)?.value ?? null;
}

/** How many entries carry each mark, in the caller's order, including marks nobody has. */
export function countByStatus<TValue extends string>(
  entries: readonly RegisterEntry<TValue>[],
  statuses: readonly RegisterStatus<TValue>[],
): { status: RegisterStatus<TValue>; count: number }[] {
  return statuses.map((status) => ({ status, count: entries.filter((e) => e.status === status.value).length }));
}

/**
 * Where focus goes from `index` on an arrow key, in a grid `columns` wide.
 * Null for any other key. Stops at the edges rather than wrapping.
 */
export function moveFocus(index: number, key: string, count: number, columns: number): number | null {
  const step = key === 'ArrowRight' ? 1 : key === 'ArrowLeft' ? -1 : key === 'ArrowDown' ? columns : key === 'ArrowUp' ? -columns : null;
  if (step === null) {
    return key === 'Home' ? 0 : key === 'End' ? count - 1 : null;
  }

  const next = index + step;
  return next < 0 || next >= count ? index : next;
}
