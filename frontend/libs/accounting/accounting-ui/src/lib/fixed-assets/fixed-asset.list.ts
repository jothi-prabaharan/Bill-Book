import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ColumnDef, DataGridComponent } from '@bill-book/ui-components';
import { FixedAssetFormComponent } from './fixed-asset-form.component';

export interface FixedAsset {
  fixedAssetId: number;
  assetCode: string;
  assetName: string;
  purchaseDate: string;
  purchasePrice: number;
  status: string;
}

/**
 * Accounts › Fixed assets — the register, and the form that adds to it.
 *
 * <b>Written twice.</b> The first version imported Ionic, which this workspace
 * has never had: `@ionic/angular/standalone` and `ionicons` are in no
 * package.json here, so the file could not compile and — because
 * `apps/desktop` was the only app anyone expected to be Ionic-shaped — nobody
 * noticed until the whole workspace was typechecked. It also drove the report
 * grid, which wants a server-run report behind it, for what is a plain list of
 * rows. This is the same shape every other list in Accounts uses.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-fixed-asset-list',
  standalone: true,
  imports: [DataGridComponent, FixedAssetFormComponent],
  templateUrl: './fixed-asset.list.html',
  styleUrl: './fixed-asset.list.scss',
})
export class FixedAssetListComponent implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<FixedAsset[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly adding = signal(false);

  protected readonly columns: ColumnDef[] = [
    { field: 'assetCode', header: 'Code', dataType: 'string', sortable: true },
    { field: 'assetName', header: 'Asset', dataType: 'string', sortable: true },
    { field: 'purchaseDate', header: 'Purchased', dataType: 'date', sortable: true },
    { field: 'purchasePrice', header: 'Cost', dataType: 'money', align: 'right', sortable: true },
    { field: 'status', header: 'Status', dataType: 'status', sortable: true },
  ];

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);

    try {
      const assets = await this.req<FixedAsset[]>('/api/accounting/fixed-assets');
      this.rows.set(assets ?? []);
    } catch {
      this.error.set('The fixed asset register could not be read. Try again.');
    } finally {
      this.busy.set(false);
    }
  }

  protected openAdd(): void {
    this.adding.set(true);
  }

  /** The form closed. It reports whether anything was actually written. */
  protected onFormClosed(saved: boolean): void {
    this.adding.set(false);

    if (saved) {
      void this.load();
    }
  }

  private req<T>(url: string): Promise<T> {
    return new Promise<T>((resolve, reject) => {
      this.http.get<T>(url).subscribe({ next: resolve, error: reject });
    });
  }
}
