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
  imports: [FormsModule],
  template: `
    <header class="page-head">
      <h1>Roles</h1>
      <button type="button" (click)="startCreate()" [disabled]="busy()">+ New role</button>
    </header>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (!editing()) {
      <table class="grid">
        <thead>
          <tr><th>Role</th><th>Description</th><th>Users</th><th>Permissions</th><th></th></tr>
        </thead>
        <tbody>
          @for (r of roles(); track r.roleId) {
            <tr [class.inactive]="!r.isActive">
              <td>
                {{ r.displayName }}
                @if (r.isSystemRole) { <span class="badge">System</span> }
              </td>
              <td>{{ r.description }}</td>
              <td>{{ r.userCount }}</td>
              <td>{{ r.permissionCount }}</td>
              <td class="row-actions">
                <button type="button" class="link" (click)="startEdit(r.roleId)">Edit</button>
                @if (!r.isSystemRole) {
                  <button type="button" class="link danger" (click)="remove(r)">Delete</button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="5">No roles.</td></tr>
          }
        </tbody>
      </table>
    } @else {
      <form class="editor" (ngSubmit)="save()">
        <label>
          Name
          <input name="displayName" [(ngModel)]="form.displayName" required maxlength="100" />
        </label>
        <label>
          Description
          <input name="description" [(ngModel)]="form.description" maxlength="300" />
        </label>

        @if (isSystem()) {
          <p class="note">
            This is a system role. You can rename it for display, but its permissions are fixed
            and it cannot be deleted.
          </p>
        }

        <h2>Permissions</h2>
        @for (group of matrix(); track group.module) {
          <fieldset class="module">
            <legend>
              {{ group.module }}
              @if (!isSystem()) {
                <button type="button" class="link" (click)="toggleModule(group)">
                  {{ allSelected(group) ? 'Clear' : 'Select all' }}
                </button>
              }
            </legend>
            <div class="actions-grid">
              @for (p of group.permissions; track p.permissionId) {
                <label class="check">
                  <input
                    type="checkbox"
                    [disabled]="isSystem()"
                    [checked]="selected().has(p.permissionId)"
                    (change)="togglePermission(p.permissionId)"
                  />
                  {{ p.action }}
                </label>
              }
            </div>
          </fieldset>
        }

        <div class="editor-actions">
          <button type="submit" [disabled]="busy()">Save</button>
          <button type="button" class="link" (click)="cancel()">Cancel</button>
        </div>
      </form>
    }
  `,
  styles: `
    .page-head { display: flex; justify-content: space-between; align-items: center; }
    .grid { width: 100%; border-collapse: collapse; margin-top: 1rem; }
    .grid th, .grid td { text-align: left; padding: .55rem .6rem; border-bottom: 1px solid #e2e4ea; }
    .grid tr.inactive { opacity: .55; }
    .badge { font-size: .65rem; background: #6a6f80; color: #fff; padding: .1rem .35rem; border-radius: 4px; margin-left: .35rem; }
    .row-actions { display: flex; gap: .5rem; }
    .link { background: none; border: 0; color: #3557d6; cursor: pointer; font: inherit; padding: 0; }
    .link.danger { color: #c0392b; }
    .error { color: #c0392b; }
    .note { background: #fffaf0; border-left: 3px solid #f0b429; padding: .5rem .75rem; font-size: .875rem; }
    .editor { display: grid; gap: .9rem; max-width: 60rem; }
    .editor label { display: grid; gap: .25rem; font-size: .85rem; }
    .editor input[type='text'], .editor input:not([type]) { padding: .5rem; border: 1px solid #cdd1dc; border-radius: 6px; font: inherit; }
    .module { border: 1px solid #e2e4ea; border-radius: 8px; }
    .module legend { display: flex; gap: .75rem; align-items: center; text-transform: capitalize; font-weight: 600; }
    .actions-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(9rem, 1fr)); gap: .35rem; }
    .check { display: flex; align-items: center; gap: .35rem; font-size: .85rem; }
    .editor-actions { display: flex; gap: .75rem; align-items: center; }

    @media (max-width: 600px) {
      .grid thead { display: none; }
      .grid tr { display: grid; gap: .2rem; padding: .6rem 0; }
      .grid td { border: 0; padding: .1rem 0; }
      .actions-grid { grid-template-columns: repeat(2, 1fr); }
    }
  `,
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
    next.has(id) ? next.delete(id) : next.add(id);
    this.selected.set(next);
  }

  allSelected(group: PermissionGroup): boolean {
    return group.permissions.every((p) => this.selected().has(p.permissionId));
  }

  toggleModule(group: PermissionGroup): void {
    const next = new Set(this.selected());
    const turnOff = this.allSelected(group);
    for (const p of group.permissions) {
      turnOff ? next.delete(p.permissionId) : next.add(p.permissionId);
    }
    this.selected.set(next);
  }

  async save(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    const body = {
      displayName: this.form.displayName,
      description: this.form.description || null,
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

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
