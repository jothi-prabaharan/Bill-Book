import { DataGridComponent, ColumnDef , TextInputComponent } from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface RoleListItem {
  roleId: number;
  displayName: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  userCount: number;
  permissionCount: number;
}

interface RoleDetail extends RoleListItem {
  permissionIds: number[];
}

interface PermissionItem {
  permissionId: number;
  code: string;
  action: string;
}

interface PermissionGroup {
  module: string;
  permissions: PermissionItem[];
}

/**
 * Role master. System roles can be relabelled but their permission matrix is
 * read-only and they cannot be deleted; customer roles are fully editable.
 * The matrix is 120 checkboxes, so modules are an accordion with select-all
 * per row and collapse to one module per screen on mobile.
 */
@Component({
  selector: 'bb-roles-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, TextInputComponent],
  templateUrl: './roles.page.html',
  styleUrl: './roles.page.scss',
})
export class RolesPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly roles = signal<RoleListItem[]>([]);
  protected readonly matrix = signal<PermissionGroup[]>([]);
  protected readonly selected = signal<Set<number>>(new Set());
  protected readonly editing = signal(false);
  protected readonly isSystem = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private editingId: number | null = null;
  form = { displayName: '', description: '' };

  columns: ColumnDef[] = [
    { field: 'displayName', header: 'Role' },
    { field: 'description', header: 'Description' },
    { field: 'userCount', header: 'Users' },
    { field: 'permissionCount', header: 'Permissions' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.roles.set(await this.req<RoleListItem[]>('GET', '/api/roles'));
    } catch {
      this.error.set('Could not load roles.');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadMatrix(): Promise<void> {
    if (this.matrix().length === 0) {
      this.matrix.set(await this.req<PermissionGroup[]>('GET', '/api/roles/permissions'));
    }
  }

  async startCreate(): Promise<void> {
    await this.loadMatrix();
    this.editingId = null;
    this.isSystem.set(false);
    this.form = { displayName: '', description: '' };
    this.selected.set(new Set());
    this.editing.set(true);
  }

  async startEdit(roleId: number): Promise<void> {
    await this.loadMatrix();
    const role = await this.req<RoleDetail>('GET', `/api/roles/${roleId}`);
    this.editingId = roleId;
    this.isSystem.set(role.isSystemRole);
    this.form = { displayName: role.displayName, description: role.description ?? '' };
    this.selected.set(new Set(role.permissionIds));
    this.editing.set(true);
  }

  togglePermission(id: number): void {
    const next = new Set(this.selected());
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    this.selected.set(next);
  }

  allSelected(group: PermissionGroup): boolean {
    return group.permissions.every((p) => this.selected().has(p.permissionId));
  }

  toggleModule(group: PermissionGroup): void {
    const next = new Set(this.selected());
    const turnOff = this.allSelected(group);
    for (const p of group.permissions) {
      if (turnOff) {
        next.delete(p.permissionId);
      } else {
        next.add(p.permissionId);
      }
    }
    this.selected.set(next);
  }

  async save(): Promise<void> {
    if (!this.hasText(this.form.displayName)) {
      this.error.set('Role name is required.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const body = {
      displayName: this.form.displayName.trim(),
      description: this.form.description.trim() || null,
      permissionIds: [...this.selected()],
    };
    try {
      if (this.editingId === null) {
        await this.req('POST', '/api/roles', body);
      } else {
        await this.req('PUT', `/api/roles/${this.editingId}`, body);
      }
      this.editing.set(false);
      await this.load();
    } catch {
      this.error.set('Could not save that role.');
    } finally {
      this.busy.set(false);
    }
  }

  async remove(role: RoleListItem): Promise<void> {
    if (!confirm(`Delete the role "${role.displayName}"?`)) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await this.req('DELETE', `/api/roles/${role.roleId}`);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.error.set(anyErr?.error?.message ?? 'Could not delete that role.');
    } finally {
      this.busy.set(false);
    }
  }

  cancel(): void {
    this.editing.set(false);
    this.error.set(null);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
