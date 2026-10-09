/**
 * Where the main window opens (TK-184). It reopens where it was left — on the
 * same monitor, at the same size, maximised if it was — unless that monitor
 * has gone, in which case it comes back centred on one that is there. A
 * window remembered on a 34-inch ultrawide is shrunk to fit a laptop screen
 * rather than opening half off it.
 */
export interface Rect {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

export interface DisplayArea {
  readonly id: number;
  readonly workArea: Rect;
}

export interface SavedWindowState {
  readonly bounds: Rect;
  readonly maximized: boolean;
  readonly fullScreen: boolean;
  readonly displayId: number | null;
}

export const MIN_SIZE = { width: 360, height: 520 } as const;
export const DEFAULT_SIZE = { width: 1440, height: 900 } as const;

/** How much of the window's top edge must be on a screen to count as reachable. */
const GRIP = { width: 120, height: 40 } as const;

export function parseWindowState(raw: unknown): SavedWindowState | null {
  if (raw == null || typeof raw !== 'object') {
    return null;
  }

  const value = raw as Record<string, unknown>;
  const bounds = value['bounds'] as Record<string, unknown> | undefined;
  const numbers = ['x', 'y', 'width', 'height'].map((k) => bounds?.[k]);

  if (!numbers.every((n) => typeof n === 'number' && Number.isFinite(n))) {
    return null;
  }

  const [x, y, width, height] = numbers as number[];

  return {
    bounds: { x, y, width, height },
    maximized: value['maximized'] === true,
    fullScreen: value['fullScreen'] === true,
    displayId: typeof value['displayId'] === 'number' ? value['displayId'] : null,
  };
}

function overlap(a: Rect, b: Rect): Rect | null {
  const x = Math.max(a.x, b.x);
  const y = Math.max(a.y, b.y);
  const right = Math.min(a.x + a.width, b.x + b.width);
  const bottom = Math.min(a.y + a.height, b.y + b.height);
  return right > x && bottom > y ? { x, y, width: right - x, height: bottom - y } : null;
}

/** Whether the window's title strip can be grabbed on some screen. */
export function isReachable(bounds: Rect, displays: readonly DisplayArea[]): boolean {
  const strip: Rect = { x: bounds.x, y: bounds.y, width: bounds.width, height: Math.min(bounds.height, GRIP.height) };

  return displays.some((d) => {
    const seen = overlap(strip, d.workArea);
    return seen !== null && seen.width >= Math.min(GRIP.width, bounds.width) && seen.height >= Math.min(GRIP.height, strip.height);
  });
}

/** The display holding most of `bounds`, or null when none holds any. */
export function displayFor(bounds: Rect, displays: readonly DisplayArea[]): DisplayArea | null {
  let best: DisplayArea | null = null;
  let bestArea = 0;

  for (const display of displays) {
    const seen = overlap(bounds, display.workArea);
    const area = seen ? seen.width * seen.height : 0;

    if (area > bestArea) {
      best = display;
      bestArea = area;
    }
  }

  return best;
}

/** `size` shrunk to fit inside `area` and centred in it. */
export function centredIn(area: Rect, size: { width: number; height: number }): Rect {
  const width = Math.min(Math.max(size.width, MIN_SIZE.width), area.width);
  const height = Math.min(Math.max(size.height, MIN_SIZE.height), area.height);

  return {
    x: Math.round(area.x + (area.width - width) / 2),
    y: Math.round(area.y + (area.height - height) / 2),
    width,
    height,
  };
}

/** A first window: up to the default size, never more than 90% of the screen. */
export function defaultSize(area: Rect): { width: number; height: number } {
  return {
    width: Math.min(DEFAULT_SIZE.width, Math.round(area.width * 0.9)),
    height: Math.min(DEFAULT_SIZE.height, Math.round(area.height * 0.9)),
  };
}

export function restoreBounds(
  saved: SavedWindowState | null,
  displays: readonly DisplayArea[],
  primary: DisplayArea,
): Rect {
  if (saved && isReachable(saved.bounds, displays)) {
    const home = displayFor(saved.bounds, displays) ?? primary;
    const area = home.workArea;

    if (saved.bounds.width <= area.width && saved.bounds.height <= area.height) {
      return saved.bounds;
    }

    return centredIn(area, saved.bounds);
  }

  const remembered = saved?.displayId != null ? displays.find((d) => d.id === saved.displayId) : undefined;
  const target = remembered ?? primary;

  return centredIn(target.workArea, saved ? saved.bounds : defaultSize(target.workArea));
}
