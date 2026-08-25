import { ChangeDetectionStrategy } from '@angular/core';
import { DataGridComponent, ColumnDef , TextInputComponent } from '@bill-book/ui-components';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface Warehouse {
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
  warehouseType: string;
  storageType: string;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateId: number | null;
  countryId: number | null;
  postalCode: string | null;
  gstin: string | null;
  contactPersonName: string | null;
  phoneNumber: string | null;
  mobileNumber: string | null;
  isDefault: boolean;
  displayOrder: number;
  isActive: boolean;
}

/**
 * Inventory › Warehouses. A location dimension only — stock is one shared pool
 * and weighted average cost is company-wide, so nothing here splits inventory.
 * Per-warehouse quantities come from aggregating movements.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-warehouses-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, TextInputComponent],
  templateUrl: './warehouses.page.html',
  styleUrl: './warehouses.page.scss',
})
export class WarehousesPage implements OnInit {
  columns: ColumnDef[] = [
    { field: 'handle', header: 'Reorder' },
    { field: 'warehouseCode', header: 'Code' },
    { field: 'warehouseName', header: 'Name' },
    { field: 'warehouseType', header: 'Type' },
    { field: 'storageType', header: 'Storage' },
    { field: 'city', header: 'City' },
    { field: 'gstin', header: 'GSTIN' },
    { field: 'isDefault', header: 'Default' },
    { field: 'isActive', header: 'Active' },
    { field: 'actions', header: 'Actions' }
  ];

  private readonly http = inject(HttpClient);

  protected readonly rows = signal<Warehouse[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editingId = signal<number | null>(null);

  showInactive = false;
  form = this.blank();

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.get<Warehouse[]>(`/api/warehouses?includeInactive=${this.showInactive}`));
    } catch {
      this.fail('Could not load warehouses.');
    } finally {
      this.busy.set(false);
    }
  }

  startAdd(): void {
    this.editingId.set(0);
    this.form = this.blank();
  }

  startEdit(row: Warehouse): void {
    this.editingId.set(row.warehouseId);
    this.form = {
      warehouseCode: row.warehouseCode,
      warehouseName: row.warehouseName,
      warehouseType: row.warehouseType,
      storageType: row.storageType,
      addressLine1: row.addressLine1,
      addressLine2: row.addressLine2,
      city: row.city,
      stateId: row.stateId,
      countryId: row.countryId,
      postalCode: row.postalCode,
      gstin: row.gstin,
      contactPersonName: row.contactPersonName,
      phoneNumber: row.phoneNumber,
      mobileNumber: row.mobileNumber,
      isActive: row.isActive,
    };
  }

  async save(): Promise<void> {
    const id = this.editingId();
    if (!this.hasText(this.form.warehouseName)) {
      this.fail('Warehouse name is required.');
      return;
    }

    if (!this.hasText(this.form.warehouseType) || !this.hasText(this.form.storageType)) {
      this.fail('Warehouse type and storage type are required.');
      return;
    }

    const body = {
      ...this.form,
      warehouseCode: this.cleanOptional(this.form.warehouseCode),
      warehouseName: this.form.warehouseName.trim(),
      warehouseType: this.form.warehouseType.trim(),
      storageType: this.form.storageType.trim(),
    };

    await this.run(async () => {
      if (id === 0) {
        await this.send('POST', '/api/warehouses', body);
      } else {
        await this.send('PUT', `/api/warehouses/${id}`, body);
      }
      this.editingId.set(null);
    }, 'Warehouse saved.');
  }

  async makeDefault(row: Warehouse): Promise<void> {
    await this.run(
      () => this.send('PUT', `/api/warehouses/${row.warehouseId}/default`, {}),
      'Default warehouse changed.',
    );
  }

  async deactivate(row: Warehouse): Promise<void> {
    await this.run(
      () => this.send('DELETE', `/api/warehouses/${row.warehouseId}`, {}),
      'Warehouse deactivated.',
    );
  }

  async onDrop(event: CdkDragDrop<Warehouse[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    await this.run(
      () =>
        this.send('PATCH', '/api/warehouses/reorder', {
          movedId: moved.warehouseId,
          previousId: list[event.currentIndex - 1]?.warehouseId ?? null,
          nextId: list[event.currentIndex + 1]?.warehouseId ?? null,
        }),
      null,
    );
  }

  private blank() {
    return {
      warehouseCode: '',
      warehouseName: '',
      warehouseType: 'Store',
      storageType: 'Ambient',
      addressLine1: null as string | null,
      addressLine2: null as string | null,
      city: null as string | null,
      stateId: null as number | null,
      countryId: null as number | null,
      postalCode: null as string | null,
      gstin: null as string | null,
      contactPersonName: null as string | null,
      phoneNumber: null as string | null,
      mobileNumber: null as string | null,
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

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private cleanOptional(value: string | null | undefined): string | null {
    const cleaned = (value ?? '').trim();
    return cleaned.length > 0 ? cleaned : null;
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

