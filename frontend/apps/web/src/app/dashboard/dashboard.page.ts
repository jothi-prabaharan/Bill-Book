import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  OdometerComponent,
  MotionService
} from '@bill-book/ui-components';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { AuthService } from '@bill-book/auth';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FormatSettingsService } from '@bill-book/currency-format';
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

export interface DashboardMonthlyTrendPoint {
  month: string;
  sales: number;
  outstanding: number;
}

export interface DashboardTopProduct {
  item: string;
  sold: string;
  profit: number;
  margin: string;
}

export interface DashboardOutstandingParty {
  party: string;
  due: string;
  amount: number;
  overdue: string;
  tone: string;
}

export interface DashboardComplianceItem {
  form: string;
  period: string;
  due: string;
  status: string;
  tone: string;
}

export interface DashboardSummary {
  salesToday: number;
  invoicesCountToday: number;
  unpaidInvoicesCountToday: number;
  totalReceivables: number;
  overdueReceivables: number;
  totalPayables: number;
  overduePayables: number;
  nextPayableDueDate: string | null;
  cashAndBankTotal: number;
  bankAccountsCount: number;
  plPeriodLabel: string;
  revenue: number;
  costOfGoodsSold: number;
  operatingExpenses: number;
  netProfit: number;
  marginPercent: number;
  monthlyTrend: DashboardMonthlyTrendPoint[];
  totalSalesTrend: number;
  totalOutstandingTrend: number;
  topProducts: DashboardTopProduct[];
  outstanding: DashboardOutstandingParty[];
  upcomingCompliance: DashboardComplianceItem[];
}

