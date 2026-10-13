import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '@bill-book/auth';
import {
  LaborerDto,
  MarginRuleDto,
  SaveAssignmentDto,
  SaveLaborerDto,
  SaveMarginRuleDto,
  SaveSiteDto,
  SaveTradeDto,
  SiteApiService,
  SiteAssignmentDto,
  SiteDto,
  TradeDto,
} from '@bill-book/site-core';

type Tab = 'sites' | 'trades' | 'laborers' | 'assignments' | 'margins';

@Component({
  selector: 'bb-sites-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './sites.page.html',
  styleUrl: './sites.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SitesPage implements OnInit {
  private readonly siteApi = inject(SiteApiService);
  private readonly auth = inject(AuthService);

  readonly tab = signal<Tab>('sites');
  readonly loading = signal<boolean>(false);
  readonly showModal = signal<boolean>(false);

  readonly sites = signal<SiteDto[]>([]);
  readonly trades = signal<TradeDto[]>([]);
  readonly laborers = signal<LaborerDto[]>([]);
  readonly assignments = signal<SiteAssignmentDto[]>([]);
  readonly margins = signal<MarginRuleDto[]>([]);

  readonly canViewCost = computed(() => this.auth.has('site.cost.view'));
  readonly canEdit = computed(() => this.auth.has('site.edit'));

  readonly activeTabLabel = computed(() => {
    switch (this.tab()) {
      case 'sites': return 'Site';
      case 'trades': return 'Trade';
      case 'laborers': return 'Laborer';
      case 'assignments': return 'Assignment';
      case 'margins': return 'Margin Rule';
    }
  });

  // Forms
  siteForm: SaveSiteDto = {
    siteName: '',
    contactId: 1,
    address: '',
    geofenceRadiusMeters: 200,
    status: 'Planning',
  };

  tradeForm: SaveTradeDto = {
    tradeCode: '',
    tradeName: '',
    defaultSellRatePerDay: 1000,
    defaultCostRatePerDay: 700,
    defaultOtSellRatePerHour: 150,
    defaultOtCostRatePerHour: 100,
    isActive: true,
  };

  laborerForm: SaveLaborerDto = {
    fullName: '',
    phone: '',
    tradeId: 1,
    isActive: true,
  };

  assignmentForm: SaveAssignmentDto = {
    siteId: 1,
    laborerId: 1,
    tradeId: 1,
    startDate: new Date().toISOString().substring(0, 10),
    status: 'Active',
  };

  marginForm: SaveMarginRuleDto = {
    scope: 'Branch',
    appliesTo: 'All',
    marginType: 'Percent',
    marginValue: 15,
    effectiveFrom: new Date().toISOString().substring(0, 10),
    isActive: true,
  };

  ngOnInit(): void {
    void this.loadAll();
  }

  setTab(t: Tab): void {
    this.tab.set(t);
  }

  openAddModal(): void {
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
  }

  tradeName(id: number): string {
    return this.trades().find((t) => t.tradeId === id)?.tradeName ?? `Trade #${id}`;
  }

  laborerName(id: number): string {
    return this.laborers().find((l) => l.laborerId === id)?.fullName ?? `Laborer #${id}`;
  }

  siteName(id: number): string {
    return this.sites().find((s) => s.siteId === id)?.siteName ?? `Site #${id}`;
  }

  async loadAll(): Promise<void> {
    this.loading.set(true);
    try {
      const [sites, trades, laborers, assignments, margins] = await Promise.all([
        this.siteApi.listSites().catch(() => []),
        this.siteApi.listTrades().catch(() => []),
        this.siteApi.listLaborers().catch(() => []),
        this.siteApi.listAssignments().catch(() => []),
        this.siteApi.listMarginRules().catch(() => []),
      ]);

      this.sites.set(sites);
      this.trades.set(trades);
      this.laborers.set(laborers);
      this.assignments.set(assignments);
      this.margins.set(margins);
    } finally {
      this.loading.set(false);
    }
  }

  async saveItem(): Promise<void> {
    try {
      switch (this.tab()) {
        case 'sites':
          await this.siteApi.saveSite(null, this.siteForm);
          break;
        case 'trades':
          await this.siteApi.saveTrade(null, this.tradeForm);
          break;
        case 'laborers':
          await this.siteApi.saveLaborer(null, this.laborerForm);
          break;
        case 'assignments':
          await this.siteApi.saveAssignment(null, this.assignmentForm);
          break;
        case 'margins':
          await this.siteApi.saveMarginRule(null, this.marginForm);
          break;
      }
      this.closeModal();
      await this.loadAll();
    } catch (e) {
      console.error('Failed to save', e);
    }
  }
}
