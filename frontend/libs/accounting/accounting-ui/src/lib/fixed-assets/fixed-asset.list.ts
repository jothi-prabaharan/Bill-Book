import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ReportGridComponent } from '@bill-book/shared/ui-components';
import { ReportQuery, ReportResult } from '@bill-book/reporting-core';
import { IonHeader, IonToolbar, IonTitle, IonContent, IonButtons, IonButton, IonIcon } from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { add } from 'ionicons/icons';

@Component({
  selector: 'bb-fixed-asset-list',
  standalone: true,
  imports: [ReportGridComponent, IonHeader, IonToolbar, IonTitle, IonContent, IonButtons, IonButton, IonIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-title>Fixed Assets</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <bb-report-grid
        [result]="reportResult()"
        [state]="query()"
        [busy]="loading()"
        (stateChange)="onStateChange($event)"
      ></bb-report-grid>
    </ion-content>
  `
})
export class FixedAssetListComponent implements OnInit {
  private readonly http = inject(HttpClient);

  readonly loading = signal(false);
  readonly query = signal<ReportQuery>({
    reportKey: 'fixed-assets',
    page: { number: 1, size: 25, includeCount: true },
    sorts: [],
    filters: [],
    freeze: { columns: 0, rows: 0 },
    options: {}
  });

  readonly reportResult = signal<ReportResult | null>(null);

  constructor() {
    addIcons({ add });
  }

  ngOnInit() {
    this.loadAssets();
  }

  onStateChange(state: ReportQuery) {
    this.query.set(state);
  }

  private loadAssets() {
    this.loading.set(true);
    this.http.get<any[]>('/api/accounting/fixed-assets').subscribe({
      next: (assets) => {
        this.reportResult.set({
          reportKey: 'fixed-assets',
          columns: [
            { key: 'assetCode', header: 'Code', dataType: 'Text', isSortable: true, isPrimary: true },
            { key: 'assetName', header: 'Name', dataType: 'Text', isSortable: true, isPrimary: true },
            { key: 'purchaseDate', header: 'Purchase Date', dataType: 'Date', isSortable: true },
            { key: 'purchasePrice', header: 'Purchase Price', dataType: 'Money', isSortable: true },
            { key: 'status', header: 'Status', dataType: 'Text', isSortable: true }
          ],
          rows: assets,
          page: { number: 1, size: Math.max(assets.length, 1), totalElements: assets.length, totalPages: 1 },
          currency: { code: 'INR', decimals: 2 },
          groups: [],
          footer: null
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
