import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { Building, FacilityApiService, SPACE_KINDS, SaveBuilding, SaveSpace, Space, floorLabel } from '@bill-book/facility-core';
import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

type Tab = 'buildings' | 'spaces';

/**
 * Maintenance › Buildings and spaces (S5, TK-65). A space sits in a building;
 * neither is deleted, because work orders and assets name them. A building is
 * deactivated once its spaces are.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-facility-spaces-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    TextInputComponent,
    NumberInputComponent,
    SelectComponent,
    CheckboxComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './spaces.page.html',
  styleUrl: '../facility-page.scss',
})
export class SpacesPage implements OnInit {
  private readonly api = inject(FacilityApiService);

  protected readonly tab = signal<Tab>('buildings');
  protected readonly buildings = signal<Building[]>([]);
  protected readonly spaces = signal<Space[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);

  protected buildingForm: SaveBuilding = { code: '', name: '', floors: 1, isActive: true };
  protected spaceForm: SaveSpace = { buildingId: 0, code: '', name: '', spaceKind: 'Classroom', floor: 0, capacity: null, isActive: true };

  protected readonly kinds = [...SPACE_KINDS];
  protected readonly floorLabel = floorLabel;
  protected readonly buildingOptions = computed<BbSelectOption<number>[]>(() =>
    this.buildings().filter((b) => b.isActive).map((b) => ({ value: b.buildingId, label: `${b.code} ${b.name}` })),
  );

  protected readonly buildingColumns: ColumnDef[] = [
    { field: 'code', header: 'Code' },
    { field: 'name', header: 'Name' },
    { field: 'floors', header: 'Floors' },
    { field: 'spaces', header: 'Spaces' },
    { field: 'isActive', header: 'Status' },
    { field: 'actions', header: '' },
  ];
  protected readonly spaceColumns: ColumnDef[] = [
    { field: 'code', header: 'Code' },
    { field: 'name', header: 'Name' },
    { field: 'buildingName', header: 'Building' },
    { field: 'floor', header: 'Floor' },
    { field: 'spaceKind', header: 'Kind' },
    { field: 'isActive', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  protected choose(tab: Tab): void {
    this.tab.set(tab);
    this.editingId.set(undefined);
  }

  protected startAdd(): void {
    this.buildingForm = { code: '', name: '', floors: 1, isActive: true };
    this.spaceForm = { buildingId: this.buildingOptions()[0]?.value ?? 0, code: '', name: '', spaceKind: 'Classroom', floor: 0, capacity: null, isActive: true };
    this.editingId.set(null);
  }

  protected editBuilding(row: Building): void {
    this.buildingForm = { code: row.code, name: row.name, floors: row.floors, isActive: row.isActive };
    this.editingId.set(row.buildingId);
  }

  protected editSpace(row: Space): void {
    this.spaceForm = { ...row };
    this.editingId.set(row.spaceId);
  }

  protected cancel(): void {
    this.editingId.set(undefined);
  }

  protected async save(): Promise<void> {
    const id = this.editingId() ?? null;
    this.busy.set(true);
    try {
      if (this.tab() === 'buildings') {
        await this.api.saveBuilding(id, this.buildingForm);
      } else {
        await this.api.saveSpace(id, this.spaceForm);
      }

      this.messages.set([{ tone: 'success', text: 'Saved.' }]);
      this.editingId.set(undefined);
      await this.load();
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      const [buildings, spaces] = await Promise.all([this.api.buildings(), this.api.spaces()]);
      this.buildings.set(buildings);
      this.spaces.set(spaces);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }
}
