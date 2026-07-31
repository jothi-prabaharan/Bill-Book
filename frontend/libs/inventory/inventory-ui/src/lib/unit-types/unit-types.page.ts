import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface Unit {
  uomId: number;
  uomTypeId: number;
  uomSystemName: string | null;
  uomCode: string;
  uqcCode: string;
  uomName: string;
  isBaseUnit: boolean;
  conversionToBase: number;
  decimalPlaces: number;
  displayOrder: number;
  isSystem: boolean;
  isActive: boolean;
  isUsed: boolean;
}

interface UomType {
  uomTypeId: number;
  uomTypeSystemName: string | null;
  uomTypeName: string;
  displayOrder: number;
  isSystem: boolean;
  isActive: boolean;
  units: Unit[];
}

/**
 * Settings › Inventory › Unit types. Types and their units on one screen,
 * because a unit only means anything relative to its type's base.
 *
 * The base unit is a radio down the units column. Moving it rescales every
 * factor in the type, so it prompts — and the server refuses outright once any
 * item uses a unit of that type.
 */
@Component({
  selector: 'bb-unit-types-page',
  standalone: true,
  imports: [FormsModule, CdkDropList, CdkDrag, CdkDragHandle],
  templateUrl: './unit-types.page.html',
  styleUrl: './unit-types.page.scss',
})
export class UnitTypesPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly types = signal<UomType[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly expanded = signal<number | null>(null);
  protected readonly editingUnitId = signal<number | null>(null);

  showInactive = false;
  newTypeName = '';
  newUnit = this.blankUnit();
  editUnit = this.blankUnit();

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.types.set(await this.get<UomType[]>(`/api/uom-types?includeInactive=${this.showInactive}`));
    } catch {
      this.fail('Could not load unit types.');
    } finally {
      this.busy.set(false);
    }
  }

  toggle(type: UomType): void {
    this.expanded.set(this.expanded() === type.uomTypeId ? null : type.uomTypeId);
    this.editingUnitId.set(null);
    this.newUnit = this.blankUnit();
  }

  async addType(): Promise<void> {
    if (!this.newTypeName.trim()) {
      return;
    }

    await this.run(async () => {
      await this.send('POST', '/api/uom-types', {
        uomTypeName: this.newTypeName.trim(),
        isActive: true,
      });
      this.newTypeName = '';
    }, 'Unit type added.');
  }

  async renameType(type: UomType, name: string): Promise<void> {
    await this.run(
      () =>
        this.send('PUT', `/api/uom-types/${type.uomTypeId}`, {
          uomTypeName: name.trim(),
          isActive: type.isActive,
        }),
      'Unit type saved.',
    );
  }

  async toggleTypeActive(type: UomType): Promise<void> {
    await this.run(
      () =>
        this.send('PUT', `/api/uom-types/${type.uomTypeId}`, {
          uomTypeName: type.uomTypeName,
          isActive: !type.isActive,
        }),
      'Unit type saved.',
    );
  }

  async deleteType(type: UomType): Promise<void> {
    await this.run(() => this.send('DELETE', `/api/uom-types/${type.uomTypeId}`, {}), 'Unit type deleted.');
  }

  async addUnit(type: UomType): Promise<void> {
    if (!this.newUnit.uomCode.trim() || !this.newUnit.uomName.trim()) {
      return;
    }

    await this.run(async () => {
      await this.send('POST', '/api/uom-types/units', {
        ...this.newUnit,
        uomTypeId: type.uomTypeId,
        uomCode: this.newUnit.uomCode.trim().toUpperCase(),
        uqcCode: (this.newUnit.uqcCode || 'OTH').trim().toUpperCase(),
      });
      this.newUnit = this.blankUnit();
    }, 'Unit added.');
  }

  startEditUnit(unit: Unit): void {
    this.editingUnitId.set(unit.uomId);
    this.editUnit = {
      uomCode: unit.uomCode,
      uqcCode: unit.uqcCode,
      uomName: unit.uomName,
      conversionToBase: unit.conversionToBase,
      decimalPlaces: unit.decimalPlaces,
      isActive: unit.isActive,
    };
  }

  async saveUnit(type: UomType, unit: Unit): Promise<void> {
    await this.run(async () => {
      await this.send('PUT', `/api/uom-types/units/${unit.uomId}`, {
        ...this.editUnit,
        uomTypeId: type.uomTypeId,
        uomCode: this.editUnit.uomCode.trim().toUpperCase(),
        uqcCode: (this.editUnit.uqcCode || 'OTH').trim().toUpperCase(),
      });
      this.editingUnitId.set(null);
    }, 'Unit saved.');
  }

  /**
   * Moving the base rewrites every factor in the type, so it asks first. The
   * server refuses outright once an item uses one of these units.
   */
  async setBase(unit: Unit): Promise<void> {
    const confirmed = confirm(
      `Make ${unit.uomCode} the base unit? Every other factor in this type is rescaled so the ` +
        `relationships between units stay the same.`,
    );

    if (!confirmed) {
      return;
    }

    await this.run(
      () => this.send('PUT', `/api/uom-types/units/${unit.uomId}/base`, {}),
      'Base unit changed.',
    );
  }

  async deleteUnit(unit: Unit): Promise<void> {
    await this.run(() => this.send('DELETE', `/api/uom-types/units/${unit.uomId}`, {}), 'Unit deleted.');
  }

  async onDrop(event: CdkDragDrop<UomType[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.types()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.types.set(list);

    await this.run(
      () =>
        this.send('PATCH', '/api/uom-types/reorder', {
          movedId: moved.uomTypeId,
          previousId: list[event.currentIndex - 1]?.uomTypeId ?? null,
          nextId: list[event.currentIndex + 1]?.uomTypeId ?? null,
        }),
      null,
    );
  }

  protected get canReorder(): boolean {
    return !this.showInactive;
  }

  /** What one of this unit is worth in the type's base unit, spelled out. */
  protected describe(type: UomType, unit: Unit): string {
    const base = type.units.find((u) => u.isBaseUnit);
    if (!base || unit.isBaseUnit) {
      return 'Base unit';
    }

    return `1 ${unit.uomCode} = ${unit.conversionToBase} ${base.uomCode}`;
  }

  private blankUnit() {
    return {
      uomCode: '',
      uqcCode: 'OTH',
      uomName: '',
      conversionToBase: 1,
      decimalPlaces: 0,
      isActive: true,
    };
  }

  private async run(action: () => Promise<unknown>, ok: string | null): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      await action();
      if (ok) {
        this.message.set(ok);
        this.messageIsError.set(false);
      }
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'That did not work.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}
