import { ChangeDetectionStrategy, booleanAttribute, computed, input } from '@angular/core';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MASTER_STATUSES,
  MessageBoxComponent,
  RegisterHeaderComponent,
  StatusTabsComponent,
  TextInputComponent,
  countStatusTabs,
  masterStatus,
  rowsInStatus,
} from '@bill-book/ui-components';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InfoTooltipComponent } from '@bill-book/ui-components';

export interface ContactPersonRole {
  contactPersonRoleId: number;
  roleSystemName: string | null;
  roleName: string;
  displayOrder: number;
  isDefault: boolean;
  isSystem: boolean;
  isActive: boolean;
  usageCount: number;
}

/**
 * The role master itself — the table, the add box and every action on them.
 *
 * <b>It has no chrome of its own</b>, because it is shown two ways and neither
 * owns it. A popup opens from the contact form, so somebody halfway through a
 * contact can add a missing role without abandoning what they have typed; a
 * Settings page shows the same master beside the others, which is where anybody
 * setting a branch up will look for it. Two copies of this list would be two
 * places to fix the next behaviour change, and they would drift.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-contact-person-roles-list',
  standalone: true,
  imports: [InfoTooltipComponent,
    DataGridCellTemplateDirective,
    DataGridComponent,
    FormsModule,
    TextInputComponent,
    MessageBoxComponent,
    RegisterHeaderComponent,
    StatusTabsComponent,
  ],
  host: { '[class.register-page]': 'register()' },
  templateUrl: './contact-person-roles.list.html',
  styleUrl: './contact-person-roles.list.scss',
})
export class ContactPersonRolesList implements OnInit {
  /** Emits whenever roles are added, edited, reordered or removed. */
  readonly changed = output<void>();

  /**
   * The Settings page's register design (TK-190): heading, status tabs and
   * card. The popup on the contact form leaves it off and keeps the compact
   * list, with the add box beneath it.
   */
  readonly register = input(false, { transform: booleanAttribute });

  columns: ColumnDef[] = [
    { field: 'roleName', header: 'Role' },
    { field: 'usageCount', header: 'In use' },
    { field: 'isDefault', header: 'Default' },
    { field: 'isActive', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  private readonly http = inject(HttpClient);

  protected readonly rows = signal<ContactPersonRole[]>([]);
  protected readonly status = signal('Active');
  protected readonly statusTabs = computed(() => countStatusTabs(this.rows(), MASTER_STATUSES, masterStatus));
  protected readonly visibleRows = computed(() => rowsInStatus(this.rows(), this.status(), masterStatus));
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly editingId = signal<number | null>(null);

  newRoleName = '';
  editName = '';

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.get<ContactPersonRole[]>('/api/contact-person-roles?includeInactive=true'));
    } catch {
      this.error.set('Could not load roles.');
    } finally {
      this.busy.set(false);
    }
  }

  async add(): Promise<void> {
    if (!this.newRoleName.trim()) {
      return;
    }

    await this.run(async () => {
      await this.send('POST', '/api/contact-person-roles', {
        roleName: this.newRoleName.trim(),
        isActive: true,
      });
      this.newRoleName = '';
    });
  }

  startEdit(row: ContactPersonRole): void {
    this.editingId.set(row.contactPersonRoleId);
    this.editName = row.roleName;
  }

  async saveEdit(row: ContactPersonRole): Promise<void> {
    if (!this.editName.trim()) {
      this.error.set('Role name is required.');
      return;
    }

    await this.run(async () => {
      await this.send('PUT', `/api/contact-person-roles/${row.contactPersonRoleId}`, {
        roleName: this.editName.trim(),
        isActive: row.isActive,
      });
      this.editingId.set(null);
    });
  }

  async toggleActive(row: ContactPersonRole): Promise<void> {
    await this.run(() =>
      this.send('PUT', `/api/contact-person-roles/${row.contactPersonRoleId}`, {
        roleName: row.roleName,
        isActive: !row.isActive,
      }),
    );
  }

  async makeDefault(row: ContactPersonRole): Promise<void> {
    await this.run(() =>
      this.send('PUT', `/api/contact-person-roles/${row.contactPersonRoleId}/default`, {}),
    );
  }

  async remove(row: ContactPersonRole): Promise<void> {
    await this.run(() => this.send('DELETE', `/api/contact-person-roles/${row.contactPersonRoleId}`, {}));
  }

  async onDrop(event: CdkDragDrop<ContactPersonRole[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    await this.run(() =>
      this.send('PATCH', '/api/contact-person-roles/reorder', {
        movedId: moved.contactPersonRoleId,
        previousId: list[event.currentIndex - 1]?.contactPersonRoleId ?? null,
        nextId: list[event.currentIndex + 1]?.contactPersonRoleId ?? null,
      }),
    );
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
      await this.load();
      this.changed.emit();
    } catch (err: unknown) {
      const anyErr = err as {
        error?: {
          message?: string;
          detail?: string;
          title?: string;
          errors?: Record<string, string[]>;
        };
      };
      const errObj = anyErr?.error;
      const extracted = errObj?.message
        || (errObj?.errors ? Object.values(errObj.errors).flat().join(' ') : null)
        || errObj?.detail
        || errObj?.title
        || 'That did not work.';
      this.error.set(extracted);
      await this.load();
    } finally {
      this.busy.set(false);
    }
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

