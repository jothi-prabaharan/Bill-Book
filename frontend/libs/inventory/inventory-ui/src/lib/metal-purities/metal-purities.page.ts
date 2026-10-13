import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  ConfirmDialogService,
  DataGridCellTemplateDirective,
  DataGridComponent,
  IconButtonComponent,
  MASTER_STATUSES,
  MessageBoxComponent,
  NumberInputComponent,
  RegisterHeaderComponent,
  SelectComponent,
  StatusTabsComponent,
  TextInputComponent,
  countStatusTabs,
  masterStatus,
  rowsInStatus,
} from '@bill-book/ui-components';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InfoTooltipComponent } from '@bill-book/ui-components';

interface Purity {
  metalPurityId: number;
  metalType: string;
  puritySystemName: string | null;
  purityName: string;
  purityFactor: number;
  displayOrder: number;
  isSystem: boolean;
  isActive: boolean;
}

/**
 * Settings › Inventory › Metal purities. Small, but the factor here scales the
 * metal rate — a wrong figure misprices every piece of that purity, so it is
 * frozen once an item uses it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-metal-purities-page',
  standalone: true,
  imports: [
    InfoTooltipComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    IconButtonComponent,
    FormsModule,
    TextInputComponent,
    NumberInputComponent,
    CheckboxComponent,
    SelectComponent,
    MessageBoxComponent,
    RegisterHeaderComponent,
    StatusTabsComponent,
  ],
  host: {
    class: 'register-page',
    '(document:keydown.escape)': 'onEscape()',
  },
  templateUrl: './metal-purities.page.html',
  styleUrl: './metal-purities.page.scss',
})
export class MetalPuritiesPage implements OnInit {

  /** The filter offers no "Other": nothing is seeded under it. */
  protected readonly metalFilterOptions: BbSelectOption<string>[] = [
    { value: 'Gold', label: 'Gold' },
    { value: 'Silver', label: 'Silver' },
    { value: 'Platinum', label: 'Platinum' },
    { value: 'Diamond', label: 'Diamond' },
  ];

  protected readonly metalOptions: BbSelectOption<string>[] = [
    ...this.metalFilterOptions,
    { value: 'Other', label: 'Other' },
  ];
  private readonly http = inject(HttpClient);
  private readonly confirmDialog = inject(ConfirmDialogService);

  columns: ColumnDef[] = [
    { field: 'metalType', header: 'Metal' },
    { field: 'purityName', header: 'Purity' },
    { field: 'purityFactor', header: 'Factor' },
    { field: 'isActive', header: 'Status' },
    { field: 'actions', header: '' }
  ];

  protected readonly rows = signal<Purity[]>([]);
  /** The register's tabs (TK-190): every row is loaded, inactive ones too, and the tab filters. */
  protected readonly status = signal('Active');
  protected readonly statusTabs = computed(() => countStatusTabs(this.rows(), MASTER_STATUSES, masterStatus));
  protected readonly visibleRows = computed(() => rowsInStatus(this.rows(), this.status(), masterStatus));
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editingId = signal<number | null>(null);

  metalFilter = '';
  form = { metalType: 'Gold', purityName: '', purityFactor: 0.916, isActive: true };

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const query = 'includeInactive=true' +
        (this.metalFilter ? `&metalType=${this.metalFilter}` : '');
      this.rows.set(await this.get<Purity[]>(`/api/metal-purities?${query}`));
    } catch {
      this.fail('Could not load purities.');
    } finally {
      this.busy.set(false);
    }
  }

  startEdit(row: Purity): void {
    this.editingId.set(row.metalPurityId);
    this.form = {
      metalType: row.metalType,
      purityName: row.purityName,
      purityFactor: row.purityFactor,
      isActive: row.isActive,
    };
  }

  startAdd(): void {
    this.editingId.set(0);
    this.form = { metalType: 'Gold', purityName: '', purityFactor: 0.916, isActive: true };
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  onEscape(): void {
    if (this.editingId() !== null) {
      this.cancelEdit();
    }
  }

  async save(): Promise<void> {
    const id = this.editingId();
    if (!this.hasText(this.form.metalType) || !this.hasText(this.form.purityName)) {
      this.fail('Metal type and purity name are required.');
      return;
    }

    if (this.form.purityFactor < 0.0001 || this.form.purityFactor > 1) {
      this.fail('Purity factor must be between 0.0001 and 1.');
      return;
    }

    const body = {
      ...this.form,
      metalType: this.form.metalType.trim(),
      purityName: this.form.purityName.trim(),
    };

    await this.run(async () => {
      if (id === 0) {
        await this.send('POST', '/api/metal-purities', body);
      } else {
        await this.send('PUT', `/api/metal-purities/${id}`, body);
      }
      this.editingId.set(null);
    }, 'Purity saved.');
  }

  async remove(row: Purity): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Delete Purity',
      message: `Delete the metal purity "${row.purityName}"?`,
      confirmText: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;

    await this.run(
      () => this.send('DELETE', `/api/metal-purities/${row.metalPurityId}`, {}),
      'Purity deleted.',
    );
  }

  async onDrop(event: CdkDragDrop<Purity[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    await this.run(
      () =>
        this.send('PATCH', '/api/metal-purities/reorder', {
          movedId: moved.metalPurityId,
          previousId: list[event.currentIndex - 1]?.metalPurityId ?? null,
          nextId: list[event.currentIndex + 1]?.metalPurityId ?? null,
        }),
      null,
    );
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

