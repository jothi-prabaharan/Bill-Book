import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
} from '@bill-book/ui-components';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { ShellBoardService } from '@bill-book/app-shell';
import { ServicesDueCard } from '@bill-book/customer-ui';
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
  imports: [DataGridComponent, DataGridCellTemplateDirective, ServicesDueCard],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPage {
  // Sample figures until the dashboard reads real ones; kept as data so the
  // cards draw through the one shared table like every other list.
  protected readonly productColumns: ColumnDef[] = [
    { field: 'item', header: 'Item' },
    { field: 'sold', header: 'Sold', numeric: true },
    { field: 'profit', header: 'Profit', numeric: true },
    { field: 'margin', header: 'Margin', numeric: true },
  ];

  protected readonly topProducts = [
    { item: 'Cotton shirting 2×2 — 44"', sold: '1,840 m', profit: '₹2,46,300', margin: '31.4%' },
    { item: 'Linen blend — natural', sold: '960 m', profit: '₹1,72,800', margin: '28.1%' },
    { item: 'Poly viscose suiting', sold: '2,410 m', profit: '₹1,38,400', margin: '19.6%' },
    { item: 'Brass buttons 18L', sold: '312 gr', profit: '₹84,900', margin: '42.8%' },
    { item: 'Corrugated cartons 5-ply', sold: '4,600 pc', profit: '₹61,200', margin: '14.2%' },
  ];

  protected readonly outstandingColumns: ColumnDef[] = [
    { field: 'party', header: 'Party' },
    { field: 'due', header: 'Due' },
    { field: 'amount', header: 'Amount', numeric: true },
    { field: 'overdue', header: 'Overdue', numeric: true },
  ];

  protected readonly outstanding = [
    { party: 'Suriya Textiles', due: '12 Apr', amount: '₹3,42,000', overdue: '121 d', tone: 'danger' },
    { party: 'Anand Hardware', due: '30 May', amount: '₹2,18,400', overdue: '73 d', tone: 'danger' },
    { party: 'Meenakshi Agencies', due: '18 Jun', amount: '₹1,64,900', overdue: '54 d', tone: 'warning' },
    { party: 'Kavitha Stores', due: '09 Jul', amount: '₹96,300', overdue: '33 d', tone: 'warning' },
    { party: 'Raja Packaging', due: '28 Jul', amount: '₹48,900', overdue: '14 d', tone: 'info' },
  ];

  protected readonly complianceColumns: ColumnDef[] = [
    { field: 'form', header: 'Filing' },
    { field: 'period', header: 'Period' },
    { field: 'due', header: 'Due Date' },
    { field: 'status', header: 'Status' },
  ];

  protected readonly upcomingCompliance = [
    { form: 'GSTR-1', period: 'Sep 2026', due: '11 Oct 2026', status: 'Ready', tone: 'success' },
    { form: 'GSTR-3B', period: 'Sep 2026', due: '20 Oct 2026', status: 'In progress', tone: 'warning' },
    { form: 'TDS Deposit', period: 'Sep 2026', due: '07 Oct 2026', status: 'Due soon', tone: 'danger' },
  ];

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