/**
 * The Home board.
 *
 * It renders no heading of its own — the breadcrumb strip is the title — and
 * its controls (basis toggle, Customize, Reset, Done) live in that strip too,
 * reaching this page through `ShellBoardService`.
 *
 * All figures, tables, and trends are loaded in real-time from the database.
 * If the organization has no transactions recorded yet, clean zero states are
 * shown rather than static mock sample figures.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-dashboard-page',
  standalone: true,
  imports: [DataGridComponent, DataGridCellTemplateDirective, ServicesDueCard, OdometerComponent],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPage implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);
  private readonly board = inject(ShellBoardService);
  private readonly auth = inject(AuthService);
  protected readonly motion = inject(MotionService);
  private lastOrgId: string | null = null;

  protected readonly productColumns: ColumnDef[] = [
    { field: 'item', header: 'Item' },
    { field: 'sold', header: 'Sold', numeric: true },
    { field: 'profit', header: 'Profit', numeric: true },
    { field: 'margin', header: 'Margin', numeric: true },
  ];

  protected readonly outstandingColumns: ColumnDef[] = [
    { field: 'party', header: 'Party' },
    { field: 'due', header: 'Due' },
    { field: 'amount', header: 'Amount', numeric: true },
    { field: 'overdue', header: 'Overdue', numeric: true },
  ];

  protected readonly complianceColumns: ColumnDef[] = [
    { field: 'form', header: 'Filing' },
    { field: 'period', header: 'Period' },
    { field: 'due', header: 'Due Date' },
    { field: 'status', header: 'Status' },
  ];

  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly loading = signal(true);

  // Widget 1: Sales today
  protected readonly salesTodayKpi = computed(() =>
    this.formats.formatMoney(this.summary()?.salesToday ?? 0),
  );
  protected readonly salesTodayMeta = computed(() => {
    const s = this.summary();
    if (!s || s.invoicesCountToday === 0) return '0 invoices today';
    return `${s.invoicesCountToday} invoice${s.invoicesCountToday === 1 ? '' : 's'} · ${s.unpaidInvoicesCountToday} unpaid`;
  });

  // Widget 2: Receivables
  protected readonly receivablesKpi = computed(() =>
    this.formats.formatMoney(this.summary()?.totalReceivables ?? 0),
  );
  protected readonly receivablesMeta = computed(() => {
    const s = this.summary();
    if (!s || s.overdueReceivables <= 0) return 'All accounts up to date';
    return `${this.formats.formatMoney(s.overdueReceivables)} overdue`;
  });
  protected readonly overdueReceivablesKpi = computed(() =>
    this.formats.formatMoney(this.summary()?.overdueReceivables ?? 0),
  );

  // Widget 3: Payables
  protected readonly payablesKpi = computed(() =>
    this.formats.formatMoney(this.summary()?.totalPayables ?? 0),
  );
  protected readonly payablesMeta = computed(() => {
    const s = this.summary();
    if (!s) return 'No outstanding payables';
    if (s.overduePayables > 0) return `${this.formats.formatMoney(s.overduePayables)} overdue`;
    if (s.nextPayableDueDate) return `Next due ${this.formats.formatDate(s.nextPayableDueDate)}`;
    return 'No outstanding payables';
  });

  // Widget 4: Cash & bank
  protected readonly cashKpi = computed(() =>
    this.formats.formatMoney(this.summary()?.cashAndBankTotal ?? 0),
  );
  protected readonly cashMeta = computed(() => {
    const count = this.summary()?.bankAccountsCount ?? 0;
    return `${count} account${count === 1 ? '' : 's'}`;
  });

  // Widget 5: Profit & loss
  protected readonly pl = computed(() => {
    const s = this.summary();
    const revenue = s?.revenue ?? 0;
    const cogs = s?.costOfGoodsSold ?? 0;
    const expenses = s?.operatingExpenses ?? 0;
    const net = s?.netProfit ?? 0;
    const margin = s?.marginPercent ?? 0;

    return {
      period: s?.plPeriodLabel || 'Fiscal Year',
      revenue: this.formats.formatMoney(revenue),
      cogs: this.formats.formatMoney(cogs),
      cogsPercent: revenue > 0 ? Math.min(100, Math.round((cogs / revenue) * 100)) : 0,
      expenses: this.formats.formatMoney(expenses),
      expensesPercent: revenue > 0 ? Math.min(100, Math.round((expenses / revenue) * 100)) : 0,
      netProfit: this.formats.formatMoney(net),
      margin: `${margin.toFixed(1)}% margin`,
    };
  });

  // Widget 6: Trend chart (Sales & outstanding)
  protected readonly trendSalesPoints = computed(() => {
    const trend = this.summary()?.monthlyTrend ?? [];
    if (!trend.length) return '8,68 50,68 92,68 134,68 176,68 212,68';
    const maxVal = Math.max(...trend.map((t) => Math.max(t.sales, t.outstanding)), 1);
    const xs = [8, 50, 92, 134, 176, 212];
    return trend
      .map((t, i) => {
        const x = xs[i] ?? 8;
        const y = Math.round(68 - (t.sales / maxVal) * 56);
        return `${x},${y}`;
      })
      .join(' ');
  });

  protected readonly trendOutstandingPoints = computed(() => {
    const trend = this.summary()?.monthlyTrend ?? [];
    if (!trend.length) return '8,68 50,68 92,68 134,68 176,68 212,68';
    const maxVal = Math.max(...trend.map((t) => Math.max(t.sales, t.outstanding)), 1);
    const xs = [8, 50, 92, 134, 176, 212];
    return trend
      .map((t, i) => {
        const x = xs[i] ?? 8;
        const y = Math.round(68 - (t.outstanding / maxVal) * 56);
        return `${x},${y}`;
      })
      .join(' ');
  });

  protected readonly trendStartMonth = computed(
    () => this.summary()?.monthlyTrend[0]?.month ?? '',
  );
  protected readonly trendEndMonth = computed(() => {
    const trend = this.summary()?.monthlyTrend ?? [];
    return trend.length ? trend[trend.length - 1].month : '';
  });
  protected readonly trendSalesLegend = computed(() =>
    this.formats.formatMoney(this.summary()?.totalSalesTrend ?? 0),
  );
  protected readonly trendOutstandingLegend = computed(() =>
    this.formats.formatMoney(this.summary()?.totalOutstandingTrend ?? 0),
  );

  // Widget 7: Top products
  protected readonly topProducts = computed(() => {
    const list = this.summary()?.topProducts ?? [];
    return list.map((p) => ({
      item: p.item,
      sold: p.sold,
      profit: this.formats.formatMoney(p.profit),
      margin: p.margin,
    }));
  });

  // Widget 8: Outstanding
  protected readonly outstanding = computed(() => {
    const list = this.summary()?.outstanding ?? [];
    return list.map((o) => ({
      party: o.party,
      due: o.due,
      amount: this.formats.formatMoney(o.amount),
      overdue: o.overdue,
      tone: o.tone,
    }));
  });

  // Widget 9: Statutory compliance
  protected readonly upcomingCompliance = computed(
    () => this.summary()?.upcomingCompliance ?? [],
  );
  protected readonly nextComplianceTitle = computed(() => {
    const items = this.upcomingCompliance();
    if (!items.length) return 'Compliance calendar';
    return `Next ${items[0].form}: ${items[0].due}`;
  });

  readonly widgets = DASHBOARD_WIDGETS;
  readonly editing = computed(() => this.board.editing());
  private readonly layout = signal<DashboardLayout>(readLayout());

  readonly visible = computed<DashboardWidget[]>(() => {
    const { order, hidden } = this.layout();
    return order
      .filter((id) => !hidden.includes(id))
      .map((id) => this.widgets.find((w) => w.id === id))
      .filter((w): w is DashboardWidget => w !== undefined);
  });

  readonly removed = computed<DashboardWidget[]>(() =>
    this.widgets.filter((w) => this.layout().hidden.includes(w.id)),
  );

  span(id: string): number {
    return this.layout().spans[id] ?? 12;
  }

  constructor() {
    let seen = this.board.resetCount();
    effect(() => {
      const count = this.board.resetCount();
      if (count === seen) return;
      seen = count;
      this.commit(defaultLayout());
    });

    effect(() => {
      const orgId = this.auth.activeOrgId();
      if (this.lastOrgId !== null && orgId !== this.lastOrgId && this.motion.active()) {
        setTimeout(() => this.triggerBranchFlip(), 100);
      }
      this.lastOrgId = orgId;
    });
  }

  triggerBranchFlip() {
    const board = document.querySelector('.board') as HTMLElement;
    if (!board) return;
    
    const bRect = board.getBoundingClientRect();
    const cards = Array.from(board.querySelectorAll('.card:not(.board .card .card)')) as HTMLElement[];
    const easing = 'cubic-bezier(.2,.75,.25,1)';

    cards.forEach((card) => {
      const r = card.getBoundingClientRect();
      const delay = ((r.left - bRect.left) / bRect.width) * 240 + Math.min(Math.max(0, r.top - bRect.top), 1400) * 0.22;
      card.animate([
        { opacity: 0, transform: 'perspective(1200px) rotateY(-90deg)' },
        { opacity: 1, transform: 'perspective(1200px) rotateY(0deg)' }
      ], { duration: 700, delay, easing, fill: 'backwards' });
    });

    const drums = Array.from(board.querySelectorAll('.odo-drum')) as HTMLElement[];
    drums.forEach(drum => {
      const dAttr = drum.getAttribute('data-d');
      const kAttr = drum.getAttribute('data-k');
      if (!dAttr || !kAttr) return;
      const d = parseInt(dAttr, 10);
      const k = parseInt(kAttr, 10);
      
      const card = drum.closest('.card') as HTMLElement;
      let cardDelay = 0;
      if (card) {
        const r = card.getBoundingClientRect();
        cardDelay = ((r.left - bRect.left) / bRect.width) * 240 + Math.min(Math.max(0, r.top - bRect.top), 1400) * 0.22;
      }
      
      const delay = cardDelay + 240 + k * 50;
      drum.animate([
        { transform: `translateZ(-1.69em) rotateX(${d * 36 - 360}deg)` },
        { transform: `translateZ(-1.69em) rotateX(${d * 36}deg)` }
      ], { duration: 1000, delay, easing, fill: 'backwards' });
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const res = await firstValueFrom(
        this.http.get<DashboardSummary>('/api/reporting/dashboard'),
      );
      this.summary.set(res);
    } catch {
      // Clean fallback if API is unreachable
      this.summary.set({
        salesToday: 0,
        invoicesCountToday: 0,
        unpaidInvoicesCountToday: 0,
        totalReceivables: 0,
        overdueReceivables: 0,
        totalPayables: 0,
        overduePayables: 0,
        nextPayableDueDate: null,
        cashAndBankTotal: 0,
        bankAccountsCount: 0,
        plPeriodLabel: '',
        revenue: 0,
        costOfGoodsSold: 0,
        operatingExpenses: 0,
        netProfit: 0,
        marginPercent: 0,
        monthlyTrend: [],
        totalSalesTrend: 0,
        totalOutstandingTrend: 0,
        topProducts: [],
        outstanding: [],
        upcomingCompliance: [],
      });
    } finally {
      this.loading.set(false);
    }
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
