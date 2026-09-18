import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { ShellBoardService } from '@bill-book/app-shell';
import {
  DASHBOARD_WIDGETS,
  DashboardLayout,
  DashboardWidget,
  defaultLayout,
  moveInOrder,
  nextSpan,
  readLayout,
  writeLayout,
} from './dashboard-layout';

/**
 * The Home board.
 *
 * It renders no heading of its own — the breadcrumb strip is the title — and
 * its controls (basis toggle, Customize, Reset, Done) live in that strip too,
 * reaching this page through `ShellBoardService`.
 *
 * In customize mode each card grows a small bar: move left, move right, cycle
 * width, remove. Removed widgets collect in a tray above the board. The
 * arrangement is this browser's, stored under `billbook.dashboard.layout.v11`.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-dashboard-page',
  standalone: true,
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPage {
  private readonly board = inject(ShellBoardService);

  readonly widgets = DASHBOARD_WIDGETS;

  readonly editing = computed(() => this.board.editing());

  private readonly layout = signal<DashboardLayout>(readLayout());

  /** Shown widgets, in the person's own order. */
  readonly visible = computed<DashboardWidget[]>(() => {
    const { order, hidden } = this.layout();
    return order
      .filter((id) => !hidden.includes(id))
      .map((id) => this.widgets.find((w) => w.id === id))
      .filter((w): w is DashboardWidget => w !== undefined);
  });

  /** Removed widgets, offered back in the tray. */
  readonly removed = computed<DashboardWidget[]>(() =>
    this.widgets.filter((w) => this.layout().hidden.includes(w.id)),
  );

  span(id: string): number {
    return this.layout().spans[id] ?? 12;
  }

  constructor() {
    // Reset arrives as a count rather than an event, so a press that lands
    // before this page has rendered is not lost.
    let seen = this.board.resetCount();
    effect(() => {
      const count = this.board.resetCount();
      if (count === seen) return;
      seen = count;
      this.commit(defaultLayout());
    });
  }

  moveLeft(id: string): void {
    this.commit({ ...this.layout(), order: moveInOrder(this.layout().order, id, -1) });
  }

  moveRight(id: string): void {
    this.commit({ ...this.layout(), order: moveInOrder(this.layout().order, id, 1) });
  }

  resize(id: string): void {
    const current = this.layout();
    this.commit({
      ...current,
      spans: { ...current.spans, [id]: nextSpan(this.span(id)) },
    });
  }

  remove(id: string): void {
    const current = this.layout();
    if (current.hidden.includes(id)) return;
    this.commit({ ...current, hidden: [...current.hidden, id] });
  }

  add(id: string): void {
    const current = this.layout();
    this.commit({ ...current, hidden: current.hidden.filter((h) => h !== id) });
  }

  private commit(layout: DashboardLayout): void {
    this.layout.set(layout);
    writeLayout(layout);
  }
}
