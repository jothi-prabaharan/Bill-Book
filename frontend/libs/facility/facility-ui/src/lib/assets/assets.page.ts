import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { ASSET_CATEGORIES, ASSET_STATUSES, AssetStatus, FacilityApiService, FacilityAsset, SaveAsset } from '@bill-book/facility-core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  MessageBoxComponent,
  MoneyInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * Maintenance › Facility assets (S5, TK-65): what is maintained, where it is,
 * and whether its warranty still runs. An asset is disposed, never deleted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-facility-assets-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    TextInputComponent,
    DateInputComponent,
    MoneyInputComponent,
    SelectComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './assets.page.html',
  styleUrl: '../facility-page.scss',
})
export class AssetsPage implements OnInit {
  private readonly api = inject(FacilityApiService);

  protected readonly rows = signal<FacilityAsset[]>([]);
  protected readonly spaceOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);

  protected search = '';
  protected status: AssetStatus | null = null;
  protected form: SaveAsset = AssetsPage.blank();

  protected readonly categories = [...ASSET_CATEGORIES];
  protected readonly statuses = [...ASSET_STATUSES];

  protected readonly columns: ColumnDef[] = [
    { field: 'assetTag', header: 'Tag' },
    { field: 'name', header: 'Asset' },
    { field: 'assetCategory', header: 'Category' },
    { field: 'spaceName', header: 'Where' },
    { field: 'warrantyUntil', header: 'Warranty' },
    { field: 'assetStatus', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected statusLabel(status: AssetStatus): string {
    return ASSET_STATUSES.find((s) => s.value === status)?.label ?? status;
  }

  protected startAdd(): void {
    this.form = AssetsPage.blank();
    this.editingId.set(null);
  }

  protected edit(row: FacilityAsset): void {
    this.form = { ...row };
    this.editingId.set(row.facilityAssetId);
  }

  protected async load(): Promise<void> {
    try {
      this.rows.set(await this.api.assets({ status: this.status, search: this.search.trim() }));
    } catch (error) {
      this.fail(error);
    }
  }

  protected async save(): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.saveAsset(this.editingId() ?? null, this.form);
      this.messages.set([{ tone: 'success', text: 'The asset is saved.' }]);
      this.editingId.set(undefined);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async start(): Promise<void> {
    try {
      const spaces = await this.api.spaces();
      this.spaceOptions.set(spaces.filter((s) => s.isActive).map((s) => ({ value: s.spaceId, label: `${s.code} ${s.name}`, group: s.buildingName })));
    } catch (error) {
      this.fail(error);
    }

    await this.load();
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }

  private static blank(): SaveAsset {
    return {
      assetTag: '', name: '', assetCategory: 'Other', spaceId: null, make: null, model: null, serialNo: null,
      purchaseDate: null, warrantyUntil: null, purchaseCost: null, assetStatus: 'InUse',
    };
  }
}
