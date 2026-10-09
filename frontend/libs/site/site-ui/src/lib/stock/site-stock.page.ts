import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '@bill-book/auth';
import {
  DispatchStockRequest,
  ReturnStockRequest,
  SubmitMaterialUsageRequest,
  SiteApiService,
  SiteDto,
  SiteStockBalanceDto,
  SiteStockDocumentDto,
} from '@bill-book/site-core';

type StockViewTab = 'balances' | 'documents' | 'dispatch' | 'return' | 'usage' | 'pending-usage';

@Component({
  selector: 'bb-site-stock-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './site-stock.page.html',
  styleUrl: './site-stock.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteStockPage implements OnInit {
  private readonly siteApi = inject(SiteApiService);
  private readonly auth = inject(AuthService);

  readonly tab = signal<StockViewTab>('balances');
  readonly loading = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly sites = signal<SiteDto[]>([]);
  readonly selectedSiteId = signal<number | null>(null);
  readonly balances = signal<SiteStockBalanceDto[]>([]);
  readonly documents = signal<SiteStockDocumentDto[]>([]);
  readonly pendingUsages = signal<SiteStockDocumentDto[]>([]);

  readonly canEdit = computed(() => this.auth.has('site.edit'));
  readonly canViewCost = computed(() => this.auth.has('site.cost.view'));

  // Dispatch form state
  dispatchForm = {
    sourceWarehouseId: 1,
    documentDate: new Date().toISOString().substring(0, 10),
    itemId: 1,
    quantity: 1,
    unitCost: null as number | null,
    remarks: '',
  };

  // Return form state
  returnForm = {
    destinationWarehouseId: 1,
    documentDate: new Date().toISOString().substring(0, 10),
    itemId: 1,
    quantity: 1,
    unitCost: null as number | null,
    remarks: '',
  };

  // Material usage form state
  usageForm = {
    documentDate: new Date().toISOString().substring(0, 10),
    itemId: 1,
    quantity: 1,
    unitCost: null as number | null,
    remarks: '',
  };

  async ngOnInit(): Promise<void> {
    await this.loadSites();
  }

  async loadSites(): Promise<void> {
    try {
      this.loading.set(true);
      const data = await this.siteApi.listSites();
      this.sites.set(data);
      if (data.length > 0 && !this.selectedSiteId()) {
        this.selectedSiteId.set(data[0].siteId);
        await this.refreshSiteData();
      }
    } catch (e: any) {
      this.errorMessage.set(e?.message || 'Failed to load sites.');
    } finally {
      this.loading.set(false);
    }
  }

  async onSiteChange(siteId: number): Promise<void> {
    this.selectedSiteId.set(siteId);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    await this.refreshSiteData();
  }

  async refreshSiteData(): Promise<void> {
    await Promise.all([
      this.loadBalances(),
      this.loadDocuments(),
      this.loadPendingUsages(),
    ]);
  }

  async loadBalances(): Promise<void> {
    const siteId = this.selectedSiteId();
    if (!siteId) return;

    try {
      const data = await this.siteApi.listSiteStockBalances(siteId);
      this.balances.set(data);
    } catch (e: any) {
      this.errorMessage.set(e?.message || 'Failed to load balances.');
    }
  }

  async loadDocuments(): Promise<void> {
    const siteId = this.selectedSiteId();
    if (!siteId) return;

    try {
      const data = await this.siteApi.listSiteStockDocuments(siteId);
      this.documents.set(data);
    } catch (e: any) {
      this.errorMessage.set(e?.message || 'Failed to load stock documents.');
    }
  }

  async loadPendingUsages(): Promise<void> {
    const siteId = this.selectedSiteId();
    try {
      const data = await this.siteApi.listPendingMaterialUsage(siteId);
      this.pendingUsages.set(data);
    } catch (e: any) {
      this.errorMessage.set(e?.message || 'Failed to load pending usage requests.');
    }
  }

  async submitDispatch(): Promise<void> {
    const siteId = this.selectedSiteId();
    if (!siteId) {
      this.errorMessage.set('Please select a site first.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const req: DispatchStockRequest = {
      siteId,
      sourceWarehouseId: this.dispatchForm.sourceWarehouseId,
      documentDate: this.dispatchForm.documentDate,
      lines: [
        {
          itemId: this.dispatchForm.itemId,
          quantity: this.dispatchForm.quantity,
          unitCost: this.dispatchForm.unitCost,
        },
      ],
      remarks: this.dispatchForm.remarks,
    };

    try {
      const doc = await this.siteApi.dispatchStock(req);
      this.successMessage.set(`Dispatch posted successfully: ${doc.documentNo}`);
      await this.refreshSiteData();
      this.tab.set('balances');
    } catch (e: any) {
      this.errorMessage.set(e?.error?.message || e?.message || 'Dispatch failed.');
    } finally {
      this.loading.set(false);
    }
  }

  async submitReturn(): Promise<void> {
    const siteId = this.selectedSiteId();
    if (!siteId) {
      this.errorMessage.set('Please select a site first.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const req: ReturnStockRequest = {
      siteId,
      destinationWarehouseId: this.returnForm.destinationWarehouseId,
      documentDate: this.returnForm.documentDate,
      lines: [
        {
          itemId: this.returnForm.itemId,
          quantity: this.returnForm.quantity,
          unitCost: this.returnForm.unitCost,
        },
      ],
      remarks: this.returnForm.remarks,
    };

    try {
      const doc = await this.siteApi.returnStock(req);
      this.successMessage.set(`Return posted successfully: ${doc.documentNo}`);
      await this.refreshSiteData();
      this.tab.set('balances');
    } catch (e: any) {
      this.errorMessage.set(e?.error?.message || e?.message || 'Return failed.');
    } finally {
      this.loading.set(false);
    }
  }

  async submitUsage(): Promise<void> {
    const siteId = this.selectedSiteId();
    if (!siteId) {
      this.errorMessage.set('Please select a site first.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const req: SubmitMaterialUsageRequest = {
      siteId,
      documentDate: this.usageForm.documentDate,
      lines: [
        {
          itemId: this.usageForm.itemId,
          quantity: this.usageForm.quantity,
          unitCost: this.usageForm.unitCost,
        },
      ],
      remarks: this.usageForm.remarks,
    };

    try {
      const doc = await this.siteApi.submitMaterialUsage(req);
      this.successMessage.set(`Material usage submitted for approval: ${doc.documentNo}`);
      await this.refreshSiteData();
      this.tab.set('pending-usage');
    } catch (e: any) {
      this.errorMessage.set(e?.error?.message || e?.message || 'Material usage submission failed.');
    } finally {
      this.loading.set(false);
    }
  }

  async approveUsage(id: number): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const doc = await this.siteApi.approveMaterialUsage(id);
      this.successMessage.set(`Usage ${doc.documentNo} approved. On-site stock updated.`);
      await this.refreshSiteData();
    } catch (e: any) {
      this.errorMessage.set(e?.error?.message || e?.message || 'Approval failed.');
    } finally {
      this.loading.set(false);
    }
  }

  async rejectUsage(id: number): Promise<void> {
    const reason = prompt('Enter rejection reason:') || 'Rejected by supervisor';
    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const doc = await this.siteApi.rejectMaterialUsage(id, reason);
      this.successMessage.set(`Usage ${doc.documentNo} rejected.`);
      await this.refreshSiteData();
    } catch (e: any) {
      this.errorMessage.set(e?.error?.message || e?.message || 'Rejection failed.');
    } finally {
      this.loading.set(false);
    }
  }
}
