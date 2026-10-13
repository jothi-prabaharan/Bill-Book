import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '@bill-book/auth';
import {
  BillingWorksheetDto,
  CreateSiteBillRequest,
  MaterialLineToBillDto,
  SiteApiService,
  SiteDto,
  TradeDto,
} from '@bill-book/site-core';

@Component({
  selector: 'bb-site-billing-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './site-billing.page.html',
  styleUrl: './site-billing.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteBillingPage implements OnInit {
  private readonly siteApi = inject(SiteApiService);
  private readonly auth = inject(AuthService);

  readonly loading = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly sites = signal<SiteDto[]>([]);
  readonly trades = signal<TradeDto[]>([]);
  readonly selectedSiteId = signal<number | null>(null);
  readonly selectedTradeId = signal<number | null>(null);
  readonly fromDate = signal<string>('');
  readonly toDate = signal<string>('');

  readonly worksheet = signal<BillingWorksheetDto | null>(null);

  // Selection states
  readonly selectedAttendanceIds = signal<Set<number>>(new Set());
  readonly selectedMaterialLines = signal<Map<number, number>>(new Map());

  readonly canViewCost = computed(() => this.auth.has('site.cost.view'));
  readonly canEdit = computed(() => this.auth.has('site.edit'));

  billingDate: string = new Date().toISOString().substring(0, 10);
  dueDate: string = new Date().toISOString().substring(0, 10);
  notes: string = '';

  readonly selectedLaborTotal = computed(() => {
    const ws = this.worksheet();
    if (!ws) return 0;
    const selected = this.selectedAttendanceIds();
    return ws.laborItems
      .filter((l) => selected.has(l.attendanceId))
      .reduce((sum, l) => sum + l.totalSellAmount, 0);
  });

  readonly selectedMaterialTotal = computed(() => {
    const ws = this.worksheet();
    if (!ws) return 0;
    const selected = this.selectedMaterialLines();
    return ws.materialItems
      .filter((m) => selected.has(m.lineId))
      .reduce((sum, m) => {
        const qty = selected.get(m.lineId) || 0;
        return sum + qty * m.sellingPrice;
      }, 0);
  });

  readonly grandTotal = computed(() => this.selectedLaborTotal() + this.selectedMaterialTotal());

  ngOnInit(): void {
    void this.loadInitialData();
  }

  async loadInitialData(): Promise<void> {
    try {
      this.loading.set(true);
      const [sitesData, tradesData] = await Promise.all([
        this.siteApi.listSites(),
        this.siteApi.listTrades(),
      ]);
      this.sites.set(sitesData);
      this.trades.set(tradesData);
      if (sitesData.length > 0 && !this.selectedSiteId()) {
        this.selectedSiteId.set(sitesData[0].siteId);
        await this.loadWorksheet();
      }
    } catch (e: any) {
      this.errorMessage.set(e?.message || 'Failed to initialize site billing data.');
    } finally {
      this.loading.set(false);
    }
  }

  async onSiteChange(siteId: number): Promise<void> {
    this.selectedSiteId.set(siteId);
    this.selectedAttendanceIds.set(new Set());
    this.selectedMaterialLines.set(new Map());
    this.errorMessage.set(null);
    this.successMessage.set(null);
    await this.loadWorksheet();
  }

  async loadWorksheet(): Promise<void> {
    const siteId = this.selectedSiteId();
    if (!siteId) return;

    try {
      this.loading.set(true);
      const ws = await this.siteApi.getBillingWorksheet(
        siteId,
        this.fromDate() || null,
        this.toDate() || null,
        this.selectedTradeId() || null
      );
      this.worksheet.set(ws);
      // Auto-select all unbilled items initially
      const attSet = new Set<number>();
      ws.laborItems.forEach((l) => attSet.add(l.attendanceId));
      this.selectedAttendanceIds.set(attSet);

      const matMap = new Map<number, number>();
      ws.materialItems.forEach((m) => matMap.set(m.lineId, m.unbilledQuantity));
      this.selectedMaterialLines.set(matMap);
    } catch (e: any) {
      this.errorMessage.set(e?.message || 'Failed to load billing worksheet.');
    } finally {
      this.loading.set(false);
    }
  }

  toggleLabor(attendanceId: number): void {
    const set = new Set(this.selectedAttendanceIds());
    if (set.has(attendanceId)) {
      set.delete(attendanceId);
    } else {
      set.add(attendanceId);
    }
    this.selectedAttendanceIds.set(set);
  }

  toggleMaterial(lineId: number, maxQty: number): void {
    const map = new Map(this.selectedMaterialLines());
    if (map.has(lineId)) {
      map.delete(lineId);
    } else {
      map.set(lineId, maxQty);
    }
    this.selectedMaterialLines.set(map);
  }

  updateMaterialQty(lineId: number, qty: number, maxQty: number): void {
    const map = new Map(this.selectedMaterialLines());
    const validQty = Math.min(Math.max(0, qty), maxQty);
    if (validQty > 0) {
      map.set(lineId, validQty);
    } else {
      map.delete(lineId);
    }
    this.selectedMaterialLines.set(map);
  }

  async generateInvoice(): Promise<void> {
    const siteId = this.selectedSiteId();
    if (!siteId) return;

    const attIds = Array.from(this.selectedAttendanceIds());
    const matLines: MaterialLineToBillDto[] = [];
    this.selectedMaterialLines().forEach((qty, lineId) => {
      if (qty > 0) {
        matLines.push({ lineId, quantityToBill: qty });
      }
    });

    if (attIds.length === 0 && matLines.length === 0) {
      this.errorMessage.set('Please select at least one labor attendance row or material line to invoice.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const req: CreateSiteBillRequest = {
      siteId,
      billingDate: this.billingDate,
      dueDate: this.dueDate,
      attendanceIdsToBill: attIds,
      materialLinesToBill: matLines,
      notes: this.notes,
    };

    try {
      const result = await this.siteApi.createSiteBill(req);
      this.successMessage.set(
        `Sales invoice generated and posted successfully! Bill: ${result.billingNumber}, Invoice: #${result.salesInvoiceId}`
      );
      this.selectedAttendanceIds.set(new Set());
      this.selectedMaterialLines.set(new Map());
      await this.loadWorksheet();
    } catch (e: any) {
      this.errorMessage.set(e?.error?.message || e?.message || 'Invoice generation failed.');
    } finally {
      this.loading.set(false);
    }
  }
}
