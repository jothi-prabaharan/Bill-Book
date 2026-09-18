/**
 * The Home board's layout — which widgets are shown, in what order, at what
 * width — and the rules for reading it back safely from storage.
 *
 * Kept apart from the component so the validation can be tested on its own:
 * the stored value is writable by anything running on this origin, and a board
 * that throws on a malformed key is worse than one that falls back to defaults.
 */

export const DASHBOARD_LAYOUT_KEY = 'billbook.dashboard.layout.v11';

/** Widths a card may take, in 12-column units. `resize` cycles through these. */
export const WIDGET_SPANS: readonly number[] = [3, 4, 6, 8, 12];

export interface DashboardWidget {
  readonly id: string;
  /** What the "add it back" button says. */
  readonly label: string;
  readonly defaultSpan: number;
}

export const DASHBOARD_WIDGETS: readonly DashboardWidget[] = [
  { id: 'sales', label: 'Sales today', defaultSpan: 3 },
  { id: 'recv', label: 'Receivables', defaultSpan: 3 },
  { id: 'pay', label: 'Payables', defaultSpan: 3 },
  { id: 'cash', label: 'Cash & bank total', defaultSpan: 3 },
  { id: 'docs', label: 'Profit & loss', defaultSpan: 6 },
  { id: 'gst', label: 'Sales & outstanding', defaultSpan: 6 },
  { id: 'stock', label: 'Top products by profit', defaultSpan: 7 },
  { id: 'aging', label: 'Outstanding list', defaultSpan: 5 },
];

export interface DashboardLayout {
  readonly order: readonly string[];
  readonly spans: Readonly<Record<string, number>>;
  readonly hidden: readonly string[];
}

export function defaultLayout(): DashboardLayout {
  return {
    order: DASHBOARD_WIDGETS.map((w) => w.id),
    spans: Object.fromEntries(DASHBOARD_WIDGETS.map((w) => [w.id, w.defaultSpan])),
    hidden: [],
  };
}

/**
 * Take whatever was stored and return a layout that is certainly usable:
 * unknown ids are dropped, widgets the stored order never mentions are appended
 * in their declared order, and a span that is not one of `WIDGET_SPANS` falls
 * back to the widget's default. Anything unparseable yields the defaults.
 */
export function coerceLayout(raw: unknown): DashboardLayout {
  const fallback = defaultLayout();
  if (typeof raw !== 'object' || raw === null) return fallback;

  const value = raw as Partial<Record<keyof DashboardLayout, unknown>>;
  const known = new Set(DASHBOARD_WIDGETS.map((w) => w.id));

  const storedOrder = Array.isArray(value.order)
    ? value.order.filter((id): id is string => typeof id === 'string' && known.has(id))
    : [];
  const order = [...new Set(storedOrder)];
  for (const widget of DASHBOARD_WIDGETS) {
    if (!order.includes(widget.id)) order.push(widget.id);
  }

  const spans: Record<string, number> = {};
  const storedSpans =
    typeof value.spans === 'object' && value.spans !== null
      ? (value.spans as Record<string, unknown>)
      : {};
  for (const widget of DASHBOARD_WIDGETS) {
    const stored = storedSpans[widget.id];
    spans[widget.id] =
      typeof stored === 'number' && WIDGET_SPANS.includes(stored) ? stored : widget.defaultSpan;
  }

  const hidden = Array.isArray(value.hidden)
    ? [...new Set(value.hidden.filter((id): id is string => typeof id === 'string' && known.has(id)))]
    : [];

  return { order, spans, hidden };
}

/** Read the stored layout, or the defaults when there is nothing usable. */
export function readLayout(): DashboardLayout {
  try {
    const raw = localStorage.getItem(DASHBOARD_LAYOUT_KEY);
    if (!raw) return defaultLayout();
    return coerceLayout(JSON.parse(raw));
  } catch {
    return defaultLayout();
  }
}

export function writeLayout(layout: DashboardLayout): void {
  try {
    localStorage.setItem(DASHBOARD_LAYOUT_KEY, JSON.stringify(layout));
  } catch {
    // Private window, or storage the browser has blocked. The board still works
    // for this session; the arrangement just will not survive a reload.
  }
}

/** The next width in the cycle, wrapping back to the narrowest. */
export function nextSpan(span: number): number {
  const index = WIDGET_SPANS.indexOf(span);
  return WIDGET_SPANS[(index + 1) % WIDGET_SPANS.length];
}

/** Move one id one place left or right, clamped at the ends. */
export function moveInOrder(
  order: readonly string[],
  id: string,
  direction: -1 | 1,
): readonly string[] {
  const from = order.indexOf(id);
  if (from === -1) return order;
  const to = from + direction;
  if (to < 0 || to >= order.length) return order;
  const next = [...order];
  next.splice(to, 0, ...next.splice(from, 1));
  return next;
}
